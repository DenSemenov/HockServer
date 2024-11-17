using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HockServer.Models
{
    public interface HQMCollision
    {
    }

    public class PlayerRinkCollision : HQMCollision
    {
        public (int, int) Indices;
        public float Overlap;
        public Vector3 Normal;

        public PlayerRinkCollision((int, int) indices, float overlap, Vector3 normal)
        {
            Indices = indices;
            Overlap = overlap;
            Normal = normal;
        }
    }

    public class PlayerPlayerCollision : HQMCollision
    {
        public (int, int) Indices1;
        public (int, int) Indices2;
        public float Overlap;
        public Vector3 Normal;

        public PlayerPlayerCollision((int, int) indices1, (int, int) indices2, float overlap, Vector3 normal)
        {
            Indices1 = indices1;
            Indices2 = indices2;
            Overlap = overlap;
            Normal = normal;
        }
    }
}
