using System.Collections.Generic;
using UnityEngine;

public class HQMRinkNet
{
    public List<(Vector3, Vector3, float)> Posts { get; set; }
    public List<(Vector3, Vector3, Vector3, Vector3)> Surfaces { get; set; }
    public Vector3 LeftPost { get; set; }
    public Vector3 RightPost { get; set; }
    public Vector3 Normal { get; set; }
    public Vector3 LeftPostInside { get; set; }
    public Vector3 RightPostInside { get; set; }

    public HQMRinkNet(Vector3 pos, Quaternion rot)
    {
        float frontWidth = 3.0f;
        float backWidth = 2.5f;
        float frontHalfWidth = frontWidth / 2.0f;
        float backHalfWidth = backWidth / 2.0f;
        float height = 1.0f;
        float upperDepth = 0.75f;
        float lowerDepth = 1.0f;

        var frontUpperLeft = pos + rot * new Vector3(-frontHalfWidth, height, 0.0f);
        var frontUpperRight = pos + rot * new Vector3(frontHalfWidth, height, 0.0f);
        var frontLowerLeft = pos + rot * new Vector3(-frontHalfWidth, 0.0f, 0.0f);
        var frontLowerRight = pos + rot * new Vector3(frontHalfWidth, 0.0f, 0.0f);
        var backUpperLeft = pos + rot * new Vector3(-backHalfWidth, height, -upperDepth);
        var backUpperRight = pos + rot * new Vector3(backHalfWidth, height, -upperDepth);
        var backLowerLeft = pos + rot * new Vector3(-backHalfWidth, 0.0f, -lowerDepth);
        var backLowerRight = pos + rot * new Vector3(backHalfWidth, 0.0f, -lowerDepth);

        Posts = new List<(Vector3, Vector3, float)>
        {
            (frontLowerRight, frontUpperRight, 0.1875f),
            (frontLowerLeft, frontUpperLeft, 0.1875f),
            (frontUpperRight, frontUpperLeft, 0.125f),
            (frontLowerLeft, backLowerLeft, 0.125f),
            (frontLowerRight, backLowerRight, 0.125f),
            (frontUpperLeft, backUpperLeft, 0.125f),
            (backUpperRight, frontUpperRight, 0.125f),
            (backLowerLeft, backUpperLeft, 0.125f),
            (backLowerRight, backUpperRight, 0.125f),
            (backLowerLeft, backLowerRight, 0.125f),
            (backUpperLeft, backUpperRight, 0.125f)
        };

        Surfaces = new List<(Vector3, Vector3, Vector3, Vector3)>
        {
            (backUpperLeft, backUpperRight, backLowerRight, backLowerLeft),
            (frontUpperLeft, backUpperLeft, backLowerLeft, frontLowerLeft),
            (frontUpperRight, frontLowerRight, backLowerRight, backUpperRight),
            (frontUpperLeft, frontUpperRight, backUpperRight, backUpperLeft)
        };

        LeftPost = frontLowerLeft;
        RightPost = frontLowerRight;
        Normal = rot * Vector3.forward;
        LeftPostInside = rot * Vector3.right;
        RightPostInside = rot* -Vector3.right;
    }
}