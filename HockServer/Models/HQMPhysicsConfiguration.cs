public class HQMPhysicsConfiguration
{
    public string ServerName { get; set; } = "HQM";
    public int Port { get; set; } = 27585;
    public bool Public { get; set; } = true;
    public int PlayerMax { get; set; } = 20;
    public int TeamMax { get; set; } = 4;
    public string Password { get; set; } = "HQM";
    public float Gravity { get; set; } = 0.000680555f;
    public float PlayerAcceleration { get; set; } = 0.000208333f;
    public float PlayerDeceleration { get; set; } = 0.000555555f;
    public float MaxPlayerSpeed { get; set; } = 0.05f;
    public float MaxPlayerShiftSpeed { get; set; } = 0.0333333f;
    public float PuckRinkFriction { get; set; } = 0.05f;
    public float PlayerTurning { get; set; } = 0.00041666666f;
    public float PlayerShiftTurning { get; set; } = 0.00038888888f;
    public float PlayerShiftAcceleration { get; set; } = 0.00027777f;
    public bool LimitJumpSpeed { get; set; } = false;
    public int Periods { get; set; } = 3;
    public int TimePeriod { get; set; } = 300;
    public int TimeWarmup { get; set; } = 300;
    public int TimeBreak { get; set; } = 10;
    public int TimeIntermission { get; set; } = 20;
    public int WarmupPucks { get; set; } = 8;
    public int Mercy { get; set; } = 0;
    public bool ShiftEnabled { get; set; } = true;
}