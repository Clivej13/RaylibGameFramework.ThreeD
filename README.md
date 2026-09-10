# RaylibGameFramework.ThreeD

Reusable 3D transforms, animation playback, material overrides, and particles for Raylib-cs.

## Current animated bone transforms

```csharp
public bool TryGetBoneTransform(string name, out System.Numerics.Matrix4x4 transform);
```

`AnimationPlayer.TryGetBoneTransform` returns the named bone's **current animated
model-space transform** for the exact pose applied by that player. It transforms
points from the bone's coordinate system into the model's coordinate system.
Animated parent transforms are included. It is not a parent-relative, bind-pose,
skinning, or world-space matrix.

```csharp
animationPlayer.Update(deltaTime);

if (animationPlayer.TryGetBoneTransform("Hand.R", out var hand))
{
    // Caller may compose this with its model/world transform.
    Matrix4x4 handWorld = hand * modelToWorld;
    Vector3 worldPosition = Vector3.Transform(Vector3.Zero, handWorld);
}
```

Use `System.Numerics` row-vector conventions, matching `Transform3D.WorldMatrix`:
`Scale * Rotation * Translation`. Compose a bone matrix with a model-to-world
matrix on the right. The result excludes Raylib's `Model.Transform` and any
caller-supplied world transform. Raylib's raw native matrix memory layout should
not be confused with the System.Numerics convention used by this API.

Names are matched exactly, case-sensitively, against imported skeleton names.
The method returns false (and identity) for an unknown, null, or empty name,
a timing-only player, a released instance, or when no valid pose has been
applied. It does not advance time or change animation, model, or source pose data.

Construction, `Update`, `SeekTime`, and `SeekPhase` retain the existing playback
behaviour: the player applies the integer part of `CurrentFrame`. A fractional
`CurrentFrame` of 1.5 therefore queries applied frame 1. Construction applies the
initial phase immediately when the model and animation are valid.

Each player copies `Model.CurrentPose` immediately after its
`Raylib.UpdateModelAnimation` call, using reusable per-player storage. This is
the pose Raylib used to produce the rendered skinning matrices; there is no
second animation clock. Bone names and indices are cached per player, and
queries do not scan the skeleton or allocate pose storage.

Use a separate `ModelInstance` with independent mutable animation buffers for
each independently animated model. Source animation keyframes can be shared.
Let one player control a given instance, and keep its native model and animation
resources alive for the player's lifetime. Direct external pose edits and
concurrent updates are outside this playback contract.

### Raylib pose conventions

The package uses Raylib-cs 8.0.0 / Raylib 6.0:

- `Model.Skeleton.BindPose` is the base pose.
- `ModelSkeleton.Bones` stores imported names and parent indices (`-1` for roots).
- `ModelAnimation.KeyframePoses[frame][bone]` stores animation poses.
- Raylib's loaders resolve local parent/child transforms into model-space poses
  before exposing keyframes. For example, `BuildPoseFromParentJoints` applies
  parent scale and rotation to child translation, adds parent translation,
  combines rotations, and multiplies scales.
- `UpdateModelAnimation` writes `Model.CurrentPose` and computes skinning
  matrices as inverse bind pose composed with current pose.

Consequently, this API converts the applied model-space TRS into a matrix
without traversing parents again or returning the inverse-bind skinning matrix.

References:
[Raylib-cs structures](https://github.com/raylib-cs/raylib-cs/blob/v8.0.0/Raylib-cs/types/Model.cs),
[Raylib pose loading and animation implementation](https://github.com/raysan5/raylib/blob/6.0/src/rmodels.c).

## Validation

```text
dotnet build -c Release
dotnet test Tests/ThreeD.Tests.csproj -c Release
```

The dependency-free console test harness is wired into `dotnet test`.
The small native skeleton fixture has two bones and three frames, uses the real
Raylib animation update, and needs no assets, meshes, or graphics context.
Tests compare noncommuting child/parent rotations, translation, and scale
against independently composed System.Numerics matrices and Raylib's actual
skinning output.
