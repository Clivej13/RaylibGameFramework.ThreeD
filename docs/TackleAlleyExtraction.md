# Tackle Alley extraction assessment

Source inspected read-only: RaylibTackleAlley/Game. No source files were moved,
deleted, or edited. The additions are adaptations, not unchanged copies.

| Source | Decision |
| --- | --- |
| RagdollPose.cs | Snapshot concept integrated into AnimationPlayer.SnapshotPose, using its cached applied pose rather than unsafe name scans or a separate model argument. Existing Transform3D.WorldMatrix already covers Matrix(Transform). |
| RagdollSkeleton.cs | Parent validation/order and local-offset composition adapted into SkeletonHierarchy. Removed body indices, Root-to-Hips alias, native pose application and drawing. |
| RagdollRecovery.cs | Parent-local conversion and TRS interpolation extracted as hierarchy conversion and Transform3D.Blend. Recovery state machine, torso alignment, clip timing, defender policy and rendering remain excluded. |
| AffinePose.cs | Not copied. Approximate rigid orientation extraction is a physics concern; native CPU skinning and GPU buffer updates need a separately tested rendering API. Exact affine hierarchy conversion needs neither decomposition nor Raylib. |
| GameplayCamera.cs / CameraLookInput.cs | Not copied. Camera presets, held movement, cut/sprint framing, action names and InputController are coupled to game control policy. |
| Ragdoll.cs / Ragdoll.Active.cs | Not copied. Fixed eleven-body humanoid layout, anatomical names, body-index motor rules, ground/down classification and player attribute scaling need a dedicated Character/Physics design. |
| RagdollContact.cs | Not copied. General capsule geometry is mixed with defender/carrier contacts, TackleImpact, torso impulse shares, wrap control and tackle energy policies. |
| RagdollDebugControls.cs | Not copied. Opponent selection, command-line switches, keyboard polling and debug drawing are application concerns. |
| PlayerPhysicalAttributes.cs / PlayerMovementAttributes.cs | Not copied. Ratings, profiles, balance multipliers, football actions and configuration belong to the game. No RPG/stat abstraction introduced. |

## Dependency boundary

Extracted math uses only System.Numerics. Snapshot acquisition stays inside the
existing Raylib-backed AnimationPlayer. There are no dependencies on
TackleAlleyConfig, Ragdoll, Opponent, PlayerProfile, TackleImpact, football bone
names, or the Input package in the new runtime API. Package references and
project settings require no changes.

## Why the solver is deferred

A faithful clean extraction is larger than this package's transform/animation
scope. A future Physics package would need generic body/capsule definitions,
joint anchors and limits, material/contact settings and externally supplied
poses. Preserve the original 1/120-second fixed step, 24 solver iterations,
0.25-second catch-up cap, activation snapshot/velocity handoff, linear and
off-centre impulses, mass/inertia behavior, ground projection, contact friction,
settling thresholds and wake semantics. Do not transplant only a few solver
methods and claim equivalent behavior.

A Character layer could own humanoid mapping, active joint targets, support and
contact response, and settled-pose-to-animation recovery. Keep tackle outcomes,
wrap rules and ratings in game code. Recovery must retain parent-local blending,
exact activation continuity (including affine residuals), down/get-up phase
timing and leftover-delta handling. Rendering/native pose application and debug
drawing must be separate adapters, with explicit mutable-buffer ownership.

## Tests and limitations

The source RagdollSkeletonTests local-offset invariant and RagdollRigTests
snapshot/no-clock-advance expectations informed the new asset-free tests.
Tests cover noncommuting rotations, nonuniform scale/shear round trips, arbitrary
bone order, multiple roots, cycles/invalid parents, singular parents, deep chains,
snapshot ownership and disposal, and local TRS blend endpoints/shortest arcs.
Existing native animation tests continue to exercise real Raylib updates.

No solver was extracted, so physics, recovery and visual-equivalence tests were
not ported or claimed as passing here. A future extraction must port the source
RagdollTests, ActiveRagdollTests, RagdollContactTests, RagdollSkeletonTests and
recovery/physical-scaling tests against generic fixtures before retiring that
code. Custom native pose application and affine skinning need independent
mesh/normal and rendering validation. Matrix conversions assume finite affine
inputs; TRS blending assumes unit quaternions. Returned pose arrays allocate.
