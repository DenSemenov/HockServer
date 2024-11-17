//using HockServer.Models;
//using System;
//using System.Collections.Generic;
//using UnityEngine;

//public class HQMGameWorld
//{
//    public HQMGameObject[] Objects = new HQMGameObject[32];
//    public HQMPhysicsConfiguration PhysicsConfig;
//    public HQMRink Rink = new HQMRink(30,61, 8.5f);
//    public int PuckSlots;

//    public HQMGameWorld(HQMPhysicsConfiguration PhysicsConfig, int puckSlots)
//    {
//        PhysicsConfig = PhysicsConfig;
//        PuckSlots = puckSlots;
//    }

//    public ObjectPacket[] GetPackets()
//    {
//        ObjectPacket[] packets = new ObjectPacket[32];
//        for (int i = 0; i < 32; i++)
//        {
//            ObjectPacket packet = Objects[i] switch
//            {
//                HQMPuck puck => puck.GetPacket(),
//                HQMSkater player => player.GetPacket(),
//                _ => new HQMNonePacket()
//            } ;
//            packets[i] = packet;
//        }
//        return packets;
//    }

//    public HQMObjectIndex? CreatePlayerObject(
//        Vector3 start,
//        Quaternion rot,
//        HQMSkaterHand hand,
//        float mass,
//        float stickLimit)
//    {
//        HQMObjectIndex? objectSlot = FindEmptyPlayerSlot();
//        if (objectSlot.HasValue)
//        {
//            Objects[objectSlot.Value.Index] = new HQMSkater(start, rot, hand, mass, stickLimit);
//        }
//        return objectSlot;
//    }

//    public HQMObjectIndex? CreatePuckObject(
//        Vector3 start,
//        Quaternion rot)
//    {
//        HQMObjectIndex? objectSlot = FindEmptyPuckSlot();
//        if (objectSlot.HasValue)
//        {
//            Objects[objectSlot.Value.Index] = new HQMPuck(start, rot);
//        }
//        return objectSlot;
//    }

//    private HQMObjectIndex? FindEmptyPuckSlot()
//    {
//        for (int i = 0; i < PuckSlots; i++)
//        {
//            if (Objects[i] == null)
//            {
//                return new HQMObjectIndex (i);
//            }
//        }
//        return null;
//    }

//    private HQMObjectIndex? FindEmptyPlayerSlot()
//    {
//        for (int i = PuckSlots; i < Objects.Length; i++)
//        {
//            if (Objects[i] == null)
//            {
//                return new HQMObjectIndex(i);
//            }
//        }
//        return null;
//    }

//    public void ClearPucks()
//    {
//        for (int i = 0; i < PuckSlots; i++)
//        {
//            Objects[i] = null;
//        }
//    }

//    public bool RemovePlayer(HQMObjectIndex index)
//    {
//        if (Objects[index.Index] is HQMSkater player)
//        {
//            Objects[index.Index] = null;
//            return true;
//        }
//        return false;
//    }

//    public bool RemovePuck(HQMObjectIndex index)
//    {
//        if (Objects[index.Index] is HQMPuck puck)
//        {
//            Objects[index.Index] = null;
//            return true;
//        }
//        return false;
//    }

//    public List<HQMSimulationEvent> SimulateStep()
//    {
//        List<HQMSimulationEvent> events = new List<HQMSimulationEvent>();
//        List<(int, HQMSkater)> players = new List<(int, HQMSkater)>();
//        List<(int, HQMPuck)> pucks = new List<(int, HQMPuck)>();

//        for (int i = 0; i < Objects.Length; i++)
//        {
//            if (Objects[i] is HQMSkater player)
//            {
//                players.Add((i, player));
//            }
//            else if (Objects[i] is HQMPuck puck)
//            {
//                pucks.Add((i, puck));
//            }
//        }

//        List<HQMCollision> collisions = new List<HQMCollision>();
//        for (int i = 0; i < players.Count; i++)
//        {
//            UpdatePlayer(i, players[i].Item2, PhysicsConfig, Rink, collisions);
//        }

//        for (int i = 0; i < players.Count; i++)
//        {
//            var p1 = players[i].Item2;
//            for (int j = i + 1; j < players.Count; j++)
//            {
//                var p2 = players[j].Item2;

//                for (int ib = 0; ib < p1.CollisionBalls.Count; ib++)
//                {
//                    var p1CollisionBall = p1.CollisionBalls[ib];

//                    for (int jb = 0; jb < p2.CollisionBalls.Count; jb++)
//                    {
//                        var p2CollisionBall = p2.CollisionBalls[jb];

//                        Vector3 posDiff = p1CollisionBall.Position - p2CollisionBall.Position;
//                        float radiusSum = p1CollisionBall.Radius + p2CollisionBall.Radius;

//                        if (posDiff.magnitude < radiusSum)
//                        {
//                            float overlap = radiusSum - posDiff.magnitude;
//                            Vector3 normal = posDiff.normalized;

//                            collisions.Add(new PlayerPlayerCollision((i, ib), (j, jb), overlap, normal));
//                        }
//                    }
//                }

//                Vector3 stickV = p1.StickPos - p2.StickPos;
//                float stickDistance = stickV.magnitude;

//                if (stickDistance < 0.25f)
//                {
//                    float stickOverlap = 0.25f - stickDistance;
//                    Vector3 normal = stickV.normalized;
//                    Vector3 force = normal * (0.125f * stickOverlap) + (p2.StickVelocity - p1.StickVelocity) * 0.25f;

//                    if (Vector3.Dot(force, normal) > 0.0f)
//                    {
//                        LimitFriction(ref force, normal, 0.01f);
//                        p1.StickVelocity += force * 0.5f;
//                        p2.StickVelocity -= force * 0.5f;
//                    }
//                }
//            }
//        }

//        List<Vector3> pucksOldPos = pucks.ConvertAll(p => p.Item2.Position);

//        foreach (var puck in pucks)
//        {
//            puck.Item2.LinearVelocity = new Vector3(puck.Item2.LinearVelocity.x, puck.Item2.LinearVelocity.y - PhysicsConfig.Gravity, puck.Item2.LinearVelocity.z);
//        }

//        UpdateSticksAndPucks(players, pucks, Rink, events, PhysicsConfig);

//        for (int i = 0; i < pucks.Count; i++)
//        {
//            var puck = pucks[i].Item2;
//            var oldPuckPos = pucksOldPos[i];

//            if (puck.LinearVelocity.magnitude > 1.0f / 65536.0f)
//            {
//                float scale = Mathf.Pow(puck.LinearVelocity.magnitude, 2) * 0.125f * 0.125f;
//                puck.LinearVelocity -= puck.LinearVelocity.normalized * scale;
//            }
//            if (puck.AngularVelocity.magnitude > 1.0f / 65536.0f)
//            {
//                puck.Rotation = RotateMatrixAroundAxis(puck.Rotation, puck.AngularVelocity.normalized, puck.AngularVelocity.magnitude);
//            }

//            //PuckDetection(puck, i, oldPuckPos, HQMTeam.Red, Rink.RedLinesAndNet, events);
//            //PuckDetection(puck, i, oldPuckPos, HQMTeam.Blue, Rink.BlueLinesAndNet, events);
//        }

//        ApplyCollisions(players, collisions);
//        return events;
//    }

//    private void UpdateSticksAndPucks(List<(int, HQMSkater)> players, List<(int, HQMPuck)> pucks, HQMRink rink, List<HQMSimulationEvent> events, HQMPhysicsConfiguration PhysicsConfig)
//    {
//        for (int i = 0; i < 10; i++)
//        {
//            foreach (var player in players)
//            {
//                player.Item2.StickPos += player.Item2.StickVelocity * 0.1f;
//            }
//            foreach (var puck in pucks)
//            {
//                puck.Item2.Position += puck.Item2.LinearVelocity * 0.1f;

//                Vector3 puckLinearVelocityBefore = puck.Item2.LinearVelocity;
//                Vector3 puckAngularVelocityBefore = puck.Item2.AngularVelocity;
//                var puckVertices = puck.Item2.GetPuckVertices().ToArray();

//                if (i == 0)
//                {
//                    DoPuckRinkForces(puck.Item2, puckVertices, rink, puckLinearVelocityBefore, puckAngularVelocityBefore, PhysicsConfig.PuckRinkFriction);
//                }

//                foreach (var player in players)
//                {
//                    Vector3 oldStickVelocity = player.Item2.StickVelocity;
//                    if ((puck.Item2.Position - player.Item2.StickPos).magnitude < 1.0f)
//                    {
//                        bool hasTouched = DoPuckStickForces(puck.Item2, player.Item2, puckVertices, puckLinearVelocityBefore, puckAngularVelocityBefore, oldStickVelocity);
//                        if (hasTouched)
//                        {
//                            events.Add(HQMSimulationEvent.PuckTouch);
//                        }
//                    }
//                }

//                //bool redNetCollision = DoPuckPostForces(puck.Item2, rink.RedLinesAndNet.Net, puckLinearVelocityBefore, puckAngularVelocityBefore);
//                //bool blueNetCollision = DoPuckPostForces(puck.Item2, rink.BlueLinesAndNet.Net, puckLinearVelocityBefore, puckAngularVelocityBefore);

//                //redNetCollision |= DoPuckNetForces(puck.Item2, rink.RedLinesAndNet.Net, puckLinearVelocityBefore, puckAngularVelocityBefore);
//                //blueNetCollision |= DoPuckNetForces(puck.Item2, rink.BlueLinesAndNet.Net, puckLinearVelocityBefore, puckAngularVelocityBefore);

//                //if (redNetCollision)
//                //{
//                //    events.Add(HQMSimulationEvent.PuckTouchedNet);
//                //}
//                //if (blueNetCollision)
//                //{
//                //    events.Add(HQMSimulationEvent.PuckTouchedNet);
//                //}
//            }
//        }
//    }

//    private void UpdatePlayer(int i, HQMSkater player, HQMPhysicsConfiguration PhysicsConfig, HQMRink rink, List<HQMCollision> collisions)
//    {
//        Vector3 linearVelocityBefore = player.LinearVelocity;
//        Vector3 angularVelocityBefore = player.AngularVelocity;

//        player.Position += player.LinearVelocity;
//        player.LinearVelocity = new Vector3(player.LinearVelocity.x, player.LinearVelocity.y - PhysicsConfig.Gravity, player.LinearVelocity.z);
//        foreach (var collisionBall in player.CollisionBalls)
//        {
//            collisionBall.Velocity *= 0.999f;
//            collisionBall.Position += collisionBall.Velocity;
//            collisionBall.Velocity = new Vector3(collisionBall.Velocity.x, collisionBall.Velocity.y - PhysicsConfig.Gravity, collisionBall.Velocity.z);
//        }

//        Vector3 feetPos = player.Position - player.Rotation * Vector3.up * player.Height;
//        if (feetPos.y < 0.0f)
//        {
//            float fwbwFromClient = Mathf.Clamp(player.Input.Fwbw, -1.0f, 1.0f);
//            if (fwbwFromClient != 0.0f)
//            {
//                Vector3 skateDirection = fwbwFromClient > 0.0f ? player.Rotation * -Vector3.forward : player.Rotation * Vector3.forward;
//                skateDirection.y = 0.0f;
//                skateDirection.Normalize();
//                Vector3 newAcceleration = skateDirection * PhysicsConfig.MaxPlayerSpeed - player.LinearVelocity;
//                player.LinearVelocity += LimitVectorLength(newAcceleration, PhysicsConfig.PlayerAcceleration);
//            }
//            if (player.Input.Jump && !player.JumpedLastFrame)
//            {
//                float diff = PhysicsConfig.LimitJumpSpeed ? Mathf.Clamp(0.025f - player.LinearVelocity.y, 0.0f, 0.025f) : 0.025f;
//                if (diff != 0.0f)
//                {
//                    player.LinearVelocity = new Vector3(player.LinearVelocity.x, player.LinearVelocity.y + diff, player.LinearVelocity.z);
//                    foreach (var collisionBall in player.CollisionBalls)
//                    {
//                        collisionBall.Velocity = new Vector3(collisionBall.Velocity.x, collisionBall.Velocity.y + diff, collisionBall.Velocity.z);
//                    }
//                }
//            }
//        }
//        player.JumpedLastFrame = player.Input.Jump;

//        float turn = Mathf.Clamp(player.Input.Turn, -1.0f, 1.0f);
//        Vector3 turnChange = player.Rotation * Vector3.up;
//        if (player.Input.Shift && PhysicsConfig.ShiftEnabled)
//        {
//            Vector3 velocityAdjustment = player.Rotation * Vector3.right;
//            velocityAdjustment.y = 0.0f;
//            velocityAdjustment.Normalize();
//            velocityAdjustment *= PhysicsConfig.MaxPlayerShiftSpeed * turn;
//            velocityAdjustment -= player.LinearVelocity;
//            player.LinearVelocity += LimitVectorLength(velocityAdjustment, PhysicsConfig.PlayerShiftAcceleration);
//            turnChange *= -turn * PhysicsConfig.PlayerShiftTurning;
//            player.AngularVelocity += turnChange;
//        }
//        else
//        {
//            turnChange *= turn * PhysicsConfig.PlayerTurning;
//            player.AngularVelocity += turnChange;
//        }

//        if (player.AngularVelocity.magnitude > 1.0f / 65536.0f)
//        {
//            player.Rotation = RotateMatrixAroundAxis(player.Rotation, player.AngularVelocity.normalized, player.AngularVelocity.magnitude);
//        }
//        player.HeadRot = AdjustHeadBodyRot(player.HeadRot, Mathf.Clamp(player.Input.HeadRot, -7.0f * Mathf.PI / 8.0f, 7.0f * Mathf.PI / 8.0f));
//        player.BodyRot = AdjustHeadBodyRot(player.BodyRot, Mathf.Clamp(player.Input.BodyRot, -Mathf.PI / 2.0f, Mathf.PI / 2.0f));

//        for (int collisionBallIndex = 0; collisionBallIndex < player.CollisionBalls.Count; collisionBallIndex++)
//        {
//            var collisionBall = player.CollisionBalls[collisionBallIndex];
//            Quaternion newRot = player.Rotation;
//            if (collisionBallIndex == 1 || collisionBallIndex == 2 || collisionBallIndex == 5)
//            {
//                Vector3 rotAxis = newRot * Vector3.up;
//                newRot = RotateMatrixAroundAxis(newRot, rotAxis, player.HeadRot * 0.5f);
//                rotAxis = newRot * Vector3.right;
//                newRot = RotateMatrixAroundAxis(newRot, rotAxis, player.BodyRot);
//            }
//            Vector3 intendedCollisionBallPos = player.Position + newRot * collisionBall.Offset;
//            Vector3 collisionPosDiff = intendedCollisionBallPos - collisionBall.Position;

//            Vector3 speed = SpeedOfPointIncludingRotation(intendedCollisionBallPos, player.Position, linearVelocityBefore, angularVelocityBefore);
//            Vector3 force = collisionPosDiff * 0.125f + (speed - collisionBall.Velocity) * 0.25f;
//            collisionBall.Velocity += force * 0.9375f;
//            player.Body = ApplyAccelerationToObject(player.Body, force * (0.9375f - 1.0f), intendedCollisionBallPos, false);
//        }

//        for (int ib = 0; ib < player.CollisionBalls.Count; ib++)
//        {
//            var collisionBall = player.CollisionBalls[ib];
//            var collision = CollisionBetweenCollisionBallAndRink(collisionBall, rink);
//            if (collision.HasValue)
//            {
//                collisions.Add(new PlayerRinkCollision((i, ib), collision.Value.overlap, collision.Value.normal));
//            }
//        }

//        if (player.Input.Crouch)
//        {
//            player.Height = Mathf.Max(player.Height - 0.015625f, 0.25f);
//        }
//        else
//        {
//            player.Height = Mathf.Min(player.Height + 0.125f, 0.75f);
//        }

//        feetPos = player.Position - player.Rotation * Vector3.up * player.Height;
//        bool touchesIce = false;
//        if (feetPos.y < 0.0f)
//        {
//            float temp1 = -feetPos.y * 0.125f * 0.125f * 0.25f;
//            Vector3 unitY = Vector3.up;

//            Vector3 temp2 = unitY * temp1 - player.LinearVelocity * 0.25f;
//            if (temp2.y > 0.0f)
//            {
//                Vector3 column = player.Input.Shift && PhysicsConfig.ShiftEnabled ? Vector3.right : Vector3.forward;
//                float rejectionLimit = player.Input.Shift && PhysicsConfig.ShiftEnabled ? 0.4f : 1.2f;
//                Vector3 direction = player.Rotation * column;
//                direction.y = 0.0f;

//                temp2 -= GetProjection(temp2, direction);

//                LimitFriction(ref temp2, unitY, rejectionLimit);
//                player.LinearVelocity += temp2;
//                touchesIce = true;
//            }
//        }
//        if (player.Position.y < 0.5f && player.LinearVelocity.magnitude < 0.025f)
//        {
//            player.LinearVelocity = new Vector3(player.LinearVelocity.x, player.LinearVelocity.y + 0.00055555555f, player.LinearVelocity.z);
//            touchesIce = true;
//        }
//        if (touchesIce)
//        {
//            player.AngularVelocity *= 0.975f;
//            Vector3 unit = Vector3.up;

//            if (!player.Input.Shift || !PhysicsConfig.ShiftEnabled)
//            {
//                Vector3 axis = player.Rotation * Vector3.forward;
//                float temp = -player.LinearVelocity.z / PhysicsConfig.MaxPlayerSpeed;
//                RotateVectorAroundAxis(ref unit, axis, 0.225f * turn * temp);
//            }

//            Vector3 temp2 = Vector3.Cross(unit, player.Rotation * Vector3.up);

//            temp2 *= 0.008333333f;
//            temp2 -= GetProjection(player.AngularVelocity, temp2) * 0.25f;
//            temp2 = LimitVectorLength(temp2, 0.000347222222f);
//            player.AngularVelocity += temp2;
//        }
//        UpdateStick(player, linearVelocityBefore, angularVelocityBefore, rink);
//    }

//    private void UpdateStick(HQMSkater player, Vector3 linearVelocityBefore, Vector3 angularVelocityBefore, HQMRink rink)
//    {
//        Vector2 stickInput = new Vector2(
//            Mathf.Clamp(ReplaceNaN(player.Input.Stick[0], 0.0f), -Mathf.PI / 2.0f, Mathf.PI / 2.0f),
//            Mathf.Clamp(ReplaceNaN(player.Input.Stick[1], 0.0f), -5.0f * Mathf.PI / 16.0f, Mathf.PI / 8.0f)
//        );

//        Vector2 placementDiff = stickInput - player.StickPlacement;
//        Vector2 placementChange = placementDiff * 0.0625f - player.StickPlacementDelta * 0.5f;

//        if (player.LimitTypeValue != 0.0f)
//        {
//            placementChange = LimitVectorLength2(placementChange, player.LimitTypeValue);
//        }

//        player.StickPlacementDelta += placementChange;
//        player.StickPlacement += player.StickPlacementDelta;

//        float mul = player.Hand == HQMSkaterHand.Right ? 1.0f : -1.0f;
//        player.StickRot = player.Rotation;
//        player.StickRot = RotateMatrixSpherical( player.StickRot, player.StickPlacement[0], player.StickPlacement[1]);

//        if (player.StickPlacement[1] > 0.0f)
//        {
//            Vector3 axis = player.StickRot * Vector3.up;
//            player.StickRot =  RotateMatrixAroundAxis(player.StickRot, axis, player.StickPlacement[1] * mul * Mathf.PI / 2.0f);
//        }

//        Vector3 handleAxis = (player.StickRot * new Vector3(0.0f, 0.75f, 1.0f)).normalized;
//        player.StickRot = RotateMatrixAroundAxis(player.StickRot, handleAxis, Mathf.Clamp(-ReplaceNaN(player.Input.StickAngle, 0.0f), -1.0f, 1.0f) * Mathf.PI / 4.0f);

//        Vector3 stickTopPosition = player.Position + player.Rotation * new Vector3(-0.375f * mul, 0.5f, -0.125f);
//        Vector3 intendedStickPosition = stickTopPosition + player.StickRot * Vector3.forward * -1.75f;
//        if (intendedStickPosition.y < 0.0f)
//        {
//            intendedStickPosition.y = 0.0f;
//        }

//        Vector3 speedAtStickPos = SpeedOfPointIncludingRotation(intendedStickPosition, player.Position, linearVelocityBefore, angularVelocityBefore);
//        Vector3 stickForce = 0.125f * (intendedStickPosition - player.StickPos) + (speedAtStickPos - player.StickVelocity) * 0.5f;

//        player.StickVelocity += stickForce * 0.996f;
//        player.Body = ApplyAccelerationToObject(player.Body, stickForce * -0.004f, intendedStickPosition, false);

//        var collision = CollisionBetweenSphereAndRink(player.StickPos, 0.09375f, rink);
//        if (collision.HasValue)
//        {
//            Vector3 n = collision.Value.normal * collision.Value.overlap * 0.25f - player.StickVelocity * 0.5f;
//            if (n.y > 0.0f)
//            {
//                LimitFriction(ref n, collision.Value.normal, 0.1f);
//                player.StickVelocity += n;
//            }
//        }
//    }

//    private void ApplyCollisions(List<(int, HQMSkater)> players, List<HQMCollision> collisions)
//    {
//        for (int iteration = 0; iteration < 16; iteration++)
//        {
//            List<List<Vector3>> originalBallVelocities = players.ConvertAll(p => p.Item2.CollisionBalls.ConvertAll(b => b.Velocity));

//            foreach (var collisionEvent in collisions)
//            {
//                if (collisionEvent is PlayerRinkCollision playerRink)
//                {
//                    var (i, ib) = playerRink.PlayerIndex;
//                    var originalVelocity = originalBallVelocities[i][ib];
//                    Vector3 newVelocity = playerRink.Normal * playerRink.Overlap * 0.03125f - originalVelocity * 0.25f;
//                    if (Vector3.Dot(newVelocity, playerRink.Normal) > 0.0f)
//                    {
//                        LimitFriction(ref newVelocity, playerRink.Normal, 0.01f);
//                        players[i].Item2.CollisionBalls[ib].Velocity += newVelocity;
//                    }
//                }
//                else if (collisionEvent is PlayerPlayerCollision playerPlayer)
//                {
//                    var (i1, ib1) = playerPlayer.Player1Index;
//                    var (i2, ib2) = playerPlayer.Player2Index;
//                    var originalVelocity1 = originalBallVelocities[i1][ib1];
//                    var originalVelocity2 = originalBallVelocities[i2][ib2];

//                    Vector3 newVelocity = playerPlayer.Normal * playerPlayer.Overlap * 0.125f + (originalVelocity2 - originalVelocity1) * 0.25f;
//                    if (Vector3.Dot(newVelocity, playerPlayer.Normal) > 0.0f)
//                    {
//                        LimitFriction(ref newVelocity, playerPlayer.Normal, 0.01f);
//                        float mass1 = players[i1].Item2.CollisionBalls[ib1].Mass;
//                        float mass2 = players[i2].Item2.CollisionBalls[ib2].Mass;
//                        float massSum = mass1 + mass2;

//                        players[i1].Item2.CollisionBalls[ib1].Velocity += newVelocity * (mass2 / massSum);
//                        players[i2].Item2.CollisionBalls[ib2].Velocity -= newVelocity * (mass1 / massSum);
//                    }
//                }
//            }
//        }
//    }

//    private void PuckDetection(HQMPuck puck, int puckIndex, Vector3 oldPuckPos, HQMTeam team, LinesAndNet linesAndNet, List<HQMSimulationEvent> events)
//    {
//        if (linesAndNet.DefensiveLine.SphereReachedLine(puck.Position, puck.Radius) && !linesAndNet.DefensiveLine.SphereReachedLine(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckReachedDefensiveLine);
//        }
//        if (linesAndNet.DefensiveLine.SpherePastLeadingEdge(puck.Position, puck.Radius) && !linesAndNet.DefensiveLine.SpherePastLeadingEdge(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckPassedDefensiveLine);
//        }

//        if (linesAndNet.MidLine.SphereReachedLine(puck.Position, puck.Radius) && !linesAndNet.MidLine.SphereReachedLine(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckReachedCenterLine);
//        }
//        if (linesAndNet.MidLine.SpherePastLeadingEdge(puck.Position, puck.Radius) && !linesAndNet.MidLine.SpherePastLeadingEdge(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckPassedCenterLine);
//        }

//        if (linesAndNet.OffensiveLine.SphereReachedLine(puck.Position, puck.Radius) && !linesAndNet.OffensiveLine.SphereReachedLine(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckReachedOffensiveZone);
//        }
//        if (linesAndNet.OffensiveLine.SpherePastLeadingEdge(puck.Position, puck.Radius) && !linesAndNet.OffensiveLine.SpherePastLeadingEdge(oldPuckPos, puck.Radius))
//        {
//            events.Add(HQMSimulationEvent.PuckEnteredOffensiveZone);
//        }

//        if (Vector3.Dot(linesAndNet.Net.LeftPost - puck.Position, linesAndNet.Net.Normal) >= 0.0f)
//        {
//            if (Vector3.Dot(linesAndNet.Net.LeftPost - oldPuckPos, linesAndNet.Net.Normal) < 0.0f)
//            {
//                if (Vector3.Dot(linesAndNet.Net.LeftPost - puck.Position, linesAndNet.Net.LeftPostInside) < 0.0f &&
//                    Vector3.Dot(linesAndNet.Net.RightPost - puck.Position, linesAndNet.Net.RightPostInside) < 0.0f &&
//                    puck.Position.y < 1.0f)
//                {
//                    events.Add(HQMSimulationEvent.PuckEnteredNet);
//                }
//                else
//                {
//                    events.Add(HQMSimulationEvent.PuckPassedGoalLine);
//                }
//            }
//        }
//    }

//    private bool DoPuckNetForces(HQMPuck puck, HQMRinkNet net, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity)
//    {
//        bool res = false;
//        var collision = CollisionBetweenSphereAndNet(puck.Position, puck.Radius, net);
//        if (collision.HasValue)
//        {
//            res = true;
//            Vector3 vertexVelocity = SpeedOfPointIncludingRotation(collision.Value.overlapPos, puck.Position, puckLinearVelocity, puckAngularVelocity);
//            Vector3 puckForce = collision.Value.normal * collision.Value.overlap * 0.5f - vertexVelocity * 0.5f;

//            if (Vector3.Dot(puckForce, collision.Value.normal) > 0.0f)
//            {
//                LimitFriction(ref puckForce, collision.Value.normal, 0.5f);
//                puck.Body = ApplyAccelerationToObject(puck.Body, puckForce, collision.Value.overlapPos, false);
//                puck.Body.LinearVelocity *= 0.9875f;
//                puck.Body.AngularVelocity *= 0.95f;
//            }
//        }
//        return res;
//    }

//    private bool DoPuckPostForces(HQMPuck puck, HQMRinkNet net, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity)
//    {
//        bool res = false;
//        foreach (var post in net.Posts)
//        {
//            var collision = CollisionBetweenSphereAndPost(puck.Position, puck.Radius, post);
//            if (collision.HasValue)
//            {
//                res = true;
//                Vector3 p = puck.Position - collision.Value.normal * puck.Radius;
//                Vector3 vertexVelocity = SpeedOfPointIncludingRotation(p, puck.Position, puckLinearVelocity, puckAngularVelocity);
//                Vector3 puckForce = collision.Value.normal * collision.Value.overlap * 0.125f - vertexVelocity * 0.25f;

//                if (Vector3.Dot(puckForce, collision.Value.normal) > 0.0f)
//                {
//                    LimitFriction(ref puckForce, collision.Value.normal, 0.2f);
//                    puck.Body = ApplyAccelerationToObject(puck.Body, puckForce, p, false);
//                }
//            }
//        }
//        return res;
//    }

//    private bool DoPuckStickForces(HQMPuck puck, HQMSkater player, Vector3[] puckVertices, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity, Vector3 stickVelocity)
//    {
//        var stickSurfaces = GetStickSurfaces(player);
//        bool res = false;
//        foreach (var puckVertex in puckVertices)
//        {
//            var col = CollisionBetweenPuckVertexAndStick(puck.Position, puckVertex, stickSurfaces);
//            if (col.HasValue)
//            {
//                res = true;
//                Vector3 puckVertexSpeed = SpeedOfPointIncludingRotation(puckVertex, puck.Position, puckLinearVelocity, puckAngularVelocity);

//                Vector3 puckForce = col.Value.normal * col.Value.overlap * 0.125f * 0.5f + (stickVelocity - puckVertexSpeed) * 0.125f;
//                if (Vector3.Dot(puckForce, col.Value.normal) > 0.0f)
//                {
//                    LimitFriction(ref puckForce, col.Value.normal, 0.5f);
//                    player.StickVelocity -= puckForce * 0.25f;
//                    puckForce *= 0.75f;
//                    bool isLimited = player.LimitTypeValue == 0.0f || player.LimitTypeValue > 0.01f;
//                    puck.Body = ApplyAccelerationToObject(puck.Body, puckForce, puckVertex, isLimited);
//                }
//            }
//        }
//        return res;
//    }

//    private void DoPuckRinkForces(HQMPuck puck, Vector3[] puckVertices, HQMRink rink, Vector3 puckLinearVelocity, Vector3 puckAngularVelocity, float friction)
//    {
//        foreach (var vertex in puckVertices)
//        {
//            var c = CollisionBetweenVertexAndRink(vertex, rink);
//            if (c.HasValue)
//            {
//                Vector3 vertexVelocity = SpeedOfPointIncludingRotation(vertex, puck.Position, puckLinearVelocity, puckAngularVelocity);
//                Vector3 puckForce = (c.Value.normal * c.Value.overlap * 0.5f - vertexVelocity) * 0.125f * 0.125f;

//                if (Vector3.Dot(puckForce, c.Value.normal) > 0.0f)
//                {
//                    LimitFriction(ref puckForce, c.Value.normal, friction);
//                    puck.Body = ApplyAccelerationToObject(puck.Body, puckForce, vertex, false);
//                }
//            }
//        }
//    }

//    private (Vector3, Vector3, Vector3, Vector3)[] GetStickSurfaces(HQMSkater player)
//    {
//        Vector3 stickSize = new Vector3(0.0625f, 0.25f, 0.5f);
//        Vector3 nnn = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, -0.5f, -0.5f), stickSize);
//        Vector3 nnp = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, -0.5f, 0.5f), stickSize);
//        Vector3 npn = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, 0.5f, -0.5f), stickSize);
//        Vector3 npp = player.StickPos + player.StickRot * ComponentMul(new Vector3(-0.5f, 0.5f, 0.5f),stickSize);
//        Vector3 pnn = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, -0.5f, -0.5f),stickSize);
//        Vector3 pnp = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, -0.5f, 0.5f),stickSize);
//        Vector3 ppn = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, 0.5f, -0.5f),stickSize);
//        Vector3 ppp = player.StickPos + player.StickRot * ComponentMul(new Vector3(0.5f, 0.5f, 0.5f), stickSize);

//        return new (Vector3, Vector3, Vector3, Vector3)[]
//        {
//            (nnp, pnp, pnn, nnn),
//            (npp, ppp, pnp, nnp),
//            (npn, npp, nnp, nnn),
//            (ppn, npn, nnn, pnn),
//            (ppp, ppn, pnn, pnp),
//            (npn, ppn, ppp, npp)
//        };
//    }

//    private bool InsideSurface(Vector3 pos, (Vector3, Vector3, Vector3, Vector3) surface, Vector3 normal)
//    {
//        var (p1, p2, p3, p4) = surface;
//        return Vector3.Dot(Vector3.Cross(pos - p1, p2 - p1), normal) >= 0.0f
//            && Vector3.Dot(Vector3.Cross(pos - p2, p3 - p2), normal) >= 0.0f
//            && Vector3.Dot(Vector3.Cross(pos - p3, p4 - p3), normal) >= 0.0f
//            && Vector3.Dot(Vector3.Cross(pos - p4, p1 - p4), normal) >= 0.0f;
//    }

//    private (Vector3 overlapPos, float overlap, Vector3 normal)? CollisionBetweenSphereAndNet(Vector3 pos, float radius, HQMRinkNet net)
//    {
//        float maxOverlap = 0.0f;
//        (Vector3, float, Vector3)? res = null;

//        foreach (var surface in net.Surfaces)
//        {
//            Vector3 normal = Vector3.Cross((surface.Item4 - surface.Item1),(surface.Item2 - surface.Item1)).normalized;

//            Vector3 diff = surface.Item1 - pos;
//            float dot = Vector3.Dot(diff, normal);
//            float overlap = dot + radius;
//            float overlap2 = -dot + radius;

//            if (overlap > 0.0f && overlap < radius)
//            {
//                Vector3 overlapPos = pos + (radius - overlap) * normal;
//                if (InsideSurface(overlapPos, surface, normal))
//                {
//                    if (overlap > maxOverlap)
//                    {
//                        maxOverlap = overlap;
//                        res = (overlapPos, overlap, normal);
//                    }
//                }
//            }
//            else if (overlap2 > 0.0f && overlap2 < radius)
//            {
//                Vector3 overlapPos = pos + (radius - overlap) * normal;
//                if (InsideSurface(overlapPos, surface, normal))
//                {
//                    if (overlap2 > maxOverlap)
//                    {
//                        maxOverlap = overlap2;
//                        res = (overlapPos, overlap2, -normal);
//                    }
//                }
//            }
//        }

//        return res;
//    }

//    private (float overlap, Vector3 normal)? CollisionBetweenSphereAndPost(Vector3 pos, float radius, (Vector3, Vector3, float) post)
//    {
//        var (p1, p2, postRadius) = post;
//        float a = postRadius + radius;
//        Vector3 directionVector = p2 - p1;

//        Vector3 diff = pos - p1;
//        float t0 = Vector3.Dot(diff, directionVector) / directionVector.sqrMagnitude;
//        float dot = Mathf.Clamp(t0, 0.0f, 1.0f);

//        Vector3 projection = dot * directionVector;
//        Vector3 rejection = diff - projection;
//        float rejectionNorm = rejection.magnitude;
//        float overlap = a - rejectionNorm;
//        if (overlap > 0.0f)
//        {
//            return (overlap, rejection.normalized);
//        }
//        else
//        {
//            return null;
//        }
//    }

//    private (float intersection, Vector3 intersectionPos, float overlap, Vector3 normal)? CollisionBetweenPuckAndSurface(Vector3 puckPos, Vector3 puckPos2, (Vector3, Vector3, Vector3, Vector3) surface)
//    {
//        Vector3 normal = Vector3.Cross((surface.Item4 - surface.Item1),(surface.Item2 - surface.Item1)).normalized;
//        Vector3 p1 = surface.Item1;
//        float puckPos2Projection = Vector3.Dot(p1 - puckPos2, normal);
//        if (puckPos2Projection >= 0.0f)
//        {
//            float puckPosProjection = Vector3.Dot(p1 - puckPos, normal);
//            if (puckPosProjection <= 0.0f)
//            {
//                Vector3 diff = puckPos2 - puckPos;
//                float diffProjection = Vector3.Dot(diff, normal);
//                if (diffProjection != 0.0f)
//                {
//                    float intersection = puckPosProjection / diffProjection;
//                    Vector3 intersectionPos = puckPos + diff * intersection;

//                    float overlap = Vector3.Dot(intersectionPos - puckPos2, normal);

//                    if (InsideSurface(intersectionPos, surface, normal))
//                    {
//                        return (intersection, intersectionPos, overlap, normal);
//                    }
//                }
//            }
//        }
//        return null;
//    }

//    private (float overlap , Vector3 normal)? CollisionBetweenPuckVertexAndStick(Vector3 puckPos, Vector3 puckVertex, (Vector3, Vector3, Vector3, Vector3)[] stickSurfaces)
//    {
//        float minIntersection = 1f;
//        (float, Vector3)? res = null;
//        foreach (var stickSurface in stickSurfaces)
//        {
//            var collision = CollisionBetweenPuckAndSurface(puckPos, puckVertex, stickSurface);
//            if (collision.HasValue)
//            {
//                if (collision.Value.Item1 < minIntersection)
//                {
//                    res = (collision.Value.Item3, collision.Value.Item4);
//                    minIntersection = collision.Value.Item1;
//                }
//            }
//        }
//        return res;
//    }

//    private (float overlap, Vector3 normal)? CollisionBetweenSphereAndRink(Vector3 pos, float radius, HQMRink rink)
//    {
//        float maxOverlap = 0f;
//        Vector3? collNormal = null;
//        foreach (var (p, normal) in rink.Planes)
//        {
//            float overlap = Vector3.Dot((p - pos), normal) + radius;
//            if (overlap > maxOverlap)
//            {
//                maxOverlap = overlap;
//                collNormal = normal;
//            }
//        }
//        foreach (var (p, dir, cornerRadius) in rink.Corners)
//        {
//            Vector3 p2 = p - pos;
//            p2.y = 0.0f;
//            if (p2.x * dir.x < 0.0f && p2.z * dir.z < 0.0f)
//            {
//                float overlap = p2.magnitude + radius - cornerRadius;
//                if (overlap > maxOverlap)
//                {
//                    maxOverlap = overlap;
//                    Vector3 p2n = p2.normalized;
//                    collNormal = p2n;
//                }
//            }
//        }

//        return collNormal.HasValue ? ((float overlap, Vector3 normal)?)(maxOverlap, collNormal.Value) : null;
//    }

//    private (float overlap, Vector3 normal)? CollisionBetweenCollisionBallAndRink(HQMSkaterCollisionBall ball, HQMRink rink)
//    {
//        return CollisionBetweenSphereAndRink(ball.Position, ball.Radius, rink);
//    }

//    private (float overlap, Vector3 normal)? CollisionBetweenVertexAndRink(Vector3 vertex, HQMRink rink)
//    {
//        return CollisionBetweenSphereAndRink(vertex, 0.0f, rink);
//    }

//    private HQMBody ApplyAccelerationToObject( HQMBody body, Vector3 change, Vector3 point, bool isLimited)
//    {
//        Vector3 diff1 = point - body.Position;
//        body.LinearVelocity += change;
//        if (isLimited)
//        {
//            body.LinearVelocity = LimitVectorLength(body.LinearVelocity, 0.2665f);
//        }
//        Vector3 cross = Vector3.Cross(change, diff1);
//        //body.AngularVelocity += body.Rotation * ComponentMul((Quaternion.Inverse(body.Rotation) * cross), body.RotMul);
//        body.AngularVelocity += body.Rotation * ComponentMul((new Quaternion(-body.Rotation.x, -body.Rotation.y, -body.Rotation.z, body.Rotation.w) * cross), body.RotMul);

//        return body;
//    }

//    private Vector3 SpeedOfPointIncludingRotation(Vector3 p, Vector3 pos, Vector3 linearVelocity, Vector3 angularVelocity)
//    {
//        return linearVelocity + Vector3.Cross(p - pos, angularVelocity);
//    }

//    private Quaternion RotateMatrixSpherical( Quaternion matrix, float azimuth, float inclination)
//    {
//        Vector3 col1 = matrix * Vector3.up;
//        matrix = RotateMatrixAroundAxis(matrix, col1, azimuth);
//        Vector3 col0 = matrix * Vector3.right;
//        matrix = RotateMatrixAroundAxis(matrix, col0, inclination);

//        return matrix;
//    }

//    private float AdjustHeadBodyRot(float rot, float inputRot)
//    {
//        float headRotDiff = inputRot - rot;
//        if (headRotDiff <= 0.06666667f)
//        {
//            if (headRotDiff >= -0.06666667f)
//            {
//                rot = inputRot;
//            }
//            else
//            {
//                rot -= 0.06666667f;
//            }
//        }
//        else
//        {
//            rot += 0.06666667f;
//        }

//        return rot;
//    }

//    public Vector3 LimitVectorLength(Vector3 v, float maxLen)
//    {
//        float norm = v.magnitude;
//        Vector3 res = v;
//        if (norm > maxLen)
//        {
//            res *= maxLen / norm;
//        }
//        return res;
//    }

//    private Vector2 LimitVectorLength2(Vector2 v, float maxLen)
//    {
//        float norm = v.magnitude;
//        Vector2 res = v;
//        if (norm > maxLen)
//        {
//            res *= maxLen / norm;
//        }
//        return res;
//    }

//    public void LimitFriction(ref Vector3 v, Vector3 normal, float d)
//    {
//        float projectionLength = Vector3.Dot(v, normal);
//        Vector3 projection = normal * projectionLength;
//        Vector3 rejection = v - projection;
//        float rejectionLength = rejection.magnitude;
//        v = projection;

//        if (rejectionLength > 1.0f / 65536.0f)
//        {
//            Vector3 rejectionNorm = rejection.normalized;

//            float rejectionLength2 = Mathf.Min(rejectionLength, projection.magnitude * d);
//            v += rejectionNorm * rejectionLength2;
//        }
//    }

//    private void RotateVectorAroundAxis(ref Vector3 v, Vector3 axis, float angle)
//    {
//            Quaternion rot = AngleAxis(-angle, axis);
//            v = rot * v;
//    }

//    private Quaternion RotateMatrixAroundAxis(Quaternion v, Vector3 axis, float angle)
//    {
//        Quaternion rot = AngleAxis(-angle, axis);
//        return rot * v;
//    }

//    private Quaternion AngleAxis(float angle, Vector3 axis)
//    {
//        axis.Normalize();

//        float radAngle = angle * Mathf.Deg2Rad;

//        float halfAngle = radAngle * 0.5f;
//        float sinHalfAngle = Mathf.Sin(halfAngle);
//        float cosHalfAngle = Mathf.Cos(halfAngle);

//        float x = axis.x * sinHalfAngle;
//        float y = axis.y * sinHalfAngle;
//        float z = axis.z * sinHalfAngle;
//        float w = cosHalfAngle;

//        return new Quaternion(x, y, z, w);
//    }

//    private Vector3 GetProjection(Vector3 a, Vector3 b)
//    {
//        Vector3 normal = NormalOrZero(b);
//        return normal * Vector3.Dot(normal, a);
//    }

//    private Vector3 NormalOrZero(Vector3 v)
//    {
//        if (v.sqrMagnitude > 0.0f)
//        {
//            return v.normalized;
//        }
//        else
//        {
//            return Vector3.zero;
//        }
//    }

//    private float ReplaceNaN(float v, float d)
//    {
//        return float.IsNaN(v) ? d : v;
//    }

//    public static Vector3 ComponentMul(Vector3 a, Vector3 b)
//    {
//        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
//    }
//}