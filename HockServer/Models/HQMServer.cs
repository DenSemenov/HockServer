using HockServer;
using HockServer.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static HQMMessage;

public struct HQMServerPlayerIndex
{
    public int Index { get; }

    public HQMServerPlayerIndex(int index)
    {
        Index = index;
    }
}

public struct HQMObjectIndex
{
    public int Index { get; }

    public HQMObjectIndex(int index)
    {
        Index = index;
    }
}

public class HQMNetworkPlayerData
{
    public IPEndPoint Addr { get; set; }
    public HQMClientVersion ClientVersion { get; set; }
    public uint Inactivity { get; set; }
    public uint KnownPacket { get; set; }
    public int KnownMsgPos { get; set; }
    public byte? ChatRep { get; set; }
    public uint DeltaTime { get; set; }
    public List<float> LastPing { get; set; }
    public HQMServerPlayerIndex ViewPlayerIndex { get; set; }
    public uint GameId { get; set; }
    public List<HQMMessage> Messages { get; set; }
}

public enum HQMMuteStatus
{
    NotMuted,
    ShadowMuted,
    Muted,
}

public class PingData
{
    public float Min{get;set;}
    public float Max{get;set;}
    public float Avg{get;set;}
    public float Deviation { get; set; }
}

public class HQMServerPlayer
{
    public string PlayerName { get; set; }
    public (HQMObjectIndex, HQMTeam)? Object { get; set; }
    public Guid Id { get; set; }
    public HQMNetworkPlayerData Data { get; set; }
    public bool IsAdmin { get; set; }
    public HQMMuteStatus IsMuted { get; set; }
    public HQMSkaterHand Hand { get; set; }
    public float Mass { get; set; } = 1;
    public HQMPlayerInput Input { get; set; } = new HQMPlayerInput();
    public float StickLimit { get; set; }

    public static HQMServerPlayer NewNetworkPlayer(
        HQMServerPlayerIndex playerIndex,
        string playerName,
        IPEndPoint addr,
        List<HQMMessage> globalMessages)
    {
        return new HQMServerPlayer
        {
            PlayerName = playerName,
            Object = null,
            Id = Guid.NewGuid(),
            IsAdmin = false,
            Input = new HQMPlayerInput(),
            IsMuted = HQMMuteStatus.NotMuted,
            Hand = HQMSkaterHand.Right,
            Mass = 1.0f,
            StickLimit = 0.01f,
            Data = new HQMNetworkPlayerData
            {
                Addr = addr,
                ClientVersion = HQMClientVersion.Vanilla,
                Inactivity = 0,
                KnownPacket = uint.MaxValue,
                KnownMsgPos = 0,
                ChatRep = null,
                DeltaTime = 0,
                LastPing = new List<float>(),
                ViewPlayerIndex = playerIndex,
                GameId = uint.MaxValue,
                Messages = new List<HQMMessage>(globalMessages)
            }
        };
    }

    public bool Reset(HQMServerPlayerIndex playerIndex)
    {
        Object = null;
        if (Data is HQMNetworkPlayerData networkPlayer)
        {
            networkPlayer.KnownMsgPos = 0;
            networkPlayer.KnownPacket = uint.MaxValue;
            networkPlayer.Messages.Clear();
            networkPlayer.ViewPlayerIndex = playerIndex;
        }
        return true;
    }

    public HQMMessage GetUpdateMessage(HQMServerPlayerIndex playerIndex)
    {
        return new HQMMessage(HQMMessageType.PlayerUpdate, new PlayerUpdateItem
        {
            Name = PlayerName,
            ObjectInfo = Object,
            Index = playerIndex,
            InServer = true

        });
    }

    public void AddMessage(HQMMessage message)
    {
        if (Data is HQMNetworkPlayerData networkPlayer)
        {
            networkPlayer.Messages.Add(message);
        }
    }

    public IPEndPoint Addr()
    {
        if (Data is HQMNetworkPlayerData networkPlayer)
        {
            return networkPlayer.Addr;
        }
        return null;
    }

    public PingData PingData()
    {
        if (Data is HQMNetworkPlayerData networkPlayer)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0f;
            foreach (float ping in networkPlayer.LastPing)
            {
                min = Math.Min(min, ping);
                max = Math.Max(max, ping);
                sum += ping;
            }
            float avg = sum / networkPlayer.LastPing.Count;
            float dev = 0f;
            foreach (float ping in networkPlayer.LastPing)
            {
                dev += (ping - avg) * (ping - avg);
            }
            dev = (float)Math.Sqrt(dev / networkPlayer.LastPing.Count);
            return new PingData
            {
                Min = min,
                Max = max,
                Avg = avg,
                Deviation = dev
            };
        }
        return null;
    }
}

public class HQMServerPlayerList
{
    public List<HQMServerPlayer?> Players;

    public HQMServerPlayerList()
    {
        Players = new List<HQMServerPlayer?>();
    }

    public HQMServerPlayer? Get(HQMServerPlayerIndex playerIndex)
    {
        var index = playerIndex.Index;
        return Players.ElementAtOrDefault(index);
    }

    public HQMServerPlayer? GetMut(HQMServerPlayerIndex playerIndex)
    {
        var index = playerIndex.Index;
        return Players.ElementAtOrDefault(index);
    }

    public (HQMServerPlayerIndex, HQMTeam, HQMServerPlayer)? GetFromObjectIndex(HQMObjectIndex objectIndex)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            if (player != null && player.Object.HasValue && player.Object.Value.Item1.Index == objectIndex.Index)
            {
                return (new HQMServerPlayerIndex(i), player.Object.Value.Item2, player);
            }
        }
        return null;
    }

    public void RemovePlayer(HQMServerPlayerIndex playerIndex)
    {
        var index = playerIndex.Index;
        if (index < Players.Count)
        {
            Players[index] = null;
        }
    }

    public void AddPlayer(HQMServerPlayerIndex playerIndex, HQMServerPlayer player)
    {
        var index = playerIndex.Index;
        if (index < Players.Count)
        {
            Players[index] = player;
        }
    }
}

public enum HQMMessageType
{
    PlayerUpdate,
    Goal,
    Chat
}

public class HQMMessage
{
    public HQMMessageType Type { get; }
    public object Data { get; }

    public HQMMessage(HQMMessageType type, object data)
    {
        Type = type;
        Data = data;
    }

    public class PlayerUpdateItem 
    {
        public HQMServerPlayerIndex Index { get; set; }
        public bool InServer { get; set; }
        public (HQMObjectIndex, HQMTeam)? ObjectInfo { get; set; }
        public string Name { get; set; }
    }

    public class GoalItem
    {
        public HQMServerPlayerIndex? GoalIndex { get; set; }
        public HQMServerPlayerIndex? AssistIndex { get; set; }
        public HQMTeam Team { get; set; }
    }

    public class ChatItem
    {
        public HQMServerPlayerIndex? PlayerIndex { get; set; }
        public string Message { get; set; }
    }

    public static HQMMessage PlayerUpdate(string playerName, (HQMObjectIndex, HQMTeam)? objectInfo, HQMServerPlayerIndex playerIndex, bool inServer)
    {
        return new HQMMessage(HQMMessageType.PlayerUpdate, new PlayerUpdateItem { Name = playerName, ObjectInfo = objectInfo, Index = playerIndex, InServer = inServer });
    }

    public static HQMMessage Goal(HQMTeam team, HQMServerPlayerIndex? goalPlayerIndex, HQMServerPlayerIndex? assistPlayerIndex)
    {
        return new HQMMessage(HQMMessageType.Goal, new GoalItem { Team = team, GoalIndex = goalPlayerIndex, AssistIndex = assistPlayerIndex });
    }

    public static HQMMessage Chat(HQMServerPlayerIndex? playerIndex, string message)
    {
        return new HQMMessage(HQMMessageType.Chat, new ChatItem { PlayerIndex = playerIndex, Message = message });
    }
}

public interface HQMWaitingMessageReceiver
{
}

public class HQMWaitingMessageReceiverAll: HQMWaitingMessageReceiver
{

}

public class HQMWaitingMessageReceiverSpecific : HQMWaitingMessageReceiver {
    public HQMServerPlayerIndex Index { get; set; }
}

public class HQMServerMessages
{
    public List<HQMMessage> PersistentMessages;
    public List<HQMMessage> ReplayMessages;
    public List<(HQMWaitingMessageReceiver, HQMMessage)> WaitingMessages;

    public HQMServerMessages()
    {
        PersistentMessages = new List<HQMMessage>(1024);
        ReplayMessages = new List<HQMMessage>(1024);
        WaitingMessages = new List<(HQMWaitingMessageReceiver, HQMMessage)>(64);
    }

    public void Clear()
    {
        PersistentMessages.Clear();
        ReplayMessages.Clear();
        WaitingMessages.Clear();
    }

    public void AddUserChatMessage(string message, HQMServerPlayerIndex senderIndex)
    {
        var chat = HQMMessage.Chat(senderIndex, message);
        AddGlobalMessage(chat, false, true);
    }

    public void AddServerChatMessage(string message)
    {
        var chat = HQMMessage.Chat(null, message);
        AddGlobalMessage(chat, false, true);
    }

    public void AddDirectedChatMessage(string message, HQMServerPlayerIndex receiverIndex, HQMServerPlayerIndex? senderIndex)
    {
        var chat = HQMMessage.Chat(senderIndex, message);
        AddDirectedMessage(chat, receiverIndex);
    }

    public void AddDirectedUserChatMessage(string message, HQMServerPlayerIndex receiverIndex, HQMServerPlayerIndex senderIndex)
    {
        AddDirectedChatMessage(message, receiverIndex, senderIndex);
    }

    public void AddDirectedServerChatMessage(string message, HQMServerPlayerIndex receiverIndex)
    {
        AddDirectedChatMessage(message, receiverIndex, null);
    }

    public void AddGoalMessage(HQMTeam team, HQMServerPlayerIndex? goalPlayerIndex, HQMServerPlayerIndex? assistPlayerIndex)
    {
        var message = HQMMessage.Goal(team, goalPlayerIndex, assistPlayerIndex);
        AddGlobalMessage(message, true, true);
    }

    public void AddGlobalMessage(HQMMessage message, bool persistent, bool replay)
    {
        if (replay)
        {
            ReplayMessages.Add(message);
        }
        if (persistent)
        {
            PersistentMessages.Add(message);
        }
        WaitingMessages.Add((new HQMWaitingMessageReceiverAll(), message));
    }

    public void AddDirectedMessage(HQMMessage message, HQMServerPlayerIndex receiver)
    {
        WaitingMessages.Add((new HQMWaitingMessageReceiverSpecific { Index = receiver}, message));
    }
}

public enum ReplaySaving
{
    File,
    Endpoint
}

public class ReplaySavingEndpoint
{
    public string Url { get; set; }

    public ReplaySavingEndpoint(string url)
    {
        Url = url;
    }
}

[Serializable]
public enum ReplayEnabled
{
    Off,
    On,
    Standby
}

public class HQMServerConfiguration
{
    public List<string> Welcome { get; set; } = new List<string> { "Welcome" };
    public string Password { get; set; } = "hqm";
    public int PlayerMax { get; set; } = 20;
    public ReplayEnabled ReplaysEnabled { get; set; }= ReplayEnabled.Off;
    public HQMSpawnPoint SpawnPoint { get; set; } = HQMSpawnPoint.Center;
    public object ReplaySaving { get; set; }
    public string ServerName { get; set; } = "C# implementation";
    public string ServerService { get; set; }

    //match config
    public uint TimePeriod { get; set; } = 300;
    public uint TimeWarmup { get; set; } = 300;
    public uint TimeBreak { get; set; } = 10;
    public uint TimeIntermission { get; set; } = 20;
    public uint Mercy { get; set; } = 7;
    public uint FirstTo { get; set; } = 0;
    public uint Periods { get; set; } = 3;
    public int WarmupPucks { get; set; } = 16;
    public bool UseMph { get; set; } = false;
    public bool GoalReplay { get; set; } = true;
    public float SpawnPointOffset { get; set; } = 2.75f;
    public float SpawnPlayerAltitude { get; set; } = 1.5f;
    public float SpawnPuckAltitude { get; set; } = 1.5f;
    public bool SpawnKeepStickPosition { get; set; } = false;
    public HQMPhysicsConfiguration PhysicsConfiguration { get; set; } = new HQMPhysicsConfiguration();

    public HQMServerConfiguration()
    {
    }

    public HQMServerConfiguration(List<string> welcome, string password, int playerMax, ReplayEnabled replaysEnabled, object replaySaving, string serverName, string serverService)
    {
        Welcome = welcome;
        Password = password;
        PlayerMax = playerMax;
        ReplaysEnabled = replaysEnabled;
        ReplaySaving = replaySaving;
        ServerName = serverName;
        ServerService = serverService;
    }
}

public class HQMGameValues
{
    public HQMRulesState RulesState { get; set; }
    public uint RedScore { get; set; }
    public uint BlueScore { get; set; }
    public uint Period { get; set; }
    public uint Time { get; set; }
    public uint GoalMessageTimer { get; set; }
    public bool GameOver { get; set; }

    public HQMGameValues()
    {
        RulesState = new HQMRulesState.Regular(false, false);
        RedScore = 0;
        BlueScore = 0;
        Period = 0;
        Time = 0;
        GoalMessageTimer = 0;
        GameOver = false;
    }
}

public class HQMRulesState
{
    public class Regular : HQMRulesState
    {
        public bool OffsideWarning { get; set; }
        public bool IcingWarning { get; set; }

        public Regular(bool offsideWarning, bool icingWarning)
        {
            OffsideWarning = offsideWarning;
            IcingWarning = icingWarning;
        }
    }

    public class Offside : HQMRulesState
    {
        public Offside() { }
    }

    public class Icing : HQMRulesState
    {
        public Icing() { }
    }
}

public struct ReplayElement
{
    public List<ReplayTick> Data { get; set; }
    public HQMServerPlayerIndex? ForceView { get; set; }

    public ReplayElement(List<ReplayTick> data, HQMServerPlayerIndex? forceView)
    {
        Data = data;
        ForceView = forceView;
    }
}

public struct ReplayTick
{
    public uint GameStep { get; set; }
    public ObjectPacket[] Packets { get; set; }

    public ReplayTick(uint gameStep, ObjectPacket[] packets)
    {
        GameStep = gameStep;
        Packets = packets;
    }
}

public class HQMServer
{
    public static readonly byte[] GAME_HEADER = new byte[] { 0x48, 0x6F, 0x63, 0x6B };
    public HQMServerPlayerList Players { get; set; }
    public HQMServerMessages Messages { get; set; }
    private HashSet<IPAddress> BanList;
    private bool AllowJoin;
    public HQMServerConfiguration Config { get; set; } = new HQMServerConfiguration();
    public HQMGameValues Values { get; set; }
    public HQMGameWorld2 World { get; set; }
    private List<ReplayElement> ReplayQueue;
    private List<(uint, uint, HQMServerPlayerIndex?)> RequestedReplays;
    public uint GameId { get; set; }
    public uint GameStep { get; set; }
    public bool IsMuted { get; set; }
    public DateTime StartTime { get; set; }
    private HttpClient ReqwestClient;
    private bool HasCurrentGameBeenActive;
    private uint Packet;
    private byte[] ReplayData;
    private int ReplayMsgPos;
    private uint ReplayLastPacket;
    private new List<ObjectPacket[]> SavedPackets;
    private new List<DateTime> SavedPings;
    private List<ReplayTick> SavedHistory;
    public int HistoryLength { get; set; }
    private List<(uint, Queue<(HQMObjectIndex, HQMObjectIndex)>)> SavedEvents;
    private Dictionary<int, uint> TeamSwitchTimer = new Dictionary<int, uint>();
    public HQMServer()
    {

    }
    public HQMServer(HQMServerConfiguration config, HQMGameValues values, HQMGameWorld2 world)
    {
        var playerVec = new List<HQMServerPlayer?>(64);
        for (int i = 0; i < 64; i++)
        {
            playerVec.Add(null);
        }

        Players = new HQMServerPlayerList
        {
            Players = playerVec
        };
        Messages = new HQMServerMessages();
        BanList = new HashSet<IPAddress>();
        AllowJoin = true;
        Config = config;
        Values = values;
        World = world;
        ReplayQueue = new List<ReplayElement>();
        RequestedReplays = new List<(uint, uint, HQMServerPlayerIndex?)>();
        GameId = 1;
        GameStep = 0;
        IsMuted = false;
        StartTime = DateTime.UtcNow;
        ReqwestClient = new HttpClient();
        HasCurrentGameBeenActive = false;
        Packet = 0;
        ReplayData = new byte[64 * 1024 * 1024];
        ReplayMsgPos = 0;
        ReplayLastPacket = 0;
        SavedPackets = Enumerable.Repeat(new ObjectPacket[32], 192).ToList();
        SavedPings = Enumerable.Repeat(new DateTime(), 100).ToList();
        SavedHistory = new List<ReplayTick>();
        HistoryLength = 0;
        SavedEvents = new List<(uint, Queue<(HQMObjectIndex, HQMObjectIndex)>)>();
    }

    public void RemoveInactivePlayers()
    {
        List<(HQMServerPlayerIndex, string)> inactivePlayers = new List<(HQMServerPlayerIndex, string)>();

        //for (int i = 0; i < Players.Players.Count; i++)
        //{
        //    HQMServerPlayer player = Players.Players[i];
        //    if (player !=null && player.Data is HQMNetworkPlayerData networkPlayer)
        //    {
        //        networkPlayer.Inactivity += 1;
        //        if (networkPlayer.Inactivity > 500)
        //        {
        //            inactivePlayers.Add((new HQMServerPlayerIndex(i), player.PlayerName));
        //        }
        //    }
        //}

        foreach (var (playerIndex, playerName) in inactivePlayers)
        {
            RemovePlayer(playerIndex, true);
            Console.WriteLine($"{playerName} ({playerIndex.Index}) timed out");
            string chatMsg = $"{playerName} timed out";
            Messages.AddServerChatMessage(chatMsg);
        }
    }

    public bool MoveToSpectator(int playerIndex)
    {
        var player = Players.Players[playerIndex];
        if (player != null)
        {
            if (player.Object.HasValue)
            {
                var (objectIndex, _) = player.Object.Value;
                if (World.RemovePlayer(objectIndex))
                {
                    player.Object = null;
                    var update = player.GetUpdateMessage(new HQMServerPlayerIndex(playerIndex));
                    Messages.AddGlobalMessage(update, true, true);

                    return true;
                }
            }
        }
        return false;
    }

    public int? SpawnSkater(
        int playerIndex,
        HQMTeam team,
        Vector3 pos,
        Quaternion rot,
        bool keepStickPosition)
    {
        var player = Players.Players[playerIndex];
        if (player !=null)
        {
            if (player.Object.HasValue)
            {
                var (objectIndex, _) = player.Object.Value;
                var skater = World.Objects[objectIndex.Index] as HQMSkater;
                if (skater !=null)
                {
                    var newSkater = new HQMSkater(pos, rot, player.Hand, player.Mass, player.StickLimit);
                    if (keepStickPosition)
                    {
                        var stickPosDiff = skater.StickPos - skater.Body.Position;
                        var rotChange = Quaternion.Inverse(skater.Body.Rotation) * rot;
                        var stickRotDiff = Quaternion.Inverse(skater.Body.Rotation) * skater.StickRot;

                        newSkater.StickPos = pos + (rotChange * stickPosDiff);
                        newSkater.StickRot = stickRotDiff * rot;
                        newSkater.StickPlacement = skater.StickPlacement;
                    }
                    skater = newSkater;
                    player.Object = (objectIndex, team);
                    var update = player.GetUpdateMessage(new HQMServerPlayerIndex(playerIndex));
                    Messages.AddGlobalMessage(update, true, true);
                }
            }
            else
            {
                var skater = World.CreatePlayerObject(pos, rot, player.Hand, player.Mass, player.StickLimit);
                if (skater.HasValue)
                {
                    if (player.Data is HQMNetworkPlayerData networkPlayer)
                    {
                        networkPlayer.ViewPlayerIndex = new HQMServerPlayerIndex(playerIndex);
                    }

                    player.Object = (skater.Value, team);
                    var update = player.GetUpdateMessage(new HQMServerPlayerIndex(playerIndex));
                    Messages.AddGlobalMessage(update, true, true);
                    return skater.Value.Index;
                }
            }
        }
        return null;
    }

    public bool AddPlayer(
        int playerIndex,
        string playerName,
        HQMTeam team,
        HQMSpawnPoint spawnPoint,
        ref int playerCount,
        int teamMax)
    {
        if (playerCount >= teamMax)
        {
            return false;
        }

        var (pos, rot) = HQMHelpers.GetSpawnPoint(World.Rink, team, spawnPoint);

        var skater = SpawnSkater(playerIndex, team, pos, rot, false);
        if (skater.HasValue)
        {
            Console.WriteLine($"{playerName} ({playerIndex}) has joined team {team}");
            playerCount += 1;

            //m.ClearStartedGoalie(playerIndex);
            return true;
        }
        else
        {
            return false;
        }
    }

    public void UpdatePlayers()
    {
        var spectatingPlayers = new List<Tuple<int, string>>(32);
        var joiningRed = new List<Tuple<int, string>>(32);
        var joiningBlue = new List<Tuple<int, string>>(32);

        var i = 0;
        foreach (var player in Players.Players)
        {
            if (player == null)
            {
                continue;
            }
            if (TeamSwitchTimer.ContainsKey(i))
            {
                TeamSwitchTimer[i] = Math.Max(0, TeamSwitchTimer[i] - 1);
            }

            if (player.Input.JoinRed || player.Input.JoinBlue)
            {
                var hasSkater = player.Object.HasValue;
                if (!hasSkater && (!TeamSwitchTimer.ContainsKey(i) || TeamSwitchTimer[i] == 0))
                {
                    if (player.Input.JoinRed)
                    {
                        joiningRed.Add(new Tuple<int, string>(i, player.PlayerName));
                    }
                    else if (player.Input.JoinBlue)
                    {
                        joiningBlue.Add(new Tuple<int, string>(i, player.PlayerName));
                    }
                }
            }
            else if (player.Input.Spectate)
            {
                var hasSkater = player.Object.HasValue;
                if (hasSkater)
                {
                    TeamSwitchTimer[i] = 500;
                    spectatingPlayers.Add(new Tuple<int, string>(i, player.PlayerName));
                }
            }

            i += 1;
        }

        foreach (var (playerIndex, playerName) in spectatingPlayers)
        {
            Console.WriteLine($"{playerName} ({playerIndex}) is spectating");
            MoveToSpectator(playerIndex);
            var message = $"{playerName} is spectating";
        }

        if (joiningRed.Count > 0 || joiningBlue.Count > 0)
        {
            var (redPlayerCount, bluePlayerCount) = (0, 0);
            foreach (var player in Players.Players.Where(x=>x !=null))
            {
                if (player.Object.HasValue)
                {
                    var (_, team) = player.Object.Value;
                    if (team == HQMTeam.Red)
                    {
                        redPlayerCount++;
                    }
                    else if (team == HQMTeam.Blue)
                    {
                        bluePlayerCount++;
                    }
                }
            }

            var newRedPlayerCount = redPlayerCount;
            var newBluePlayerCount = bluePlayerCount;

            foreach (var (playerIndex, playerName) in joiningRed)
            {
                if (AddPlayer(playerIndex, playerName, HQMTeam.Red, Config.SpawnPoint, ref newRedPlayerCount, Config.PhysicsConfiguration.TeamMax))
                {
                }
            }

            foreach (var (playerIndex, playerName) in joiningBlue)
            {
                if (AddPlayer(playerIndex, playerName, HQMTeam.Blue, Config.SpawnPoint, ref newBluePlayerCount, Config.PhysicsConfiguration.TeamMax))
                {
                }
            }

            if (Values.Period == 0 && Values.Time > 2000 && newRedPlayerCount > 0 && newBluePlayerCount > 0)
            {
                Values.Time = 2000;
            }
        }
    }

    public void RunGameStep()
    {
        GameStep = GameStep + 1;

        UpdatePlayers();

        foreach (var player in Players.Players)
        {
            if (player != null && player.Object.HasValue)
            {
                (HQMObjectIndex objectIndex, _) = player.Object.Value;
                var skater = World.Objects[objectIndex.Index] as HQMSkater;
                if (skater != null)
                {
                    skater.Input = player.Input;
                }
            }
        }

        var events = World.SimulateStep();

        var tempEvents = new List<HQMSimulationEvent>(events);

        //SavedEvents.RemoveRange(3 - 1, SavedEvents.Count - 3 + 1);

        //Queue<(int, int)> stepEvents = new Queue<(int, int)>();

        //foreach (var @event in tempEvents)
        //{
        //    if (@event is HQMSimulationEvent.PuckTouch puckTouch)
        //    {
        //        savedEvents.Clear();
        //        stepEvents.Enqueue((puckTouch.Player, puckTouch.Puck));
        //    }
        //}

        //savedEvents.Insert(0, (gameStep, new List<(int, int)>(stepEvents)));

        //if (savedEvents.Count == 3)
        //{
        //    List<(int, int)> eventsFiveFrameAgo = savedEvents[2].Item2;
        //    foreach (var e in eventsFiveFrameAgo)
        //    {
        //        HQMSkater skater = world.Objects.GetSkater(e.Item1);
        //        if (skater != null && (skater.StickLimit == 0.0 || skater.StickLimit > 0.01))
        //        {
        //            HQMPuck puck = world.Objects.GetPuckMut(e.Item2);
        //            if (puck != null)
        //            {
        //                puck.Body.LinearVelocity = LimitVectorLength(puck.Body.LinearVelocity, 0.2665f);
        //            }
        //        }
        //    }
        //}

        var packets = World.GetPackets();

        if (HistoryLength > 0)
        {
            var newReplayTick = new ReplayTick
            {
                GameStep = GameStep,
                Packets = packets
            };

            SavedEvents.RemoveRange(HistoryLength - 1, SavedHistory.Count - HistoryLength + 1);
            SavedHistory.Insert(0, newReplayTick);
        }
        else
        {
            SavedHistory.Clear();
        }

        try
        {
            SavedPackets.RemoveRange(192 - 1, SavedPackets.Count - 192 + 1);
            SavedPackets.Insert(0, packets);
            Packet = Packet + 1;
            SavedPings.RemoveRange(100 - 1, SavedPings.Count - 100 + 1);
            SavedPings.Insert(0, DateTime.Now);
        }
        catch (Exception ex)
        {

        }

        //if (Config.ReplaysEnabled != ReplayEnabled.Off )
        //{
        //    WriteReplay();
        //}
    }
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

    public bool HasRules(HQMClientVersion v)
    {
        switch (v)
        {
            case HQMClientVersion.Vanilla:
            case HQMClientVersion.Ping:
                return false;
            case HQMClientVersion.PingRules:
                return true;
            default:
                return false;
        }
    }

    public async Task SendUpdates(
        uint gameId,
        List<ObjectPacket[]> packets,
        uint gameStep,
        bool gameOver,
        uint redScore,
        uint blueScore,
        uint time,
        uint goalMessageTime,
        uint period,
        HQMRulesState rulesState,
        uint currentPacket,
        List<HQMServerPlayer> players,
        UdpClient socket,
        HQMServerPlayerIndex? forceView,
        List<byte> writeBuf)
    {
        foreach (var player in players.Where(x => x != null))
        {
            if (player.Data is HQMNetworkPlayerData networkPlayer)
            {
                writeBuf.Clear();
                var writer = new HQMMessageWriter(writeBuf);

                if (networkPlayer.GameId != gameId)
                {
                    writer.WriteBytesAligned(GAME_HEADER);
                    writer.WriteByteAligned(6);
                    writer.WriteUInt32Aligned(gameId);
                }
                else
                {
                    writer.WriteBytesAligned(GAME_HEADER);
                    writer.WriteByteAligned(5);
                    writer.WriteUInt32Aligned(gameId);
                    writer.WriteUInt32Aligned(gameStep);
                    writer.WriteBits(1, gameOver ? 1u : 0u);
                    writer.WriteBits(8, redScore);
                    writer.WriteBits(8, blueScore);
                    writer.WriteBits(16, time);

                    writer.WriteBits(16, goalMessageTime);
                    writer.WriteBits(8, period);
                    uint view = forceView.HasValue ? (uint)forceView.Value.Index : (uint)networkPlayer.ViewPlayerIndex.Index;
                    writer.WriteBits(8, view);

                    // if using a non-cryptic version, send ping
                    if (HasPing(networkPlayer.ClientVersion))
                    {
                        writer.WriteUInt32Aligned(networkPlayer.DeltaTime);
                    }

                    // if baba's second version or above, send rules
                    if (HasRules(networkPlayer.ClientVersion))
                    {
                        uint num = rulesState switch
                        {
                            HQMRulesState.Regular { OffsideWarning: true, IcingWarning: true } => 3,
                            HQMRulesState.Regular { OffsideWarning: true, IcingWarning: false } => 1,
                            HQMRulesState.Regular { OffsideWarning: false, IcingWarning: true } => 2,
                            HQMRulesState.Offside { } => 4,
                            HQMRulesState.Icing { } => 8,
                            _ => 0
                        };
                        writer.WriteUInt32Aligned(num);
                    }

                    writer.WriteObjects(packets, currentPacket, networkPlayer.KnownPacket);

                    int start = networkPlayer.KnownMsgPos > networkPlayer.Messages.Count ? networkPlayer.Messages.Count : networkPlayer.KnownMsgPos;
                    int remainingMessages = Math.Min(networkPlayer.Messages.Count - start, 15);

                    writer.WriteBits(4, (uint)remainingMessages);
                    writer.WriteBits(16, (uint)start);

                    for (int i = start; i < start + remainingMessages; i++)
                    {
                        writer.WriteMessage(networkPlayer.Messages[i]);
                    }
                }

                byte[] slice = writeBuf.ToArray();
                await socket.SendAsync(slice, slice.Length, networkPlayer.Addr);

                Console.WriteLine("Sent update to {0}", networkPlayer.Addr);
            }
        }
    }

    public void NewGame(HQMInitialGameValues v)
    {
        Values = v.Values;
        World = new HQMGameWorld2(v.PhysicsConfiguration, v.PuckSlots);
        GameId += 1;
        Messages.Clear();

        ReplayMsgPos = 0;
        Packet = uint.MaxValue;
        ReplayLastPacket = uint.MaxValue;
        GameStep = uint.MaxValue;

        SavedPackets = Enumerable.Repeat(new ObjectPacket[32], 192).ToList();
        SavedPings = Enumerable.Repeat(new DateTime(), 100).ToList();
        SavedHistory.Clear();
        ReplayQueue.Clear();
        HasCurrentGameBeenActive = false;

        byte[] oldReplayData = ReplayData;
        ReplayData = new byte[0];

        if (Config.ReplaysEnabled == ReplayEnabled.On && oldReplayData.Length > 0)
        {
            byte[] replayDataWithHeader = new byte[oldReplayData.Length + 8];
            Array.Copy(BitConverter.GetBytes(0u), 0, replayDataWithHeader, 0, 4);
            Array.Copy(BitConverter.GetBytes((uint)oldReplayData.Length), 0, replayDataWithHeader, 4, 4);
            Array.Copy(oldReplayData, 0, replayDataWithHeader, 8, oldReplayData.Length);

            string time = StartTime.ToString("yyyy-MM-ddTHHmmss");
            string fileName = $"{Config.ServerName}.{time}.hrp";

            switch (Config.ReplaySaving)
            {
                case ReplaySaving.File:
                    Task.Run(async () =>
                    {
                        try
                        {
                            Directory.CreateDirectory("replays");
                            string path = Path.Combine("replays", fileName);
                            await File.WriteAllBytesAsync(path, replayDataWithHeader);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    });
                    break;

                    //case ReplaySaving.Endpoint endpoint:
                    //    Task.Run(async () =>
                    //    {
                    //        try
                    //        {
                    //            MultipartFormDataContent form = new MultipartFormDataContent
                    //            {
                    //                { new StringContent(time), "time" },
                    //                { new StringContent(serverName), "server" },
                    //                { new ByteArrayContent(replayDataWithHeader), "replay", fileName }
                    //            };

                    //            await reqwestClient.PostAsync(endpoint.Url, form);
                    //        }
                    //        catch (Exception e)
                    //        {
                    //            Console.WriteLine(e);
                    //        }
                    //    });
                    //    break;
            }
        }

        for (int i = 0; i < Players.Players.Count; i++)
        {
            HQMServerPlayerIndex playerIndex = new HQMServerPlayerIndex(i);
            HQMServerPlayer player = Players.Players[i];
            if (player != null)
            {
                if (player.Reset(playerIndex))
                {
                    HQMMessage update = player.GetUpdateMessage(playerIndex);
                    Messages.AddGlobalMessage(update, true, true);
                }
                else
                {
                    HQMMessage update = new HQMMessage(HQMMessageType.PlayerUpdate, new PlayerUpdateItem
                    {
                        Name = player.PlayerName,
                        ObjectInfo = null,
                        Index = playerIndex,
                        InServer = false
                    });
                    Messages.AddGlobalMessage(update, false, false);
                    Players.Players[i] = null;
                }
            }
        }
    }

    public async Task Tick(
        UdpClient socket,
        List<byte> writeBuf)
    {
        Console.WriteLine("Tick");
        try
        {
            if (PlayerCount() != 0)
            {
                if (!HasCurrentGameBeenActive)
                {
                    StartTime = DateTime.UtcNow;
                    HasCurrentGameBeenActive = true;

                    var puckLineStart = World.Rink.Width / 2.0f - 0.4f * ((Config.WarmupPucks - 1f));

                    for (int i = 0; i < Config.WarmupPucks; i++)
                    {
                        var pos = new Vector3(
                            puckLineStart + 0.8f * i,
                            Config.SpawnPuckAltitude,
                            World.Rink.Length / 2.0f
                        );
                        World.CreatePuckObject(pos, Quaternion.identity);
                    }

                    Console.WriteLine($"New game {GameId} started");
                }

                uint gameStep = GameStep;
                HQMServerPlayerIndex? forcedView = null;
                RemoveInactivePlayers();

                ReplayTick? tick = null;
                if (ReplayQueue.Count > 0)
                {
                    ReplayElement replayElement = ReplayQueue[0];
                    if (replayElement.Data.Count > 0)
                    {
                        tick = replayElement.Data[0];
                        replayElement.Data.RemoveAt(0);
                        forcedView = replayElement.ForceView;
                    }
                    else
                    {
                        ReplayQueue.RemoveAt(0);
                    }
                }

                if (tick != null)
                {
                    gameStep = tick.Value.GameStep;
                    var packets = tick.Value.Packets;
                    SavedPackets.RemoveRange(192 - 1, SavedPackets.Count - 192 + 1);
                    SavedPackets.Insert(0, packets);
                    SavedPings.RemoveRange(100 - 1, SavedPings.Count - 100 + 1);
                    SavedPings.Insert(0, DateTime.Now);

                    Packet = Packet + 1;
                }
                else
                {
                    RunGameStep();
                    forcedView = null;
                }

                foreach (var (rec, message) in Messages.WaitingMessages)
                {
                    switch (rec)
                    {
                        case HQMWaitingMessageReceiverAll all:
                            foreach (var player in Players.Players.Where(x => x != null))
                            {
                                player.AddMessage(message);
                            }
                            break;
                        case HQMWaitingMessageReceiverSpecific specificReceiver:
                            if (specificReceiver.Index.Index < Players.Players.Count)
                            {
                                Players.Players[specificReceiver.Index.Index].AddMessage(message);
                            }
                            break;
                    }
                }

                Messages.WaitingMessages.Clear();

                await SendUpdates(
                    GameId,
                    SavedPackets,
                    gameStep,
                    Values.GameOver,
                    Values.RedScore,
                    Values.BlueScore,
                    Values.Time,
                    Values.GoalMessageTimer,
                    Values.Period,
                    Values.RulesState,
                    Packet,
                    Players.Players,
                    socket,
                    forcedView,
                    writeBuf);

                while (RequestedReplays.Count > 0)
                {
                    (var startStep, var endStep, var forceView) = RequestedReplays[0];
                    RequestedReplays.RemoveAt(0);

                    var iEnd = GameStep - endStep;
                    var iStart = GameStep - startStep;
                    if (iStart <= iEnd)
                    {
                        continue;
                    }

                    var data = SavedHistory.GetRange((int)iEnd, (int)iStart - (int)iEnd + 1);
                    ReplayQueue.Add(new ReplayElement { Data = data, ForceView = forceView });
                }
            }
            else if (HasCurrentGameBeenActive)
            {
                Console.WriteLine($"Game {GameId} abandoned");
                NewGame(GetInitialGameValues());
                AllowJoin = true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public class HQMInitialGameValues
    {
        public HQMGameValues Values { get; set; }
        public int PuckSlots { get; set; }
        public HQMPhysicsConfiguration PhysicsConfiguration { get; set; }
    }

    public HQMInitialGameValues GetInitialGameValues()
    {
        var values = new HQMGameValues();

        values.Time = Config.TimeWarmup * 100;
        return new HQMInitialGameValues
        {
            Values = values,
            PuckSlots = Config.WarmupPucks,
            PhysicsConfiguration = Config.PhysicsConfiguration
        };
    }
    private int PlayerCount()
    {
        return Players.Players.Count(x => x != null);
    }

    public HQMServerPlayerIndex? FindPlayerSlot(IPEndPoint addr)
    {
        var i = 0;
        foreach (var kvp in Players.Players)
        {
            if (kvp != null && kvp.Data is HQMNetworkPlayerData networkPlayer)
            {
                if (networkPlayer.Addr.Equals(addr))
                {
                    return new HQMServerPlayerIndex(i);
                }
            }
            i += 1;
        }
        return null;
    }

    public HQMServerPlayerIndex? FindEmptyPlayerSlot()
    {
        for (int i = 0; i < Players.Players.Count; i++)
        {
            if (Players.Players[i] == null)
            {
                return new HQMServerPlayerIndex(i);
            }
        }
        return null;
    }

    public HQMServerPlayerIndex? AddPlayer(string playerName, IPEndPoint addr)
    {
        var playerIndex = FindEmptyPlayerSlot();
        if (playerIndex.HasValue)
        {
            var newPlayer = HQMServerPlayer.NewNetworkPlayer(
                playerIndex.Value,
                playerName,
                addr,
                Messages.PersistentMessages
            );
            var update = newPlayer.GetUpdateMessage(playerIndex.Value);


            Players.Players[playerIndex.Value.Index] = newPlayer;

            Messages.AddGlobalMessage(update, true, true);

            List<string> welcome = Config.Welcome;
            foreach (string welcomeMsg in welcome)
            {
                Messages.AddDirectedServerChatMessage(welcomeMsg, playerIndex.Value);
            }

            return playerIndex;
        }
        return null;
    }

    public void PlayerJoin(
        IPEndPoint addr,
        uint playerVersion,
        string name)
    {

        int playerCount = PlayerCount();
        var maxPlayerCount = Config.PlayerMax;
        if (playerCount >= maxPlayerCount)
        {
            return; // Ignore join request
        }
        if (playerVersion != 55)
        {
            return; // Not the right version
        }
        var currentSlot = FindPlayerSlot(addr);
        if (currentSlot.HasValue)
        {
            return; // Player has already joined
        }

        // Check ban list
        if (BanList.Contains(addr.Address))
        {
            return;
        }

        // Disabled join
        //if (!AllowJoin)
        //{
        //    return;
        //}

        var playerIndex = AddPlayer(name, addr);
        if (playerIndex.HasValue)
        {
            Console.WriteLine($"{name} ({playerIndex.Value}) joined server from address {addr}");
            string msg = $"{name} joined";
            Messages.AddServerChatMessage(msg);
        }
    }

    public async Task RequestInfo(
        UdpClient socket,
        IPEndPoint addr,
        uint version,
        uint ping,
        List<byte> writeBuf)
    {
        writeBuf.Clear();
        var writer = new HQMMessageWriter(writeBuf);
        writer.WriteBytesAligned(GAME_HEADER);
        writer.WriteByteAligned(1);
        writer.WriteBits(8, 55);
        writer.WriteUInt32Aligned(ping);

        int playerCount = PlayerCount();
        writer.WriteBits(8, (uint)playerCount);
        writer.WriteBits(4, 4);
        writer.WriteBits(4, (uint)Config.PhysicsConfiguration.TeamMax);

        writer.WriteBytesAlignedPadded(32, Encoding.ASCII.GetBytes(Config.ServerName));

        byte[] slice = writeBuf.ToArray();
        await socket.SendAsync(slice, slice.Length, addr);
    }

    public void RemovePlayer(HQMServerPlayerIndex playerIndex, bool onReplay)
    {
        if (playerIndex.Index < Players.Players.Count)
        {
            HQMServerPlayer player = Players.Players[playerIndex.Index];
            string playerName = player.PlayerName;
            bool isAdmin = player.IsAdmin;

            if (player.Object.HasValue)
            {
                (HQMObjectIndex objectIndex, _) = player.Object.Value;
                World.RemovePlayer(objectIndex);
            }

            HQMMessage update = new HQMMessage(HQMMessageType.PlayerUpdate, new PlayerUpdateItem
            {
                Name = playerName,
                ObjectInfo = null,
                Index = playerIndex,
                InServer = false
            });

            Messages.AddGlobalMessage(update, true, onReplay);

            Players.Players[playerIndex.Index] = null;

            if (isAdmin)
            {
                bool adminFound = Players.Players.Exists(p => p.IsAdmin);

                if (!adminFound)
                {
                    AllowJoin = true;
                }
            }
        }
    }

    public void PlayerExit(IPEndPoint addr)
    {
        var playerIndex = FindPlayerSlot(addr);

        if (playerIndex.HasValue)
        {
            var playerName = Players.Players[playerIndex.Value.Index].PlayerName;
            RemovePlayer(playerIndex.Value, true);
            Console.WriteLine($"{playerName} ({playerIndex.Value}) exited server");
            string msg = $"{playerName} exited";
            Messages.AddServerChatMessage(msg);
        }
    }

    private void ProcessCommand(string command, string arg, HQMServerPlayerIndex playerIndex)
    {

    }

    public void ProcessMessage(
       string msg,
       HQMServerPlayerIndex playerIndex)
    {
        if (playerIndex.Index < Players.Players.Count)
        {
            if (msg.StartsWith("/"))
            {
                string[] split = msg.Split(new[] { ' ' }, 2);
                string command = split[0].Substring(1);
                string arg = split.Length < 2 ? "" : split[1];
                ProcessCommand(command, arg, playerIndex);
            }
            else
            {
                if (!IsMuted)
                {
                    HQMServerPlayer player = Players.Players[playerIndex.Index];
                    switch (player.IsMuted)
                    {
                        case HQMMuteStatus.NotMuted:
                            Console.WriteLine($"{player.PlayerName} ({playerIndex.Index}): {msg}");
                            Messages.AddUserChatMessage(msg, playerIndex);
                            break;
                        case HQMMuteStatus.ShadowMuted:
                            Messages.AddDirectedUserChatMessage(msg, playerIndex, playerIndex);
                            break;
                        case HQMMuteStatus.Muted:
                            break;
                    }
                }
            }
        }
    }

    public void PlayerUpdate(
        IPEndPoint addr,
        uint currentGameId,
        HQMPlayerInput input,
        uint? deltatime,
        uint newKnownPacket,
        int knownMsgpos,
        (byte, string)? chat,
        HQMClientVersion clientVersion)
    {
        HQMServerPlayerIndex? currentSlot = FindPlayerSlot(addr);
        if (currentSlot.HasValue)
        {
            HQMServerPlayer player = Players.Players[currentSlot.Value.Index];
            if (player.Data is HQMNetworkPlayerData networkPlayer)
            {
                DateTime timeReceived = DateTime.Now;

                TimeSpan? durationSincePacket = null;
                if (networkPlayer.GameId == currentGameId && networkPlayer.KnownPacket < newKnownPacket)
                {
                    int diff = (int)(Packet - newKnownPacket);
                    if (diff >= 0 && diff < SavedPings.Count)
                    {
                        var lastTimeReceived = SavedPings[diff];
                        durationSincePacket = timeReceived - lastTimeReceived;
                    }
                }

                if (durationSincePacket.HasValue)
                {
                    if (networkPlayer.LastPing.Count >= 100)
                    {
                        networkPlayer.LastPing.RemoveAt(networkPlayer.LastPing.Count - 1);
                    }
                    networkPlayer.LastPing.Insert(0, (float)durationSincePacket.Value.TotalSeconds);
                }

                networkPlayer.Inactivity = 0;
                networkPlayer.ClientVersion = clientVersion;
                networkPlayer.KnownPacket = newKnownPacket;
                player.Input = input;
                networkPlayer.GameId = currentGameId;
                networkPlayer.KnownMsgPos = knownMsgpos;
                if (deltatime.HasValue)
                {
                    networkPlayer.DeltaTime = deltatime.Value;
                }

                if (chat.HasValue)
                {
                    (byte rep, string message) = chat.Value;
                    if (networkPlayer.ChatRep != rep)
                    {
                        networkPlayer.ChatRep = rep;
                        ProcessMessage(message, currentSlot.Value);
                    }
                }
            }
        }
    }

    public async Task HandleMessage(
        IPEndPoint addr,
        UdpClient socket,
        HQMClientToServerMessage command,
        List<byte> writeBuf)
    {
        switch (command)
        {
            case JoinMessage joinCommand:
                Console.WriteLine("Join {0} v {1} pn {2}", addr, joinCommand.Version, joinCommand.PlayerName);
                PlayerJoin(addr, joinCommand.Version, joinCommand.PlayerName);
                break;
            case UpdateMessage updateCommand:
                Console.WriteLine("UpdateCommand {0} curGame {1}", addr, updateCommand.CurrentGameId);
                PlayerUpdate(
                    addr,
                    updateCommand.CurrentGameId,
                    updateCommand.Input,
                    updateCommand.Deltatime,
                    updateCommand.NewKnownPacket,
                    updateCommand.KnownMsgPos,
                    updateCommand.Chat,
                    updateCommand.Version
                    );
                break;
            case ExitMessage exitCommand:
                Console.WriteLine("exitCommand {0} ", addr);
                PlayerExit(addr);
                break;
            case ServerInfoMessage serverInfoCommand:
                await RequestInfo(socket, addr, serverInfoCommand.Version, serverInfoCommand.Ping, writeBuf);
                break;
            default:
                throw new ArgumentException("Unknown command type");
        }
    }
    public async Task RunServer(ushort port, string publicAddress, HQMServerConfiguration config)
    {
        try
        {
            var initialValues = GetInitialGameValues();

            var reqwestClient = new HttpClient();

            var server = new HQMServer(config, initialValues.Values, new HQMGameWorld2(initialValues.PhysicsConfiguration, initialValues.PuckSlots));

            Console.WriteLine("Server started");

            var addr = new IPEndPoint(IPAddress.Any, port);
            var socket = new UdpClient(addr);

            void Tick(object state)
            {
                _ = server.Tick(socket, new List<byte>(new byte[4096]));
            }

            var tickTimer = new Timer(Tick, null, 0, 10);

            Console.WriteLine($"Server listening at address {socket.Client.LocalEndPoint}");


            async Task<IPEndPoint> GetHttpResponse(HttpClient client, string address)
            {
                var response = await client.GetAsync(address);
                var responseText = await response.Content.ReadAsStringAsync();

                var split = responseText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                var ip = IPAddress.Parse(split[1]);
                var port = ushort.Parse(split[2]);
                return new IPEndPoint(ip, port);
            }

            if (!string.IsNullOrEmpty(publicAddress))
            {
                var publicClient = new HttpClient();
                var publicAddr = publicAddress;

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var masterServer = await GetHttpResponse(publicClient, publicAddr);
                            for (int i = 0; i < 60; i++)
                            {
                                var msg = Encoding.ASCII.GetBytes("Hock\x20");
                                await socket.SendAsync(msg, msg.Length, masterServer);
                                Console.WriteLine("Sent master query");
                                await Task.Delay(TimeSpan.FromSeconds(10));
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error: {e.Message}");
                            await Task.Delay(TimeSpan.FromSeconds(15));
                        }
                    }
                });
            }

            var sendUpdatesStream = Task.Run(async () =>
            {
                var buf = new List<byte>(new byte[4096]);
                while (true)
                {
                    server.Tick(socket, buf);
                }
            });

            var packetStream = Task.Run(async () =>
            {
                var buf = new byte[512];
                var codec = new HQMMessageCodec();
                while (true)
                {
                    try
                    {
                        var result = await socket.ReceiveAsync();
                        var data = codec.ParseMessage(result.Buffer);
                        await server.HandleMessage(result.RemoteEndPoint, socket, data, new List<byte>(new byte[4096]));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + ex.StackTrace);
                    }
                }
            });

            await Task.WhenAll(packetStream, sendUpdatesStream);

            Console.WriteLine("Stopped");
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message + ex.StackTrace);
        }
    }
}