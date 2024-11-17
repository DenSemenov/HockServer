using UnityEngine;

public class HQMBody
{
    public Vector3 Position { get; set; }
    public Vector3 LinearVelocity { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 AngularVelocity { get; set; }
    public Vector3 RotMul { get; set; }

    public HQMBody(Vector3 position, Vector3 linearVelocity, Quaternion rotation, Vector3 angularVelocity, Vector3 rotMul)
    {
        Position = position;
        LinearVelocity = linearVelocity;
        Rotation = rotation;
        AngularVelocity = angularVelocity;
        RotMul = rotMul;
    }
}