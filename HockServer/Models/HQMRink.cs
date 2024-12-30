using System.Collections.Generic;
using UnityEngine;

public class HQMRink
{
    public List<(Vector3, Vector3)> Planes { get; set; }
    public List<(Vector3, Vector3, float)> Corners { get; set; }
    public HQMRinkNet RedNet { get; set; }
    public HQMRinkNet BlueNet { get; set; }
    public HQMRinkLine CenterLine { get; set; }
    public HQMRinkLine RedZoneBlueLine { get; set; }
    public HQMRinkLine BlueZoneBlueLine { get; set; }
    public float Width { get; set; }
    public float Length { get; set; }
    public HQMRink(float width, float length, float cornerRadius)
    {
        Vector3 zero = new Vector3(0.0f, 0.0f, 0.0f);
        Planes = new List<(Vector3, Vector3)>
        {
            (zero, Vector3.up),
            (new Vector3(0.0f, 0.0f, length), -Vector3.forward),
            (zero, Vector3.forward),
            (new Vector3(width, 0.0f, 0.0f), -Vector3.right),
            (zero, Vector3.right)
        };

        float r = cornerRadius;
        float wr = width - cornerRadius;
        float lr = length - cornerRadius;
        Corners = new List<(Vector3, Vector3, float)>
        {
            (new Vector3(r, 0.0f, r), new Vector3(-1.0f, 0.0f, -1.0f), cornerRadius),
            (new Vector3(wr, 0.0f, r), new Vector3(1.0f, 0.0f, -1.0f), cornerRadius),
            (new Vector3(wr, 0.0f, lr), new Vector3(1.0f, 0.0f, 1.0f), cornerRadius),
            (new Vector3(r, 0.0f, lr), new Vector3(-1.0f, 0.0f, 1.0f), cornerRadius)
        };

        float lineWidth = 0.3f; // IIHF rule 17iii, 17iv
        float goalLineDistance = 4.0f; // IIHF rule 17iv

        float blueLineDistanceNeutralZoneEdge = 22.86f;
        float blueLineDistanceMid = blueLineDistanceNeutralZoneEdge - lineWidth / 2.0f; // IIHF rule 17v and 17vi
                                                                                        // IIHF specifies distance between end boards and edge closest to the neutral zone, but my code specifies middle of line

        float centerX = width / 2.0f;

        float redZoneBluelineZ = length - blueLineDistanceMid;
        float centerZ = length / 2.0f;
        float blueZoneBluelineZ = blueLineDistanceMid;

        RedNet = new HQMRinkNet(
            new Vector3(centerX, 0.0f, goalLineDistance),
            Quaternion.identity
        );

        //var m = Quaternion.EulerRotation(new Vector3(-1.0f, 1.0f, -1.0f));

        BlueNet = new HQMRinkNet(
            new Vector3(centerX, 0.0f, length - goalLineDistance),
            Quaternion.identity
        );

        RedZoneBlueLine = new HQMRinkLine
        {
            Z = redZoneBluelineZ,
            Width = lineWidth
        };
        BlueZoneBlueLine = new HQMRinkLine
        {
            Z = blueZoneBluelineZ,
            Width = lineWidth
        };
        CenterLine = new HQMRinkLine
        {
            Z = centerZ,
            Width = lineWidth
        };

        Width = width;
        Length = length;
    }
}


public enum HQMRinkSideOfLine
{
    BlueSide,
    On,
    RedSide,
}

public class HQMRinkLine
{
    public float Z { get; set; }
    public float Width { get; set; }

    public HQMRinkSideOfLine SideOfLine(Vector3 pos, float radius)
    {
        float dot = pos.z - Z;
        if (dot > (Width / 2.0f) + radius)
        {
            return HQMRinkSideOfLine.RedSide;
        }
        else if (dot < -Width - radius)
        {
            return HQMRinkSideOfLine.BlueSide;
        }
        else
        {
            return HQMRinkSideOfLine.On;
        }
    }
}