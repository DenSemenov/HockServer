using UnityEngine;

public interface HQMGameObject
{
    Vector3 Position { get; set; }
    Quaternion Rotation { get; set; }
    Vector3 LinearVelocity { get; set; }
    Vector3 AngularVelocity { get; set; }
}