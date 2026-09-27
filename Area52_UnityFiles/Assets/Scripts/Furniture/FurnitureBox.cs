using UnityEngine;

/*
 * Treats every piece of furniture as a rotated box in world space, taken from its Box Collider
 * The "does it fit / does it touch anything else" checks are done with plain math to ensure the same results on every device type
*/

public struct FurnitureBox
{
    public Vector3 center;
    public Vector3 halfExtents;
    public Quaternion rotation;

    public FurnitureBox(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        this.center = center;
        this.halfExtents = halfExtents;
        this.rotation = rotation;
    }

    public Vector3 AxisX => rotation * Vector3.right;
    public Vector3 AxisY => rotation * Vector3.up;
    public Vector3 AxisZ => rotation * Vector3.forward;

    // Fills an array of 8 with the box's corners
    public void GetCorners(Vector3[] corners)
    {
        Vector3 x = AxisX * halfExtents.x;
        Vector3 y = AxisY * halfExtents.y;
        Vector3 z = AxisZ * halfExtents.z;
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    corners[i++] = center + x * sx + y * sy + z * sz;
    }

    // Halves the box's size along the world up axis - should work even if the box is tipped over
    public float HalfHeight =>
        Mathf.Abs(AxisX.y) * halfExtents.x + Mathf.Abs(AxisY.y) * halfExtents.y + Mathf.Abs(AxisZ.y) * halfExtents.z;

    public float MinY => center.y - HalfHeight;
    public float MaxY => center.y + HalfHeight;

    // Middle of the underside, the point that rests on the floor or a surface
    public Vector3 BottomCenter => new Vector3(center.x, MinY, center.z);
    public Vector3 TopCenter => new Vector3(center.x, MaxY, center.z);

    public FurnitureBox Moved(Vector3 delta)
    {
        return new FurnitureBox(center + delta, halfExtents, rotation);
    }

    // Spins the box around the world up axis, around its own center
    public FurnitureBox RotatedY(float degrees)
    {
        return new FurnitureBox(center, halfExtents, Quaternion.AngleAxis(degrees, Vector3.up) * rotation);
    }

    public FurnitureBox Inflated(float amount)
    {
        return new FurnitureBox(center, halfExtents + Vector3.one * amount, rotation);
    }

    public FurnitureBox CarryAlong(FurnitureBox child, FurnitureBox to)
    {
        Quaternion delta = to.rotation * Quaternion.Inverse(rotation);
        return new FurnitureBox(to.center + delta * (child.center - center), child.halfExtents, delta * child.rotation);
    }

    // Separating axis test for two rotated boxes to prevent overlapping.
    public static bool Intersects(FurnitureBox a, FurnitureBox b)
    {
        const float epsilon = 1e-5f;

        Vector3 a0 = a.AxisX, a1 = a.AxisY, a2 = a.AxisZ;
        Vector3 b0 = b.AxisX, b1 = b.AxisY, b2 = b.AxisZ;
        float ea0 = a.halfExtents.x, ea1 = a.halfExtents.y, ea2 = a.halfExtents.z;
        float eb0 = b.halfExtents.x, eb1 = b.halfExtents.y, eb2 = b.halfExtents.z;

        // Rotation of b expressed in a's frame
        float r00 = Vector3.Dot(a0, b0), r01 = Vector3.Dot(a0, b1), r02 = Vector3.Dot(a0, b2);
        float r10 = Vector3.Dot(a1, b0), r11 = Vector3.Dot(a1, b1), r12 = Vector3.Dot(a1, b2);
        float r20 = Vector3.Dot(a2, b0), r21 = Vector3.Dot(a2, b1), r22 = Vector3.Dot(a2, b2);

        float q00 = Mathf.Abs(r00) + epsilon, q01 = Mathf.Abs(r01) + epsilon, q02 = Mathf.Abs(r02) + epsilon;
        float q10 = Mathf.Abs(r10) + epsilon, q11 = Mathf.Abs(r11) + epsilon, q12 = Mathf.Abs(r12) + epsilon;
        float q20 = Mathf.Abs(r20) + epsilon, q21 = Mathf.Abs(r21) + epsilon, q22 = Mathf.Abs(r22) + epsilon;

        // Offset between centers in a's frame
        Vector3 d = b.center - a.center;
        float t0 = Vector3.Dot(d, a0), t1 = Vector3.Dot(d, a1), t2 = Vector3.Dot(d, a2);

        // a's axes
        if (Mathf.Abs(t0) > ea0 + eb0 * q00 + eb1 * q01 + eb2 * q02) return false;
        if (Mathf.Abs(t1) > ea1 + eb0 * q10 + eb1 * q11 + eb2 * q12) return false;
        if (Mathf.Abs(t2) > ea2 + eb0 * q20 + eb1 * q21 + eb2 * q22) return false;

        // b's axes
        if (Mathf.Abs(t0 * r00 + t1 * r10 + t2 * r20) > ea0 * q00 + ea1 * q10 + ea2 * q20 + eb0) return false;
        if (Mathf.Abs(t0 * r01 + t1 * r11 + t2 * r21) > ea0 * q01 + ea1 * q11 + ea2 * q21 + eb1) return false;
        if (Mathf.Abs(t0 * r02 + t1 * r12 + t2 * r22) > ea0 * q02 + ea1 * q12 + ea2 * q22 + eb2) return false;

        // Cross products of the axes
        if (Mathf.Abs(t2 * r10 - t1 * r20) > ea1 * q20 + ea2 * q10 + eb1 * q02 + eb2 * q01) return false;
        if (Mathf.Abs(t2 * r11 - t1 * r21) > ea1 * q21 + ea2 * q11 + eb0 * q02 + eb2 * q00) return false;
        if (Mathf.Abs(t2 * r12 - t1 * r22) > ea1 * q22 + ea2 * q12 + eb0 * q01 + eb1 * q00) return false;

        if (Mathf.Abs(t0 * r20 - t2 * r00) > ea0 * q20 + ea2 * q00 + eb1 * q12 + eb2 * q11) return false;
        if (Mathf.Abs(t0 * r21 - t2 * r01) > ea0 * q21 + ea2 * q01 + eb0 * q12 + eb2 * q10) return false;
        if (Mathf.Abs(t0 * r22 - t2 * r02) > ea0 * q22 + ea2 * q02 + eb0 * q11 + eb1 * q10) return false;

        if (Mathf.Abs(t1 * r00 - t0 * r10) > ea0 * q10 + ea1 * q00 + eb1 * q22 + eb2 * q21) return false;
        if (Mathf.Abs(t1 * r01 - t0 * r11) > ea0 * q11 + ea1 * q01 + eb0 * q22 + eb2 * q20) return false;
        if (Mathf.Abs(t1 * r02 - t0 * r12) > ea0 * q12 + ea1 * q02 + eb0 * q21 + eb1 * q20) return false;

        return true;
    }

    public void DrawGizmo()
    {
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = old;
    }
}