using HockServer.Models;
using System.Collections.Generic;
using UnityEngine;

public class HQMSkater : HQMGameObject
{
    public HQMBody Body { get; set; }
    public Vector3 StickPos { get; set; }
    public Vector3 StickVelocity { get; set; }
    public Quaternion StickRot { get; set; }
    public float HeadRot { get; set; }
    public float BodyRot { get; set; }
    public float Height { get; set; }
    public HQMPlayerInput Input { get; set; }
    public bool JumpedLastFrame { get; set; }
    public Vector2 StickPlacement { get; set; }
    public Vector2 StickPlacementDelta { get; set; }
    public List<HQMSkaterCollisionBall> CollisionBalls { get; set; }
    public HQMSkaterHand Hand { get; set; } = HQMSkaterHand.Left;
    public float LimitTypeValue { get; set; } = 0;

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

    public HQMSkater(Vector3 position, Quaternion rotation, HQMSkaterHand hand, float mass, float limitTypeValue)
    {
        Body = new HQMBody(position, Vector3.zero, rotation, Vector3.zero, new Vector3(2.75f, 6.16f, 2.35f));
        StickPos = position;
        StickVelocity = Vector3.zero;
        StickRot = Quaternion.identity;
        HeadRot = 0.0f;
        BodyRot = 0.0f;
        Height = 0.75f;
        Input = new HQMPlayerInput();
        JumpedLastFrame = false;
        StickPlacement = Vector2.zero;
        StickPlacementDelta = Vector2.zero;
        Hand = hand;
        LimitTypeValue = limitTypeValue;
        CollisionBalls = GetCollisionBalls(position, rotation, Vector3.zero, mass);
    }

    private List<HQMSkaterCollisionBall> GetCollisionBalls(Vector3 position, Quaternion rotation, Vector3 linearVelocity, float mass)
    {
        List<HQMSkaterCollisionBall> collisionBalls = new List<HQMSkaterCollisionBall>
        {
            new HQMSkaterCollisionBall(new Vector3(0.0f, 0.0f, 0.0f), position, rotation, linearVelocity, 0.225f, mass),
            new HQMSkaterCollisionBall(new Vector3(0.25f, 0.3125f, 0.0f), position, rotation, linearVelocity, 0.25f, mass),
            new HQMSkaterCollisionBall(new Vector3(-0.25f, 0.3125f, 0.0f), position, rotation, linearVelocity, 0.25f, mass),
            new HQMSkaterCollisionBall(new Vector3(-0.1875f, -0.1875f, 0.0f), position, rotation, linearVelocity, 0.1875f, mass),
            new HQMSkaterCollisionBall(new Vector3(0.1875f, -0.1875f, 0.0f), position, rotation, linearVelocity, 0.1875f, mass),
            new HQMSkaterCollisionBall(new Vector3(0.0f, 0.5f, 0.0f), position, rotation, linearVelocity, 0.1875f, mass)
        };
        return collisionBalls;
    }

    public HQMSkaterPacket GetPacket()
    {
        var rot = RotationHelper.ConvertMatrixToNetwork(31, Matrix4x4.Rotate(Rotation));
        var stickRot = RotationHelper.ConvertMatrixToNetwork(24, Matrix4x4.Rotate(StickRot));
        return new HQMSkaterPacket
        {
            Pos = (
                HQMHelpers.GetPosition(17, 1024 * Position.x),
                HQMHelpers.GetPosition(17, 1024 * Position.y),
                HQMHelpers.GetPosition(17, 1024 * Position.z)
            ),
            Rot = rot,
            StickPos = (
                HQMHelpers.GetPosition(13, 1024 * (StickPos.x - Position.x + 4)),
                HQMHelpers.GetPosition(13, 1024 * (StickPos.y - Position.y + 4)),
                HQMHelpers.GetPosition(13, 1024 * (StickPos.z - Position.z + 4))
            ),
            StickRot = stickRot,
            HeadRot = HQMHelpers.GetPosition(16, 8192 * (HeadRot + 2)),
            BodyRot = HQMHelpers.GetPosition(16, 8192 * (BodyRot + 2)),
        };
    }
}