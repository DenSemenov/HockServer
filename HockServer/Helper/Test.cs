using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HockServer.Helper
{
    public static  class Test
    {
        public static void Run()
        {
            var world = new HQMGameWorld2(new HQMPhysicsConfiguration(), 0);

            var pos = new Vector3(1,2,3);
            var rot = Quaternion.identity;
            var hand = HQMSkaterHand.Left;
            var mass = 5;
            world.CreatePlayerObject(pos, rot, hand, mass, 0);
            world.SimulateStep();
        }
       
    }
}
