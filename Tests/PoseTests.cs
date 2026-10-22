using System.Numerics;
using RaylibGameFramework.ThreeD;

static partial class Test
{
    private static void MatrixEqual(Matrix4x4 expected, Matrix4x4 actual)
    {
        foreach (var point in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            var value = Vector3.Transform(point, actual);
            Check(float.IsFinite(value.LengthSquared()), "non-finite matrix");
            Equal(Vector3.Transform(point, expected), value);
        }
    }

    private static void ThrowsArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected argument exception.");
    }

    private static void PoseHierarchy()
    {
        int[] parents = [2, -1, 1, -1];
        var hierarchy = new SkeletonHierarchy(parents);
        parents[0] = -1; // Ownership is independent.
        Matrix4x4[] local = [
            Matrix4x4.CreateRotationX(.7f) * Matrix4x4.CreateTranslation(1, 2, 3),
            Matrix4x4.CreateScale(2, 3, 4) * Matrix4x4.CreateRotationY(.4f),
            Matrix4x4.CreateRotationZ(.8f) * Matrix4x4.CreateTranslation(4, 0, 2),
            Matrix4x4.CreateTranslation(-5, 0, 0)];
        var original = local.ToArray();
        var model = hierarchy.ToModelSpace(local);
        MatrixEqual(local[0] * local[2] * local[1], model[0]);
        MatrixEqual(local[2] * local[1], model[2]);
        MatrixEqual(local[3], model[3]);
        var restored = hierarchy.ToLocalSpace(model);
        for (int i = 0; i < local.Length; i++)
        {
            MatrixEqual(original[i], local[i]);
            MatrixEqual(local[i], restored[i]);
        }
        // Adapted from the source's unmapped-bone offset invariant.
        var moved = local.ToArray();
        moved[1] *= Matrix4x4.CreateTranslation(10, 2, -3);
        var movedModel = hierarchy.ToModelSpace(moved);
        Matrix4x4.Invert(movedModel[2], out var inverseParent);
        MatrixEqual(local[0], movedModel[0] * inverseParent);
        Check(new SkeletonHierarchy([]).ToModelSpace([]).Length == 0, "empty hierarchy");
    }

    private static void InvalidHierarchy()
    {
        ThrowsArgument(() => new SkeletonHierarchy([0]));
        ThrowsArgument(() => new SkeletonHierarchy([1, 0]));
        ThrowsArgument(() => new SkeletonHierarchy([-2]));
        ThrowsArgument(() => new SkeletonHierarchy([1]));
        var hierarchy = new SkeletonHierarchy([-1, 0]);
        ThrowsArgument(() => hierarchy.ToModelSpace([Matrix4x4.Identity]));
        ThrowsArgument(() => hierarchy.ToLocalSpace([]));
        ThrowsArgument(() => hierarchy.ToLocalSpace([default, Matrix4x4.Identity]));
        // Long chains must not require recursive stack space.
        var parents = Enumerable.Range(0, 10000).Select(i => i - 1).ToArray();
        Check(new SkeletonHierarchy(parents).BoneCount == 10000, "long hierarchy");
    }

    private static void PoseBlend()
    {
        var from = Transform3D.Identity;
        var to = new Transform3D(new(4, 6, 8),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2), new(3, 5, 7));
        var middle = Transform3D.Blend(from, to, .5f);
        Equal(new Vector3(2, 3, 4), middle.Position);
        Equal(new Vector3(2, 3, 4), middle.Scale);
        Equal(new Vector3(MathF.Sqrt(.5f), MathF.Sqrt(.5f), 0), middle.TransformDirection(Vector3.UnitX));
        MatrixEqual(from.WorldMatrix, Transform3D.Blend(from, to, -1).WorldMatrix);
        MatrixEqual(to.WorldMatrix, Transform3D.Blend(from, to, 2).WorldMatrix);
        var opposite = to;
        opposite.Rotation = -to.Rotation;
        MatrixEqual(to.WorldMatrix, Transform3D.Blend(to, opposite, .5f).WorldMatrix);
        ThrowsArgument(() => Transform3D.Blend(from, to, float.NaN));
        ThrowsArgument(() => Transform3D.Blend(from, to, float.PositiveInfinity));
        var hierarchy = new SkeletonHierarchy([-1, 0]);
        var pose = hierarchy.ToModelSpace([middle.WorldMatrix, Matrix4x4.CreateTranslation(1, 0, 0)]);
        Equal(middle.TransformPoint(Vector3.UnitX), pose[1].Translation);
    }

    private static void PoseSnapshot()
    {
        using AnimationFixture fixture = new();
        var instance = fixture.CreateInstance();
        var player = new AnimationPlayer(instance, fixture.Animation, framesPerSecond: 10);
        player.SeekTime(.15f);
        var snapshot = player.SnapshotPose();
        Check(snapshot.Length == 2, "snapshot count");
        Check(player.TryGetBoneTransform("Hand.R", out var hand), "missing hand");
        MatrixEqual(hand, snapshot[1]);
        Equal(.15f, player.CurrentTime);
        var saved = snapshot.ToArray();
        snapshot[0] = default;
        MatrixEqual(saved[0], player.SnapshotPose()[0]);
        player.SeekTime(.2f);
        MatrixEqual(hand, snapshot[1]);
        Check(player.SnapshotPose()[1] != hand, "snapshot did not update");
        AnimationFixture.Invalidate(instance);
        Check(player.SnapshotPose().Length == 0, "released snapshot");
        Check(new AnimationPlayer(3).SnapshotPose().Length == 0, "timing-only snapshot");
        Check(new AnimationPlayer(null, fixture.Animation).SnapshotPose().Length == 0, "null instance snapshot");
    }
}
