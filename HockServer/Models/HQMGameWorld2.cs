using HockServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Windows;
using UnityEngine.XR;

public class HQMGameWorld2
{
    public HQMGameObject[] Objects = new HQMGameObject[32];
    public HQMPhysicsConfiguration PhysicsConfig;
    public HQMRink Rink = new HQMRink(30, 61, 8.5f);
    public int PuckSlots;

    public HQMGameWorld2(HQMPhysicsConfiguration physicsConfig, int puckSlots)
    {
        PhysicsConfig = physicsConfig;
        PuckSlots = puckSlots;
    }

    public ObjectPacket[] GetPackets()
    {
        ObjectPacket[] packets = new ObjectPacket[32];
        for (int i = 0; i < 32; i++)
        {
            ObjectPacket packet = Objects[i] switch
            {
                HQMPuck puck => puck.GetPacket(),
                HQMSkater player => player.GetPacket(),
                _ => new HQMNonePacket()
            };
            packets[i] = packet;
        }
        return packets;
    }

    public HQMObjectIndex? CreatePlayerObject(
        Vector3 start,
        Quaternion rot,
        HQMSkaterHand hand,
        float mass,
        float stickLimit)
    {
        HQMObjectIndex? objectSlot = FindEmptyPlayerSlot();
        if (objectSlot.HasValue)
        {
            Objects[objectSlot.Value.Index] = new HQMSkater(start, rot, hand, mass, stickLimit);
        }
        return objectSlot;
    }

    public HQMObjectIndex? CreatePuckObject(
        Vector3 start,
        Quaternion rot)
    {
        HQMObjectIndex? objectSlot = FindEmptyPuckSlot();
        if (objectSlot.HasValue)
        {
            Objects[objectSlot.Value.Index] = new HQMPuck(start, rot);
        }
        return objectSlot;
    }

    private HQMObjectIndex? FindEmptyPuckSlot()
    {
        for (int i = 0; i < PuckSlots; i++)
        {
            if (Objects[i] == null)
            {
                return new HQMObjectIndex(i);
            }
        }
        return null;
    }

    private HQMObjectIndex? FindEmptyPlayerSlot()
    {
        for (int i = PuckSlots; i < Objects.Length; i++)
        {
            if (Objects[i] == null)
            {
                return new HQMObjectIndex(i);
            }
        }
        return null;
    }

    public void ClearPucks()
    {
        for (int i = 0; i < PuckSlots; i++)
        {
            Objects[i] = null;
        }
    }

    public bool RemovePlayer(HQMObjectIndex index)
    {
        if (Objects[index.Index] is HQMSkater player)
        {
            Objects[index.Index] = null;
            return true;
        }
        return false;
    }

    public bool RemovePuck(HQMObjectIndex index)
    {
        if (Objects[index.Index] is HQMPuck puck)
        {
            Objects[index.Index] = null;
            return true;
        }
        return false;
    }

    public List<HQMSimulationEvent> SimulateStep()
    {
        var events = new List<HQMSimulationEvent>();
        var players = new List<(int, HQMSkater)>();
        var pucks = new List<(int, HQMPuck)>();

        for (int i = 0; i < Objects.Length; i++)
        {
            var obj = Objects[i];
            switch (obj)
            {
                case HQMSkater player:
                    players.Add((i, player));
                    break;
                case HQMPuck puck:
                    pucks.Add((i, puck));
                    break;
            }
        }

        var collisions = new List<HQMCollision>();
        for (int i = 0; i < players.Count; i++)
        {
            var item = players[i];
            var skater = UpdatePlayer(item.Item1, item.Item2, PhysicsConfig, Rink, ref collisions);
            players[i] = (item.Item1, skater);
            i += 1;
        }

        for (int i = 0; i < players.Count; i++)
        {
            var (_, p1) = players[i];

            for (int j = i + 1; j < players.Count; j++)
            {
                var (_, p2) = players[j];

                var p1i = 0;
                foreach (var p1CollisionBall in p1.CollisionBalls)
                {
                    var p2i = 0;
                    foreach (var p2CollisionBall in p2.CollisionBalls)
                    {
                        var posDiff = p1CollisionBall.Position - p2CollisionBall.Position;
                        var radiusSum = p1CollisionBall.Radius + p2CollisionBall.Radius;
                        if (posDiff.magnitude < radiusSum)
                        {
                            var overlap = radiusSum - posDiff.magnitude;
                            var normal = posDiff.normalized;
                            collisions.Add(new PlayerPlayerCollision(
                                (i, p1i),
                                (j, p2i),
                                overlap,
                                normal
                            ));
                        }

                        p2i += 1;
                    }
                    p1i += 1;
                }

                var stickV = p1.StickPos - p2.StickPos;
                var stickDistance = stickV.magnitude;
                if (stickDistance < 0.25f)
                {
                    var stickOverlap = 0.25f - stickDistance;
                    var normal = stickV.normalized;
                    var force = 0.125f * stickOverlap * normal + 0.25f * (p2.StickVelocity - p1.StickVelocity);
                    if (Vector3.Dot(force, normal) > 0.0f)
                    {
                        LimitFriction(ref force, normal, 0.01f);
                        force *= 0.5f;
                        p1.StickVelocity += 0.5f * force;
                        p2.StickVelocity -= 0.5f * force;
                    }
                }
            }
        }

        var pucksOldPos = new List<Vector3>();
        foreach (var (_, puck) in pucks)
        {
            pucksOldPos.Add(puck.Position);
            puck.LinearVelocity = new Vector3(puck.LinearVelocity.x, puck.LinearVelocity.y - PhysicsConfig.Gravity, puck.LinearVelocity.z);
        }

        UpdateSticksAndPucks(ref players, ref pucks, Rink, ref events, PhysicsConfig);

        for (int i = 0; i < pucks.Count; i++)
        {
            var (puckIndex, puck) = pucks[i];
            var oldPuckPos = pucksOldPos[i];

            if (puck.LinearVelocity.magnitude > 1.0f / 65536.0f)
            {
                var scale = Mathf.Pow(puck.LinearVelocity.magnitude, 2) * 0.125f * 0.125f;
                var scaled = scale * puck.LinearVelocity.normalized;
                puck.LinearVelocity -= scaled;
            }
            if (puck.AngularVelocity.magnitude > 1.0f / 65536.0f)
            {
                puck.Rotation = RotateMatrixAroundAxis(puck.Rotation, puck.AngularVelocity.normalized, puck.AngularVelocity.magnitude);
            }

            PuckDetection(puck, puckIndex, oldPuckPos, Rink, ref events);
        }

        ApplyCollisions(ref players, collisions);


        return events;
    }

    private void UpdateSticksAndPucks(ref List<(int, HQMSkater)> players, ref List<(int, HQMPuck)> pucks, HQMRink rink, ref List<HQMSimulationEvent> events, HQMPhysicsConfiguration PhysicsConfig)
    {
        for (int i = 0; i < 10; i++)
        {
            foreach (var (_, player) in players)
            {
                player.StickPos += 0.1f * player.StickVelocity;
            }
            foreach (var (puckIndex, puck) in pucks)
            {
                puck.Position += 0.1f * puck.LinearVelocity;

                var puckLinearVelocityBefore = puck.LinearVelocity;
                var puckAngularVelocityBefore = puck.AngularVelocity;
                var puckVertices = puck.GetPuckVertices();
                if (i == 0)
                {
                    DoPuckRinkForces(puck, puckVertices, rink, puckLinearVelocityBefore, puckAngularVelocityBefore, PhysicsConfig.PuckRinkFriction);
                }
                foreach (var (playerIndex, player) in players)
                {
                    var oldStickVelocity = player.StickVelocity;
                    if ((puck.Position - player.StickPos).magnitude < 1.0f)
                    {
                        var hasTouched = DoPuckStickForces(puck, player, puckVertices, puckLinearVelocityBefore, puckAngularVelocityBefore, oldStickVelocity);
                        if (hasTouched)
                        {
                            events.Add(new PuckTouchEvent(new HQMObjectIndex(puckIndex), new HQMObjectIndex(playerIndex)));
                        }
                    }
                }
                var redNetCollision = DoPuckPostForces(puck, rink.RedNet, puckLinearVelocityBefore, puckAngularVelocityBefore);
                var blueNetCollision = DoPuckPostForces(puck, rink.BlueNet, puckLinearVelocityBefore, puckAngularVelocityBefore);

                redNetCollision |= DoPuckNetForces(puck, rink.RedNet, puckLinearVelocityBefore, puckAngularVelocityBefore);
                blueNetCollision |= DoPuckNetForces(puck, rink.BlueNet, puckLinearVelocityBefore, puckAngularVelocityBefore);

                if (redNetCollision)
                {
                    events.Add(new PuckTouchedNetEvent(HQMTeam.Red, new HQMObjectIndex(puckIndex)));
                }
                if (blueNetCollision)
                {
                    events.Add(new PuckTouchedNetEvent(HQMTeam.Blue, new HQMObjectIndex(puckIndex)));
                }
            }
        }
    }

    private HQMSkater UpdatePlayer(int i, HQMSkater player, HQMPhysicsConfiguration PhysicsConfig, HQMRink rink, ref List<HQMCollision> collisions)
    {
        var linearVelocityBefore = player.LinearVelocity;
        var angularVelocityBefore = player.AngularVelocity;

        player.Position += player.LinearVelocity;
        player.LinearVelocity = new Vector3(player.LinearVelocity.x, player.LinearVelocity.y - PhysicsConfig.Gravity, player.LinearVelocity.z);
        foreach (var collisionBall in player.CollisionBalls)
        {
            collisionBall.Velocity *= 0.999f;
            collisionBall.Position += collisionBall.Velocity;
            collisionBall.Velocity = new Vector3(collisionBall.Velocity.x, collisionBall.Velocity.y - PhysicsConfig.Gravity, collisionBall.Velocity.z); 
        }

        var feetPos = player.Position - player.Rotation * (player.Height * Vector3.up);
        if (feetPos.y < 0.0f)
        {
            var fwbwFromClient = Mathf.Clamp(player.Input.Fwbw, -1.0f, 1.0f);
            if (fwbwFromClient != 0.0f)
            {
                var skateDirection = fwbwFromClient > 0.0f ? player.Rotation * -Vector3.forward : player.Rotation * Vector3.forward;
                skateDirection.y = 0.0f;
                skateDirection.Normalize();
                var maxAcceleration = Vector3.Dot(player.LinearVelocity, skateDirection) < 0.0f ? PhysicsConfig.PlayerDeceleration : PhysicsConfig.PlayerAcceleration;
                var newAcceleration = PhysicsConfig.MaxPlayerSpeed * skateDirection - player.LinearVelocity;
                player.LinearVelocity += LimitVectorLength(newAcceleration, maxAcceleration);
            }
            if (player.Input.Jump && !player.JumpedLastFrame)
            {
                var diff = PhysicsConfig.LimitJumpSpeed ? Mathf.Clamp(0.025f - player.LinearVelocity.y, 0.0f, 0.025f) : 0.025f;
                if (diff != 0.0f)
                {
                    player.LinearVelocity = new Vector3(player.LinearVelocity.x, player.LinearVelocity.y + diff, player.LinearVelocity.z); 
                    foreach (var collisionBall in player.CollisionBalls)
                    {
                        collisionBall.Velocity = new Vector3(collisionBall.Velocity.x, collisionBall.Velocity.y + diff, collisionBall.Velocity.z); 
                    }
                }
            }
        }
        player.JumpedLastFrame = player.Input.Jump;

        // Turn player
        var turn = Mathf.Clamp(player.Input.Turn, -1.0f, 1.0f);
        if (player.Input.Shift)
        {
            var velocityDirection = player.Rotation * Vector3.right;
            velocityDirection.y = 0.0f;
            velocityDirection.Normalize();

            var velocityAdjustment = PhysicsConfig.MaxPlayerShiftSpeed * turn * velocityDirection - player.LinearVelocity;
            player.LinearVelocity += LimitVectorLength(velocityAdjustment, PhysicsConfig.PlayerShiftAcceleration);
            var turnChange = -turn * PhysicsConfig.PlayerShiftTurning * (player.Rotation * Vector3.up);
            player.AngularVelocity += turnChange;
        }
        else
        {
            var turnChange = turn * PhysicsConfig.PlayerTurning * (player.Rotation * Vector3.up);
            player.AngularVelocity += turnChange;
        }

        if (player.AngularVelocity.magnitude > 1.0f / 65536.0f)
        {
            player.Rotation = RotateMatrixAroundAxis(player.Rotation, player.AngularVelocity.normalized, player.AngularVelocity.magnitude);
        }
        player.HeadRot = AdjustHeadBodyRot(player.HeadRot, Mathf.Clamp(player.Input.HeadRot, -7.0f * Mathf.PI / 8.0f, 7.0f * Mathf.PI / 8.0f));
        player.BodyRot = AdjustHeadBodyRot(player.BodyRot, Mathf.Clamp(player.Input.BodyRot, -Mathf.PI / 2.0f, Mathf.PI / 2.0f));

        for (int collisionBallIndex = 0; collisionBallIndex < player.CollisionBalls.Count; collisionBallIndex++)
        {
            var collisionBall = player.CollisionBalls[collisionBallIndex];
            var newRot = player.Rotation;
            if (collisionBallIndex == 1 || collisionBallIndex == 2 || collisionBallIndex == 5)
            {
                var rotAxis = newRot * Vector3.up;
                newRot = RotateMatrixAroundAxis( newRot, rotAxis, player.HeadRot * 0.5f);
                rotAxis = newRot * Vector3.right;
                newRot = RotateMatrixAroundAxis( newRot, rotAxis, player.BodyRot);
            }


            var intendedCollisionBallPos = player.Position + newRot * collisionBall.Offset;
            var collisionPosDiff = intendedCollisionBallPos - collisionBall.Position;

            var speed = SpeedOfPointIncludingRotation(intendedCollisionBallPos, player.Position, linearVelocityBefore, angularVelocityBefore);
            var force = 0.125f * collisionPosDiff + 0.25f * (speed - collisionBall.Velocity);
            collisionBall.Velocity += 0.9375f * force;


            player.Body = ApplyAccelerationToObject( player.Body, (0.9375f - 1.0f) * force, intendedCollisionBallPos);
        }

        for (int ib = 0; ib < player.CollisionBalls.Count; ib++)
        {
            var collisionBall = player.CollisionBalls[ib];
            var collision = CollisionBetweenCollisionBallAndRink(collisionBall, rink);
            if (collision.HasValue)
            {
                var (overlap, normal) = collision.Value;
                collisions.Add(new PlayerRinkCollision((i, ib), overlap, normal));
            }
        }

        linearVelocityBefore = player.LinearVelocity;
        angularVelocityBefore = player.AngularVelocity;

        if (player.Input.Crouch)
        {
            player.Height = Mathf.Max(player.Height - 0.015625f, 0.25f);
        }
        else
        {
            player.Height = Mathf.Min(player.Height + 0.125f, 0.75f);
        }





        feetPos = player.Position - player.Rotation * (player.Height * Vector3.up);
        var touchesIce = false;
        if (feetPos.y < 0.0f)
        {
            var unitY = Vector3.up;
            var temp2 = 0.25f * ((-feetPos.y * 0.125f * 0.125f) * unitY - player.LinearVelocity);
            if (Vector3.Dot(temp2, unitY) > 0.0f)
            {
                var (axis, rejectionLimit) = player.Input.Shift ? (Vector3.right, 0.4f) : (Vector3.forward, 1.2f);
                var direction = player.Rotation * axis;
                direction.y = 0.0f;
                direction.Normalize();

                var acceleration = temp2 - GetProjection(temp2, direction);
                LimitFriction(ref acceleration, unitY, rejectionLimit);
                player.LinearVelocity += acceleration;
                touchesIce = true;
            }
        }
        if (player.Position.y < 0.5f && player.LinearVelocity.magnitude < 0.025f)
        {
            player.LinearVelocity += new Vector3(player.LinearVelocity.x, player.LinearVelocity.y + 0.00055555555f, player.LinearVelocity.z);
            touchesIce = true;
        }
        if (touchesIce)
        {
            player.AngularVelocity *= 0.975f;
            var intendedUp = Vector3.up;

            if (!player.Input.Shift)
            {
                var axis = player.Rotation * Vector3.forward;
                var fractionOfMaxSpeed = Vector3.Dot(player.LinearVelocity, axis) / PhysicsConfig.MaxPlayerSpeed;
                RotateVectorAroundAxis(ref intendedUp, axis, -0.225f * turn * fractionOfMaxSpeed);
            }

            var rotation1 = Vector3.Cross(intendedUp, player.Rotation * Vector3.up);
            if (rotation1.sqrMagnitude > 0.0f)
            {
                var rotation1Direction = rotation1.normalized;
                var angularChange = 0.008333333f * rotation1 - 0.25f * GetProjection(player.AngularVelocity, rotation1Direction);
                angularChange = LimitVectorLength(angularChange, 0.000347222222f);
                player.AngularVelocity += angularChange;
            }
        }

        UpdateStick(player, linearVelocityBefore, angularVelocityBefore, rink);

        return player;
    }

    private float ReplaceNaN(float value, float defaultValue)
    {
        return float.IsNaN(value) ? defaultValue : value;
    }

    private Quaternion CalculateStickRotation(HQMSkater skater, float mul)
    {
        Vector3 pivot1Pos = skater.Position + skater.Rotation * new Vector3(-0.375f * mul, -0.5f, -0.125f);

        Vector3 stickPosConverted = Transpose(skater.Rotation) * (skater.StickPos - pivot1Pos);

        float currentAzimuth = Mathf.Atan2(stickPosConverted.x, -stickPosConverted.z);
        float currentInclination = -Mathf.Atan2(stickPosConverted.y, Mathf.Sqrt(stickPosConverted.x * stickPosConverted.x + stickPosConverted.z * stickPosConverted.z));

        Quaternion newStickRotation = skater.Rotation;

        newStickRotation = RotateMatrixSpherical(newStickRotation, currentAzimuth, currentInclination);

        if (skater.StickPlacement.y > 0.0f)
        {
            Vector3 axis = newStickRotation * Vector3.up;
            newStickRotation = RotateMatrixAroundAxis(newStickRotation, axis, skater.StickPlacement.y * mul * Mathf.PI / 2);
        }

        Vector3 handleAxis = newStickRotation * Vector3.Normalize(new Vector3(0.0f, 0.75f, 1.0f));
        newStickRotation = RotateMatrixAroundAxis(newStickRotation, handleAxis, -Mathf.Clamp(ReplaceNaN(skater.Input.StickAngle, 0.0f), -1.0f, 1.0f) * Mathf.PI / 4);

        return newStickRotation;
    }

    private (Vector3, Vector3) CalculateStickForceAndPosition(HQMSkater skater, Vector3 linearVelocityBefore, Vector3 angularVelocityBefore, float mul)
    {
        Quaternion stickRotation2 = skater.Rotation;

        stickRotation2 = RotateMatrixSpherical(stickRotation2, skater.StickPlacement.x, skater.StickPlacement.y);

        Vector3 temp = stickRotation2 * Vector3.right;
        stickRotation2 = RotateMatrixAroundAxis(stickRotation2, temp, Mathf.PI / 4);

        float stickLength = 1.75f;

        Vector3 stickTopPosition = skater.Position + skater.Rotation * new Vector3(-0.375f * mul, 0.5f, -0.125f);

        Vector3 intendedStickPosition = stickTopPosition + stickRotation2 * new Vector3(0.0f, 0.0f, -stickLength);

        if (intendedStickPosition.y < 0.0f)
        {
            intendedStickPosition.y = 0.0f;
        }

        Vector3 speedAtStickPos = SpeedOfPointIncludingRotation(intendedStickPosition, skater.Position, linearVelocityBefore, angularVelocityBefore);

        Vector3 stickForce = 0.125f * (intendedStickPosition - skater.StickPos) + (speedAtStickPos - skater.StickVelocity) * 0.5f;

        return (stickForce, intendedStickPosition);
    }

    private void UpdateStick(HQMSkater player, Vector3 linearVelocityBefore, Vector3 angularVelocityBefore, HQMRink rink)
    {

        Vector2 stickInput = new Vector2(
            Mathf.Clamp(ReplaceNaN(player.Input.Stick[0], 0.0f), -Mathf.PI / 2, Mathf.PI / 2),
            Mathf.Clamp(ReplaceNaN(player.Input.Stick[1], 0.0f), -5.0f * Mathf.PI / 16.0f, Mathf.PI / 8.0f)
        );

        Vector2 placementDiff = stickInput - player.StickPlacement;

        Vector2 placementChange = placementDiff * 0.0625f - player.StickPlacementDelta * 0.5f;

        player.StickPlacementDelta += placementChange;

        player.StickPlacement += player.StickPlacementDelta;

        float mul = player.Hand == HQMSkaterHand.Right ? 1.0f : -1.0f;

        player.StickRot = CalculateStickRotation(player, mul);

        (Vector3 stickForce, Vector3 intendedStickPosition) = CalculateStickForceAndPosition(player, linearVelocityBefore, angularVelocityBefore, mul);

        player.StickVelocity += 0.996f * stickForce;

        player.Body = ApplyAccelerationToObject(player.Body, -0.004f * stickForce, intendedStickPosition);

        var col = CollisionBetweenSphereAndRink(player.StickPos, 0.09375f, rink);
        if (col != null && col.Value.overlap > 0.0f)
        {

            Vector3 n = col.Value.overlap * 0.25f * col.Value.normal - 0.5f * player.StickVelocity;

            if (Vector3.Dot(n, col.Value.normal) > 0.0f)
            {
                LimitFriction(ref n, col.Value.normal, 0.1f);

                player.StickVelocity += n;
            }
        }
    }

    private void UpdateStickTemp(HQMSkater player, Vector3 linearVelocityBefore, Vector3 angularVelocityBefore, HQMRink rink)
    {
        var stickInput = new Vector2(
            Mathf.Clamp(player.Input.Stick[0], -Mathf.PI / 2.0f, Mathf.PI / 2.0f),
            Mathf.Clamp(player.Input.Stick[1], -5.0f * Mathf.PI / 16.0f, Mathf.PI / 8.0f)
        );

        var placementDiff = stickInput - player.StickPlacement;
        var placementChange = placementDiff * 0.0625f - player.StickPlacementDelta * 0.5f;

        if (player.LimitTypeValue != 0.0f)
        {
            placementChange = LimitVectorLength2(placementChange, player.LimitTypeValue);
        }

        player.StickPlacementDelta += placementChange;
        player.StickPlacement += player.StickPlacementDelta;

        var mul = player.Hand == HQMSkaterHand.Right ? 1.0f : -1.0f;
        player.StickRot = player.Rotation;
        player.StickRot = RotateMatrixSpherical(player.StickRot, player.StickPlacement[0], player.StickPlacement[1]);


        if (player.StickPlacement[1] > 0.0f)
        {
            var rotAxis = player.StickRot * Vector3.up;
            player.StickRot = RotateMatrixAroundAxis( player.StickRot, rotAxis, player.StickPlacement[1] * mul * Mathf.PI / 2.0f);
        }

        var handleAxis = player.StickRot * new Vector3(0.0f, 0.75f, 1.0f).normalized;
        player.StickRot = RotateMatrixAroundAxis(player.StickRot, handleAxis, (-Mathf.Clamp(player.Input.StickAngle, -1.0f, 1.0f)) * Mathf.PI / 4.0f);


        var stickRotation2 = player.Rotation;
        stickRotation2 = RotateMatrixSpherical(stickRotation2, player.StickPlacement[0], player.StickPlacement[1]);

        var temp = stickRotation2 * Vector3.right;
        stickRotation2 = RotateMatrixAroundAxis(stickRotation2, temp, Mathf.PI / 4.0f);


        var stickLength = 1.75f;
        var stickTopPosition = player.Position + player.Rotation * new Vector3(-0.375f * mul, 0.5f, -0.125f);
        var intendedStickPosition = stickTopPosition + stickRotation2 * new Vector3(0.0f, 0.0f, -stickLength);
        if (intendedStickPosition.y < 0.0f)
        {
            intendedStickPosition.y = 0.0f;
        }


       

        var speedAtStickPos = SpeedOfPointIncludingRotation(intendedStickPosition, player.Position, linearVelocityBefore, angularVelocityBefore);
        var stickForce = 0.125f * (intendedStickPosition - player.StickPos) + (speedAtStickPos - player.StickVelocity) * 0.5f;

        player.StickVelocity += 0.996f * stickForce;

        player.Body = ApplyAccelerationToObject(player.Body, -0.004f * stickForce, intendedStickPosition);

        if (CollisionBetweenSphereAndRink(player.StickPos, 0.09375f, rink) is (float overlap, Vector3 normal) collision)
        {
            var mutN = overlap * 0.25f * normal - 0.5f * player.StickVelocity;
            if (Vector3.Dot(mutN, normal) > 0.0f)
            {
                LimitFriction(ref mutN, normal, 0.1f);
                player.StickVelocity += mutN;
            }
        }
    }

    private Vector3 LimitVectorLength(Vector3 v, float maxLen)
    {
        var norm = v.magnitude;
        if (norm > maxLen)
        {
            v *= maxLen / norm;
        }
        return v;
    }

    private Vector2 LimitVectorLength2(Vector2 v, float maxLen)
    {
        var norm = v.magnitude;
        if (norm > maxLen)
        {
            v *= maxLen / norm;
        }
        return v;
    }

    private void LimitFriction(ref Vector3 v, Vector3 normal, float d)
    {
        var projectionLength = Vector3.Dot(v, normal);
        var projection = projectionLength * normal;
        var rejection = v - projection;
        var rejectionLength = rejection.magnitude;
        v = projection;

        if (rejectionLength > 1.0f / 65536.0f)
        {
            var rejectionNorm = rejection.normalized;
            var rejectionLength2 = Mathf.Min(rejectionLength, projection.magnitude * d);
            v += rejectionLength2 * rejectionNorm;
        }
    }

    private void RotateVectorAroundAxis(ref Vector3 v, Vector3 axis, float angle)
    {
        var rot = AngleAxis(-angle , axis);
        v = rot * v;
    }

    private Quaternion RotateMatrixAroundAxis(Quaternion v, Vector3 axis, float angle)
    {
        angle = angle * Mathf.Rad2Deg;
        var rot = AngleAxis(-angle , axis);
        v = rot * v;

        return v;
    }

    public  Quaternion AngleAxis(float angle, Vector3 axis)
    {
        axis.Normalize();

        float radians = angle * Mathf.Deg2Rad;

        float halfAngle = radians * 0.5f;
        float sinHalfAngle = Mathf.Sin(halfAngle);

        float w = Mathf.Cos(halfAngle);
        float x = axis.x * sinHalfAngle;
        float y = axis.y * sinHalfAngle;
        float z = axis.z * sinHalfAngle;

        return new Quaternion(x, y, z, w);
    }

    private Quaternion RotateMatrixSpherical(Quaternion q, float azimuth, float inclination)
    {
        var col1 = q * Vector3.up;
        q = RotateMatrixAroundAxis(q, col1, azimuth);
        var col0 = q * Vector3.right;
        q = RotateMatrixAroundAxis(q, col0, inclination);

        return q;
    }

    private float AdjustHeadBodyRot( float rot, float inputRot)
    {
        var headRotDiff = inputRot - rot;
        if (headRotDiff <= 0.06666667f)
        {
            if (headRotDiff >= -0.06666667f)
            {
                rot = inputRot;
            }
            else
            {
                rot -= 0.06666667f;
            }
        }
        else
        {
            rot += 0.06666667f;
        }

        return rot;
    }

    private Vector3 GetProjection(Vector3 a, Vector3 normal)
    {
        return Vector3.Dot(a, normal) * normal;
    }

    private void ApplyCollisions(ref List<(int, HQMSkater)> players, List<HQMCollision> collisions)
    {
        for (int _ = 0; _ < 16; _++)
        {
            var originalBallVelocities = new List<List<Vector3>>();
            foreach (var (_, skater) in players)
            {
                originalBallVelocities.Add(skater.CollisionBalls.ConvertAll(x => x.Velocity));
            }

            foreach (var collisionEvent in collisions)
            {
                try
                {
                    switch (collisionEvent)
                    {
                        case PlayerRinkCollision prc:
                            var originalVelocity = originalBallVelocities[prc.Indices.Item1][prc.Indices.Item2];
                            var mutNew = prc.Overlap * 0.03125f * prc.Normal - 0.25f * originalVelocity;
                            if (Vector3.Dot(mutNew, prc.Normal) > 0.0f)
                            {
                                LimitFriction(ref mutNew, prc.Normal, 0.01f);
                                var (_, skater) = players[prc.Indices.Item1];
                                skater.CollisionBalls[prc.Indices.Item2].Velocity += mutNew;
                            }
                            break;
                        case PlayerPlayerCollision ppc:
                            var originalVelocity1 = originalBallVelocities[ppc.Indices1.Item1][ppc.Indices1.Item2];
                            var originalVelocity2 = originalBallVelocities[ppc.Indices2.Item1][ppc.Indices2.Item2];
                            var mutNew2 = ppc.Normal * (ppc.Overlap * 0.125f) + 0.25f * (originalVelocity2 - originalVelocity1);
                            if (Vector3.Dot(mutNew2, ppc.Normal) > 0.0f)
                            {
                                LimitFriction(ref mutNew2, ppc.Normal, 0.01f);
                                var (_, skater1) = players[ppc.Indices1.Item1];
                                var (_, skater2) = players[ppc.Indices2.Item1];
                                var mass1 = skater1.CollisionBalls[ppc.Indices1.Item2].Mass;
                                var mass2 = skater2.CollisionBalls[ppc.Indices2.Item2].Mass;
                                var massSum = mass1 + mass2;
                                skater1.CollisionBalls[ppc.Indices1.Item2].Velocity += (mass2 / massSum) * mutNew2;
                                skater2.CollisionBalls[ppc.Indices2.Item2].Velocity -= (mass1 / massSum) * mutNew2;
                            }
                            break;
                    }
                }
                catch { }
            }
        }
    }

    private void PuckDetection(HQMPuck puck, int puckIndex, Vector3 oldPuckPos, HQMRink rink, ref List<HQMSimulationEvent> events)
    {
        var puckPos = puck.Position;

        void CheckLines(int puckIndex, Vector3 puckPos, Vector3 oldPuckPos, float puckRadius, HQMTeam team, HQMRink rink, ref List<HQMSimulationEvent> events)
        {
            var (ownSide, otherSide, defensiveLine, offensiveLine) = team == HQMTeam.Red ? (HQMRinkSideOfLine.RedSide, HQMRinkSideOfLine.BlueSide, rink.RedZoneBlueLine, rink.BlueZoneBlueLine) : (HQMRinkSideOfLine.BlueSide, HQMRinkSideOfLine.RedSide, rink.BlueZoneBlueLine, rink.RedZoneBlueLine);
            var oldPosition = defensiveLine.SideOfLine(oldPuckPos, puckRadius);
            var position = defensiveLine.SideOfLine(puckPos, puckRadius);

            if (oldPosition == ownSide && position != ownSide)
            {
                events.Add(new PuckReachedDefensiveLineEvent(team, new HQMObjectIndex(puckIndex)));
            }
            if (position == otherSide && oldPosition != otherSide)
            {
                events.Add(new PuckPassedDefensiveLineEvent(team, new HQMObjectIndex(puckIndex)));
            }
            oldPosition = rink.CenterLine.SideOfLine(oldPuckPos, puckRadius);
            position = rink.CenterLine.SideOfLine(puckPos, puckRadius);

            if (oldPosition == ownSide && position != ownSide)
            {
                events.Add(new PuckReachedCenterLineEvent(team, new HQMObjectIndex(puckIndex)));
            }
            if (position == otherSide && oldPosition != otherSide)
            {
                events.Add(new PuckPassedCenterLineEvent(team, new HQMObjectIndex(puckIndex)));
            }

            oldPosition = offensiveLine.SideOfLine(oldPuckPos, puckRadius);
            position = offensiveLine.SideOfLine(puckPos, puckRadius);

            if (oldPosition == ownSide && position != ownSide)
            {
                events.Add(new PuckReachedOffensiveZoneEvent(team, new HQMObjectIndex(puckIndex)));
            }
            if (position == otherSide && oldPosition != otherSide)
            {
                events.Add(new PuckEnteredOffensiveZoneEvent(team, new HQMObjectIndex(puckIndex)));
            }
        }

        void CheckNet(int puckIndex, Vector3 puckPos, Vector3 oldPuckPos, HQMRinkNet net, HQMTeam team, ref List<HQMSimulationEvent> events)
        {
            if (Vector3.Dot(net.LeftPost - puckPos, net.Normal) >= 0.0f)
            {
                if (Vector3.Dot(net.LeftPost - oldPuckPos, net.Normal) < 0.0f)
                {
                    if (Vector3.Dot(net.LeftPost - puckPos, net.LeftPostInside) < 0.0f && Vector3.Dot(net.RightPost - puckPos, net.RightPostInside) < 0.0f && puckPos.y < 1.0f)
                    {
                        events.Add(new PuckEnteredNetEvent(team, new HQMObjectIndex(puckIndex)));
                    }
                    else
                    {
                        events.Add(new PuckPassedGoalLineEvent(team, new HQMObjectIndex(puckIndex)));
                    }
                }
            }
        }

        CheckLines(puckIndex, puckPos, oldPuckPos, puck.Radius, HQMTeam.Red, rink, ref events);
        CheckLines(puckIndex, puckPos, oldPuckPos, puck.Radius, HQMTeam.Blue, rink, ref events);
        CheckNet(puckIndex, puckPos, oldPuckPos, rink.RedNet, HQMTeam.Red, ref events);
        CheckNet(puckIndex, puckPos, oldPuckPos, rink.BlueNet, HQMTeam.Blue, ref events);
    }

    private bool DoPuckNetForces(HQMPuck puck, HQMRinkNet net, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity)
    {
        var mutRes = false;
        if (CollisionBetweenSphereAndNet(puck.Position, puck.Radius, net) is (Vector3 overlapPos, float overlap, Vector3 normal) collision)
        {
            mutRes = true;
            var vertexVelocity = SpeedOfPointIncludingRotation(overlapPos, puck.Position, puckLinearVelocity, puckAngularVelocity);
            var mutPuckForce = normal * (0.5f * overlap) - 0.5f * vertexVelocity;

            if (Vector3.Dot(mutPuckForce, normal) > 0.0f)
            {
                LimitFriction(ref mutPuckForce, normal, 0.5f);
                puck.Body = ApplyAccelerationToObject( puck.Body, mutPuckForce, overlapPos);
                puck.LinearVelocity *= 0.9875f;
                puck.AngularVelocity *= 0.95f;
            }
        }
        return mutRes;
    }

    private bool DoPuckPostForces(HQMPuck puck, HQMRinkNet net, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity)
    {
        var mutRes = false;
        foreach (var post in net.Posts)
        {
            if (CollisionBetweenSphereAndPost(puck.Position, puck.Radius, post) is (float overlap, Vector3 normal) collision)
            {
                mutRes = true;
                var p = puck.Position - normal * puck.Radius;
                var vertexVelocity = SpeedOfPointIncludingRotation(p, puck.Position, puckLinearVelocity, puckAngularVelocity);
                var mutPuckForce = normal * (overlap * 0.125f) - 0.25f * vertexVelocity;

                if (Vector3.Dot(mutPuckForce, normal) > 0.0f)
                {
                    LimitFriction(ref mutPuckForce, normal, 0.2f);
                    puck.Body = ApplyAccelerationToObject(puck.Body, mutPuckForce, p);
                }
            }
        }
        return mutRes;
    }

    private bool DoPuckStickForces(HQMPuck puck, HQMSkater player, List<Vector3> puckVertices, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity, Vector3 stickVelocity)
    {
        var stickSurfaces = GetStickSurfaces(player);
        var mutRes = false;
        foreach (var puckVertex in puckVertices)
        {
            if (CollisionBetweenPuckVertexAndStick(puck.Position, puckVertex, stickSurfaces.ToList()) is (float dot, Vector3 normal) col)
            {
                mutRes = true;
                var puckVertexSpeed = SpeedOfPointIncludingRotation(puckVertex, puck.Position, puckLinearVelocity, puckAngularVelocity);

                var mutPuckForce = dot * 0.125f * 0.5f * normal + 0.125f * (stickVelocity - puckVertexSpeed);
                if (Vector3.Dot(mutPuckForce, normal) > 0.0f)
                {
                    LimitFriction(ref mutPuckForce, normal, 0.5f);
                    player.StickVelocity -= 0.25f * mutPuckForce;
                    mutPuckForce *= 0.75f;
                    puck.Body = ApplyAccelerationToObject(puck.Body, mutPuckForce, puckVertex);
                }
            }
        }
        return mutRes;
    }

    private void DoPuckRinkForces(HQMPuck puck, List<Vector3> puckVertices, HQMRink rink, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity, float friction)
    {
        foreach (var vertex in puckVertices)
        {
            if (CollisionBetweenVertexAndRink(vertex, rink) is (float overlap, Vector3 normal) c)
            {
                var vertexVelocity = SpeedOfPointIncludingRotation(vertex, puck.Position, puckLinearVelocity, puckAngularVelocity);
                var mutPuckForce = 0.125f * 0.125f * (overlap * 0.5f * normal - vertexVelocity);

                if (Vector3.Dot(mutPuckForce, normal) > 0.0f)
                {
                    LimitFriction(ref mutPuckForce, normal, friction);
                    puck.Body = ApplyAccelerationToObject(puck.Body, mutPuckForce, vertex);
                }
            }
        }
    }

    private (Vector3, Vector3, Vector3, Vector3)[] GetStickSurfaces(HQMSkater player)
    {
        Vector3 stickSize = new Vector3(0.0625f, 0.25f, 0.5f);
        Vector3 nnn = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, -0.5f, -0.5f), stickSize);
        Vector3 nnp = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, -0.5f, 0.5f), stickSize);
        Vector3 npn = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, 0.5f, -0.5f), stickSize);
        Vector3 npp = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, 0.5f, 0.5f), stickSize);
        Vector3 pnn = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, -0.5f, -0.5f), stickSize);
        Vector3 pnp = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, -0.5f, 0.5f), stickSize);
        Vector3 ppn = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, 0.5f, -0.5f), stickSize);
        Vector3 ppp = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, 0.5f, 0.5f), stickSize);

        return new (Vector3, Vector3, Vector3, Vector3)[]
        {
            (nnp, pnp, pnn, nnn),
            (npp, ppp, pnp, nnp),
            (npn, npp, nnp, nnn),
            (ppn, npn, nnn, pnn),
            (ppp, ppn, pnn, pnp),
            (npn, ppn, ppp, npp)
        };
    }

    private bool InsideSurface(Vector3 pos, (Vector3, Vector3, Vector3, Vector3) surface, Vector3 normal)
    {
        var (p1, p2, p3, p4) = surface;
        return Vector3.Dot(Vector3.Cross(pos - p1, p2 - p1), normal) >= 0.0f
            && Vector3.Dot(Vector3.Cross(pos - p2, p3 - p2), normal) >= 0.0f
            && Vector3.Dot(Vector3.Cross(pos - p3, p4 - p3), normal) >= 0.0f
            && Vector3.Dot(Vector3.Cross(pos - p4, p1 - p4), normal) >= 0.0f;
    }

    private (Vector3, float, Vector3)? CollisionBetweenSphereAndNet(Vector3 pos, float radius, HQMRinkNet net)
    {
        var mutMaxOverlap = 0.0f;
        (Vector3, float, Vector3)? mutRes = null;

        foreach (var surface in net.Surfaces)
        {
            Vector3 normal = Vector3.Cross((surface.Item4 - surface.Item1), (surface.Item2 - surface.Item1)).normalized;

            var diff = surface.Item1 - pos;
            var dot = Vector3.Dot(diff, normal);
            var overlap = dot + radius;
            var overlap2 = -dot + radius;

            if (overlap > 0.0f && overlap < radius)
            {
                var overlapPos = pos + (radius - overlap) * normal;
                if (InsideSurface(overlapPos, surface, normal))
                {
                    if (overlap > mutMaxOverlap)
                    {
                        mutMaxOverlap = overlap;
                        mutRes = (overlapPos, overlap, normal);
                    }
                }
            }
            else if (overlap2 > 0.0f && overlap2 < radius)
            {
                var overlapPos = pos + (radius - overlap) * normal;
                if (InsideSurface(overlapPos, surface, normal))
                {
                    if (overlap2 > mutMaxOverlap)
                    {
                        mutMaxOverlap = overlap2;
                        mutRes = (overlapPos, overlap2, -normal);
                    }
                }
            }
        }

        return mutRes;
    }

    private (float, Vector3)? CollisionBetweenSphereAndPost(Vector3 pos, float radius, (Vector3, Vector3, float) post)
    {
        var (p1, p2, postRadius) = post;
        var a = postRadius + radius;
        var directionVector = p2 - p1;

        var diff = pos - p1;
        var t0 = Vector3.Dot(diff, directionVector) / directionVector.sqrMagnitude;
        var dot = Mathf.Clamp(t0, 0.0f, 1.0f);

        var projection = dot * directionVector;
        var rejection = diff - projection;
        var rejectionNorm = rejection.magnitude;
        var overlap = a - rejectionNorm;
        if (overlap > 0.0f)
        {
            return (overlap, rejection.normalized);
        }
        else
        {
            return null;
        }
    }

    private (float, Vector3, float, Vector3)? CollisionBetweenPuckAndSurface(Vector3 puckPos, Vector3 puckPos2, (Vector3, Vector3, Vector3, Vector3) surface)
    {
        var normal = Vector3.Cross((surface.Item4 - surface.Item1),(surface.Item2 - surface.Item1)).normalized;
        var p1 = surface.Item1;
        var puckPos2Projection = Vector3.Dot(p1 - puckPos2, normal);
        if (puckPos2Projection >= 0.0f)
        {
            var puckPosProjection = Vector3.Dot(p1 - puckPos, normal);
            if (puckPosProjection <= 0.0f)
            {
                var diff = puckPos2 - puckPos;
                var diffProjection = Vector3.Dot(diff, normal);
                if (diffProjection != 0.0f)
                {
                    var intersection = puckPosProjection / diffProjection;
                    var intersectionPos = puckPos + intersection * diff;

                    var overlap = Vector3.Dot(intersectionPos - puckPos2, normal);

                    if (InsideSurface(intersectionPos, surface, normal))
                    {
                        return (intersection, intersectionPos, overlap, normal);
                    }
                }
            }
        }
        return null;
    }

    private (float, Vector3)? CollisionBetweenPuckVertexAndStick(Vector3 puckPos, Vector3 puckVertex, List<(Vector3, Vector3, Vector3, Vector3)> stickSurfaces)
    {
        var mutMinIntersection = 1f;
        (float, Vector3)? mutRes = null;
        foreach (var stickSurface in stickSurfaces)
        {
            if (CollisionBetweenPuckAndSurface(puckPos, puckVertex, stickSurface) is (float intersection, Vector3 intersectionPos, float overlap, Vector3 normal) collision)
            {
                if (intersection < mutMinIntersection)
                {
                    mutRes = (overlap, normal);
                    mutMinIntersection = intersection;
                }
            }
        }
        return mutRes;
    }

    private (float overlap, Vector3 normal)? CollisionBetweenSphereAndRink(Vector3 pos, float radius, HQMRink rink)
    {
        float maxOverlap = 0f;
        Vector3? collNormal = null;
        foreach (var (p, normal) in rink.Planes)
        {
            float overlap = Vector3.Dot((p - pos), normal) + radius;
            if (overlap > maxOverlap)
            {
                maxOverlap = overlap;
                collNormal = normal;
            }
        }
        foreach (var (p, dir, cornerRadius) in rink.Corners)
        {
            Vector3 p2 = p - pos;
            p2.y = 0.0f;
            if (p2.x * dir.x < 0.0f && p2.z * dir.z < 0.0f)
            {
                float overlap = p2.magnitude + radius - cornerRadius;
                if (overlap > maxOverlap)
                {
                    maxOverlap = overlap;
                    Vector3 p2n = p2.normalized;
                    collNormal = p2n;
                }
            }
        }

        return collNormal.HasValue ? ((float overlap, Vector3 normal)?)(maxOverlap, collNormal.Value) : null;
    }

    private (float, Vector3)? CollisionBetweenCollisionBallAndRink(HQMSkaterCollisionBall ball, HQMRink rink)
    {
        return CollisionBetweenSphereAndRink(ball.Position, ball.Radius, rink);
    }

    private (float, Vector3)? CollisionBetweenVertexAndRink(Vector3 vertex, HQMRink rink)
    {
        return CollisionBetweenSphereAndRink(vertex, 0.0f, rink);
    }

    private HQMBody ApplyAccelerationToObject( HQMBody body, Vector3 change, Vector3 point)
    {
        var diff1 = point - body.Position;

        body.LinearVelocity += change;
        var cross = Vector3.Cross(change, diff1);
        body.AngularVelocity += body.Rotation * ComponentMul((Transpose(body.Rotation) * cross), body.RotMul);

        return body;
    }

    private Vector3 SpeedOfPointIncludingRotation(Vector3 p, Vector3 pos, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        return linearVelocity + Vector3.Cross(p - pos, angularVelocity);
    }

    public static Vector3 ComponentMul(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    public static Quaternion Transpose(Quaternion a)
    {
        return new Quaternion(-a.x, -a.y, -a.z, a.w);
    }
}