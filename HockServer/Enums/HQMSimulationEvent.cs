public interface HQMSimulationEvent
{
}

public class PuckTouchEvent : HQMSimulationEvent
{
    public HQMObjectIndex Player { get; }
    public HQMObjectIndex Puck { get; }

    public PuckTouchEvent(HQMObjectIndex player, HQMObjectIndex puck)
    {
        Player = player;
        Puck = puck;
    }
}

public class PuckReachedDefensiveLineEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckReachedDefensiveLineEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckPassedDefensiveLineEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckPassedDefensiveLineEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckReachedCenterLineEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckReachedCenterLineEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckPassedCenterLineEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckPassedCenterLineEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckReachedOffensiveZoneEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckReachedOffensiveZoneEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckEnteredOffensiveZoneEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckEnteredOffensiveZoneEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckEnteredNetEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckEnteredNetEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckPassedGoalLineEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckPassedGoalLineEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}

public class PuckTouchedNetEvent : HQMSimulationEvent
{
    public HQMTeam Team { get; }
    public HQMObjectIndex Puck { get; }

    public PuckTouchedNetEvent(HQMTeam team, HQMObjectIndex puck)
    {
        Team = team;
        Puck = puck;
    }
}