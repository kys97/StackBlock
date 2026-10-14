using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using StackBlock.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Copied into the isolated validation project's Assets/Editor only.
public static class MaterialUtilityValidation
{
    private static readonly string ReportRoot = Path.GetFullPath("../../Logs");

    public static void DiagnoseMaterialCopies()
    {
        var differences = new List<string>();
        var report = JsonUtility.FromJson<MaterialExtractionUtility.Report>(File.ReadAllText(Path.Combine(ReportRoot, "material-applied.json")));
        foreach (string item in report.materials.Where(m => m.StartsWith("EXTRACT ")))
        {
            string[] mapping = item.Substring(8).Split(new[] { " :: ", " -> " }, StringSplitOptions.None);
            var source = AssetDatabase.LoadAllAssetsAtPath(mapping[0]).OfType<Material>().Single(m => m.name == mapping[1]);
            var target = AssetDatabase.LoadAssetAtPath<Material>(mapping[2]);
            string[] left = SerializedSnapshot(source, false).Split('\n');
            string[] right = SerializedSnapshot(target, false).Split('\n');
            foreach (string line in left.Except(right)) differences.Add(mapping[2] + " SOURCE " + line);
            foreach (string line in right.Except(left)) differences.Add(mapping[2] + " COPIED " + line);
        }
        Write("material-copy-differences.txt", string.Join("\n", differences));
        EditorApplication.Exit(0);
    }

    public static void AnalyzeBatch()
    {
        try
        {
            var before = HashFiles("Assets/Resources");
            var report = MaterialExtractionUtility.Analyze();
            Write("material-analysis.json", JsonUtility.ToJson(report, true));
            RequireEqual(before, HashFiles("Assets/Resources"), "Dry run modified Resources");
            if (report.conflicts.Count > 0)
            {
                var rejected = MaterialExtractionUtility.ExtractAndRelink();
                if (rejected.applied) throw new InvalidOperationException("Conflicted operation was not blocked.");
                RequireEqual(before, HashFiles("Assets/Resources"), "Blocked operation modified Resources");
                Write("material-blocked-validation.txt", "PASS: the actual out-of-scope conflicts block execution with zero Resources/.meta changes.\n");
            }
            Write("material-analysis-validation.txt", "PASS: Analyze made zero changes to Resources files, including .meta.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void ApplyBatch()
    {
        try
        {
            var before = MaterialExtractionUtility.Analyze();
            if (before.conflicts.Count > 0) throw new InvalidOperationException("Analysis conflicts block application.");
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Complete", "Assets/Resources/Puzzle", "Assets/Resources/Ground" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            CheckConflictGuard(before);
            var structure = prefabs.ToDictionary(p => p, PrefabSnapshot);
            var nonMaterialText = prefabs.ToDictionary(p => p, NonMaterialText);
            var allPrefabSlots = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .ToDictionary(p => p, p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Select(m => m != null).ToArray());
            var embeddedMaterials = new Dictionary<string, string>();
            foreach (string item in before.materials.Where(m => m.StartsWith("EXTRACT ")))
            {
                string[] mapping = item.Substring(8).Split(new[] { " :: ", " -> " }, StringSplitOptions.None);
                Material original = AssetDatabase.LoadAllAssetsAtPath(mapping[0]).OfType<Material>().Single(m => m.name == mapping[1]);
                embeddedMaterials[mapping[2]] = SerializedSnapshot(original, false);
            }
            var existingMaterials = Directory.GetFiles("Assets", "*.mat", SearchOption.AllDirectories)
                .SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, Hash);
            var fbxFiles = Directory.GetFiles("Assets/Resources/Fbx", "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p, Hash);
            var importSettings = fbxFiles.Keys.ToDictionary(p => p, p => SerializedSnapshot(AssetImporter.GetAtPath(p.Replace('\\', '/')), true));
            var untouched = fbxFiles.Keys.Select(p => p + ".meta")
                .Concat(Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)).ToDictionary(p => p, Hash);
            var expectedSlots = before.slots.ToArray();
            var unresolvedReferences = UnresolvedReferences(before.unmapped);
            var result = MaterialExtractionUtility.ExtractAndRelink();
            if (!result.applied) throw new InvalidOperationException("Application was blocked.");
            Write("material-applied.json", JsonUtility.ToJson(result, true));
            foreach (var entry in untouched)
                if (entry.Value != Hash(entry.Key)) throw new InvalidOperationException("FBX importer or scene changed: " + entry.Key);
            VerifyPlannedSlots(expectedSlots);
            RequireEqual(unresolvedReferences, UnresolvedReferences(before.unmapped), "Unresolved slot PPtr was changed");
            foreach (var entry in structure)
                if (entry.Value != PrefabSnapshot(entry.Key)) throw new InvalidOperationException("Non-material prefab changes: " + entry.Key);
            foreach (var entry in nonMaterialText)
                if (entry.Value != NonMaterialText(entry.Key)) throw new InvalidOperationException("Non-material YAML changed: " + entry.Key);
            foreach (var entry in allPrefabSlots)
            {
                bool[] slots = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Key).GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials).Select(m => m != null).ToArray();
                if (slots.Length != entry.Value.Length || slots.Where((valid, index) => entry.Value[index] && !valid).Any())
                    throw new InvalidOperationException("Material reference lost, including out-of-scope prefab: " + entry.Key);
            }
            foreach (var entry in embeddedMaterials)
                if (entry.Value != SerializedSnapshot(AssetDatabase.LoadAssetAtPath<Material>(entry.Key), false))
                    throw new InvalidOperationException("Extracted shader/material properties changed: " + entry.Key);
            foreach (var entry in existingMaterials.Concat(fbxFiles))
                if (entry.Value != Hash(entry.Key)) throw new InvalidOperationException("Original FBX/material was modified: " + entry.Key);
            foreach (var entry in importSettings)
                if (entry.Value != SerializedSnapshot(AssetImporter.GetAtPath(entry.Key.Replace('\\', '/')), true))
                    throw new InvalidOperationException("Non-remap importer settings changed: " + entry.Key);
            var after = MaterialExtractionUtility.Analyze();
            Write("material-after.json", JsonUtility.ToJson(after, true));
            if (after.newMaterialCount != 0 || after.replacementSlotCount != 0 || after.remapCount != 0 || after.conflicts.Count != 0)
                throw new InvalidOperationException("Second analyze is not clean/idempotent.");
            var hashes = HashFiles("Assets/Resources");
            MaterialExtractionUtility.ExtractAndRelink();
            RequireEqual(hashes, HashFiles("Assets/Resources"), "Second apply modified assets");
            AuditAssets();
            Write("material-apply-validation.txt", "PASS: all " + prefabs.Length + " prefab structures/components/meshes/transforms/slot counts unchanged.\n"
                + "PASS: existing materials and their meta files, all FBX binaries unchanged.\n"
                + "PASS: extracted material names/shaders/textures/properties unchanged; no material references lost in any prefab.\n"
                + "PASS: full FBX meta/importer settings and every scene file unchanged.\n"
                + "PASS: every planned slot points to its exact planned external asset.\n"
                + "PASS: unresolved slots retain their original serialized GUID/fileID.\n"
                + "PASS: prefab YAML outside material slot references is byte-for-byte unchanged.\n"
                + "PASS: second Analyze and Apply perform zero asset changes.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static string NonMaterialText(string path) => Regex.Replace(File.ReadAllText(path),
        @"(?m)^  m_Materials:\r?\n(?<slots>(?:  - .*\r?\n)*)", "<material slots>");

    private static Dictionary<string, string> UnresolvedReferences(List<string> contexts)
    {
        var result = new Dictionary<string, string>();
        foreach (string context in contexts)
        {
            string[] parts = context.Split(new[] { " | " }, StringSplitOptions.None);
            if (parts.Length < 3) continue;
            string[] info = parts[2].Split(' ');
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(parts[0]);
            Renderer renderer = root.GetComponentsInChildren<Renderer>(true).Single(r => DisplayPath(r.transform) == parts[1] && r.GetType().Name == info[0]);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string _, out long id);
            string component = Regex.Match(File.ReadAllText(parts[0]), @"(?ms)^--- !u!\d+ &" + id + @"\r?\n.*?(?=^--- !u!|\z)").Value;
            string array = Regex.Match(component, @"(?m)^  m_Materials:\r?\n(?<slots>(?:  - .*\r?\n)*)").Groups["slots"].Value;
            MatchCollection references = Regex.Matches(array, @"\{fileID:.*?\}");
            result[context] = references[int.Parse(info[2])].Value;
        }
        return result;
    }

    private static void VerifyPlannedSlots(string[] slots)
    {
        foreach (string line in slots)
        {
            string[] parts = line.Split(new[] { " | " }, StringSplitOptions.None);
            string destination = line.Substring(line.LastIndexOf(" -> ", StringComparison.Ordinal) + 4);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(parts[0]);
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true).Where(t => DisplayPath(t) == parts[1]).ToArray();
            if (nodes.Length != 1) throw new InvalidOperationException("Ambiguous validation node: " + line);
            string[] info = parts[2].Split(' ');
            Renderer[] renderers = nodes[0].GetComponents<Renderer>().Where(r => r.GetType().Name == info[0]).ToArray();
            if (renderers.Length != 1 || AssetDatabase.GetAssetPath(renderers[0].sharedMaterials[int.Parse(info[2])]) != destination)
                throw new InvalidOperationException("Wrong slot destination: " + line);
        }
    }

    private static string DisplayPath(Transform node) => node.parent == null ? node.name : DisplayPath(node.parent) + "/" + node.name;

    private static void AuditAssets()
    {
        var issues = new List<string>();
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            AuditRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path), path, issues);
        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) AuditRoot(root, path, issues);
        }
        Write("material-missing-audit.txt", issues.Count == 0 ? "No missing scripts/materials/meshes." : string.Join("\n", issues));
    }

    private static void AuditRoot(GameObject root, string path, List<string> issues)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (Component component in node.GetComponents<Component>())
                if (component == null) issues.Add("Missing Script: " + path + " | " + DisplayPath(node));
            foreach (Renderer renderer in node.GetComponents<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] == null) issues.Add("Missing Material: " + path + " | " + DisplayPath(node) + " | slot " + i);
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : node.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) issues.Add("Missing Mesh: " + path + " | " + DisplayPath(node));
            }
        }
    }

    private static void CheckConflictGuard(MaterialExtractionUtility.Report plan)
    {
        string first = plan.materials.FirstOrDefault(m => m.StartsWith("EXTRACT "));
        if (first == null) return;
        string[] mapping = first.Substring(8).Split(new[] { " :: ", " -> " }, StringSplitOptions.None);
        string folder = Path.GetDirectoryName(mapping[2]).Replace('\\', '/');
        bool createdFolder = !AssetDatabase.IsValidFolder(folder);
        if (createdFolder) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        var fixtures = new List<string>();
        try
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(mapping[0]).OfType<Material>().Single(m => m.name == mapping[1]);
            foreach (string parent in new[] { Path.GetDirectoryName(mapping[0]).Replace('\\', '/'), folder })
            {
                string path = parent + "/" + source.name + ".mat";
                if (File.Exists(path)) throw new InvalidOperationException("Fixture must not overwrite existing material: " + path);
                fixtures.Add(path);
                AssetDatabase.CreateAsset(new Material(source) { name = source.name }, path);
            }
            var before = HashFiles("Assets/Resources");
            var blocked = MaterialExtractionUtility.ExtractAndRelink();
            if (blocked.applied || blocked.conflicts.Count == 0) throw new InvalidOperationException("Ambiguous candidates were not blocked.");
            RequireEqual(before, HashFiles("Assets/Resources"), "Conflict guard allowed asset changes");
            Write("material-conflict-validation.txt", "PASS: two same-name candidate materials block the entire operation with zero asset changes.\n");
        }
        finally
        {
            foreach (string path in fixtures) AssetDatabase.DeleteAsset(path);
            if (createdFolder) AssetDatabase.DeleteAsset(folder);
        }
    }

    private static string PrefabSnapshot(string path)
    {
        var lines = new List<string>();
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            lines.Add(SerializedSnapshot(node.gameObject, false));
            foreach (Component component in node.GetComponents<Component>())
            {
                lines.Add(component == null ? "<missing>" : component.GetType().FullName + SerializedSnapshot(component, false));
                if (component is Renderer renderer) lines.Add("slot-count=" + renderer.sharedMaterials.Length);
            }
        }
        return string.Join("\n", lines);
    }

    private static string SerializedSnapshot(Object value, bool importer)
    {
        var result = new List<string>();
        var property = new SerializedObject(value).GetIterator();
        while (property.Next(true))
        {
            if (value is Renderer && (property.propertyPath == "m_Materials" || property.propertyPath.StartsWith("m_Materials."))) continue;
            if (value is Material && property.propertyPath == "m_ObjectHideFlags") continue;
            // Embedded ownership changes when the same material becomes a standalone .mat;
            // this is asset identity, not a shader/color/texture/material setting.
            if (value is Material && (property.propertyPath == "m_PrefabAsset" || property.propertyPath.StartsWith("m_PrefabAsset."))) continue;
            if (property.propertyType == SerializedPropertyType.Generic) continue;
            string contents;
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                Object target = property.objectReferenceValue;
                contents = target == null ? "null" : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long id)
                    ? guid + ":" + id : target.GetType().FullName + ":" + target.name;
            }
            else if (property.propertyType == SerializedPropertyType.Float) contents = property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            else if (property.propertyType == SerializedPropertyType.Enum) contents = property.intValue.ToString();
            else contents = Convert.ToString(property.boxedValue, CultureInfo.InvariantCulture);
            result.Add(property.propertyPath + "=" + contents);
        }
        return string.Join("\n", result);
    }

    private static Dictionary<string, string> HashFiles(string folder) => Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(p => p, Hash);
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    private static void RequireEqual(Dictionary<string, string> before, Dictionary<string, string> after, string message)
    {
        if (before.Count != after.Count || before.Any(pair => !after.TryGetValue(pair.Key, out string hash) || hash != pair.Value))
            throw new InvalidOperationException(message);
    }
    private static void Write(string name, string contents)
    {
        Directory.CreateDirectory(ReportRoot);
        File.WriteAllText(Path.Combine(ReportRoot, name), contents);
    }
}
