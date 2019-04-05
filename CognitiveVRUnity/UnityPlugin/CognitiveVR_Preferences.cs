﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace CognitiveVR
{
    public class CognitiveVR_Preferences : ScriptableObject
    {
        static bool IsSet = false;
        static CognitiveVR_Preferences instance;
        public static CognitiveVR_Preferences Instance
        {
            get
            {
                if (IsSet)
                    return instance;

                if (instance == null)
                {
                    instance = Resources.Load<CognitiveVR_Preferences>("CognitiveVR_Preferences");
                    if (instance == null)
                    {
                        Debug.LogWarning("Could not find CognitiveVR_Preferences in Resources. Settings will be incorrect!");
                        instance = CreateInstance<CognitiveVR_Preferences>();
                    }
                    IsSet = true;
                    S_SnapshotInterval = instance.SnapshotInterval;
                    S_GazeSnapshotCount = instance.GazeSnapshotCount;
                    S_DynamicSnapshotCount = instance.DynamicSnapshotCount;
                    S_DynamicObjectSearchInParent = instance.DynamicObjectSearchInParent;
                    S_TransactionSnapshotCount = instance.TransactionSnapshotCount;
                    S_SensorSnapshotCount = instance.SensorSnapshotCount;
                }
                return instance;
            }
        }

        public static float S_SnapshotInterval;
        public static int S_GazeSnapshotCount;
        public static int S_DynamicSnapshotCount;
        public static int S_TransactionSnapshotCount;
        public static int S_SensorSnapshotCount;

        public static bool S_DynamicObjectSearchInParent;

        public static void SetLobbyId(string lobbyId)
        {
            LobbyId = lobbyId;
        }
        public static string LobbyId { get; private set; }

        public string Protocol = "https";
        public string Gateway = "data.cognitive3d.com";
        public string Dashboard = "app.cognitive3d.com";
        public string Viewer = "sceneexplorer.com/scene/";
        public string Documentation = "docs.cognitive3d.com";

        public GazeType GazeType = GazeType.Command;
        //0 is multipass, 1 is single pass, 2 is singlepass instanced
        public int RenderPassType;

        public bool IsApplicationKeyValid
        {
            get
            {
                return !string.IsNullOrEmpty(ApplicationKey);
            }
        }

        [UnityEngine.Serialization.FormerlySerializedAs("APIKey")]
        public string ApplicationKey;

        public bool EnableLogging = true;
        public bool EnableDevLogging = false;

        [Header("Player Tracking")]
        //player tracking

        float snapshotInterval = 0.1f;
        public float SnapshotInterval
        {
            get
            {
                return snapshotInterval;
            }
            set
            {
                //--- IMPORTANT ---
                //It is against the Cognitive3D terms of service to set this value lower than 0.1 (ie, 10 snapshots per second)
                snapshotInterval = Mathf.Max(0.1f, value);
            }
        }
        public bool DynamicObjectSearchInParent = true;
        public bool TrackGPSLocation;
        public float GPSInterval = 1;
        public float GPSAccuracy = 2;
        public bool SyncGPSWithGaze;
        public bool RecordFloorPosition = true;

        public int GazeLayerMask = -1;
        public int DynamicLayerMask = -1;

        [Header("Send Data")]
        //min batch size
        public int GazeSnapshotCount = 64;
        public int SensorSnapshotCount = 64;
        public int DynamicSnapshotCount = 64;
        public int TransactionSnapshotCount = 64;

        //min timer
        //public int GazeSnapshotMinTimer = 6;
        public int SensorSnapshotMinTimer = 6;
        public int DynamicSnapshotMinTimer = 2;
        public int TransactionSnapshotMinTimer = 2;

        //extreme batch size
        //public int GazeExtremeSnapshotCount = 256;
        public int SensorExtremeSnapshotCount = 256;
        public int DynamicExtremeSnapshotCount = 256;
        public int TransactionExtremeSnapshotCount = 256;

        //max timer
        //public int GazeSnapshotMaxTimer = 10;
        public int SensorSnapshotMaxTimer = 10;
        public int DynamicSnapshotMaxTimer = 10;
        public int TransactionSnapshotMaxTimer = 10;



        public bool SendDataOnQuit = true;
        public bool SendDataOnHMDRemove = true;
        public bool SendDataOnLevelLoad = true;
        public bool SendDataOnHotkey = true;
        public bool HotkeyShift = true;
        public bool HotkeyCtrl = false;
        public bool HotkeyAlt = false;
        public KeyCode SendDataHotkey = KeyCode.F9;

        //defualt 10MB cache size
        public long LocalDataCacheSize = 1024 * 1024 * 10;
        public bool LocalStorage = true;
        public int ReadLocalCacheCount = 2;

        public int TextureResize = 1;
        public bool ExportSceneLODLowest = true;
        public bool ExportAOMaps = false;

        public List<SceneSettings> sceneSettings = new List<SceneSettings>();
        //use scene path instead of sceneName, if possible
        
        /// <summary>
        /// adds scene data if it doesn't already exist in scene settings
        /// </summary>
        /// <param name="scene"></param>
        public static void AddSceneSettings(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene == null || string.IsNullOrEmpty(scene.name)) { return; }
            var foundSettings = Instance.sceneSettings.Find(x => x.ScenePath == scene.path);
            if (foundSettings != null)
            {
                foundSettings.SceneName = scene.name;
                foundSettings.ScenePath = scene.path;
            }
            else
            {
                instance.sceneSettings.Add(new SceneSettings(scene.name, scene.path));
            }
        }

        public static void AddSceneSettings(CognitiveVR_Preferences newInstance, string name, string path)
        {
            //skip. this should onyl be called automatically at the construction of preferences
            newInstance.sceneSettings.Add(new SceneSettings(name, path));
        }

        public static SceneSettings FindScene(string sceneName)
        {
            return Instance.sceneSettings.Find(x => x.SceneName == sceneName);
        }

        public static SceneSettings FindSceneByPath(string scenePath)
        {
            return Instance.sceneSettings.Find(x => x.ScenePath == scenePath);
        }

        public static SceneSettings FindSceneById(string sceneid)
        {
            return Instance.sceneSettings.Find(x => x.SceneId == sceneid);
        }
        /// <summary>
        /// return the scene settings for whichever scene is currently open and active
        /// </summary>
        /// <returns></returns>
        public static SceneSettings FindCurrentScene()
        {
            SceneSettings returnSettings = null;

            returnSettings = FindSceneByPath(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

            return returnSettings;
        }

        [Serializable]
        public class SceneSettings
        {
            public string SceneName = "";
            [UnityEngine.Serialization.FormerlySerializedAs("SceneKey")]
            public string SceneId = "";
            public string ScenePath = "";
            public long LastRevision;
            public int VersionNumber = 1;
            public int VersionId;

            public SceneSettings(string name, string path)
            {
                SceneName = name;
                ScenePath = path;
            }
        }
    }
}