using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(HexGridManager))]
public class HexGridManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        HexGridManager hexManager = (HexGridManager)target;
        if (GUILayout.Button("Rebuild Grid From NavMesh"))
        {
            NavMeshBuilder.BuildNavMesh();
            hexManager.RebuildFromNavMesh();
        }

        if (GUILayout.Button("Report Stats"))
        {
            hexManager.ReportStats();
        }
    }
}