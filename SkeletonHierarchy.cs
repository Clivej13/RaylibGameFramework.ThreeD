using System.Numerics;

namespace RaylibGameFramework.ThreeD;

/// <summary>Immutable parent indices for converting bone poses without rendering or physics dependencies.</summary>
public sealed class SkeletonHierarchy
{
    private readonly int[] _parents;
    private readonly int[] _order;

    /// <summary>Number of bones, including all roots.</summary>
    public int BoneCount => _parents.Length;

    /// <summary>Copies parent indices. Roots use -1; bones may appear in any order.</summary>
    /// <exception cref="ArgumentException">A parent is out of range or the hierarchy contains a cycle.</exception>
    public SkeletonHierarchy(ReadOnlySpan<int> parents)
    {
        _parents = parents.ToArray();
        var order = new List<int>(BoneCount);
        var state = new byte[BoneCount];
        var path = new List<int>();
        foreach (int parent in _parents)
            if (parent < -1 || parent >= BoneCount)
                throw new ArgumentException("Invalid skeleton parent.", nameof(parents));
        for (int bone = 0; bone < BoneCount; bone++)
        {
            path.Clear();
            int current = bone;
            while (current >= 0 && state[current] == 0)
            {
                state[current] = 1;
                path.Add(current);
                current = _parents[current];
            }
            if (current >= 0 && state[current] == 1)
                throw new ArgumentException("Cyclic skeleton.", nameof(parents));
            for (int i = path.Count - 1; i >= 0; i--)
            {
                state[path[i]] = 2;
                order.Add(path[i]);
            }
        }
        _order = order.ToArray();
    }

    /// <summary>Returns a new model-space pose: local * parent model matrix.
    /// Matrices retain affine shear; no TRS decomposition is performed.</summary>
    /// <exception cref="ArgumentException">The pose count differs from BoneCount.</exception>
    public Matrix4x4[] ToModelSpace(ReadOnlySpan<Matrix4x4> localPose)
    {
        ValidateCount(localPose.Length);
        var result = new Matrix4x4[BoneCount];
        foreach (int bone in _order)
            result[bone] = _parents[bone] < 0 ? localPose[bone] :
                localPose[bone] * result[_parents[bone]];
        return result;
    }

    /// <summary>Returns a new parent-local pose: model * inverse(parent model).
    /// Roots retain their model matrix. Input poses are never modified.</summary>
    /// <exception cref="ArgumentException">The count differs or a parent matrix is singular.</exception>
    public Matrix4x4[] ToLocalSpace(ReadOnlySpan<Matrix4x4> modelPose)
    {
        ValidateCount(modelPose.Length);
        var result = new Matrix4x4[BoneCount];
        for (int bone = 0; bone < BoneCount; bone++)
        {
            int parent = _parents[bone];
            if (parent < 0) result[bone] = modelPose[bone];
            else
            {
                if (!Matrix4x4.Invert(modelPose[parent], out var inverse))
                    throw new ArgumentException("Singular parent pose.", nameof(modelPose));
                result[bone] = modelPose[bone] * inverse;
            }
        }
        return result;
    }

    private void ValidateCount(int count)
    {
        if (count != BoneCount)
            throw new ArgumentException("One matrix per bone is required.");
    }
}
