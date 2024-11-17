using HockServer.Enums;
using UnityEngine;

public static class HQMHelpers
{
    public static (Vector3, Quaternion) GetSpawnPoint(HQMRink rink, HQMTeam team, HQMSpawnPoint spawnPoint)
    {
        switch (team)
        {
            case HQMTeam.Red:
                switch (spawnPoint)
                {
                    case HQMSpawnPoint.Center:
                        var z = (rink.Length / 2.0f) + 3.0f;
                        var pos = new Vector3(rink.Width / 2.0f, 2.0f, z);
                        var rot = Quaternion.identity;
                        return (pos, rot);
                    case HQMSpawnPoint.Bench:
                        z = (rink.Length / 2.0f) + 4.0f;
                        pos = new Vector3(0.5f, 2.0f, z);
                        rot = Quaternion.Euler(0.0f, 3.0f * Mathf.PI / 2.0f, 0.0f);
                        return (pos, rot);
                }
                break;
            case HQMTeam.Blue:
                switch (spawnPoint)
                {
                    case HQMSpawnPoint.Center:
                        var z = (rink.Length / 2.0f) - 3.0f;
                        var pos = new Vector3(rink.Width / 2.0f, 2.0f, z);
                        //var rot = Quaternion.Euler(0.0f, Mathf.PI, 0.0f);
                        var rot = Quaternion.identity;
                        return (pos, rot);
                    case HQMSpawnPoint.Bench:
                        z = (rink.Length / 2.0f) - 4.0f;
                        pos = new Vector3(0.5f, 2.0f, z);
                        rot = Quaternion.Euler(0.0f, 3.0f * Mathf.PI / 2.0f, 0.0f);
                        return (pos, rot);
                }
                break;
        }
        return (Vector3.zero, Quaternion.identity);
    }
    public static Vector3 LimitVectorLength(Vector3 v, float maxLen)
    {
        float norm = v.magnitude;
        Vector3 res = v;
        if (norm > maxLen)
        {
            res *= maxLen / norm;
        }
        return res;
    }

    public static Vector2 LimitVectorLength2(Vector2 v, float maxLen)
    {
        float norm = v.magnitude;
        Vector2 res = v;
        if (norm > maxLen)
        {
            res *= maxLen / norm;
        }
        return res;
    }

    public static void LimitFriction(ref Vector3 v, Vector3 normal, float d)
    {
        float projectionLength = Vector3.Dot(v, normal);
        Vector3 projection = normal * projectionLength;
        Vector3 rejection = v - projection;
        float rejectionLength = rejection.magnitude;
        v = projection;

        if (rejectionLength > 1.0f / 65536.0f)
        {
            Vector3 rejectionNorm = rejection.normalized;

            float rejectionLength2 = Mathf.Min(rejectionLength, projection.magnitude * d);
            v += rejectionNorm * rejectionLength2;
        }
    }

    public static void RotateVectorAroundAxis(ref Vector3 v, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(-angle, axis);
        v = rot * v;
    }

    public static void RotateMatrixAroundAxis(ref Quaternion v, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(-angle, axis);
        v = rot * v;
    }

    public static Vector3 GetProjection(Vector3 a, Vector3 b)
    {
        Vector3 normal = NormalOrZero(b);
        return normal * Vector3.Dot(normal, a);
    }

    public static Vector3 NormalOrZero(Vector3 v)
    {
        if (v.sqrMagnitude > 0.0f)
        {
            return v.normalized;
        }
        else
        {
            return Vector3.zero;
        }
    }

    public static float ReplaceNaN(float v, float d)
    {
        return float.IsNaN(v) ? d : v;
    }

    public static uint GetPosition(uint bits, float v)
    {
        int temp = (int)v;
        if (temp < 0)
        {
            return 0;
        }
        else if (temp > ((1 << (int)bits) - 1))
        {
            return (uint)((1 << (int)bits) - 1);
        }
        else
        {
            return (uint)temp;
        }
    }
}