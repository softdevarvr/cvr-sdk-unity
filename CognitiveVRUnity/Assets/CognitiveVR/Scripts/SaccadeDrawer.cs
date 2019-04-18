﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaccadeDrawer : MonoBehaviour {

    CognitiveVR.FixationRecorder fixationRecorder;
    // Use this for initialization
    public void SetTarget (CognitiveVR.FixationRecorder target)
    {
        fixationRecorder = target;
    }


    public Material mat;
    public Material fixationMat;
    public Material gazeMat;
    Color lightWhite = new Color(1, 1, 1, 0.25f);
    // Update is called once per frame
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void OnPostRender ()
    {
        if (fixationRecorder.IsFixating)
        {
            if (fixationRecorder.ActiveFixation.IsLocal)
            {
                if (fixationRecorder.ActiveFixation.LocalTransform == null) { return; }
                GL.Begin(GL.LINES);

                fixationMat.SetPass(0);
                GL.Vertex(fixationRecorder.ActiveFixation.LocalTransform.TransformPoint(fixationRecorder.ActiveFixation.LocalPosition));
                GL.Vertex(fixationRecorder.ActiveFixation.LocalTransform.TransformPoint(fixationRecorder.ActiveFixation.LocalPosition) + Vector3.up*0.25f);
                GL.End();

                GL.Begin(GL.LINES);
                gazeMat.SetPass(0);
                GL.Vertex(fixationRecorder.ActiveFixation.LocalTransform.TransformPoint(fixationRecorder.GetLastEyeCapture().LocalPosition));
                GL.Vertex(fixationRecorder.ActiveFixation.LocalTransform.TransformPoint(fixationRecorder.GetLastEyeCapture().LocalPosition) + Vector3.up * 0.25f);
                GL.End();

            }
            else
            {
                GL.Begin(GL.LINES);
                GL.Vertex(fixationRecorder.ActiveFixation.WorldPosition);
                GL.Vertex(fixationRecorder.ActiveFixation.WorldPosition + Vector3.up * 0.25f);
                GL.End();
            }
        }

        GL.Begin(GL.LINES);
        GL.Color(lightWhite);
        mat.SetPass(0);
        int count = fixationRecorder.VISGazepoints.Count;
        for (int i = 1; i < count; i++)
        {
            GL.Vertex(fixationRecorder.VISGazepoints[i - 1]);
            GL.Vertex(fixationRecorder.VISGazepoints[i]);
        }
        GL.End();
    }
#endif
}