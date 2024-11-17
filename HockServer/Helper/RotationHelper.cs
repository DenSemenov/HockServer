using System;
using UnityEngine;

public static class RotationHelper
{
    private static readonly Vector3 UXP = new Vector3(1.0f, 0.0f, 0.0f);
    private static readonly Vector3 UXN = new Vector3(-1.0f, 0.0f, 0.0f);
    private static readonly Vector3 UYP = new Vector3(0.0f, 1.0f, 0.0f);
    private static readonly Vector3 UYN = new Vector3(0.0f, -1.0f, 0.0f);
    private static readonly Vector3 UZP = new Vector3(0.0f, 0.0f, 1.0f);
    private static readonly Vector3 UZN = new Vector3(0.0f, 0.0f, -1.0f);

    private static readonly Vector3[][] TABLE = new Vector3[][]
    {
        new Vector3[] { UYP, UXP, UZP },
        new Vector3[] { UYP, UZP, UXN },
        new Vector3[] { UYP, UZN, UXP },
        new Vector3[] { UYP, UXN, UZN },
        new Vector3[] { UZP, UXP, UYN },
        new Vector3[] { UXN, UZP, UYN },
        new Vector3[] { UXP, UZN, UYN },
        new Vector3[] { UZN, UXN, UYN }
    };

    public static (uint, uint) ConvertMatrixToNetwork(byte b, Matrix4x4 v)
    {
        var r1 = ConvertRotColumnToNetwork(b, v.GetColumn(1));
        var r2 = ConvertRotColumnToNetwork(b, v.GetColumn(2));
        return (r1, r2);
    }

    public static Matrix4x4 ConvertMatrixFromNetwork(byte b, uint v1, uint v2)
    {
        var r1 = ConvertRotColumnFromNetwork(b, v1);
        var r2 = ConvertRotColumnFromNetwork(b, v2);
        var r0 = Vector3.Cross(r1, r2).normalized;
        return new Matrix4x4(r0, r1, r2, Vector3.zero);
    }

    private static Vector3 ConvertRotColumnFromNetwork(byte b, uint v)
    {
        var start = (int)(v & 7);

        var temp1 = TABLE[start][0];
        var temp2 = TABLE[start][1];
        var temp3 = TABLE[start][2];
        int pos = 3;
        while (pos < b)
        {
            var step = (int)((v >> pos) & 3);
            var c1 = (temp1 + temp2).normalized;
            var c2 = (temp2 + temp3).normalized;
            var c3 = (temp1 + temp3).normalized;
            switch (step)
            {
                case 0:
                    temp2 = c1;
                    temp3 = c3;
                    break;
                case 1:
                    temp1 = c1;
                    temp3 = c2;
                    break;
                case 2:
                    temp1 = c3;
                    temp2 = c2;
                    break;
                case 3:
                    temp1 = c1;
                    temp2 = c2;
                    temp3 = c3;
                    break;
                default:
                    throw new Exception();
            }

            pos += 2;
        }
        return (temp1 + temp2 + temp3).normalized;
    }

    private static uint ConvertRotColumnToNetwork(byte b, Vector3 v)
    {
        uint res = 0;

        if (v.x < 0.0f)
        {
            res |= 1;
        }
        if (v.z < 0.0f)
        {
            res |= 2;
        }
        if (v.y < 0.0f)
        {
            res |= 4;
        }
        var temp1 = TABLE[(int)res][0];
        var temp2 = TABLE[(int)res][1];
        var temp3 = TABLE[(int)res][2];
        for (int i = 3; i < b; i += 2)
        {
            var temp4 = (temp1 + temp2).normalized;
            var temp5 = (temp2 + temp3).normalized;
            var temp6 = (temp1 + temp3).normalized;

            var a1 = Vector3.Cross(temp4 - temp6, v - temp6);
            if (Vector3.Dot(a1, v) < 0.0f)
            {
                var a2 = Vector3.Cross(temp5 - temp4, v - temp4);
                if (Vector3.Dot(a2, v) < 0.0f)
                {
                    var a3 = Vector3.Cross(temp6 - temp5, v - temp5);
                    if (Vector3.Dot(a3, v) < 0.0f)
                    {
                        res |= (uint)(3 << i);
                        temp1 = temp4;
                        temp2 = temp5;
                        temp3 = temp6;
                    }
                    else
                    {
                        res |= (uint)(2 << i);
                        temp1 = temp6;
                        temp2 = temp5;
                    }
                }
                else
                {
                    res |= (uint)(1 << i);
                    temp1 = temp4;
                    temp3 = temp5;
                }
            }
            else
            {
                temp2 = temp4;
                temp3 = temp6;
            }
        }
        return res;
    }
}