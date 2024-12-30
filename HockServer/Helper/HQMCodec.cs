using HockServer.Enums;
using LiteNetLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Analytics;
using static HQMMessage;



public interface HQMClientToServerMessage
{
   
}

public class JoinMessage : HQMClientToServerMessage
{
    public string PlayerName { get; set; }

    public JoinMessage(string playerName)
    {
        PlayerName = playerName;
    }
}

public class UpdateMessage : HQMClientToServerMessage
{
    public uint CurrentGameId { get; set; }
    public HQMPlayerInput Input { get; set; }
    public uint NewKnownPacket { get; set; }
    public int KnownMsgPos { get; set; }
    public (byte, string)? Chat { get; set; }

    public UpdateMessage(uint currentGameId, HQMPlayerInput input, uint newKnownPacket, int knownMsgPos, (byte, string)? chat)
    {
        CurrentGameId = currentGameId;
        Input = input;
        NewKnownPacket = newKnownPacket;
        KnownMsgPos = knownMsgPos;
        Chat = chat;
    }
}

public class ExitMessage : HQMClientToServerMessage
{
    // No additional properties or methods needed for this message type
}

public class ServerInfoMessage : HQMClientToServerMessage
{
    public uint Version { get; set; }
    public uint Ping { get; set; }

    public ServerInfoMessage(uint version, uint ping)
    {
        Version = version;
        Ping = ping;
    }
}

public static class HQMMessageCodec
{
    public static HQMClientToServerMessage ParseMessage(NetPacketReader reader)
    {
        var type = (RequestType)reader.GetByte();

        if (type == RequestType.Join)
        {
            var name = reader.GetString();
            return new JoinMessage(name);
        }
        else if (type == RequestType.Input)
        {
            var currentGameId = reader.GetUInt();
            var inputStickAngle = reader.GetFloat();
            var inputTurn = reader.GetFloat();
            var inputFwbw = reader.GetFloat();
            var inputStickRot1 = reader.GetFloat();
            var inputStickRot2 = reader.GetFloat();
            var inputHeadRot = reader.GetFloat();
            var inputBodyRot = reader.GetFloat();
            var inputKeys = reader.GetUInt();
            var input = new HQMPlayerInput
            {
                StickAngle = inputStickAngle,
                Turn = inputTurn,
                Fwbw = inputFwbw,
                Stick = new Vector2(inputStickRot1, inputStickRot2),
                HeadRot = inputHeadRot,
                BodyRot = inputBodyRot,
                Keys = inputKeys
            };

            var newKnownPacket = reader.GetUInt();
            var knownMsgPos = reader.GetInt();

            bool hasChatMsg = reader.GetBool();
            (byte, string)? chatMsg;
            if (hasChatMsg)
            {
                byte rep = reader.GetByte();
                var message = reader.GetString();
                chatMsg = (rep, message);
            }
            else
            {
                chatMsg = null;
            }

            return new UpdateMessage(currentGameId, input, newKnownPacket, knownMsgPos, chatMsg);
        }
        else if (type == RequestType.Exit)
        {
            return new ExitMessage();
        }
        else
        {
            return null;
        }
    }
}

public enum HQMClientToServerMessageDecoderError
{
    IoError,
    WrongHeader,
    UnknownType,
    StringDecoding
}

public class HQMClientToServerMessageDecoderException : Exception
{
    public HQMClientToServerMessageDecoderError Error { get; }

    public HQMClientToServerMessageDecoderException(HQMClientToServerMessageDecoderError error)
    {
        Error = error;
    }
}

public class HQMMessageWriter
{
    private readonly List<byte> _buffer;
    private int _bitPos;

    public HQMMessageWriter(List<byte> data)
    {
        _buffer = data;
        _bitPos = 0;
    }

    public void WriteByteAligned(byte v)
    {
        _bitPos = 0;
        _buffer.Add(v);
    }

    public void WriteBytesAligned(byte[] v)
    {
        _bitPos = 0;
        _buffer.AddRange(v);
    }

    public void WriteBytesAlignedPadded(int n, byte[] v)
    {
        _bitPos = 0;
        var m = Math.Min(n, v.Length);
        _buffer.AddRange(v.Take(m));
        if (n > m)
        {
            _buffer.AddRange(new byte[n - m]);
        }
    }

    public void WriteUInt32Aligned(uint v)
    {
        _bitPos = 0;
        _buffer.AddRange(BitConverter.GetBytes(v));
    }

    public void WriteFloatAligned(float v)
    {
        WriteUInt32Aligned(BitConverter.ToUInt32(BitConverter.GetBytes(v), 0));
    }

    public void WritePos(byte n, uint v, uint? oldV)
    {
        var diff = oldV.HasValue ? (int)v - (int)oldV.Value : int.MaxValue;
        if (diff >= -(1 << 2) && diff <= (1 << 2) - 1)
        {
            WriteBits(2, 0);
            WriteBits(3, (uint)diff);
        }
        else if (diff >= -(1 << 5) && diff <= (1 << 5) - 1)
        {
            WriteBits(2, 1);
            WriteBits(6, (uint)diff);
        }
        else if (diff >= -(1 << 11) && diff <= (1 << 11) - 1)
        {
            WriteBits(2, 2);
            WriteBits(12, (uint)diff);
        }
        else
        {
            WriteBits(2, 3);
            WriteBits(n, v);
        }
    }

    public void WriteBits(byte n, uint v)
    {
        var toWrite = n < 32 ? v & ~(uint.MaxValue << (int)n) : v;
        var bitsRemaining = n;
        var p = 0;
        while (bitsRemaining > 0)
        {
            var bitsPossibleToWrite = 8 - _bitPos;
            var bits = Math.Min(bitsRemaining, bitsPossibleToWrite);
            var mask = ~(uint.MaxValue << (int)bits);
            var a = (byte)((toWrite >> p) & mask);

            if (_bitPos == 0)
            {
                _buffer.Add(a);
            }
            else
            {
                _buffer[_buffer.Count - 1] |= (byte)(a << _bitPos);
            }

            if (bitsRemaining >= bitsPossibleToWrite)
            {
                bitsRemaining -= (byte)bitsPossibleToWrite;
                _bitPos = 0;
                p += bits;
            }
            else
            {
                _bitPos += bitsRemaining;
                bitsRemaining = 0;
            }
        }
    }

    public void ReplayFix()
    {
        if (_bitPos == 0)
        {
            _buffer.Add(0);
        }
    }
}
public class ObjectPacket
{
    public int index;
    public ObjectPacketType type;
}

public enum ObjectPacketType
{
    Skater,
    Puck,
    None
}

public class SkaterPacket : ObjectPacket
{
    public uint[] Pos = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue,
        uint.MaxValue
    };

    public uint[] Rot = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue
    };

    public uint[] StickPos = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue,
        uint.MaxValue
    };

    public uint[] StickRot = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue,
        uint.MaxValue
    };

    public uint HeadRot = uint.MaxValue;

    public uint BodyRot = uint.MaxValue;
}

public class PuckPacket : ObjectPacket
{
    public uint[] Pos = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue,
        uint.MaxValue
    };

    public uint[] Rot = new uint[]
    {
        uint.MaxValue,
        uint.MaxValue
    };
}

public class HQMSkaterPacket: ObjectPacket
{
    public (uint, uint, uint) Pos { get; set; }
    public (uint, uint) Rot { get; set; }
    public (uint, uint, uint) StickPos { get; set; }
    public (uint, uint) StickRot { get; set; }
    public uint HeadRot { get; set; }
    public uint BodyRot { get; set; }
}

public class HQMPuckPacket: ObjectPacket
{
    public (uint, uint, uint) Pos { get; set; }
    public (uint, uint) Rot { get; set; }
}

public static class HQMMessageExtensions
{
    public static void WriteMessage(this HQMMessageWriter writer, HQMMessage message)
    {
        switch (message.Type)
        {
            case HQMMessageType.Chat:
                var chat = message.Data as ChatItem; 
                writer.WriteBits(6, 2);
                writer.WriteBits(6, chat.PlayerIndex.HasValue ? (uint)chat.PlayerIndex.Value.Index : uint.MaxValue);
                var messageBytes = Encoding.UTF8.GetBytes(chat.Message);
                var size = Math.Min(63, messageBytes.Length);
                writer.WriteBits(6, (uint)size);
                for (var i = 0; i < size; i++)
                {
                    writer.WriteBits(7, messageBytes[i]);
                }
                break;
            case HQMMessageType.Goal:
                var goal = message.Data as GoalItem;
                writer.WriteBits(6, 1);
                writer.WriteBits(2, (uint)goal.Team);
                writer.WriteBits(6, goal.GoalIndex.HasValue ? (uint)goal.GoalIndex.Value.Index : uint.MaxValue);
                writer.WriteBits(6, goal.AssistIndex.HasValue ? (uint)goal.AssistIndex.Value.Index : uint.MaxValue);
                break;
            case HQMMessageType.PlayerUpdate:
                var playerUpdate = message.Data as PlayerUpdateItem;
                writer.WriteBits(6, 0);
                writer.WriteBits(6, (uint)playerUpdate.Index.Index);
                writer.WriteBits(1, playerUpdate.InServer ? 1u : 0u);
                var (objectIndex, teamNum) = playerUpdate.ObjectInfo.HasValue ? ((uint)playerUpdate.ObjectInfo.Value.Item1.Index, (uint)playerUpdate.ObjectInfo.Value.Item2) : (uint.MaxValue, uint.MaxValue);
                writer.WriteBits(2, teamNum);
                writer.WriteBits(6, objectIndex);
                var nameBytes = Encoding.UTF8.GetBytes(playerUpdate.Name);
                for (var i = 0; i < 31; i++)
                {
                    var v = i < nameBytes.Length ? nameBytes[i] : (byte)0;
                    writer.WriteBits(7, v);
                }
                break;
        }
    }

    public static void WriteObjects(this HQMMessageWriter writer, List<ObjectPacket[]> packets, uint currentPacket, uint knownPacket)
    {
        var currentPackets = packets[0];

        var oldPackets = knownPacket == uint.MaxValue ? null : packets.Skip((int)(currentPacket - knownPacket)).FirstOrDefault();

        writer.WriteUInt32Aligned(currentPacket);
        writer.WriteUInt32Aligned(knownPacket);

        for (var i = 0; i < 32; i++)
        {
            var current = currentPackets[i];
            var oldPacket = oldPackets?[i];
            switch (current)
            {
                case HQMPuckPacket puck:
                    var oldPuck = oldPacket as HQMPuckPacket;
                    writer.WriteBits(1, 1);
                    writer.WriteBits(2, 1); // Puck type
                    writer.WritePos(17, puck.Pos.Item1, oldPuck?.Pos.Item1);
                    writer.WritePos(17, puck.Pos.Item2, oldPuck?.Pos.Item2);
                    writer.WritePos(17, puck.Pos.Item3, oldPuck?.Pos.Item3);
                    writer.WritePos(31, puck.Rot.Item1, oldPuck?.Rot.Item1);
                    writer.WritePos(31, puck.Rot.Item2, oldPuck?.Rot.Item2);
                    break;
                case HQMSkaterPacket skater:
                    var oldSkater = oldPacket as HQMSkaterPacket;
                    writer.WriteBits(1, 1);
                    writer.WriteBits(2, 0); // Skater type
                    writer.WritePos(17, skater.Pos.Item1, oldSkater?.Pos.Item1);
                    writer.WritePos(17, skater.Pos.Item2, oldSkater?.Pos.Item2);
                    writer.WritePos(17, skater.Pos.Item3, oldSkater?.Pos.Item3);
                    writer.WritePos(31, skater.Rot.Item1, oldSkater?.Rot.Item1);
                    writer.WritePos(31, skater.Rot.Item2, oldSkater?.Rot.Item2);
                    writer.WritePos(13, skater.StickPos.Item1, oldSkater?.StickPos.Item1);
                    writer.WritePos(13, skater.StickPos.Item2, oldSkater?.StickPos.Item2);
                    writer.WritePos(13, skater.StickPos.Item3, oldSkater?.StickPos.Item3);
                    writer.WritePos(25, skater.StickRot.Item1, oldSkater?.StickRot.Item1);
                    writer.WritePos(25, skater.StickRot.Item2, oldSkater?.StickRot.Item2);
                    writer.WritePos(16, skater.HeadRot, oldSkater?.HeadRot);
                    writer.WritePos(16, skater.BodyRot, oldSkater?.BodyRot);
                    break;
                default:
                    writer.WriteBits(1, 0);
                    break;
            }
        }
    }

    //public static ObjectPacket[] GetPackets(HQMGameObject[] objects)
    //{
    //    var packets = new ObjectPacket[32];
    //    for (var i = 0; i < 32; i++)
    //    {
    //        packets[i] = objects[i] switch
    //        {
    //            HQMGameObject.Puck puck => HQMObjectPacket.Puck,
    //            HQMGameObject.Player player => HQMObjectPacket.Skater,
    //            _ => HQMObjectPacket.None,
    //        };
    //    }
    //    return packets;
    //}
}