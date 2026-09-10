using System.Numerics;
using System.Text;
using Raylib_cs;
using RaylibGameFramework.Assets;

namespace RaylibGameFramework.ThreeD;

public sealed class AnimationPlayer
{
    public const float DefaultFramesPerSecond = 60f;
    private int _appliedFrame = -1;
    private readonly int _frameCount;
    private readonly Dictionary<string, int> _boneIndices = new(StringComparer.Ordinal);
    private Transform[] _appliedPose = [];

    public ModelInstance? Instance { get; }
    public ModelAnimation Animation { get; }
    public float FramesPerSecond { get; }
    public bool Loop { get; set; }
    public float PlaybackSpeed { get; set; } = 1f;
    public float CurrentTime { get; private set; }
    public float CurrentFrame { get; private set; }
    public int FrameCount => _frameCount;
    public bool IsComplete => !Loop && CurrentFrame >= Math.Max(0, FrameCount - 1);

    public AnimationPlayer(ModelInstance? instance, ModelAnimation animation,
        bool loop = true, float framesPerSecond = DefaultFramesPerSecond, float phase = 0f)
    {
        if (framesPerSecond <= 0 || !float.IsFinite(framesPerSecond))
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

        Instance = instance;
        Animation = animation;
        _frameCount = animation.KeyFrameCount;
        FramesPerSecond = framesPerSecond;
        Loop = loop;
        SeekPhase(phase);
    }

    public AnimationPlayer(int frameCount, bool loop = true,
        float framesPerSecond = DefaultFramesPerSecond, float phase = 0f)
        : this(null, default, loop, framesPerSecond, phase)
    {
        if (frameCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        _frameCount = frameCount;
        SeekPhase(phase);
    }

    public void Update(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0 || FrameCount <= 0 || PlaybackSpeed == 0)
            return;

        float duration = FrameCount / FramesPerSecond;
        CurrentTime += deltaTime * PlaybackSpeed;

        if (Loop)
        {
            CurrentTime = Modulo(CurrentTime, duration);
        }
        else
        {
            CurrentTime = Math.Clamp(CurrentTime, 0, Math.Max(0, duration - 1f / FramesPerSecond));
        }

        ApplyCurrentFrame();
    }

    public void SeekTime(float time)
    {
        if (!float.IsFinite(time))
            throw new ArgumentOutOfRangeException(nameof(time));

        float duration = FrameCount <= 0 ? 0 : FrameCount / FramesPerSecond;
        CurrentTime = Loop && duration > 0
            ? Modulo(time, duration)
            : Math.Clamp(time, 0, Math.Max(0, duration - 1f / FramesPerSecond));
        ApplyCurrentFrame();
    }

    public void SeekPhase(float phase)
    {
        if (!float.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        SeekTime(Math.Clamp(phase, 0, 1) * (FrameCount / FramesPerSecond));
    }

    /// <summary>
    /// Gets the named bone's current animated transform from bone space to model space.
    /// </summary>
    /// <remarks>
    /// Uses the exact pose last applied by this player, including animated parents.
    /// This is not a bind-pose, skinning, or world-space matrix; Model.Transform and
    /// caller world transforms are excluded. Uses System.Numerics row vectors:
    /// scale * rotation * translation; compose with a model-to-world matrix on the right.
    /// Names are case-sensitive imported skeleton names. Queries do not advance playback.
    /// Keep the model and animation resources alive while using this player, and use a
    /// separate ModelInstance for each independently animated model.
    /// </remarks>
    /// <param name="name">The imported bone name.</param>
    /// <param name="transform">The current model-space transform, or identity on failure.</param>
    /// <returns>False for a missing name or when no valid pose has been applied.</returns>
    public bool TryGetBoneTransform(string name, out Matrix4x4 transform)
    {
        transform = Matrix4x4.Identity;
        if (string.IsNullOrEmpty(name) || _appliedFrame < 0 ||
            !TryGetModel(out _) || !_boneIndices.TryGetValue(name, out int index))
            return false;

        Transform pose = _appliedPose[index];
        transform = Matrix4x4.CreateScale(pose.Scale) *
            Matrix4x4.CreateFromQuaternion(pose.Rotation) *
            Matrix4x4.CreateTranslation(pose.Translation);
        return true;
    }

    private bool TryGetModel(out Model model)
    {
        model = default;
        if (Instance is null) return false;
        try
        {
            model = Instance.Model;
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private unsafe void ApplyCurrentFrame()
    {
        CurrentFrame = FrameCount <= 0 ? 0 : Math.Clamp(CurrentTime * FramesPerSecond, 0, FrameCount - 1);
        int frame = (int)CurrentFrame;
        if (frame == _appliedFrame || Instance is null || FrameCount <= 0)
            return;

        // Raylib 6 reads both keyframes even for an integer frame (blend == 0).
        if (!TryGetModel(out Model model) || model.Skeleton.BoneCount <= 0 ||
            model.Skeleton.BoneCount != Animation.BoneCount ||
            model.Skeleton.Bones == null || model.Skeleton.BindPose == null ||
            model.CurrentPose == null || model.BoneMatrices == null ||
            Animation.KeyframePoses == null || Animation.KeyframePoses[frame] == null ||
            Animation.KeyframePoses[(frame + 1) % FrameCount] == null)
        {
            _appliedFrame = -1;
            _appliedPose = [];
            _boneIndices.Clear();
            return;
        }

        Raylib.UpdateModelAnimation(model, Animation, frame);

        if (_appliedPose.Length != model.Skeleton.BoneCount)
        {
            _appliedPose = new Transform[model.Skeleton.BoneCount];
            _boneIndices.Clear();
            for (int i = 0; i < model.Skeleton.BoneCount; i++)
            {
                // Imported names occupy a fixed 32-byte UTF-8 buffer.
                var bytes = new ReadOnlySpan<byte>(model.Skeleton.Bones[i].Name, 32);
                int end = bytes.IndexOf((byte)0);
                string boneName = Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
                _boneIndices.TryAdd(boneName, i);
            }
        }

        // Raylib's loaders have already composed the hierarchy into model space.
        // Snapshot the native pose used to produce the rendered skinning matrices,
        // rather than reading shared animation data or calculating another clock.
        new ReadOnlySpan<Transform>(model.CurrentPose, model.Skeleton.BoneCount).CopyTo(_appliedPose);
        _appliedFrame = frame;
    }

    private static float Modulo(float value, float modulus) =>
        value - MathF.Floor(value / modulus) * modulus;
}
