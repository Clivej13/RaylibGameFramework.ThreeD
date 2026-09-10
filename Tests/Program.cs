using System.Numerics;
using Raylib_cs;
using RaylibGameFramework.ThreeD;

static partial class Test
{
    private static int _passed;

    public static void Main()
    {
        Run("point transform", PointTransform);
        Run("direction ignores scale and position", DirectionTransform);
        Run("rotation and non-uniform scale", NonUniformScale);
        Run("animation timing and looping", AnimationTiming);
        Run("particle lifetime", ParticleLifetime);
        Run("particle burst count", ParticleBurst);
        Run("local particle emission", LocalParticleEmission);
        Run("known and missing bone lookup", BoneLookup);
        Run("unavailable bone poses", BoneUnavailable);
        Run("exact current animated frame", BoneCurrentFrame);
        Run("bone hierarchy and native matrix convention", BoneHierarchyAndNativeConvention);
        Run("independent animated instances", BoneIndependentInstances);
        Run("bone queries do not mutate state", BoneQueryDoesNotMutate);
        Run("animation playback regression", BonePlaybackRegression);
        Console.WriteLine($"{_passed} focused tests passed.");
        if (_passed != 14) Environment.ExitCode = 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); Environment.ExitCode = 1; }
    }

    private static void Equal(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
    {
        if (Vector3.Distance(expected, actual) > tolerance)
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    private static void Equal(float expected, float actual, float tolerance = 0.0001f)
    {
        if (MathF.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void PointTransform()
    {
        Transform3D transform = new(new Vector3(10, 2, -3),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One);
        Equal(new Vector3(10, 2, -4), transform.TransformPoint(new Vector3(1, 0, 0)));
    }

    private static void DirectionTransform()
    {
        Transform3D transform = new(new Vector3(10, 2, -3),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), new Vector3(2, 3, 4));
        Equal(new Vector3(1, 0, 0), transform.TransformDirection(Vector3.UnitZ));
    }

    private static void NonUniformScale()
    {
        Transform3D transform = new(Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), new Vector3(2, 3, 4));
        Equal(new Vector3(0, 0, -2), transform.TransformPoint(Vector3.UnitX));
        Equal(new Vector3(4, 0, 0), transform.TransformPoint(Vector3.UnitZ));
    }

    private static void AnimationTiming()
    {
        AnimationPlayer player = new(10, loop: true, framesPerSecond: 10);
        player.Update(0.25f);
        Equal(2.5f, player.CurrentFrame);
        player.Update(0.8f);
        Equal(0.5f, player.CurrentFrame);
        player.PlaybackSpeed = 2f;
        player.Update(0.25f);
        Equal(5.5f, player.CurrentFrame);
    }

    private static void ParticleLifetime()
    {
        ParticleSystem3D particles = new();
        particles.Emit(new Particle3D { Lifetime = 1f, Size = 1f, Color = Color.White });
        particles.Update(0.5f);
        Check(particles.ActiveCount == 1, "particle removed too early");
        particles.Update(0.5f);
        Check(particles.ActiveCount == 0, "expired particle remains");
    }

    private static void ParticleBurst()
    {
        ParticleSystem3D particles = new(10, 1);
        Check(particles.Burst(Vector3.Zero, 6) == 6, "burst count mismatch");
        Check(particles.ActiveCount == 6, "active count mismatch");
    }

    private static void LocalParticleEmission()
    {
        ParticleSystem3D particles = new();
        Transform3D transform = new(new Vector3(10, 0, 0),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), new Vector3(2, 1, 1));
        particles.EmitLocal(transform, Vector3.UnitX, Vector3.UnitZ, 2f, 1f, Color.White);
        Equal(new Vector3(10, 0, -2), particles.Particles[0].Position);
        Equal(new Vector3(1, 0, 0), particles.Particles[0].Velocity);
    }
}
