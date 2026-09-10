using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Raylib_cs;
using RaylibGameFramework.Assets;

// Two bones and three frames, no meshes or graphics context. All calls to
// AnimationPlayer use the real native Raylib.UpdateModelAnimation implementation.
internal sealed unsafe class AnimationFixture : IDisposable
{
    private readonly List<nint> _allocations = [];
    public ModelAnimation Animation { get; }
    public ModelSkeleton Skeleton { get; }

    public AnimationFixture()
    {
        BoneInfo* bones = Allocate<BoneInfo>(2);
        SetName(&bones[0], "Root");
        SetName(&bones[1], "Hand.R");
        bones[0].Parent = -1;
        bones[1].Parent = 0;
        Transform* bindPose = Allocate<Transform>(2);
        // Deliberately different from every animated frame.
        bindPose[0] = new Transform { Translation = new(0, 0, -7), Rotation = Quaternion.Identity, Scale = Vector3.One };
        bindPose[1] = new Transform { Translation = new(0, 3, -7), Rotation = Quaternion.Identity, Scale = Vector3.One };
        Skeleton = new ModelSkeleton { BoneCount = 2, Bones = bones, BindPose = bindPose };

        Transform** frames = (Transform**)Allocate<nint>(3);
        for (int frame = 0; frame < 3; frame++)
        {
            frames[frame] = Allocate<Transform>(2);
            frames[frame][0] = new Transform
            {
                Translation = new(10 + 5 * frame, 2, 3),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2),
                Scale = new(2)
            };
            // Local child: translation (1,0,0), Y rotation 90 degrees,
            // scale (1,2,3). Raylib loaders resolve its parent before exposing
            // keyframe poses. These are the independently calculated results.
            frames[frame][1] = new Transform
            {
                Translation = new(10 + 5 * frame, 4, 3),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2),
                Scale = new(2, 4, 6)
            };
        }

        // Raylib-cs exposes the native counts as readonly fields without a constructor.
        object animation = default(ModelAnimation);
        typeof(ModelAnimation).GetField(nameof(ModelAnimation.BoneCount))!.SetValue(animation, 2);
        typeof(ModelAnimation).GetField(nameof(ModelAnimation.KeyFrameCount))!.SetValue(animation, 3);
        ModelAnimation value = (ModelAnimation)animation;
        value.KeyframePoses = frames;
        Animation = value;
    }

    public ModelInstance CreateInstance()
    {
        Model model = new()
        {
            // Excluded from bone model-space results.
            Transform = Matrix4x4.CreateTranslation(100, 200, 300),
            Skeleton = Skeleton,
            CurrentPose = Allocate<Transform>(2),
            BoneMatrices = Allocate<Matrix4x4>(2)
        };
        return Wrap(model);
    }

    public static ModelInstance Wrap(Model model) =>
        (ModelInstance)Activator.CreateInstance(typeof(ModelInstance),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, args: [model], culture: null)!;

    public static void Invalidate(ModelInstance instance) =>
        typeof(ModelInstance).GetMethod("Invalidate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, null);

    private T* Allocate<T>(int count) where T : unmanaged
    {
        T* pointer = (T*)NativeMemory.AllocZeroed((nuint)count, (nuint)sizeof(T));
        _allocations.Add((nint)pointer);
        return pointer;
    }

    private static void SetName(BoneInfo* bone, string name) =>
        Encoding.UTF8.GetBytes(name, new Span<byte>(bone->Name, 32));

    public void Dispose()
    {
        foreach (nint allocation in _allocations) NativeMemory.Free((void*)allocation);
    }
}
