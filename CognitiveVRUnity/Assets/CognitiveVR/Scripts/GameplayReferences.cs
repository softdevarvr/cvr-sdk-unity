﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//static access point to get references to main cameras and controllers

namespace CognitiveVR
{
    public static class GameplayReferences
    {
        #region HMD and Controllers

        private static Camera cam;
        public static Camera HMDCameraComponent
        {
            get
            {
                if (cam == null)
                {
                    if (HMD != null)
                    {
                        cam = HMD.GetComponent<Camera>();
                    }
                }
                return cam;
            }
        }

#if CVR_OCULUS
        static OVRCameraRig _cameraRig;
        static OVRCameraRig CameraRig
        {
            get
            {
                if (_cameraRig == null)
                {
                    _cameraRig = GameObject.FindObjectOfType<OVRCameraRig>();
                }
                return _cameraRig;
            }
        }
#endif

        private static Transform _hmd;
        /// <summary>Returns HMD based on included SDK, or Camera.Main if no SDK is used. MAY RETURN NULL!</summary>
        public static Transform HMD
        {
            get
            {
                if (_hmd == null)
                {
#if CVR_STEAMVR
                    SteamVR_Camera cam = GameObject.FindObjectOfType<SteamVR_Camera>();
                    if (cam != null){ _hmd = cam.transform; }
                    if (_hmd == null)
                    {
                        if (Camera.main == null)
                            _hmd = GameObject.FindObjectOfType<Camera>().transform;
                        else
                            _hmd = Camera.main.transform;
                    }
#elif CVR_OCULUS
                    OVRCameraRig rig = GameObject.FindObjectOfType<OVRCameraRig>();
                    if (rig != null)
                    {
                        Camera cam = rig.centerEyeAnchor.GetComponent<Camera>();
                        _hmd = cam.transform;
                    }
                    if (_hmd == null)
                    {
                        if (Camera.main == null)
                            _hmd = GameObject.FindObjectOfType<Camera>().transform;
                        else
                            _hmd = Camera.main.transform;
                    }
#elif CVR_FOVE
                    /*FoveEyeCamera eyecam = GameObject.FindObjectOfType<FoveEyeCamera>();
                    if (eyecam != null)
                    {
                        Camera cam = eyecam.GetComponentInChildren<Camera>();
                        _hmd = cam.transform;
                    }*/
                    if (_hmd == null)
                    {
                        if (Camera.main == null)
                            _hmd = GameObject.FindObjectOfType<Camera>().transform;
                        else
                            _hmd = Camera.main.transform;
                    }
#elif CVR_SNAPDRAGON
                    _hmd = GameObject.FindObjectOfType<Camera>().transform;
#else
                    if (Camera.main == null)
                        _hmd = GameObject.FindObjectOfType<Camera>().transform;
                    else
                        _hmd = Camera.main.transform;

#endif
                }
                return _hmd;
            }
        }

#if CVR_OCULUS
        //records controller transforms from either interaction player or behaviour poses
        static void InitializeControllers()
        {
            if (controllers == null)
            {
                controllers = new ControllerInfo[2];
            }

            if (controllers[0] == null)
            {
                controllers[0] = new ControllerInfo() { transform = CameraRig.leftHandAnchor, isRight = false, id = 1 };
                controllers[0].connected = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
                controllers[0].visible = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);

            }

            if (controllers[1] == null)
            {
                controllers[1] = new ControllerInfo() { transform = CameraRig.rightHandAnchor, isRight = true, id = 2 };
                controllers[1].connected = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
                controllers[1].visible = OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            }
        }
#elif CVR_STEAMVR2

        static Valve.VR.SteamVR_Behaviour_Pose[] poses;

        //records controller transforms from either interaction player or behaviour poses
        static void InitializeControllers()
        {
            if (controllers == null || controllers[0].transform == null || controllers[1].transform == null)
            {
                if (controllers == null)
                {
                    controllers = new ControllerInfo[2];
                    controllers[0] = new ControllerInfo() { transform = null, isRight = false, id = -1 };
                    controllers[1] = new ControllerInfo() { transform = null, isRight = false, id = -1 };
                }

                if (poses == null)
                {
                    poses = GameObject.FindObjectsOfType<Valve.VR.SteamVR_Behaviour_Pose>();
                }
                if (poses != null && poses.Length > 1)
                {
                    controllers[0].transform = poses[0].transform;
                    controllers[1].transform = poses[1].transform;
                    controllers[0].isRight = poses[0].inputSource == Valve.VR.SteamVR_Input_Sources.RightHand;
                    controllers[1].isRight = poses[1].inputSource == Valve.VR.SteamVR_Input_Sources.RightHand;
                    controllers[0].id = poses[0].GetDeviceIndex();
                    controllers[1].id = poses[1].GetDeviceIndex();
                }
            }
        }

#elif CVR_STEAMVR

        static SteamVR_ControllerManager cm;
        static Valve.VR.InteractionSystem.Player player;

        static void InitializeControllers()
        {
            if (controllers != null && controllers[0].transform != null && controllers[1].transform != null && controllers[0].id >0 && controllers[1].id > 0) {return;}

            if (controllers == null)
            {
                controllers = new ControllerInfo[2];
                controllers[0] = new ControllerInfo();
                controllers[1] = new ControllerInfo();
            }
            //try to initialize with controllermanager
            //otherwise try to initialize with player.hands
            if (cm == null)
            {
                cm = GameObject.FindObjectOfType<SteamVR_ControllerManager>();
            }
            if (cm != null)
            {
                var left = cm.left.GetComponent<SteamVR_TrackedObject>();
                controllers[0].transform = left.transform;
                controllers[0].id = (int)left.index;
                controllers[0].isRight = false;
                if (left.index != SteamVR_TrackedObject.EIndex.None)
                {
                    controllers[0].connected = SteamVR_Controller.Input((int)left.index).connected;
                    controllers[0].visible = SteamVR_Controller.Input((int)left.index).valid;
                }
                else
                {
                    controllers[0].connected = false;
                    controllers[0].visible = false;
                }

                var right = cm.right.GetComponent<SteamVR_TrackedObject>();
                controllers[1].transform = right.transform;
                controllers[1].id = (int)right.index;
                controllers[1].isRight = true;
                if (right.index != SteamVR_TrackedObject.EIndex.None)
                {
                    controllers[1].connected = SteamVR_Controller.Input((int)right.index).connected;
                    controllers[1].visible = SteamVR_Controller.Input((int)right.index).valid;
                }
                else
                {
                    controllers[1].connected = false;
                    controllers[1].visible = false;
                }
            }
            else
            {
                if (player == null)
                {
                    player = GameObject.FindObjectOfType<Valve.VR.InteractionSystem.Player>();
                }
                if (player != null)
                {
                    var left = player.leftHand;
                    if (left != null && left.controller != null)
                    {
                        controllers[0].transform = player.leftHand.transform;
                        controllers[0].id = (int)player.leftHand.controller.index;
                        controllers[0].isRight = false;
                        controllers[0].connected = left.controller.connected;
                        controllers[0].visible = left.controller.valid;
                    }

                    var right = player.rightHand;
                    if (right != null && right.controller != null)
                    {
                        controllers[1].transform = player.rightHand.transform;
                        controllers[1].id = (int)player.rightHand.controller.index;
                        controllers[1].isRight = true;
                        controllers[1].connected = right.controller.connected;
                        controllers[1].visible = right.controller.valid;
                    }
                }
            }

        }
#else
        static void InitializeControllers()
        {
            if (controllers == null)
            {
                controllers = new ControllerInfo[2];
            }
        }
#endif




        public class ControllerInfo
        {
            public Transform transform;
            public bool isRight;
            public int id = -1;

            public bool connected;
            public bool visible;
        }

        static ControllerInfo[] controllers;

        public static bool GetControllerInfo(int deviceID, out ControllerInfo info)
        {
            InitializeControllers();
            if (controllers[0].id == deviceID && controllers[0].transform != null) { info = controllers[0]; return true; }
            if (controllers[1].id == deviceID && controllers[1].transform != null) { info = controllers[1]; return true; }
            info = null;
            return false;
        }

        public static bool GetControllerInfo(bool right, out ControllerInfo info)
        {
            InitializeControllers();
            if (controllers[0].isRight == right && controllers[0].id > 0 && controllers[0].transform != null) { info = controllers[0]; return true; }
            if (controllers[1].isRight == right && controllers[1].id > 0 && controllers[1].transform != null) { info = controllers[1]; return true; }
            info = null;
            return false;
        }


        /// <summary>
        /// steamvr ID is tracked device id
        /// oculus ID 0 is right, 1 is left controller
        /// </summary>
        public static bool GetController(int deviceid, out Transform transform)
        {
#if CVR_STEAMVR || CVR_STEAMVR2 || CVR_OCULUS
            InitializeControllers();
            if (controllers[0].id == deviceid) { transform = controllers[0].transform; return true; }
            if (controllers[1].id == deviceid) { transform = controllers[1].transform; return true; }
            transform = null;
            return false;
#else
            transform = null;
            return false;
#endif
        }

        public static bool GetController(bool right, out Transform transform)
        {
#if CVR_STEAMVR || CVR_STEAMVR2 || CVR_OCULUS
            InitializeControllers();
            if (right == controllers[0].isRight && controllers[0].id > 0) { transform = controllers[0].transform; return true; }
            if (right == controllers[1].isRight && controllers[1].id > 0) { transform = controllers[1].transform; return true; }
            transform = null;
            return false;
#else
            transform = null;
            return false;
#endif
        }

        /// <summary>Returns Tracked Controller position by index. Based on SDK</summary>
        public static bool GetControllerPosition(bool right, out Vector3 position)
        {
#if CVR_STEAMVR || CVR_STEAMVR2 || CVR_OCULUS

            InitializeControllers();
            if (right == controllers[0].isRight && controllers[0].transform != null && controllers[0].id > 0) { position = controllers[0].transform.position; return true; }
            if (right == controllers[1].isRight && controllers[1].transform != null && controllers[1].id > 0) { position = controllers[1].transform.position; return true; }
            position = Vector3.zero;
            return false;
#else
            position = Vector3.zero;
            return false;
#endif
        }

        #endregion
    }
}