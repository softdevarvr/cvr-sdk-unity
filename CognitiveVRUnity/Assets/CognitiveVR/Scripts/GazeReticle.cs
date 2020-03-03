﻿using UnityEngine;
using System.Collections;
using CognitiveVR;
#if CVR_AH
using AdhawkApi;
using AdhawkApi.Numerics.Filters;
#endif

//debug helper for gaze tracking with Fove, Pupil, Tobii, Vive Pro Eye, Adhawk, Varjo

namespace CognitiveVR
{
    [AddComponentMenu("Cognitive3D/Testing/Gaze Reticle")]
    public class GazeReticle : MonoBehaviour
{
    public float Speed = 0.3f;
    public float Distance = 3;

#if CVR_PUPIL
        
        public Vector3 GetLookDirection()
        {
            return gazeDirection;
        }

        PupilLabs.GazeController gazeController;
        Vector3 gazeDirection = Vector3.forward;

        void Start()
        {
            gazeController = FindObjectOfType<PupilLabs.GazeController>();
            if (gazeController != null)
                gazeController.OnReceive3dGaze += ReceiveEyeData;
            else
                Debug.LogError("Pupil Labs GazeController is null!");
            gazeController.OnReceive3dGaze += ReceiveEyeData;
        }

        void ReceiveEyeData(PupilLabs.GazeData data)
        {
            if (data.Confidence < 0.6f) { return; }
            gazeDirection = data.GazeDirection;
        }

        void Update()
        {
            if (GameplayReferences.HMD == null) { return; }

            Vector3 newPosition = transform.position;
            var worldDir = GameplayReferences.HMD.TransformDirection(gazeDirection);
            var ray = new Ray(GameplayReferences.HMDCameraComponent.transform.position, worldDir);
            newPosition = ray.GetPoint(Distance);

            transform.position = Vector3.Lerp(t.position, newPosition, Speed);
            transform.LookAt(GameplayReferences.HMD.position);
        }

        private void OnDisable()
        {
            gazeController.OnReceive3dGaze -= ReceiveEyeData;
        }

#elif CVR_FOVE
    void Start()
    {
        transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
    }

    void Update()
    {
        if (GameplayReferences.HMD == null){return;}

        transform.position = Vector3.Lerp(transform.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
        transform.LookAt(GameplayReferences.HMD.position);
    }

    public Vector3 GetLookDirection()
    {
        Fove.Unity.FoveInterface fi = GameplayReferences.FoveInstance;
        if (fi == null)
        {
            return GameplayReferences.HMD.forward;
        }
        var eyeRays = fi.GetGazeRays();
        Vector3 v = new Vector3(eyeRays.left.direction.x, eyeRays.left.direction.y, eyeRays.left.direction.z);
        return v.normalized;
    }
#elif CVR_TOBIIVR
    public Vector3 lastDirection = Vector3.forward;
    void Start()
    {
        transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
        if (GameplayReferences.HMD == null) { return; }
    }
    void Update()
    {
        if (GameplayReferences.HMD == null) { return; }

        transform.position = Vector3.Lerp(transform.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
        transform.LookAt(GameplayReferences.HMD.position);
    }

    public Vector3 GetLookDirection()
    {
        var provider = Tobii.XR.TobiiXR.Internal.Provider;

        if (provider == null)
        {
            return GameplayReferences.HMD.forward;
        }
        if (provider.EyeTrackingDataLocal.GazeRay.IsValid)
        {
            lastDirection = GameplayReferences.HMD.TransformDirection(provider.EyeTrackingDataLocal.GazeRay.Direction);
        }

        return lastDirection;
    }
#elif CVR_NEURABLE
    void Start()
    {
        transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
        if (GameplayReferences.HMD == null) { return; }
    }
    void Update()
    {
        if (GameplayReferences.HMD == null) { return; }

        transform.position = Vector3.Lerp(t.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
        transform.LookAt(GameplayReferences.HMD.position);
    }

    public Vector3 GetLookDirection()
    {
        return Neurable.Core.NeurableUser.Instance.NeurableCam.GazeRay().direction;
    }
#elif CVR_AH
    void Start()
    {
        transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
        if (GameplayReferences.HMD == null) { return; }
    }
    void Update()
    {
        if (GameplayReferences.HMD == null) { return; }
        transform.position = Vector3.Lerp(transform.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
        transform.LookAt(GameplayReferences.HMD.position);
    }
    public Vector3 GetLookDirection()
    {
        return Calibrator.Instance.GetGazeVector(filterType: FilterType.ExponentialMovingAverage);
    }
#elif CVR_SNAPDRAGON
        void Start()
        {
            transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
            if (GameplayReferences.HMD == null) { return; }
        }
        void Update()
        {
            if (GameplayReferences.HMD == null) { return; }
            transform.position = Vector3.Lerp(t.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
            transform.LookAt(GameplayReferences.HMD.position);
        }
        public Vector3 GetLookDirection()
        {
            return SvrManager.Instance.EyeDirection;
        }
#elif CVR_VIVEPROEYE

        ViveSR.anipal.Eye.SRanipal_Eye_Framework framework;
        ViveSR.anipal.Eye.SRanipal_Eye_Framework.SupportedEyeVersion version;
        void Start()
        {
            transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
            framework = ViveSR.anipal.Eye.SRanipal_Eye_Framework.Instance;
            if (framework != null)
            {
                version = framework.EnableEyeVersion;
            }
            if (GameplayReferences.HMD == null) { return; }
        }

        void Update()
        {
            if (GameplayReferences.HMD == null) { return; }

            transform.position = Vector3.Lerp(transform.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
            transform.LookAt(GameplayReferences.HMD.position);
        }

        Vector3 lastDir = Vector3.forward;
        public Vector3 GetLookDirection()
        {
            var ray = new Ray();
            if (version == ViveSR.anipal.Eye.SRanipal_Eye_Framework.SupportedEyeVersion.version1)
            {
                if (ViveSR.anipal.Eye.SRanipal_Eye.GetGazeRay(ViveSR.anipal.Eye.GazeIndex.COMBINE, out ray))
                {
                    lastDir = GameplayReferences.HMD.TransformDirection(ray.direction);
                }
            }
            else
            {
                if (ViveSR.anipal.Eye.SRanipal_Eye_v2.GetGazeRay(ViveSR.anipal.Eye.GazeIndex.COMBINE, out ray))
                {
                    lastDir = GameplayReferences.HMD.TransformDirection(ray.direction);
                }
            }
            return lastDir;
        }
#elif CVR_VARJO
        void Start()
        {
            transform.position = GameplayReferences.HMD.position + GetLookDirection() * Distance;
            if (GameplayReferences.HMD == null) { return; }
        }

        void Update()
        {
            if (GameplayReferences.HMD == null) { return; }

            transform.position = Vector3.Lerp(transform.position, GameplayReferences.HMD.position + GetLookDirection() * Distance, Speed);
            transform.LookAt(GameplayReferences.HMD.position);
        }

        Vector3 lastDir = Vector3.forward;
        public Vector3 GetLookDirection()
        {
            if (Varjo.VarjoPlugin.InitGaze())
            {
                var data = Varjo.VarjoPlugin.GetGaze();
                if (data.status != Varjo.VarjoPlugin.GazeStatus.INVALID)
                {
                    var ray = data.gaze;
                    lastDir = GameplayReferences.HMD.TransformDirection(new Vector3((float)ray.forward[0], (float)ray.forward[1], (float)ray.forward[2]));
                    return lastDir;
                }
            }
            return lastDir;
        }
#else
        public Vector3 GetLookDirection()
        {
            return GameplayReferences.HMD.forward;
        }
#endif
    }
}