﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CognitiveVR;

public class ManageDynamicObjects : EditorWindow
{
    Rect steptitlerect = new Rect(30, 0, 100, 440);

    public static void Init()
    {
        ManageDynamicObjects window = (ManageDynamicObjects)EditorWindow.GetWindow(typeof(ManageDynamicObjects), true, "");
        window.minSize = new Vector2(500, 500);
        window.maxSize = new Vector2(500, 500);
        window.Show();
    }

    private void OnGUI()
    {
        GUI.skin = EditorCore.WizardGUISkin;

        GUI.DrawTexture(new Rect(0, 0, 500, 500), EditorGUIUtility.whiteTexture);

        var currentscene = CognitiveVR_Preferences.FindCurrentScene();

        if (string.IsNullOrEmpty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name))
        {
            GUI.Label(steptitlerect, "DYNAMIC OBJECTS   Scene Not Saved", "steptitle");
        }
        else if (currentscene == null || string.IsNullOrEmpty(currentscene.SceneId))
        {
            GUI.Label(steptitlerect, "DYNAMIC OBJECTS   Scene Not Uploaded", "steptitle");
        }
        else
        {
            GUI.Label(steptitlerect, "DYNAMIC OBJECTS   " + currentscene.SceneName + " Version: " + currentscene.VersionNumber, "steptitle");
        }

        GUI.Label(new Rect(30, 45, 440, 440), "These are the current <color=#8A9EB7FF>Dynamic Object</color> components in your scene:", "boldlabel");

        //headers
        Rect mesh = new Rect(30, 95, 120, 30);
        GUI.Label(mesh, "Dynamic Mesh Name", "dynamicheader");
        Rect gameobject = new Rect(190, 95, 120, 30);
        GUI.Label(gameobject, "GameObject", "dynamicheader");
        //Rect ids = new Rect(320, 95, 120, 30);
        //GUI.Label(ids, "Ids", "dynamicheader");
        Rect uploaded = new Rect(380, 95, 120, 30);
        GUI.Label(uploaded, "Uploaded", "dynamicheader");


        //content
        DynamicObject[] tempdynamics = GetDynamicObjects;
        Rect innerScrollSize = new Rect(30, 0, 420, tempdynamics.Length * 30);
        dynamicScrollPosition = GUI.BeginScrollView(new Rect(30, 120, 440, 270), dynamicScrollPosition, innerScrollSize, false, true);

        Rect dynamicrect;
        for (int i = 0; i < tempdynamics.Length; i++)
        {
            if (tempdynamics[i] == null) { RefreshSceneDynamics(); GUI.EndScrollView(); return; }
            dynamicrect = new Rect(30, i * 30, 460, 30);
            DrawDynamicObject(tempdynamics[i], dynamicrect, i % 2 == 0);
        }
        GUI.EndScrollView();
        GUI.Box(new Rect(30, 120, 425, 270), "", "box_sharp_alpha");

        //buttons

        string scenename = "Not Saved";
        int versionnumber = 0;
        string buttontextstyle = "button_bluetext";
        if (currentscene == null || string.IsNullOrEmpty(currentscene.SceneId))
        {
            buttontextstyle = "button_disabledtext";
        }
        else
        {
            scenename = currentscene.SceneName;
            versionnumber = currentscene.VersionNumber;
        }

        EditorGUI.BeginDisabledGroup(currentscene == null || string.IsNullOrEmpty(currentscene.SceneId));
        if (GUI.Button(new Rect(60, 400, 150, 40), new GUIContent("Upload Selected", "Export and Upload to " + scenename + " version " + versionnumber), buttontextstyle))
        {
            //dowhattever thing get scene version
            EditorCore.RefreshSceneVersion(() =>
            {
                if (CognitiveVR_SceneExportWindow.ExportSelectedObjectsPrefab())
                {
                    if (CognitiveVR_SceneExportWindow.UploadSelectedDynamicObjects(true))

                        UploadManifest();
                }
            });
            //TODO pop up upload ids to scene modal
        }

        if (GUI.Button(new Rect(320, 400, 100, 40), new GUIContent("Upload All","Export and Upload to "+ scenename + " version " + versionnumber), buttontextstyle))
        {
            EditorCore.RefreshSceneVersion(() =>
            {
                if (CognitiveVR_SceneExportWindow.ExportAllDynamicsInScene())
                {
                    if (CognitiveVR_SceneExportWindow.UploadAllDynamicObjects(true))
                        UploadManifest();
                }
            });
            //TODO pop up upload ids to scene modal
        }
        EditorGUI.EndDisabledGroup();

        //export and upload all

        /*if (GUI.Button(new Rect(30,400,140,40),"Upload Ids to Scene"))
        {
            EditorCore.RefreshSceneVersion(delegate() { ManageDynamicObjects.UploadManifest(); }); //get latest scene version then upload manifest to there
        }*/

        DrawFooter();
        Repaint(); //manually repaint gui each frame to make sure it's responsive
    }
    
    #region Dynamic Objects

    Vector2 dynamicScrollPosition;

    DynamicObject[] _cachedDynamics;
    DynamicObject[] GetDynamicObjects { get { if (_cachedDynamics == null || _cachedDynamics.Length == 0) { _cachedDynamics = FindObjectsOfType<DynamicObject>(); } return _cachedDynamics; } }

    private void OnFocus()
    {
        RefreshSceneDynamics();
    }
    
    void RefreshSceneDynamics()
    {
        _cachedDynamics = FindObjectsOfType<DynamicObject>();
        EditorCore.ExportedDynamicObjects = null;
    }

    //each row is 30 pixels
    void DrawDynamicObject(DynamicObject dynamic, Rect rect, bool darkbackground)
    {
        Event e = Event.current;
        if (e.isMouse && e.type == EventType.mouseDown)
        {
            if (e.mousePosition.x < rect.x || e.mousePosition.x > rect.x+rect.width || e.mousePosition.y < rect.y || e.mousePosition.y > rect.y+rect.height)
            {
            }
            else
            {
                if (e.shift) //add to selection
                {
                    GameObject[] gos = new GameObject[Selection.transforms.Length + 1];
                    Selection.gameObjects.CopyTo(gos, 0);
                    gos[gos.Length - 1] = dynamic.gameObject;
                    Selection.objects = gos;
                }
                else
                {
                    Selection.activeTransform = dynamic.transform;
                }
            }
        }

        if (darkbackground)
            GUI.Box(rect, "", "dynamicentry_even");
        else
            GUI.Box(rect, "", "dynamicentry_odd");

        //GUI.color = Color.white;

        Rect mesh = new Rect(rect.x + 10, rect.y, 120, rect.height);
        Rect gameobject = new Rect(rect.x + 160, rect.y, 120, rect.height);
        //Rect id = new Rect(rect.x + 290, rect.y, 120, rect.height);

        Rect collider = new Rect(rect.x + 320, rect.y, 24, rect.height);
        Rect uploaded = new Rect(rect.x + 380, rect.y, 24, rect.height);

        GUI.Label(mesh, dynamic.MeshName, "dynamiclabel");
        GUI.Label(gameobject, dynamic.gameObject.name, "dynamiclabel");
        
        if (!dynamic.HasCollider())
        {
            GUI.Label(collider, new GUIContent(EditorCore.Alert,"Tracking Gaze requires a collider"), "image_centered");
        }
        if (EditorCore.GetExportedDynamicObjectNames().Contains(dynamic.MeshName))
        {
            GUI.Label(uploaded, EditorCore.Checkmark, "image_centered");
        }
        else
        {
            GUI.Label(uploaded, EditorCore.EmptyCheckmark, "image_centered");
        }
    }

    #endregion
    
    void DrawFooter()
    {
        GUI.color = EditorCore.BlueishGrey;
        GUI.DrawTexture(new Rect(0, 450, 500, 50), EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        string tooltip = "";
        
        var currentScene = CognitiveVR_Preferences.FindCurrentScene();
        if (currentScene == null || string.IsNullOrEmpty(currentScene.SceneId))
        {
            tooltip = "Upload list of all Dynamic Object IDs. Scene settings not saved";
        }
        else
        {
            tooltip = "Upload list of all Dynamic Object IDs and Mesh Names to " + currentScene.SceneName + " version " + currentScene.VersionNumber;
        }

        EditorGUI.BeginDisabledGroup(currentScene == null || string.IsNullOrEmpty(currentScene.SceneId));
        if (GUI.Button(new Rect(130, 450, 250, 50), new GUIContent("Upload Aggregation List", tooltip), "button_bluetext"))
        {
            EditorCore.RefreshSceneVersion(delegate () { ManageDynamicObjects.UploadManifest(); });
        }
        EditorGUI.EndDisabledGroup();
    }

    //get dynamic object aggregation manifest for the current scene
    void GetManifest()
    {
        var currentSceneSettings = CognitiveVR_Preferences.FindCurrentScene();
        if (currentSceneSettings == null)
        {
            return;
        }
        if (string.IsNullOrEmpty(currentSceneSettings.SceneId))
        {
            Util.logWarning("Get Manifest current scene doesn't have an id!");
            return;
        }

        string url = Constants.GETDYNAMICMANIFEST(currentSceneSettings.VersionId);

        Dictionary<string, string> headers = new Dictionary<string, string>();
        if (EditorCore.IsDeveloperKeyValid)
            headers.Add("Authorization", "APIKEY:DEVELOPER " + EditorCore.DeveloperKey);
        EditorNetwork.Get(url, GetManifestResponse, headers,false);//AUTH
    }


    void GetManifestResponse(int responsecode, string error, string text)
    {
        if (responsecode == 200)
        {
            //BuildManifest(getRequest.text);
            var allEntries = JsonUtil.GetJsonArray<AggregationManifest.AggregationManifestEntry>(text);

            Debug.Log("Number of Dynamic Objects in current Manifest: " + allEntries.Length);

            Manifest = new AggregationManifest();

            Manifest.objects = new List<AggregationManifest.AggregationManifestEntry>(allEntries);
            Repaint();

            //also hit settings to get the current version of the scene
            EditorCore.RefreshSceneVersion(null);
        }
        else
        {
            Util.logWarning("GetManifestResponse " + responsecode + " " + error);
        }
    }

    //send an http request to get all versions of the current scene
    /*System.Action onSceneVersionComplete;
    void GetSceneVersion(System.Action onComplete)
    {
        var currentSceneSettings = CognitiveVR_Preferences.FindCurrentScene();
        if (currentSceneSettings == null)
        {
            onSceneVersionComplete = null;
            return;
        }
        if (string.IsNullOrEmpty(currentSceneSettings.SceneId))
        {
            Util.logWarning("Cannot Get Scene Version. Current scene doesn't have an id!");
            onSceneVersionComplete = null;
            return;
        }

        onSceneVersionComplete = onComplete;
        string url = Constants.GETSCENEVERSIONS(currentSceneSettings.SceneId);

        Dictionary<string, string> headers = new Dictionary<string, string>();
        if (EditorCore.IsDeveloperKeyValid)
            headers.Add("Authorization", "APIKEY:DEVELOPER " + EditorCore.DeveloperKey);
        //EditorNetwork.Get(url, GetSceneSettingsResponse, headers, false);//AUTH
        Util.logDebug("GetSceneVersion request sent");
    }*/

    /*void GetSceneSettingsResponse(int responsecode, string error, string text)
    {
        Util.logDebug("GetSettingsResponse responseCode: " + responsecode);

        SceneVersionCollection = JsonUtility.FromJson<SceneVersionCollection>(text);

        if (SceneVersionCollection != null)
        {
            var sv = SceneVersionCollection.GetLatestVersion();
            Util.logDebug(sv.versionNumber.ToString());
            if (onSceneVersionComplete != null)
            {
                onSceneVersionComplete.Invoke();
            }
        }
        onSceneVersionComplete = null;
    }*/

    static List<DynamicObject> ObjectsInScene;
    public static List<DynamicObject> GetDynamicObjectsInScene()
    {
        if (ObjectsInScene == null)
        {
            ObjectsInScene = new List<DynamicObject>(GameObject.FindObjectsOfType<DynamicObject>());
        }
        return ObjectsInScene;
    }

    [System.Serializable]
    public class AggregationManifest
    {
        [System.Serializable]
        public class AggregationManifestEntry
        {
            public string name;
            public string mesh;
            public string id;
            public AggregationManifestEntry(string _name, string _mesh, string _id)
            {
                name = _name;
                mesh = _mesh;
                id = _id;
            }
            public override string ToString()
            {
                return "{\"name\":\"" + name + "\",\"mesh\":\"" + mesh + "\",\"id\":\"" + id + "\"}";
            }
        }
        public List<AggregationManifestEntry> objects = new List<AggregationManifestEntry>();
        //public int Version;
        //public string SceneId;
    }

    //only need id, mesh and name
    static AggregationManifest Manifest;
    //static SceneVersionCollection SceneVersionCollection;

    /// <summary>
    /// generate manifest from scene objects and upload to latest version of scene
    /// </summary>
    public static void UploadManifest()
    {
        if (Manifest == null) { Manifest = new AggregationManifest(); }
        //if (SceneVersionCollection == null) { Debug.LogError("SceneVersionCollection is null! Make sure RefreshSceneVersion was called before this"); return; }

        ObjectsInScene = null;
        foreach (var v in GetDynamicObjectsInScene())
        {
            AddOrReplaceDynamic(Manifest, v);
        }
        string json = "";
        if (ManifestToJson(out json))
        {
            var currentSettings = CognitiveVR_Preferences.FindCurrentScene();
            if (currentSettings != null && currentSettings.VersionNumber > 0)
                SendManifest(json, currentSettings.VersionNumber);
            else
                Util.logError("Could not find scene version for current scene");
        }
        Util.logDebug(json);
    }

    static bool ManifestToJson(out string json)
    {
        json = "{\"objects\":[";

        List<string> usedIds = new List<string>();

        bool containsValidEntry = false;
        bool meshNameMissing = false;
        List<string> missingMeshGameObjects = new List<string>();
        foreach (var entry in Manifest.objects)
        {
            if (string.IsNullOrEmpty(entry.mesh)) { meshNameMissing = true; missingMeshGameObjects.Add(entry.name); continue; }
            if (string.IsNullOrEmpty(entry.id)) { Debug.LogWarning(entry.name + " has empty dynamic id. This will not be aggregated"); continue; }
            if (usedIds.Contains(entry.id)) { Debug.LogWarning(entry.name + " using id that already exists in the scene. This may not be aggregated correctly"); } //TODO popup option to choose new GUID for dynamic
            usedIds.Add(entry.id);
            json += "{";
            json += "\"id\":\"" + entry.id + "\",";
            json += "\"mesh\":\"" + entry.mesh + "\",";
            json += "\"name\":\"" + entry.name + "\"";
            json += "},";
            containsValidEntry = true;
        }

        json = json.Remove(json.Length - 1, 1);
        json += "]}";

        if (meshNameMissing)
        {
            string debug = "Dynamic Objects missing mesh name:\n";
            foreach (var v in missingMeshGameObjects)
            {
                debug += v + "\n";
            }
            Debug.LogError(debug);
            EditorUtility.DisplayDialog("Error", "One or more dynamic objects are missing a mesh name and were not uploaded to scene.\n\nSee Console for details", "Ok");
        }

        return containsValidEntry;
    }

    static void SendManifest(string json, int versionNumber)
    {
        var settings = CognitiveVR_Preferences.FindCurrentScene();
        if (settings == null)
        {
            Debug.LogWarning("Send Manifest settings are null " + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path);
            string s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(s))
            {
                s = "Unknown Scene";
            }
            EditorUtility.DisplayDialog("Upload Failed", "Could not find the Scene Settings for \"" + s + "\". Are you sure you've saved, exported and uploaded this scene to SceneExplorer?", "Ok");
            return;
        }

        string url = Constants.POSTDYNAMICMANIFEST(settings.SceneId, versionNumber);
        Util.logDebug("Manifest Url: " + url);
        Util.logDebug("Manifest Contents: " + json);

        //upload manifest
        Dictionary<string, string> headers = new Dictionary<string, string>();
        if (EditorCore.IsDeveloperKeyValid)
            headers.Add("Authorization", "APIKEY:DEVELOPER " + EditorCore.DeveloperKey);
        EditorNetwork.Post(url, json, PostManifestResponse,headers,false);//AUTH
    }

    static void PostManifestResponse(int responsecode, string error, string text)
    {
        Util.logDebug("Manifest upload complete. response: " + text + " error: " + error);
    }

    static void AddOrReplaceDynamic(AggregationManifest manifest, DynamicObject dynamic)
    {
        var replaceEntry = manifest.objects.Find(delegate (AggregationManifest.AggregationManifestEntry obj) { return obj.id == dynamic.CustomId.ToString(); });
        if (replaceEntry == null)
        {
            //don't include meshes with empty mesh names in manifest
            if (!string.IsNullOrEmpty(dynamic.MeshName))
            {
                manifest.objects.Add(new AggregationManifest.AggregationManifestEntry(dynamic.gameObject.name, dynamic.MeshName, dynamic.CustomId.ToString()));
            }
        }
        else
        {
            replaceEntry.mesh = dynamic.MeshName;
            replaceEntry.name = dynamic.gameObject.name;
        }
    }
}
