﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

namespace CognitiveVR
{
    public class CognitiveVR_SelectSDKPopup : PopupWindowContent
    {
        public override Vector2 GetWindowSize()
        {
            return new Vector2(292, 170);
        }

        public override void OnOpen()
        {
#if CVR_STEAMVR
            option.Add("CVR_STEAMVR");
#endif
#if CVR_OCULUS
            option.Add("CVR_OCULUS");
#endif
#if CVR_GOOGLEVR
            option.Add("CVR_GOOGLEVR");
#endif
#if CVR_DEFAULT
            option.Add("CVR_DEFAULT");
#endif
#if CVR_FOVE
            option.Add("CVR_FOVE");
#endif
#if CVR_PUPIL
            option.Add("CVR_PUPIL");
#endif
        }

        public override void OnClose()
        {
            CognitiveVR_Settings.Instance.SetPlayerDefine(option);
        }

        List<string> option = new List<string>();
        public override void OnGUI(Rect rect)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Please Select your VR SDK</b>");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (option.Contains("CVR_STEAMVR")) { GUI.color = CognitiveVR_Settings.GreenButton; GUI.contentColor = Color.white; }
            if (GUILayout.Button("Steam VR 1.2.0+"))
            {
                if (option.Contains("CVR_STEAMVR"))
                    option.Remove("CVR_STEAMVR");
                else
                {
                    if (!Event.current.shift)
                        option.Clear();
                    option.Add("CVR_STEAMVR");
                }
            }
            GUI.color = Color.white;

            if (option.Contains("CVR_OCULUS")) { GUI.color = CognitiveVR_Settings.GreenButton; GUI.contentColor = Color.white; }
            if (GUILayout.Button("Oculus Utilities 1.9.0+"))
            {
                if (option.Contains("CVR_OCULUS"))
                    option.Remove("CVR_OCULUS");
                else
                {
                    if (!Event.current.shift)
                        option.Clear();
                    option.Add("CVR_OCULUS");
                }
            }
            GUI.color = Color.white;

            if (option.Contains("CVR_FOVE")) { GUI.color = CognitiveVR_Settings.GreenButton; GUI.contentColor = Color.white; }
            if (GUILayout.Button("Fove VR 0.9.2"))
            {
                if (option.Contains("CVR_FOVE"))
                {
                    option.Remove("CVR_FOVE");
                    option.Remove("CVR_GAZETRACK");
                }
                else
                {
                    if (!Event.current.shift)
                        option.Clear();
                    option.Add("CVR_FOVE");
                    option.Add("CVR_GAZETRACK");
                }
            }
            GUI.color = Color.white;

            if (option.Contains("CVR_PUPIL")) { GUI.color = CognitiveVR_Settings.GreenButton; GUI.contentColor = Color.white; }
            if (GUILayout.Button("Pupil Labs"))
            {
                if (option.Contains("CVR_PUPIL"))
                {
                    option.Remove("CVR_PUPIL");
                    option.Remove("CVR_GAZETRACK");
                }
                else
                {
                    if (!Event.current.shift)
                        option.Clear();
                    option.Add("CVR_PUPIL");
                    option.Add("CVR_GAZETRACK");
                }
            }
            GUI.color = Color.white;

            if (option.Contains("CVR_DEFAULT")) { GUI.color = CognitiveVR_Settings.GreenButton; GUI.contentColor = Color.white; }
            if (GUILayout.Button("Unity Default VR"))
            {
                if (option.Contains("CVR_DEFAULT"))
                    option.Remove("CVR_DEFAULT");
                else
                {
                    if (!Event.current.shift)
                        option.Clear();
                    option.Add("CVR_DEFAULT");
                }
            }
            GUI.color = Color.white;

            GUILayout.Space(5);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            GUILayout.Space(5);

            GUI.color = CognitiveVR_Settings.GreenButton;
            GUI.contentColor = Color.white;
            if (GUILayout.Button("Save and Close"))
            {
                editorWindow.Close();
            }
            GUI.color = Color.white;
        }
    }
}