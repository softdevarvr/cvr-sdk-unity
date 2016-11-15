﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Adds room size from SteamVR chaperone to device info
/// </summary>

namespace CognitiveVR
{
    public class RoomSizeTracker : CognitiveVRAnalyticsComponent
    {
        public override void CognitiveVR_Init(Error initError)
        {
            base.CognitiveVR_Init(initError);

#if CVR_STEAMVR
            float roomX = 0;
            float roomY = 0;
            if (Valve.VR.OpenVR.Chaperone == null || !Valve.VR.OpenVR.Chaperone.GetPlayAreaSize(ref roomX, ref roomY))
            {
                Instrumentation.updateDeviceState(new Dictionary<string, object>() { { "cvr.vr.roomsize", "0 x 0" }, { "cvr.vr.roomscale", false } });
            }
            else
            {
                bool seated = Mathf.Approximately(roomX, 1f) && roomX == roomY;
                Instrumentation.updateDeviceState(new Dictionary<string, object>()
                {
                    { "cvr.vr.roomsize", string.Format("{0:0.0} x {1:0.0}", roomX, roomY) },
                    { "cvr.vr.roomscale", !seated }
                });
            }
#elif CVR_OCULUS

            //(x = width, y = height, z = depth)
            Vector3 dimensions = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);

            Instrumentation.updateDeviceState(new Dictionary<string, object>()
            {
                { "cvr.vr.roomsize", string.Format("{0:0.0} x {1:0.0}", dimensions.x, dimensions.z) }
            });
#endif
        }

        public static string GetDescription()
        {
            return "Include Room Size in Device Info from SteamVR Chaperone or Oculus Guardian";
        }
    }
}