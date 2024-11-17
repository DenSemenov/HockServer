using UnityEngine;

public class HQMPlayerInput
{
    public float StickAngle { get; set; }
    public float Turn { get; set; }
    public float Fwbw { get; set; }
    public Vector2 Stick { get; set; }
    public float HeadRot { get; set; }
    public float BodyRot { get; set; }
    public uint Keys { get; set; }

    public bool Jump => (Keys & 0x1) != 0;
    public bool Crouch => (Keys & 0x2) != 0;
    public bool JoinRed => (Keys & 0x4) != 0;
    public bool JoinBlue => (Keys & 0x8) != 0;
    public bool Shift => (Keys & 0x10) != 0;
    public bool Spectate => (Keys & 0x20) != 0;
}