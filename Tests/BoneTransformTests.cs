using System.Numerics;
using Raylib_cs;
using RaylibGameFramework.ThreeD;

static partial class Test
{
    private static void BoneLookup()
    {
        using AnimationFixture fixture = new();
        AnimationPlayer player = new(fixture.CreateInstance(), fixture.Animation);
        Check(player.TryGetBoneTransform("Hand.R", out var hand), "known child missing");
        Equal(new Vector3(10, 4, 3), hand.Translation);
        Check(player.TryGetBoneTransform("Root", out var root), "known root missing");
        Equal(new Vector3(10, 2, 3), root.Translation);
        foreach (string name in new[] { "Unknown", "hand.r", "", null! })
        {
            Check(!player.TryGetBoneTransform(name, out var missing), "missing name succeeded");
            Check(missing == Matrix4x4.Identity, "failure output is not identity");
        }
    }

    private static unsafe void BoneUnavailable()
    {
        Check(!new AnimationPlayer(3).TryGetBoneTransform("Hand.R", out _), "timing-only player has pose");
        using AnimationFixture fixture = new();
        Check(!new AnimationPlayer(null, fixture.Animation).TryGetBoneTransform("Hand.R", out _), "null instance has pose");
        Check(!new AnimationPlayer(fixture.CreateInstance(), default).TryGetBoneTransform("Hand.R", out _), "empty animation has pose");
        Check(!new AnimationPlayer(AnimationFixture.Wrap(default), fixture.Animation).TryGetBoneTransform("Hand.R", out _), "empty model has pose");
        var instance = fixture.CreateInstance();
        var model = instance.Model;
        model.CurrentPose = null;
        Check(!new AnimationPlayer(AnimationFixture.Wrap(model), fixture.Animation).TryGetBoneTransform("Hand.R", out _), "null current pose accepted");
        model = instance.Model;
        model.Skeleton.BoneCount = 1;
        Check(!new AnimationPlayer(AnimationFixture.Wrap(model), fixture.Animation).TryGetBoneTransform("Hand.R", out _), "mismatched skeleton accepted");
        var animation = fixture.Animation;
        animation.KeyframePoses = null;
        Check(!new AnimationPlayer(instance, animation).TryGetBoneTransform("Hand.R", out _), "null keyframes accepted");
        AnimationPlayer player = new(instance, fixture.Animation);
        AnimationFixture.Invalidate(instance);
        Check(!player.TryGetBoneTransform("Hand.R", out _), "released instance has pose");
    }

    private static void BoneCurrentFrame()
    {
        using AnimationFixture fixture = new();
        AnimationPlayer player = new(fixture.CreateInstance(), fixture.Animation, framesPerSecond: 10);
        player.Update(0.15f);
        Equal(1.5f, player.CurrentFrame);
        Check(player.TryGetBoneTransform("Hand.R", out var hand), "updated pose missing");
        Equal(new Vector3(15, 4, 3), hand.Translation); // Applied integer frame 1, not 0 or interpolated 1.5.
        player.Update(0.06f);
        Check(player.TryGetBoneTransform("Hand.R", out hand), "second pose missing");
        Equal(new Vector3(20, 4, 3), hand.Translation);
        player.SeekTime(0);
        Check(player.TryGetBoneTransform("Hand.R", out hand), "seek pose missing");
        Equal(new Vector3(10, 4, 3), hand.Translation);
    }

    private static unsafe void BoneHierarchyAndNativeConvention()
    {
        using AnimationFixture fixture = new();
        var instance = fixture.CreateInstance();
        AnimationPlayer player = new(instance, fixture.Animation);
        Check(player.TryGetBoneTransform("Hand.R", out var hand), "child missing");
        // Noncommuting child Y rotation and parent Z rotation, translated and scaled.
        Matrix4x4 local = Matrix4x4.CreateScale(1, 2, 3) *
            Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(1, 0, 0);
        Matrix4x4 parent = Matrix4x4.CreateScale(2) *
            Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(10, 2, 3);
        foreach (Vector3 point in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(2, -3, 4) })
        {
            Equal(Vector3.Transform(point, local * parent), Vector3.Transform(point, hand));
            // Recover bone-to-model using Raylib's actual skinning matrix:
            // bind * (inverse(bind) * animated) == animated.
            Vector3 bindPoint = point + new Vector3(0, 3, -7);
            Vector3 nativePoint = Raymath.Vector3Transform(bindPoint, instance.Model.BoneMatrices[1]);
            Equal(nativePoint, Vector3.Transform(point, hand));
        }
        Equal(new Vector3(10, 4, 1), Vector3.Transform(Vector3.UnitX, hand));
        Equal(new Vector3(6, 4, 3), Vector3.Transform(Vector3.UnitY, hand));
        Equal(new Vector3(10, 10, 3), Vector3.Transform(Vector3.UnitZ, hand));
    }

    private static unsafe void BoneIndependentInstances()
    {
        using AnimationFixture fixture = new();
        var instanceA = fixture.CreateInstance();
        var instanceB = fixture.CreateInstance();
        Check(instanceA.Model.CurrentPose != instanceB.Model.CurrentPose, "fixture shares pose memory");
        AnimationPlayer a = new(instanceA, fixture.Animation, framesPerSecond: 10);
        AnimationPlayer b = new(instanceB, fixture.Animation, framesPerSecond: 10);
        b.SeekTime(0.2f);
        Check(a.TryGetBoneTransform("Hand.R", out var first), "A missing");
        Check(b.TryGetBoneTransform("Hand.R", out var second), "B missing");
        Equal(new Vector3(10, 4, 3), first.Translation);
        Equal(new Vector3(20, 4, 3), second.Translation);
        a.Update(0.1f);
        Check(a.TryGetBoneTransform("Hand.R", out first), "A updated missing");
        Check(b.TryGetBoneTransform("Hand.R", out second), "B updated missing");
        Equal(new Vector3(15, 4, 3), first.Translation);
        Equal(new Vector3(20, 4, 3), second.Translation);
        Equal(second.Translation, instanceB.Model.CurrentPose[1].Translation);
    }

    private static unsafe void BoneQueryDoesNotMutate()
    {
        using AnimationFixture fixture = new();
        var instance = fixture.CreateInstance();
        AnimationPlayer player = new(instance, fixture.Animation, loop: false, framesPerSecond: 10);
        player.Update(0.15f);
        float time = player.CurrentTime, frame = player.CurrentFrame;
        Transform pose = instance.Model.CurrentPose[1];
        Transform keyframe = fixture.Animation.KeyframePoses[1][1];
        Matrix4x4 skin = instance.Model.BoneMatrices[1];
        for (int i = 0; i < 100; i++)
        {
            Check(player.TryGetBoneTransform("Hand.R", out _), "query failed");
            Check(!player.TryGetBoneTransform("Absent", out _), "unknown query succeeded");
        }
        Equal(time, player.CurrentTime);
        Equal(frame, player.CurrentFrame);
        Check(pose == instance.Model.CurrentPose[1], "query changed native pose");
        Check(keyframe == fixture.Animation.KeyframePoses[1][1], "query changed source frame");
        Check(skin == instance.Model.BoneMatrices[1], "query changed skinning");
        Check(!player.IsComplete && !player.Loop && player.PlaybackSpeed == 1, "query changed playback");
    }

    private static void BonePlaybackRegression()
    {
        using AnimationFixture fixture = new();
        AnimationPlayer player = new(fixture.CreateInstance(), fixture.Animation, framesPerSecond: 10);
        AnimationPlayer timing = new(3, framesPerSecond: 10);
        foreach (float delta in new[] { 0.05f, 0.1f, 0.2f, 1f, 0f, -1f, float.NaN })
        {
            player.Update(delta);
            timing.Update(delta);
            Equal(timing.CurrentTime, player.CurrentTime);
            Equal(timing.CurrentFrame, player.CurrentFrame);
        }
        player.Loop = false;
        player.SeekTime(100);
        Check(player.IsComplete, "nonlooping animation did not clamp");
        Check(player.TryGetBoneTransform("Hand.R", out var hand), "clamped pose missing");
        Equal(new Vector3(20, 4, 3), hand.Translation);
        player.PlaybackSpeed = 0;
        player.Update(1);
        Equal(2, player.CurrentFrame);
        player.PlaybackSpeed = -1;
        player.Update(0.1f);
        Equal(1, player.CurrentFrame);
        Check(player.TryGetBoneTransform("Hand.R", out hand), "reverse pose missing");
        Equal(new Vector3(15, 4, 3), hand.Translation);
        player.SeekPhase(0);
        Equal(0, player.CurrentFrame);
    }
}
