using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Installed only into the isolated validation project's Assets/Editor directory.
public static class ProjectValidationEditor
{
    [Serializable] private class AuditReport
    {
        public int sceneCount;
        public int prefabCount;
        public int componentCount;
        public int eventCount;
        public List<string> missingScripts = new List<string>();
        public List<string> missingReferences = new List<string>();
        public List<string> invalidEvents = new List<string>();
    }

    public static void AuditAndBuildWebGL()
    {
        string reportRoot = Path.GetFullPath("../../Logs");
        Directory.CreateDirectory(reportRoot);
        var audit = new AuditReport();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            audit.sceneCount++;
            foreach (GameObject root in scene.GetRootGameObjects()) Inspect(root, path, audit);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { audit.prefabCount++; Inspect(root, path, audit); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText(Path.Combine(reportRoot, "final-asset-audit.json"), JsonUtility.ToJson(audit, true));
        Debug.Log("ASSET_AUDIT scenes=" + audit.sceneCount + " prefabs=" + audit.prefabCount
            + " missingScripts=" + audit.missingScripts.Count + " missingReferences=" + audit.missingReferences.Count
            + " invalidEvents=" + audit.invalidEvents.Count);

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.WebGL,
            locationPathName = Path.GetFullPath("../FinalWebGLBuild"),
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        File.WriteAllText(Path.Combine(reportRoot, "final-webgl-summary.txt"),
            "Result: " + summary.result + "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings
            + "\nSize: " + summary.totalSize + "\nTime: " + summary.totalTime + "\nOutput: " + summary.outputPath);
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static void Inspect(GameObject root, string path, AuditReport audit)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            string context = path + ":" + AnimationUtility.CalculateTransformPath(transform, null);
            foreach (Component component in transform.GetComponents<Component>())
            {
                if (component == null) { audit.missingScripts.Add(context); continue; }
                audit.componentCount++;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null && property.objectReferenceEntityIdValue != default)
                        audit.missingReferences.Add(context + ":" + component.GetType().Name + "." + property.propertyPath);
                    // Inspect serialized calls, including nested EventTrigger entries.
                    if (!property.isArray || property.name != "m_Calls" || !property.propertyPath.Contains("m_PersistentCalls")) continue;
                    for (int i = 0; i < property.arraySize; i++)
                    {
                        audit.eventCount++;
                        var call = property.GetArrayElementAtIndex(i);
                        UnityEngine.Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                        string method = call.FindPropertyRelative("m_MethodName").stringValue;
                        if (target == null || string.IsNullOrEmpty(method) || !target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(m => m.Name == method))
                            audit.invalidEvents.Add(context + ":" + property.propertyPath + " -> " + method);
                    }
                }
            }
        }
    }
}
