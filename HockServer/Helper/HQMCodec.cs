using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using UnityEngine;
using static HQMMessage;



public interface HQMClientToServerMessage
{
   
}

public class JoinMessage : HQMClientToServerMessage
{
    public uint Version { get; set; }
    public string PlayerName { get; set; }

    public JoinMessage(uint version, string playerName)
    {
        Version = version;
        PlayerName = playerName;
    }
}

public class UpdateMessage : HQMClientToServerMessage
{
    public uint CurrentGameId { get; set; }
    public HQMPlayerInput Input { get; set; }
    public uint? Deltatime { get; set; }
    public uint NewKnownPacket { get; set; }
    public int KnownMsgPos { get; set; }
    public (byte, string)? Chat { get; set; }
    public HQMClientVersion Version { get; set; }

    public UpdateMessage(uint currentGameId, HQMPlayerInput input, uint? deltatime, uint newKnownPacket, int knownMsgPos, (byte, string)? chat, HQMClientVersion version)
    {
        CurrentGameId = currentGameId;
        Input = input;
        Deltatime = deltatime;
        NewKnownPacket = newKnownPacket;
        KnownMsgPos = knownMsgPos;
        Chat = chat;
        Version = version;
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

public enum HQMClientVersion
{
    Vanilla,
    Ping,
    PingRules,
}



public class HQMMessageCodec
{
    private static readonly byte[] GAME_HEADER = Encoding.ASCII.GetBytes("Hock");
    public bool HasPing(HQMClientVersion v)
    {
        switch (v)
        {
            case HQMClientVersion.Vanilla:
                return false;
            case HQMClientVersion.Ping:
            case HQMClientVersion.PingRules:
                return true;
            default:
                return false;
        }
    }
    public HQMClientToServerMessage ParseMessage(byte[] src)
    {
        var parser = new HQMMessageReader(src);
        var header = parser.ReadBytesAligned(4);
        if (!header.SequenceEqual(GAME_HEADER))
        {
            throw new HQMClientToServerMessageDecoderException(HQMClientToServerMessageDecoderError.WrongHeader);
        }

        var command = parser.ReadByteAligned();
        Console.WriteLine("New request with command {0}", command);
        return command switch
        {
            0 => ParseRequestInfo(parser),
            2 => ParsePlayerJoin(parser),
            4 => ParsePlayerUpdate(parser, HQMClientVersion.Vanilla),
            8 => ParsePlayerUpdate(parser, HQMClientVersion.Ping),
            0x10 => ParsePlayerUpdate(parser, HQMClientVersion.PingRules),
            7 => new ExitMessage(),
            _ => throw new HQMClientToServerMessageDecoderException(HQMClientToServerMessageDecoderError.UnknownType),
        };
    }

    private HQMClientToServerMessage ParseRequestInfo(HQMMessageReader parser)
    {
        var version = parser.ReadBits(8);
        var ping = parser.ReadU32Aligned();
        return new ServerInfoMessage(version, ping);
    }

    private HQMClientToServerMessage ParsePlayerJoin(HQMMessageReader parser)
    {
        var version = parser.ReadBits(8);
        var playerName = parser.ReadBytesAligned(32);
        return new JoinMessage(version, GetPlayerName(playerName.ToArray()));
    }

    private HQMClientToServerMessage ParsePlayerUpdate(HQMMessageReader parser, HQMClientVersion clientVersion)
    {
        var currentGameId = parser.ReadU32Aligned();

        var inputStickAngle = parser.ReadF32Aligned();
        var inputTurn = parser.ReadF32Aligned();
        var _inputUnknown = parser.ReadF32Aligned();
        var inputFwbw = parser.ReadF32Aligned();
        var inputStickRot1 = parser.ReadF32Aligned();
        var inputStickRot2 = parser.ReadF32Aligned();
        var inputHeadRot = parser.ReadF32Aligned();
        var inputBodyRot = parser.ReadF32Aligned();
        var inputKeys = parser.ReadU32Aligned();
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

        uint? deltaTime = HasPing(clientVersion) ? parser.ReadU32Aligned() : 0;

        var newKnownPacket = parser.ReadU32Aligned();
        var knownMsgPos = parser.ReadU16Aligned();

        bool hasChatMsg = parser.ReadBits(1) == 1;
        (byte, string)? chatMsg;
        if (hasChatMsg)
        {
            byte rep = (byte)parser.ReadBits(3);
            int byteNum = (int)parser.ReadBits(8);
            var message = parser.ReadBytesAligned(byteNum);
            var msg = Encoding.UTF8.GetString(message.ToArray());
            chatMsg = (rep, msg);
        }
        else
        {
            chatMsg =  null;
        }

        return new UpdateMessage(currentGameId, input, deltaTime, newKnownPacket, knownMsgPos, chatMsg, clientVersion);
    }

    private string GetPlayerName(byte[] bytes)
    {
        var firstNull = Array.IndexOf(bytes, (byte)0);
        var nameBytes = firstNull >= 0 ? bytes.Take(firstNull).ToArray() : bytes;
        var name = Encoding.UTF8.GetString(nameBytes);
        return string.IsNullOrEmpty(name) ? "Noname" : name;
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

public class HQMMessageReader
{
    private int bufLength;
    private BitArray buf;
    public int pos;
    public byte bitPos;
    public BitArray safeGetByteChunk = new BitArray(8);
    public byte[] safeGetByteResultArray = new byte[1];
    private byte readByteAlignedRes;
    public List<byte> readBytesAlignedRes = new List<byte>();
    public ushort readU16AlignedB1;
    public ushort readU16AlignedB2;
    public byte readU32AlignedB1;
    public byte readU32AlignedB2;
    public byte readU32AlignedB3;
    public byte readU32AlignedB4;
    public byte[] readU32AlignedBytes = new byte[4];
    public uint readF32AlignedI;
    public uint readPosType;
    public int readPosSignedOldValue;
    public int readPosDiff;
    public int readBitsSignedA;
    public byte readBitsBitsRemaining;
    public uint readBitsRes;
    public byte readBitsP;
    public byte readBitsPosWBits;
    public byte readBitsBits;
    public uint readBitsMask;
    public uint readBitsA;

    public HQMMessageReader(byte[] inputData)
    {
        buf = new BitArray(inputData);
        bufLength = buf.Length;
    }

    public int GetPos()
    {
        return pos;
    }

    public byte SafeGetByte(int inPos)
    {
        if (inPos < bufLength)
        {
            for (int i = 0; i < 8; i++)
            {
                safeGetByteChunk[i] = buf[inPos + i];
            }
            safeGetByteChunk.CopyTo(safeGetByteResultArray, 0);
            return safeGetByteResultArray[0];
        }
        return 0;
    }

    public byte ReadByteAligned()
    {
        Align();
        readByteAlignedRes = SafeGetByte(pos);
        pos += 8;
        return readByteAlignedRes;
    }

    public List<byte> ReadBytesAligned(int n)
    {
        Align();
        readBytesAlignedRes.Clear();
        for (int i = pos; i < pos + n * 8; i += 8)
        {
            readBytesAlignedRes.Add(SafeGetByte(i));
        }
        pos += n * 8;
        return readBytesAlignedRes;
    }

    public ushort ReadU16Aligned()
    {
        Align();
        readU16AlignedB1 = (ushort)SafeGetByte(pos);
        readU16AlignedB2 = (ushort)SafeGetByte(pos + 8);
        pos += 16;
        return (ushort)((int)readU16AlignedB1 | (int)readU16AlignedB2 << 8);
    }

    public uint ReadU32Aligned()
    {
        Align();
        readU32AlignedB1 = SafeGetByte(pos);
        readU32AlignedB2 = SafeGetByte(pos + 8);
        readU32AlignedB3 = SafeGetByte(pos + 16);
        readU32AlignedB4 = SafeGetByte(pos + 24);
        pos += 32;
        readU32AlignedBytes[0] = readU32AlignedB1;
        readU32AlignedBytes[1] = readU32AlignedB2;
        readU32AlignedBytes[2] = readU32AlignedB3;
        readU32AlignedBytes[3] = readU32AlignedB4;
        return BitConverter.ToUInt32(readU32AlignedBytes, 0);
    }

    public float ReadF32Aligned()
    {
        readF32AlignedI = ReadU32Aligned();
        return BitConverter.ToSingle(BitConverter.GetBytes(readF32AlignedI), 0);
    }

    public uint ReadPos(byte b, uint oldValue)
    {
        readPosType = ReadBits(2);
        readPosSignedOldValue = (int)oldValue;
        readPosDiff = 0;
        switch (readPosType)
        {
            case 0U:
                readPosDiff = ReadBitsSigned(3);
                return (uint)Math.Max(0, readPosSignedOldValue + readPosDiff);
            case 1U:
                readPosDiff = ReadBitsSigned(6);
                return (uint)Math.Max(0, readPosSignedOldValue + readPosDiff);
            case 2U:
                readPosDiff = ReadBitsSigned(12);
                return (uint)Math.Max(0, readPosSignedOldValue + readPosDiff);
            case 3U:
                return ReadBits(b);
            default:
                Console.WriteLine("NONPOS");
                return 0U;
        }
    }

    public int ReadBitsSigned(byte b)
    {
        readBitsSignedA = (int)ReadBits(b);
        if (readBitsSignedA >= 1 << (int)(b - 1))
        {
            return -1 << (int)b | readBitsSignedA;
        }
        return readBitsSignedA;
    }

    public uint ReadBits(byte b)
    {
        readBitsBitsRemaining = b;
        readBitsRes = 0U;
        readBitsP = 0;
        while (readBitsBitsRemaining > 0)
        {
            readBitsPosWBits = (byte)(8 - bitPos);
            readBitsBits = Math.Min(readBitsBitsRemaining, readBitsPosWBits);
            readBitsMask = ~(uint.MaxValue << (int)readBitsBits);
            readBitsA = ((uint)SafeGetByte(pos) >> (int)bitPos & readBitsMask);
            readBitsRes |= readBitsA << (int)readBitsP;
            if (readBitsBitsRemaining >= readBitsPosWBits)
            {
                readBitsBitsRemaining -= readBitsPosWBits;
                bitPos = 0;
                pos += 8;
                readBitsP += readBitsBits;
            }
            else
            {
                bitPos += readBitsBitsRemaining;
                readBitsBitsRemaining = 0;
            }
        }
        return readBitsRes;
    }

    public void Align()
    {
        if (bitPos > 0)
        {
            bitPos = 0;
            pos += 8;
        }
    }

    public void Next()
    {
        bitPos = 0;
        pos += 8;
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