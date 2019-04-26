﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//this should only contain the component to send dynamic data/engagements to the plugin


//manually send snapshots with builder pattern
//presets for controllers/level geometry/pooled enemies/grabable item
//example scripts for snow fortress blocks

/*level geo. no ticks, update transform on start*/
/*controllers. tick. update on start. never disabled. custom id*/
/*enemies. tick. update on start non-custom id. reused on enable*/
/*grabable item. custom id. update ticks. never disabled*/

//iterate through and write updates
namespace CognitiveVR
{
    [HelpURL("https://docs.cognitive3d.com/unity/dynamic-objects/")]
    [AddComponentMenu("Cognitive3D/Common/Dynamic Object")]
    public class DynamicObject : MonoBehaviour
    {
#if UNITY_EDITOR
        //stores instanceid. used to check if something in editor has changed
        public int editorInstanceId;
#endif

        public enum CommonDynamicMesh
        {
            ViveController,
            OculusTouchLeft,
            OculusTouchRight,
            ViveTracker,
            ExitPoll,
            LeapMotionHandLeft,
            LeapMotionHandRight,
            MicrosoftMixedRealityLeft,
            MicrosoftMixedRealityRight,
            VideoSphereLatitude,
            VideoSphereCubemap,
            SnapdragonVRController,
        }

        [HideInInspector]
        public Transform _t;

        public bool SnapshotOnEnable = true;
        public bool ContinuallyUpdateTransform = true;

        public float PositionThreshold = 0.001f;
        public Vector3 lastPosition;
        public float RotationThreshold = 0.1f;
        public Quaternion lastRotation;

        //original scale, set on enable
        //assuming that this is the scale used to export this mesh and that this should be divided by current scale to get 'relative scale from upload'
        //THIS IS ONLY USED IN SDK TO DETERMINE IF SCALE CHANGED
        private bool HasSetScale = false;
        public Vector3 StartingScale { get; private set; }

        public float ScaleThreshold = 0.1f;
        Vector3 lastRelativeScale = Vector3.one;

        public bool UseCustomId = true;
        public string CustomId = "";
        public bool ReleaseIdOnDestroy = false; //only release the id for reuse if not tracking gaze
        public bool ReleaseIdOnDisable = false; //only release the id for reuse if not tracking gaze

        public bool IsController = false;
        public string ControllerType;
        public bool IsRight = false;

        private DynamicObjectId viewerId;
        //used internally for scene explorer
        public DynamicObjectId ViewerId
        {
            get
            {
                if (viewerId == null)
                    GenerateDynamicObjectId();
                return viewerId;
            }
            set
            {
                viewerId = value;
            }
        }

        //the unique identifying string for this dynamic object
        public string Id
        {
            get
            {
                return ViewerId.Id;
            }
        }

        public bool UseCustomMesh = true;
        public CommonDynamicMesh CommonMesh;
        public string MeshName;

        public bool SyncWithPlayerUpdate = true;
        public float UpdateRate = 0.5f;
        private YieldInstruction updateTick;

        //video settings
        bool FlipVideo = false;
        public string ExternalVideoSource;
        float SendFrameTimeRemaining; //counts down to 0 during update. sends video time if it hasn't been sent lately
        float MaxSendFrameTime = 5;
        bool wasPlayingVideo = false;
        bool wasBufferingVideo = false;

        public bool TrackGaze = true;

        public bool RequiresManualEnable = false;

        //custom events with a uniqueid + dynamicid
        Dictionary<string, CustomEvent> EngagementsDict;

        //static variables
        private static int uniqueIdOffset = 1000;
        private static int currentUniqueId;
        //cleared between scenes so new snapshots will re-write to the manifest and get uploaded to the scene
        public static List<DynamicObjectId> ObjectIds = new List<DynamicObjectId>();

        ///don't recycle object ids between scenes - otherwise ids wont be written into new scene's manifest
        ///disconnect all the objectids from dynamics. they will make new objectids in the scene when they write a new snapshot
        public static void ClearObjectIds()
        {
            foreach (var v in ObjectIds)
            {
                if (v == null) { continue; }
                if (v.Target == null) { continue; }
                v.Target.ViewerId = null;
            }
            ObjectIds.Clear();
        }

        private static Queue<DynamicObjectSnapshot> NewSnapshotQueue = new Queue<DynamicObjectSnapshot>();
        private static Queue<DynamicObjectManifestEntry> NewObjectManifestQueue = new Queue<DynamicObjectManifestEntry>();

        private static int jsonpart = 1;

        public UnityEngine.Video.VideoPlayer VideoPlayer;
        bool IsVideoPlayer;

        bool registeredToEvents = false;

#if UNITY_EDITOR
        private void Reset()
        {
            //set name is not set otherwise
            if (UseCustomMesh && string.IsNullOrEmpty(MeshName))
            {
                MeshName = gameObject.name.ToLower().Replace(" ", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace("?", "_").Replace("*", "_").Replace("\"", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }

            //set custom id if not set otherwise
            if (UseCustomId && string.IsNullOrEmpty(CustomId))
            {
                string s = System.Guid.NewGuid().ToString();
                CustomId = "editor_" + s;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
        }
#endif

        /// <summary>
        /// called on enable and after scene load. registers to tick and records 'onenable' snapshot for new scene
        /// </summary>
        void OnEnable()
        {
            if (transform != null)
            {
                _t = transform;
            }
            else
            {
                Util.logWarning("Dynamic Object destroyed");
                return;
            }
            if (!HasSetScale)
            {
                HasSetScale = true;
                StartingScale = _t.lossyScale;
            }

            if (!Application.isPlaying) { return; }
            if (RequiresManualEnable)
            {
                return;
            }

            //set the 'custom mesh name' to be the lowercase of the common name
            if (!UseCustomMesh)
            {
                UseCustomMesh = true;
                MeshName = CommonMesh.ToString().ToLower();
            }

            if (!registeredToEvents)
            {
                registeredToEvents = true;
                CognitiveVR_Manager.LevelLoadedEvent += CognitiveVR_Manager_LevelLoadedEvent;

                if (VideoPlayer != null && !string.IsNullOrEmpty(ExternalVideoSource))
                {
                    IsVideoPlayer = true;
                    VideoPlayer.prepareCompleted += VideoPlayer_prepareCompleted;
                    VideoPlayer.loopPointReached += VideoPlayer_loopPointReached;

                    //TODO wait for first frame should set buffering to true for first snapshot
                }
            }

            if (string.IsNullOrEmpty(Core.TrackingSceneId))
            {
                return;
            }

            if (CognitiveVR_Manager.InitResponse == Error.None)
            {
                CognitiveVR_Manager_InitEvent(Error.None);
            }

            NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetEnabled(true);
            lastPosition = transform.position;
            lastRotation = transform.rotation;

            if (ContinuallyUpdateTransform || IsVideoPlayer)
            {
                if (SyncWithPlayerUpdate)
                {
                    CognitiveVR_Manager.TickEvent -= CognitiveVR_Manager_TickEvent;
                    CognitiveVR_Manager.TickEvent += CognitiveVR_Manager_TickEvent;
                }
                else
                {
                    StopAllCoroutines();
                    StartCoroutine(UpdateTick());
                }
            }
#if UNITY_EDITOR
            if (TrackGaze)
            {
                if (CognitiveVR_Preferences.S_DynamicObjectSearchInParent)
                {
                    if (GetComponentInChildren<Collider>() == null)
                    {
                        Debug.LogWarning("Tracking Gaze on Dynamic Object " + name + " requires a collider!", this);
                    }
                }
                else
                {
                    if (GetComponent<Collider>() == null)
                    {
                        Debug.LogWarning("Tracking Gaze on Dynamic Object " + name + " requires a collider!", this);
                    }
                }
            }
#endif
        }

        //post level loaded. also called when cognitive manager first initialized, to make sure onenable registers everything correctly
        private void CognitiveVR_Manager_LevelLoadedEvent()
        {
            OnEnable();
        }

        private void VideoPlayer_loopPointReached(UnityEngine.Video.VideoPlayer source)
        {
            SendVideoTime();

            if (VideoPlayer.isLooping)
            {
                //snapshot at end, then snapshot at beginning
                NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetProperty("videotime", 0);
                lastPosition = transform.position;
                lastRotation = transform.rotation;
            }
            else
            {
                NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetProperty("videoplay", false).SetProperty("videotime", (int)((VideoPlayer.frame / VideoPlayer.frameRate) * 1000));
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                wasPlayingVideo = false;
            }
        }

        private void VideoPlayer_prepareCompleted(UnityEngine.Video.VideoPlayer source)
        {
            //buffering complete?
            if (wasBufferingVideo)
            {
                SendVideoTime().SetProperty("videoisbuffer", false);
                wasBufferingVideo = false;
            }
        }

        private void CognitiveVR_Manager_InitEvent(Error initError)
        {
            CognitiveVR_Manager.InitEvent -= CognitiveVR_Manager_InitEvent;

            if (initError != Error.None)
            {
                StopAllCoroutines();
                return;
            }
        }

        /// <summary>
        /// used to manually enable dynamic object. useful for setting custom properties before first snapshot
        /// </summary>
        public void Init()
        {
            RequiresManualEnable = false;
            OnEnable();
        }

        //public so snapshot can begin this
        public IEnumerator UpdateTick()
        {
            updateTick = new WaitForSeconds(UpdateRate);

            while (true)
            {
                yield return updateTick;
                CheckUpdate(UpdateRate);
                if (IsVideoPlayer)
                    UpdateFrame(UpdateRate);
            }
        }

        //public so snapshot can tie cognitivevr_manager tick event to this. this is for syncing player tick and this tick
        public void CognitiveVR_Manager_TickEvent()
        {
            CheckUpdate(CognitiveVR_Preferences.S_SnapshotInterval);
            if (IsVideoPlayer)
                UpdateFrame(CognitiveVR_Preferences.S_SnapshotInterval);
        }

        void UpdateFrame(float timeSinceLastTick)
        {
            if (VideoPlayer.isPlaying)
            {
                SendFrameTimeRemaining -= timeSinceLastTick;
            }

            if (SendFrameTimeRemaining < 0)
            {
                SendVideoTime();
            }
        }

        /// <summary>
        /// makes a new snapshot and adds the video's current frame as a property. also sets the current transform of the object
        /// </summary>
        /// <returns>returns the new snapshot</returns>
        public DynamicObjectSnapshot SendVideoTime()
        {
            SendFrameTimeRemaining = MaxSendFrameTime;
            var snap = NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetProperty("videotime", (int)((VideoPlayer.frame / VideoPlayer.frameRate) * 1000));
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            return snap;
        }

        //puts outstanding snapshots (from last update) into json
        private static void CognitiveVR_Manager_Update()
        {
            if (string.IsNullOrEmpty(Core.TrackingSceneId))
            {
                if (NewSnapshotQueue.Count + NewObjectManifestQueue.Count > 0)
                {
                    CognitiveVR.Util.logError("Dynamic Object Update - sceneid is empty! do not send Dynamic Objects to sceneexplorer");
                    
                    while(NewSnapshotQueue.Count > 0)
                    {
                        NewSnapshotQueue.Dequeue().ReturnToPool();
                    }
                    NewObjectManifestQueue.Clear();
                }
                return;
            }

            //only need this because dynamic objects don't have a clear 'send' function
            //queue
            if ((NewObjectManifestQueue.Count + NewSnapshotQueue.Count) > CognitiveVR_Preferences.S_DynamicSnapshotCount)
            {

                bool withinMinTimer = lastSendTime + CognitiveVR_Preferences.Instance.DynamicSnapshotMinTimer > Time.realtimeSinceStartup;
                bool withinExtremeBatchSize = NewObjectManifestQueue.Count + NewSnapshotQueue.Count < CognitiveVR_Preferences.Instance.DynamicExtremeSnapshotCount;

                //within last send interval and less than extreme count
                if (withinMinTimer && withinExtremeBatchSize)
                {
                    return;
                }
                lastSendTime = Time.realtimeSinceStartup;
                CognitiveVR_Manager.Instance.StartCoroutine(Thread_StringThenSend(NewObjectManifestQueue, NewSnapshotQueue, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID));
            }
        }

        static float nextSendTime = 0;
        internal static IEnumerator AutomaticSendTimer()
        {
            nextSendTime = Time.realtimeSinceStartup + CognitiveVR_Preferences.Instance.DynamicSnapshotMaxTimer;
            while (true)
            {
                while (nextSendTime > Time.realtimeSinceStartup)
                {
                    yield return null;
                }
                //try to send!
                nextSendTime = Time.realtimeSinceStartup + CognitiveVR_Preferences.Instance.DynamicSnapshotMaxTimer;

                if (CognitiveVR_Preferences.Instance.EnableDevLogging)
                    Util.logDevelopment("check to automatically send dynamics");
                if (NewObjectManifestQueue.Count + NewSnapshotQueue.Count > 0)
                {

                    //don't bother checking min timer here

                    CognitiveVR_Manager.Instance.StartCoroutine(Thread_StringThenSend(NewObjectManifestQueue, NewSnapshotQueue, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID));
                }
            }
        }
        
        //writes manifest entry and object snapshot to string in threads, then passes value to send saved snapshots
        static IEnumerator Thread_StringThenSend(Queue<DynamicObjectManifestEntry> SendObjectManifest, Queue<DynamicObjectSnapshot> SendObjectSnapshots, CognitiveVR_Preferences.SceneSettings trackingSettings, string uniqueid, double sessiontimestamp, string sessionid)
        {
            //save and clear snapshots and manifest entries
            DynamicObjectManifestEntry[] tempObjectManifest = new DynamicObjectManifestEntry[SendObjectManifest.Count];
            SendObjectManifest.CopyTo(tempObjectManifest, 0);
            SendObjectManifest.Clear();

            //copy snapshots into temporary collection
            DynamicObjectSnapshot[] tempSnapshots = new DynamicObjectSnapshot[SendObjectSnapshots.Count];
            //SendObjectSnapshots.CopyTo(tempSnapshots, 0);
            //SendObjectSnapshots.Clear();

            int index=0;
            while (SendObjectSnapshots.Count > 0)
            {
                var oldsnapshot = SendObjectSnapshots.Dequeue();
                tempSnapshots[index] = oldsnapshot.Copy();
                index++;
                oldsnapshot.ReturnToPool();
            }
            
            //write manifest entries to list in thread
            List<string> manifestEntries = new List<string>(tempObjectManifest.Length);
            bool done = true;
            if (tempObjectManifest.Length > 0)
            {
                done = false;
                new System.Threading.Thread(() =>
                {
                    for (int i = 0; i < tempObjectManifest.Length; i++)
                    {
                        manifestEntries.Add(SetManifestEntry(tempObjectManifest[i]));
                    }
                    done = true;
                }).Start();

                while (!done)
                {
                    yield return null;
                }
            }

            //write snapshots to list in thread
            List<string> snapshots = new List<string>(tempSnapshots.Length);
            if (tempSnapshots.Length > 0)
            {
                done = false;
                new System.Threading.Thread(() =>
                {
                    for (int i = 0; i < tempSnapshots.Length; i++)
                    {
                        snapshots.Add(SetSnapshot(tempSnapshots[i]));
                    }
                    done = true;
                }).Start();

                while (!done)
                {
                    yield return null;
                }
            }

            for(int i = 0;i< tempSnapshots.Length;i++)
            {
                tempSnapshots[i].ReturnToPool();
            }
            
            SendSavedSnapshots(manifestEntries, snapshots, trackingSettings, uniqueid, sessiontimestamp, sessionid);
        }

        /// <summary>
        /// send a snapshot of the position and rotation if the object has moved beyond its threshold
        /// </summary>
        public void CheckUpdate(float timeSinceLastCheck)
        {
            if (!Core.IsInitialized) { return; }
            if (string.IsNullOrEmpty(Core.TrackingSceneId)) { return; }

            var pos = _t.position;
            var rot = _t.rotation;
            var relativescale = new Vector3(_t.lossyScale.x / StartingScale.x, _t.lossyScale.y / StartingScale.y, _t.lossyScale.z / StartingScale.z);

            Vector3 heading;
            heading.x = pos.x - lastPosition.x;
            heading.y = pos.y - lastPosition.y;
            heading.z = pos.z - lastPosition.z;

            var distanceSquared = heading.x * heading.x + heading.y * heading.y + heading.z * heading.z;

            bool doWrite = false;
            bool writeScale = false;
            if (distanceSquared > PositionThreshold * PositionThreshold)
            {
                doWrite = true;
            }
            if (!doWrite)
            {
                float f = Quaternion.Dot(lastRotation, rot);
                if (Mathf.Acos(Mathf.Min(Mathf.Abs(f), 1f)) * 114.59156f > RotationThreshold)
                {
                    doWrite = true;
                }
            }
            if (Vector3.SqrMagnitude(relativescale - lastRelativeScale) > ScaleThreshold * ScaleThreshold)
            {
                writeScale = true;
            }

            DynamicObjectSnapshot snapshot = null;
            if (doWrite || writeScale)
            {
                snapshot = NewSnapshot();
                snapshot.Position[0] = pos.x;
                snapshot.Position[1] = pos.y;
                snapshot.Position[2] = pos.z;

                snapshot.Rotation[0] = rot.x;
                snapshot.Rotation[1] = rot.y;
                snapshot.Rotation[2] = rot.z;
                snapshot.Rotation[3] = rot.w;
                lastPosition = pos;
                lastRotation = rot;
                if (writeScale)
                {
                    snapshot.DirtyScale = true;
                    snapshot.Scale[0] = relativescale.x;
                    snapshot.Scale[1] = relativescale.y;
                    snapshot.Scale[2] = relativescale.z;
                    lastRelativeScale = relativescale;
                }
            }
        }

        private static bool HasRegisteredAnyDynamics = false;

        public DynamicObjectSnapshot NewSnapshot()
        {
            //new objectId and manifest entry (if required)
            if (ViewerId == null)
            {
                GenerateDynamicObjectId();
            }

            //create snapshot for this object
            var snapshot = DynamicObjectSnapshot.GetSnapshot(Id);

            if (IsVideoPlayer)
            {
                if (!VideoPlayer.isPrepared)
                {
                    snapshot.SetProperty("videoisbuffer", true);
                    wasBufferingVideo = true;
                }
            }
            NewSnapshotQueue.Enqueue(snapshot);

            return snapshot;
        }

        //this should probably be static
        void GenerateDynamicObjectId()
        {
            if (!UseCustomId)
            {
                DynamicObjectId recycledId = ObjectIds.Find(x => !x.Used && x.MeshName == MeshName);

                //do not allow video players to recycle ids - could point to different urls, making the manifest invalid
                //could allow sharing objectids if the url target is the same, but that's not stored in the objectid - need to read objectid from manifest

                if (recycledId != null && !IsVideoPlayer)
                {
                    viewerId = recycledId;
                    viewerId.Used = true;
                    //id is already on manifest
                }
                else
                {
                    viewerId = GetUniqueID(MeshName, this);
                    var manifestEntry = new DynamicObjectManifestEntry(viewerId.Id, gameObject.name, MeshName);

                    if (IsController)
                    {
                        manifestEntry.isController = true;
                        manifestEntry.controllerType = ControllerType;
                        string controllerName = "left";

                        if (IsRight)
                            controllerName = "right";
                        
                        if (manifestEntry.Properties == null)
                        {
                            manifestEntry.Properties = new Dictionary<string, object>() { { "controller", controllerName } };
                        }
                        else
                        {
                            manifestEntry.Properties.Add("controller", controllerName);
                        }
                    }

                    if (!string.IsNullOrEmpty(ExternalVideoSource))
                    {
                        manifestEntry.videoURL = ExternalVideoSource;
                        manifestEntry.videoFlipped = FlipVideo;
                    }

                    ObjectIds.Add(viewerId);
                    NewObjectManifestQueue.Enqueue(manifestEntry);

                    if ((NewObjectManifestQueue.Count + NewSnapshotQueue.Count) > CognitiveVR_Preferences.S_DynamicSnapshotCount)
                    {
                        bool withinMinTimer = lastSendTime + CognitiveVR_Preferences.Instance.DynamicSnapshotMinTimer > Time.realtimeSinceStartup;
                        bool withinExtremeBatchSize = NewObjectManifestQueue.Count + NewSnapshotQueue.Count < CognitiveVR_Preferences.Instance.DynamicExtremeSnapshotCount;

                        //within last send interval and less than extreme count
                        if (withinMinTimer && withinExtremeBatchSize)
                        {
                            return;
                        }
                        lastSendTime = Time.realtimeSinceStartup;
                        CognitiveVR_Manager.Instance.StartCoroutine(Thread_StringThenSend(NewObjectManifestQueue, NewSnapshotQueue, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID));
                    }
                }
            }
            else
            {
                viewerId = new DynamicObjectId(CustomId, MeshName, this);
                var manifestEntry = new DynamicObjectManifestEntry(viewerId.Id, gameObject.name, MeshName);

                if (IsController)
                {
                    manifestEntry.isController = true;
                    manifestEntry.controllerType = ControllerType;
                    string controllerName = "left";

                    if (IsRight)
                        controllerName = "right";

                    if (manifestEntry.Properties == null)
                    {
                        manifestEntry.Properties = new Dictionary<string, object>() { { "controller", controllerName } };
                    }
                    else
                    {
                        manifestEntry.Properties.Add("controller", controllerName);
                    }
                }

                if (!string.IsNullOrEmpty(ExternalVideoSource))
                {
                    manifestEntry.videoURL = ExternalVideoSource;
                    manifestEntry.videoFlipped = FlipVideo;
                    IsVideoPlayer = true;
                }
                
                ObjectIds.Add(viewerId);
                NewObjectManifestQueue.Enqueue(manifestEntry);
                if ((NewObjectManifestQueue.Count + NewSnapshotQueue.Count) > CognitiveVR_Preferences.S_DynamicSnapshotCount)
                {
                    bool withinMinTimer = lastSendTime + CognitiveVR_Preferences.Instance.DynamicSnapshotMinTimer > Time.realtimeSinceStartup;
                    bool withinExtremeBatchSize = NewObjectManifestQueue.Count + NewSnapshotQueue.Count < CognitiveVR_Preferences.Instance.DynamicExtremeSnapshotCount;

                    //within last send interval and less than extreme count
                    if (withinMinTimer && withinExtremeBatchSize)
                    {
                        return;
                    }
                    lastSendTime = Time.realtimeSinceStartup;
                    CognitiveVR_Manager.Instance.StartCoroutine(Thread_StringThenSend(NewObjectManifestQueue, NewSnapshotQueue, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID));
                }
            }

            if (IsVideoPlayer)
            {
                CognitiveVR_Manager.UpdateEvent -= VideoPlayer_Update;
                CognitiveVR_Manager.UpdateEvent += VideoPlayer_Update;
            }
            if (!HasRegisteredAnyDynamics)
            {
                HasRegisteredAnyDynamics = true;
                CognitiveVR_Manager.UpdateEvent += CognitiveVR_Manager_Update;
                Core.OnSendData += Core_OnSendData;
                CognitiveVR_Manager.Instance.StartCoroutine(AutomaticSendTimer());

                for (int i = 0; i < CognitiveVR_Preferences.Instance.DynamicExtremeSnapshotCount; i++)
                {
                    DynamicObjectSnapshot.SnapshotPool.Enqueue(new DynamicObjectSnapshot());
                }
            }
        }

        //update on instance of dynamic game obejct
        //only used when dynamic object is written to manifest as a video player
        private void VideoPlayer_Update()
        {
            if (VideoPlayer.isPlaying != wasPlayingVideo)
            {
                if (VideoPlayer.frameRate == 0)
                {
                    //hasn't actually loaded anything yet
                    return;
                }

                SendVideoTime().SetProperty("videoplay", VideoPlayer.isPlaying);
                wasPlayingVideo = VideoPlayer.isPlaying;
            }
        }

        public void UpdateLastPositions()
        {
            lastPosition = _t.position;
            lastRotation = _t.rotation;
        }

        public void UpdateLastPositions(Vector3 pos, Quaternion rot)
        {
            lastPosition = pos;
            lastRotation = rot;
        }

        /// <summary>
        /// used to generate a new unique dynamic object ids at runtime
        /// </summary>
        /// <param name="MeshName"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static DynamicObjectId GetUniqueID(string MeshName, DynamicObject target)
        {
            DynamicObjectId usedObjectId = null;
            while (true) //keep going through objectid list until a new one is reached
            {
                currentUniqueId++;
                usedObjectId = ObjectIds.Find(delegate (DynamicObjectId obj)
                {
                    return obj.Id == "runtime_" + (currentUniqueId + uniqueIdOffset).ToString();
                });
                if (usedObjectId == null)
                {
                    break; //break once we have a currentuniqueid that isn't in objectid list
                }
            }
            return new DynamicObjectId("runtime_" + (currentUniqueId + uniqueIdOffset).ToString(), MeshName, target);
        }

        //the last realtime dynamic data was successfully sent
        static float lastSendTime = -60;

        static void Core_OnSendData()
        {
            List<string> savedDynamicManifest = new List<string>();
            List<string> savedDynamicSnapshots = new List<string>();

            //write dynamic object snapshots to strings
            DynamicObjectSnapshot snap = null;
            while (NewSnapshotQueue.Count > 0)
            {
                snap = NewSnapshotQueue.Dequeue();
                if (snap == null)
                {
                    Util.logWarning("snapshot immediate is null");
                    continue;
                }
                savedDynamicSnapshots.Add(SetSnapshot(snap));
                snap.ReturnToPool();
                if (savedDynamicSnapshots.Count + savedDynamicManifest.Count >= CognitiveVR_Preferences.S_DynamicSnapshotCount)
                {
                    SendSavedSnapshots(savedDynamicManifest, savedDynamicSnapshots, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID);
                    savedDynamicManifest.Clear();
                    savedDynamicSnapshots.Clear();
                }
            }

            //write dynamic manifest entries to strings
            DynamicObjectManifestEntry entry = null;
            while (NewObjectManifestQueue.Count > 0)
            {
                entry = NewObjectManifestQueue.Dequeue();
                if (entry == null) { continue; }
                savedDynamicManifest.Add(SetManifestEntry(entry));
                if (savedDynamicSnapshots.Count + savedDynamicManifest.Count >= CognitiveVR_Preferences.S_DynamicSnapshotCount)
                {
                    SendSavedSnapshots(savedDynamicManifest, savedDynamicSnapshots, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID);
                    savedDynamicManifest.Clear();
                    savedDynamicSnapshots.Clear();
                }
            }

            //send any outstanding manifest entries or snapshots
            SendSavedSnapshots(savedDynamicManifest, savedDynamicSnapshots, Core.TrackingScene, Core.UniqueID, Core.SessionTimeStamp, Core.SessionID);
        }

        //string entries and snapshots are either written in thread or synchronously
        public static void SendSavedSnapshots(List<string> stringEntries, List<string> stringSnapshots, CognitiveVR_Preferences.SceneSettings trackingsettings, string uniqueid, double sessiontimestamp, string sessionid)
        {
            if (stringEntries.Count == 0 && stringSnapshots.Count == 0) { return; }

            //TODO should hold until extreme batch size reached
            if (string.IsNullOrEmpty(Core.TrackingSceneId))
            {
                CognitiveVR.Util.logError("SceneId is empty. Do not send Dynamic Objects to SceneExplorer");

                while (NewSnapshotQueue.Count > 0)
                {
                    NewSnapshotQueue.Dequeue().ReturnToPool();
                }
                NewObjectManifestQueue.Clear();
                return;
            }

            System.Text.StringBuilder sendSnapshotBuilder = new System.Text.StringBuilder(256*CognitiveVR_Preferences.Instance.DynamicExtremeSnapshotCount + 8000);

            //lastSendTime = Time.realtimeSinceStartup;

            sendSnapshotBuilder.Append("{");

            //header
            JsonUtil.SetString("userid", uniqueid, sendSnapshotBuilder);
            sendSnapshotBuilder.Append(",");

            if (!string.IsNullOrEmpty(Core.LobbyId))
            {
                JsonUtil.SetString("lobbyId", Core.LobbyId, sendSnapshotBuilder);
                sendSnapshotBuilder.Append(",");
            }

            JsonUtil.SetDouble("timestamp", (int)sessiontimestamp, sendSnapshotBuilder);
            sendSnapshotBuilder.Append(",");
            JsonUtil.SetString("sessionid", sessionid, sendSnapshotBuilder);
            sendSnapshotBuilder.Append(",");
            JsonUtil.SetInt("part", jsonpart, sendSnapshotBuilder);
            sendSnapshotBuilder.Append(",");
            jsonpart++;
            JsonUtil.SetString("formatversion", "1.0", sendSnapshotBuilder);
            sendSnapshotBuilder.Append(",");

            //format all the savedmanifest entries

            if (stringEntries.Count > 0)
            {
                //manifest
                sendSnapshotBuilder.Append("\"manifest\":{");
                for (int i = 0; i < stringEntries.Count; i++)
                {
                    sendSnapshotBuilder.Append(stringEntries[i]);
                    sendSnapshotBuilder.Append(",");
                }
                sendSnapshotBuilder.Remove(sendSnapshotBuilder.Length - 1, 1);
                sendSnapshotBuilder.Append("},");
            }

            if (stringSnapshots.Count > 0)
            {
                //snapshots
                sendSnapshotBuilder.Append("\"data\":[");
                for (int i = 0; i < stringSnapshots.Count; i++)
                {
                    sendSnapshotBuilder.Append(stringSnapshots[i]);
                    sendSnapshotBuilder.Append(",");
                }
                sendSnapshotBuilder.Remove(sendSnapshotBuilder.Length - 1, 1);
                sendSnapshotBuilder.Append("]");
            }
            else
            {
                sendSnapshotBuilder.Remove(sendSnapshotBuilder.Length - 1, 1); //remove last comma from manifest array
            }

            sendSnapshotBuilder.Append("}");

            string url = CognitiveStatics.POSTDYNAMICDATA(trackingsettings.SceneId, trackingsettings.VersionNumber);

            string content = sendSnapshotBuilder.ToString();
            
            CognitiveVR.NetworkManager.Post(url, content);
        }

        static string SetManifestEntry(DynamicObjectManifestEntry entry)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(256);

            builder.Append("\"");
            builder.Append(entry.Id);
            builder.Append("\":{");


            if (!string.IsNullOrEmpty(entry.Name))
            {
                JsonUtil.SetString("name", entry.Name, builder);
                builder.Append(",");
            }
            JsonUtil.SetString("mesh", entry.MeshName, builder);
            builder.Append(",");
            JsonUtil.SetString("fileType", DynamicObjectManifestEntry.FileType, builder);

            if (!string.IsNullOrEmpty(entry.videoURL))
            {
                builder.Append(",");
                JsonUtil.SetString("externalVideoSource", entry.videoURL, builder);
                builder.Append(",");
                JsonUtil.SetObject("flipVideo", entry.videoFlipped, builder);
            }

            if (entry.isController)
            {
                builder.Append(",");
                JsonUtil.SetString("controllerType", entry.controllerType, builder);
            }

            if (entry.Properties != null && entry.Properties.Keys.Count > 0)
            {
                builder.Append(",");
                builder.Append("\"properties\":[");
                foreach (var v in entry.Properties)
                {
                    builder.Append("{");
                    if (v.Value.GetType() == typeof(string))
                    {
                        JsonUtil.SetString(v.Key, (string)v.Value, builder);
                    }
                    else
                    {
                        JsonUtil.SetObject(v.Key, v.Value, builder);
                    }
                    builder.Append("},");
                }
                builder.Remove(builder.Length - 1, 1); //remove last comma
                builder.Append("]"); //close properties object
            }

            builder.Append("}"); //close manifest entry

            return builder.ToString();
        }

        static string SetSnapshot(DynamicObjectSnapshot snap)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(256);
            builder.Append("{");

            JsonUtil.SetString("id", snap.Id, builder);
            builder.Append(",");
            JsonUtil.SetDouble("time", snap.Timestamp, builder);
            builder.Append(",");
            JsonUtil.SetVector("p", snap.Position, builder);
            builder.Append(",");
            JsonUtil.SetQuat("r", snap.Rotation, builder);
            if (snap.DirtyScale)
            {
                builder.Append(",");
                JsonUtil.SetVector("s", snap.Scale, builder);
            }


            if (snap.Properties != null && snap.Properties.Keys.Count > 0)
            {
                builder.Append(",");
                builder.Append("\"properties\":[");
                builder.Append("{");
                foreach (var v in snap.Properties)
                {
                    if (v.Value.GetType() == typeof(string))
                    {
                        JsonUtil.SetString(v.Key, (string)v.Value, builder);
                    }
                    else
                    {
                        JsonUtil.SetObject(v.Key, v.Value, builder);
                    }
                    builder.Append(",");
                }
                builder.Remove(builder.Length - 1, 1); //remove last comma
                builder.Append("}");
                builder.Append("]"); //close properties object
            }

            if (snap.Buttons != null)
            {
                if (snap.Buttons.Count > 0)
                {
                    builder.Append(",");
                    builder.Append("\"buttons\":{");
                    foreach (var button in snap.Buttons)
                    {
                        builder.Append("\"");
                        builder.Append(button.Key);
                        builder.Append("\":{");
                        builder.Append("\"buttonPercent\":");
                        builder.Append(button.Value.ButtonPercent);
                        if (button.Value.IncludeXY)
                        {
                            builder.Append(",\"x\":");
                            builder.Append(button.Value.X.ToString("0.000"));
                            builder.Append(",\"y\":");
                            builder.Append(button.Value.Y.ToString("0.000"));
                        }
                        builder.Append("},");
                    }
                    builder.Remove(builder.Length - 1, 1); //remove last comma
                    builder.Append("}");
                }
            }

            builder.Append("}"); //close object snapshot

            return builder.ToString();
        }

        void OnDisable()
        {
            if (CognitiveVR_Manager.IsQuitting) { return; }
            if (!Application.isPlaying) { return; }
            CognitiveVR_Manager.TickEvent -= CognitiveVR_Manager_TickEvent;
            CognitiveVR_Manager.InitEvent -= CognitiveVR_Manager_InitEvent;
            CognitiveVR_Manager.LevelLoadedEvent -= CognitiveVR_Manager_LevelLoadedEvent;
            if (IsVideoPlayer)
            {
                VideoPlayer.prepareCompleted -= VideoPlayer_prepareCompleted;
                CognitiveVR_Manager.UpdateEvent -= VideoPlayer_Update;
                VideoPlayer.loopPointReached -= VideoPlayer_loopPointReached;
            }
            registeredToEvents = false;
            if (string.IsNullOrEmpty(Core.TrackingSceneId)) { return; }

            if (EngagementsDict != null)
            {
                foreach (var engagement in EngagementsDict)
                { engagement.Value.Send(transform.position); }
                EngagementsDict = null;
            }

            if (!ReleaseIdOnDisable)
            {
                //don't release id to be used again. makes sure tracked gaze on this will be unique
                NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetEnabled(false);
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                ViewerId = null;
                return;
            }
            if (CognitiveVR_Manager.Instance != null)
            {
                NewSnapshot().UpdateTransform(transform.position, transform.rotation).SetEnabled(false);
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                var foundId = DynamicObject.ObjectIds.Find(x => x.Id == this.Id);

                if (foundId != null)
                {
                    foundId.Used = false;
                }
                ViewerId = null;
            }
        }

        //destroyed, scene unloaded or quit. also called when disabled then destroyed
        void OnDestroy()
        {
            if (CognitiveVR_Manager.IsQuitting) { return; }
            if (!Application.isPlaying) { return; }
            CognitiveVR_Manager.TickEvent -= CognitiveVR_Manager_TickEvent;
            CognitiveVR_Manager.InitEvent -= CognitiveVR_Manager_InitEvent;
            CognitiveVR_Manager.LevelLoadedEvent -= CognitiveVR_Manager_LevelLoadedEvent;
            if (string.IsNullOrEmpty(Core.TrackingSceneId)) { return; }

            if (EngagementsDict != null)
            {
                foreach (var engagement in EngagementsDict)
                { engagement.Value.Send(transform.position); }
                EngagementsDict = null;
            }

            if (IsVideoPlayer)
            {
                VideoPlayer.prepareCompleted -= VideoPlayer_prepareCompleted;
                CognitiveVR_Manager.UpdateEvent -= VideoPlayer_Update;
                VideoPlayer.loopPointReached -= VideoPlayer_loopPointReached;
            }
            if (!ReleaseIdOnDestroy)
            {
                //NewSnapshot().SetEnabled(false); //already has a enabled=false snapshot from OnDisable
                return;
            }
            if (CognitiveVR_Manager.Instance != null && viewerId != null) //creates another snapshot to destroy an already probably disabled thing
            {
                NewSnapshot().UpdateTransform(transform.position,transform.rotation);
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                var foundId = DynamicObject.ObjectIds.Find(x => x.Id == this.Id);

                if (foundId != null)
                {
                    foundId.Used = false;
                }
                ViewerId = null;
            }
        }

#if UNITY_EDITOR
        public bool HasCollider()
        {
            if (TrackGaze)
            {
                if (CognitiveVR_Preferences.Instance.DynamicObjectSearchInParent)
                {
                    var collider = GetComponentInChildren<Collider>();
                    if (collider == null)
                    {
                        return false;
                    }
                    return true;
                }
                else
                {
                    var collider = GetComponent<Collider>();
                    if (collider == null)
                    {
                        return false;
                    }
                    return true;
                }
            }
            return true;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.right);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.up);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward);
        }



        /// <summary>
        /// begin an engagement on this dynamic object with a name 'engagementName'. if multiple engagements with the same name may be active at once on this dynamic, uniqueEngagementId should be set
        /// </summary>
        /// <param name="engagementName"></param>
        /// <param name="uniqueEngagementId"></param>
        public void BeginEngagement(string engagementName = "default", string uniqueEngagementId = null, Dictionary<string,object> properties = null)
        {
            if (EngagementsDict == null) EngagementsDict = new Dictionary<string, CustomEvent>();

            if (uniqueEngagementId == null)
            {
                CustomEvent ce = new CustomEvent(engagementName).SetProperties(properties).SetDynamicObject(Id);
                if (!EngagementsDict.ContainsKey(engagementName))
                {
                    EngagementsDict.Add(engagementName, ce);
                }
                else
                {
                    //send old engagement, record this new one
                    EngagementsDict[engagementName].Send(transform.position);
                    EngagementsDict[engagementName] = ce;
                }
            }
            else
            {
                CustomEvent ce = new CustomEvent(engagementName).SetProperties(properties).SetDynamicObject(Id);
                string key = uniqueEngagementId + Id;
                if (!EngagementsDict.ContainsKey(key))
                    EngagementsDict.Add(key,ce);
                else
                {
                    //send existing engagement and start a new one. this uniqueEngagementId isn't very unique
                    EngagementsDict[key].Send(transform.position);
                    EngagementsDict[key] = ce;
                }
            }
        }

        /// <summary>
        /// ends an engagement on this dynamic object with the matchign uniqueEngagementId. if this is not set, ends an engagement with a name 'engagementName'
        /// </summary>
        /// <param name="engagementName"></param>
        /// <param name="uniqueEngagementId"></param>
        public void EndEngagement(string engagementName = "default", string uniqueEngagementId = null, Dictionary<string, object> properties = null)
        {
            if (EngagementsDict == null) EngagementsDict = new Dictionary<string, CustomEvent>();

            if (uniqueEngagementId == null)
            {
                CustomEvent ce = null;
                if (EngagementsDict.TryGetValue(engagementName, out ce))
                {
                    ce.SetProperties(properties).Send(transform.position);
                    EngagementsDict.Remove(engagementName);
                }
                else
                {
                    //create and send immediately
                    new CustomEvent(engagementName).SetProperties(properties).SetDynamicObject(Id).Send(transform.position);
                }
            }
            else
            {
                CustomEvent ce = null;
                string key = uniqueEngagementId + Id;
                if (EngagementsDict.TryGetValue(key, out ce))
                {
                    ce.SetProperties(properties).Send(transform.position);
                    EngagementsDict.Remove(key);
                }
                else
                {
                    //create and send immediately
                    new CustomEvent(engagementName).SetProperties(properties).SetDynamicObject(Id).Send(transform.position);
                }
            }
        }
    }

    public class DynamicObjectSnapshot
    {
        public static Queue<DynamicObjectSnapshot> SnapshotPool = new Queue<DynamicObjectSnapshot>();

        public DynamicObjectSnapshot Copy()
        {
            var dyn = GetSnapshot(Id);
            dyn.Timestamp = Timestamp;
            dyn.Id = Id;
            dyn.Position = Position;
            dyn.Rotation = Rotation;
            dyn.DirtyScale = DirtyScale;
            dyn.Scale = Scale;

            if (Buttons != null)
            {
                dyn.Buttons = new Dictionary<string, ButtonState>(Buttons.Count);
                foreach(var v in Buttons)
                {
                    dyn.Buttons.Add(v.Key, new ButtonState(v.Value));
                }
            }
            if (Properties != null)
            {
                dyn.Properties = new Dictionary<string, object>(Properties.Count);
                foreach (var v in Properties)
                {
                    dyn.Properties.Add(v.Key, v.Value); //as long as the property value is a value type, everything should be fine
                }
            }
            return dyn;
        }

        public void ReturnToPool()
        {
            Properties = null;
            Buttons = null;
            Position = new float[3] { 0, 0, 0 };
            Scale = new float[3] { 0, 0, 0 };
            DirtyScale = false;
            Rotation = new float[4] { 0, 0, 0, 1 };
            SnapshotPool.Enqueue(this);
        }

        public static DynamicObjectSnapshot GetSnapshot(string id)
        {
            if (SnapshotPool.Count > 0)
            {
                DynamicObjectSnapshot dos = SnapshotPool.Dequeue();
                if (dos == null)
                {
                    dos = new DynamicObjectSnapshot();
                }
                dos.Id = id;
                dos.Timestamp = Util.Timestamp(CognitiveVR_Manager.FrameCount);
                return dos;
            }
            else
            {
                var dos = new DynamicObjectSnapshot();
                dos.Id = id;
                dos.Timestamp = Util.Timestamp(CognitiveVR_Manager.FrameCount);
                return dos;
            }
        }

        public string Id;
        public Dictionary<string, object> Properties;
        public Dictionary<string, CognitiveVR.ButtonState> Buttons;
        public float[] Position = new float[3] { 0, 0, 0 };
        public float[] Rotation = new float[4] { 0, 0, 0, 1 };
        public bool DirtyScale = false;
        public float[] Scale = new float[3] { 1, 1, 1 };
        public double Timestamp;

        public DynamicObjectSnapshot(DynamicObject dynamic)
        {
            Id = dynamic.Id;
            Timestamp = Util.Timestamp(CognitiveVR_Manager.FrameCount);
        }

        public DynamicObjectSnapshot()
        {
            //empty. only used to fill the pool
        }

        private DynamicObjectSnapshot(DynamicObject dynamic, Vector3 pos, Quaternion rot, Dictionary<string, object> props = null)
        {
            Id = dynamic.Id;
            Properties = props;

            Position[0] = pos.x;
            Position[1] = pos.y;
            Position[2] = pos.z;

            Rotation[0] = rot.x;
            Rotation[1] = rot.y;
            Rotation[2] = rot.z;
            Rotation[3] = rot.w;

            Timestamp = Util.Timestamp(CognitiveVR_Manager.FrameCount);
        }

        private DynamicObjectSnapshot(DynamicObject dynamic, float[] pos, float[] rot, Dictionary<string, object> props = null)
        {
            Id = dynamic.Id;
            Properties = props;
            Position = pos;

            Rotation = rot;
            Timestamp = Util.Timestamp(CognitiveVR_Manager.FrameCount);
        }

        /// <summary>
        /// Add the position and rotation to the snapshot, even if the dynamic object hasn't moved beyond its threshold
        /// </summary>
        public DynamicObjectSnapshot UpdateTransform(Vector3 pos, Quaternion rot)
        {
            Position[0] = pos.x;
            Position[1] = pos.y;
            Position[2] = pos.z;

            Rotation[0] = rot.x;
            Rotation[1] = rot.y;
            Rotation[2] = rot.z;
            Rotation[3] = rot.w;

            return this;
        }

        /// <summary>
        /// Set various properties on the snapshot. Currently unused
        /// </summary>
        /// <param name="dict"></param>
        public DynamicObjectSnapshot SetProperties(Dictionary<string, object> dict)
        {
            Properties = dict;
            return this;
        }

        public DynamicObjectSnapshot SetProperty(string key, object value)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<string, object>();
            }

            if (Properties.ContainsKey(key))
            {
                Properties[key] = value;
            }
            else
            {
                Properties.Add(key, value);
            }
            return this;
        }

        /// <summary>
        /// Append various properties on the snapshot without overwriting previous properties. Currently unused
        /// </summary>
        /// <param name="dict"></param>
        public DynamicObjectSnapshot AppendProperties(Dictionary<string, object> dict)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<string, object>();
            }
            foreach (var v in dict)
            {
                Properties[v.Key] = v.Value;
            }
            return this;
        }

        /// <summary>
        /// Hide or show the dynamic object on SceneExplorer. This is happens automatically when you create, disable or destroy a gameobject
        /// </summary>
        /// <param name="enable"></param>
        public DynamicObjectSnapshot SetEnabled(bool enable)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<string, object>();
            }
            Properties["enabled"] = enable;
            return this;
        }
    }

    /// <summary>
    /// <para>holds info about which ids are used and what meshes they are held by</para> 
    /// <para>used to 'release' unique ids so meshes can be pooled in scene explorer</para> 
    /// </summary>
    public class DynamicObjectId
    {
        public string Id;
        public bool Used = true;
        public string MeshName;
        public DynamicObject Target;

        public DynamicObjectId(string id, string meshName, DynamicObject target)
        {
            this.Id = id;
            this.MeshName = meshName;
            Target = target;
        }
    }

    public class DynamicObjectManifestEntry
    {
        public static string FileType = "gltf";

        public string Id;
        public string Name;
        public string MeshName;
        public Dictionary<string, object> Properties;
        public string videoURL;
        public bool videoFlipped;
        public bool isController;
        public string controllerType;

        public DynamicObjectManifestEntry(string id, string name, string meshName)
        {
            this.Id = id;
            this.Name = name;
            this.MeshName = meshName;
        }

        public DynamicObjectManifestEntry(string id, string name, string meshName, Dictionary<string, object> props)
        {
            this.Id = id;
            this.Name = name;
            this.MeshName = meshName;
            this.Properties = props;
        }
    }
}