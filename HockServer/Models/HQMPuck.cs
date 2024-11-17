using HockServer.Models;
using System.Collections.Generic;
using UnityEngine;

public class HQMPuck : HQMGameObject
{
    public HQMBody Body { get; set; }
    public float Radius { get; set; }
    public float Height { get; set; }

    public Vector3 Position
    {
        get => Body.Position;
        set => Body.Position = value;
    }

    public Quaternion Rotation
    {
        get => Body.Rotation;
        set => Body.Rotation = value;
    }

    public Vector3 LinearVelocity
    {
        get => Body.LinearVelocity;
        set => Body.LinearVelocity = value;
    }

    public Vector3 AngularVelocity
    {
        get => Body.AngularVelocity;
        set => Body.AngularVelocity = value;
    }

    public HQMPuck(Vector3 position, Quaternion rotation)
    {
        Body = new HQMBody(position, Vector3.zero, rotation, Vector3.zero, new Vector3(223.5f, 128.0f, 223.5f));
        Radius = 0.125f;
        Height = 0.0412500016391f;
    }

    public HQMPuckPacket GetPacket()
    {
        var rot = RotationHelper.ConvertMatrixToNetwork(31, Matrix4x4.Rotate(Rotation));
        return new HQMPuckPacket
        {
            Pos = (
                HQMHelpers.GetPosition(17, 1024 * Position.x),
                HQMHelpers.GetPosition(17, 1024 * Position.y),
                HQMHelpers.GetPosition(17, 1024 * Position.z)
            ),
            Rot = (
                 rot.Item1,
                 rot.Item2
            )
        };
    }

    public List<Vector3> GetPuckVertices()
    {
        List<Vector3> vertices = new List<Vector3>();
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8.0f;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            for (int j = -1; j <= 1; j++)
            {
                Vector3 point = new Vector3(cos * Radius, j * Height, sin * Radius);
                Vector3 point2 = Body.Rotation * point;
                vertices.Add(Body.Position + point2);
            }
        }
        return vertices;
    }
}