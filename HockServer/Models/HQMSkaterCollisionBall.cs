using UnityEngine;

public class HQMSkaterCollisionBall
{
    public Vector3 Offset { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public float Radius { get; set; }
    public float Mass { get; set; }

    public HQMSkaterCollisionBall(Vector3 offset, Vector3 skaterPos, Quaternion skaterRot, Vector3 velocity, float radius, float mass)
    {
        Offset = offset;
        Position = skaterPos + skaterRot * offset;
        Velocity = velocity;
        Radius = radius;
        Mass = mass;
    }
}