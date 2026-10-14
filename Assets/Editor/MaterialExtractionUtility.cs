using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StackBlock.EditorTools
{
    /// <summary>Explicit, source-FBX-scoped extraction. No import callbacks or runtime code.</summary>
    public static class MaterialExtractionUtility
    {
        private const string FbxRoot = "Assets/Resources/Fbx";
        private static readonly string[] PrefabRoots = { "Assets/Resources/Complete", "Assets/Resources/Puzzle", "Assets/Resources/Ground" };

        [Serializable]
        public sealed class Report
        {
            public int fbxCount, embeddedMaterialCount, newMaterialCount, reusedMaterialCount;
            public int completePrefabCount, puzzlePrefabCount, replacementSlotCount, existingExternalSlotCount;
            public int remapCount;
            public int groundPrefabCount, springRecoveredSlots, summerRecoveredSlots, desertRecoveredSlots, embeddedReferenceCount;
            public List<string> embeddedReferences = new List<string>();
            public List<string> recoveries = new List<string>();
            public bool applied;
            public string backupDirectory;
            public List<string> materials = new List<string>();
            public List<string> prefabs = new List<string>();
            public List<string> slots = new List<string>();
            public List<string> unmapped = new List<string>();
            public List<string> conflicts = new List<string>();
            public List<string> outsideScopeReferences = new List<string>();
            public List<string> protectedStages = new List<string>();
        }

        private sealed class MaterialPlan
        {
            public string fbx, name, destination;
            public string guid;
            public long localId;
            public bool create;
        }

        private sealed class SlotPlan
        {
            public string route, displayPath;
            public int rendererIndex, slot, slotCount;
            public long rendererLocalId;
            public MaterialPlan material;
        }

        private sealed class PrefabPlan
        {
            public string path, hash;
            public readonly List<SlotPlan> slots = new List<SlotPlan>();
        }

        private sealed class Plan
        {
            public readonly Report report = new Report();
            public readonly List<MaterialPlan> materials = new List<MaterialPlan>();
            public readonly List<PrefabPlan> prefabs = new List<PrefabPlan>();
        }

        [MenuItem("Tools/StackBlock/Materials/Analyze Material References")]
        public static void AnalyzeMenu() => Analyze();

        /// <summary>Reads imported assets only. Does not create folders, save or reimport anything.</summary>
        public static Report Analyze()
        {
            Plan plan = BuildPlan();
            Print(plan.report, "ANALYZE — no assets modified");
            return plan.report;
        }

        [MenuItem("Tools/StackBlock/Materials/Extract And Relink Materials")]
        public static void ExtractMenu() => ExtractAndRelink();

        public static Report ExtractAndRelink()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before processing material assets.");

            // Always rebuild; never execute a stale dry-run plan after an artist edits assets.
            Plan plan = BuildPlan();
            Print(plan.report, "PREFLIGHT — no assets modified yet");
            if (plan.report.conflicts.Count != 0)
            {
                Debug.LogWarning("[StackBlock Materials] Execution blocked: resolve the reported conflicts and analyze again.");
                return plan.report;
            }
            if (plan.materials.All(m => !m.create) && plan.prefabs.Count == 0)
            {
                plan.report.applied = true;
                return plan.report;
            }

            var originals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var created = new List<string>();
            var folders = new List<string>();
            string backup = "Library/StackBlockMaterialBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            foreach (string path in plan.prefabs.Select(p => p.path).Distinct())
            {
                originals[path] = File.ReadAllBytes(path);
                string copy = backup + "/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.WriteAllBytes(copy, originals[path]);
            }
            plan.report.backupDirectory = backup;
            try
            {
                // Copy embedded materials without extracting/removing sub-assets or touching importers.
                foreach (MaterialPlan item in plan.materials.Where(m => m.create))
                {
                    if (File.Exists(item.destination) || File.Exists(item.destination + ".meta"))
                        throw new IOException("Destination appeared after analysis: " + item.destination);
                    string folder = Parent(item.destination);
                    if (!AssetDatabase.IsValidFolder(folder))
                    {
                        string guid = AssetDatabase.CreateFolder(Parent(folder), Path.GetFileName(folder));
                        if (string.IsNullOrEmpty(guid)) throw new IOException("Cannot create " + folder);
                        string actualFolder = AssetDatabase.GUIDToAssetPath(guid);
                        folders.Add(actualFolder);
                        if (actualFolder != folder) throw new IOException("Folder name collision: " + folder);
                    }
                    Material source = AssetDatabase.LoadAllAssetsAtPath(item.fbx).OfType<Material>().Single(m => MatchesSource(m, item));
                    created.Add(item.destination);
                    AssetDatabase.CreateAsset(new Material(source) { name = source.name }, item.destination);
                    AssetDatabase.ImportAsset(item.destination, ImportAssetOptions.ForceSynchronousImport);
                    Material extracted = AssetDatabase.LoadAssetAtPath<Material>(item.destination);
                    if (extracted == null || extracted.name != item.name)
                        throw new InvalidOperationException("Extraction did not preserve the material name: " + item.destination);
                }

                foreach (PrefabPlan prefab in plan.prefabs)
                {
                    if (Hash(prefab.path) != prefab.hash) throw new IOException("Prefab changed after analysis: " + prefab.path);
                    GameObject root = PrefabUtility.LoadPrefabContents(prefab.path);
                    try
                    {
                        foreach (var group in prefab.slots.GroupBy(s => s.route + ":" + s.rendererIndex))
                        {
                            SlotPlan first = group.First();
                            Transform node = ResolveRoute(root.transform, first.route);
                            Renderer renderer = node.GetComponents<Renderer>()[first.rendererIndex];
                            Material[] slots = renderer.sharedMaterials;
                            if (slots.Length != first.slotCount) throw new InvalidOperationException("Material slot count changed: " + prefab.path);
                            var serialized = new SerializedObject(renderer);
                            SerializedProperty materialSlots = serialized.FindProperty("m_Materials");
                            foreach (SlotPlan slot in group)
                            {
                                Material replacement = AssetDatabase.LoadAssetAtPath<Material>(slot.material.destination);
                                if (replacement == null) throw new InvalidOperationException("Missing extracted asset: " + slot.material.destination);
                                materialSlots.GetArrayElementAtIndex(slot.slot).objectReferenceValue = replacement;
                            }
                            // Assign only planned elements: writing the entire sharedMaterials array
                            // would turn unrelated dangling PPtrs into explicit null references.
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, prefab.path, out bool saved);
                        if (!saved) throw new IOException("Failed to save " + prefab.path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    PreserveUnrelatedSerialization(prefab, originals[prefab.path]);
                }

                plan.report.applied = true;
                File.WriteAllText(backup + "/operation.json", JsonUtility.ToJson(plan.report, true));
                File.WriteAllLines(backup + "/created-assets.txt", created.Concat(folders));
                Print(plan.report, "APPLIED");
                return plan.report;
            }
            catch (Exception exception)
            {
                // Restore only assets owned by this operation; importers and scenes are never written.
                foreach (var original in originals) File.WriteAllBytes(original.Key, original.Value);
                foreach (string path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                foreach (string folder in folders.AsEnumerable().Reverse())
                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.LogError("[StackBlock Materials] Operation rolled back. Backup: " + backup + "\n" + exception);
                throw;
            }
        }

        private static Plan BuildPlan()
        {
            var plan = new Plan();
            string[] models = AssetDatabase.FindAssets("", new[] { FbxRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Where(IsFbx).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            plan.report.fbxCount = models.Length;
            var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var embedded = new Dictionary<string, Material[]>(StringComparer.Ordinal);
            foreach (string fbx in models)
            {
                embedded[fbx] = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToArray();
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer == null) { plan.report.conflicts.Add("Not a ModelImporter: " + fbx); continue; }
                if (EditorUtility.IsDirty(importer))
                    plan.report.conflicts.Add("Unsaved importer settings; apply or revert them first: " + fbx);
                names[fbx] = new HashSet<string>(embedded[fbx].Select(m => m.name), StringComparer.Ordinal);
                foreach (var remap in importer.GetExternalObjectMap().Where(p => p.Key.type == typeof(Material))) names[fbx].Add(remap.Key.name);
                foreach (var duplicate in embedded[fbx].GroupBy(m => m.name).Where(g => g.Count() > 1))
                    plan.report.conflicts.Add("Duplicate embedded name in one FBX: " + fbx + " :: " + duplicate.Key);
                plan.report.embeddedMaterialCount += embedded[fbx].Length;
            }

            foreach (string fbx in names.Keys)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
                var remaps = importer.GetExternalObjectMap();
                int existingCount = 0, createCount = 0;
                foreach (string name in names[fbx].OrderBy(n => n, StringComparer.Ordinal))
                {
                    var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                    Material mapped = remaps.TryGetValue(identifier, out Object remapped) ? remapped as Material : null;
                    string output = Parent(fbx) + "/" + Path.GetFileNameWithoutExtension(fbx) + "_Materials/" + name + ".mat";
                    string destination = null;
                    if (remapped != null && (mapped == null || !IsExternal(mapped)))
                    {
                        plan.report.conflicts.Add("Existing remap is not an external .mat: " + fbx + " :: " + name);
                        continue;
                    }
                    if (mapped != null) destination = AssetDatabase.GetAssetPath(mapped);
                    else
                    {
                        // Search only the dedicated output directory and this FBX's own directory.
                        // Shared parent folders are usable only when this source name has a single FBX owner.
                        string[] candidates = AssetDatabase.FindAssets("t:Material", new[] { Parent(fbx) })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Where(p => (Parent(p) == Parent(output) || Parent(p) == Parent(fbx)) && p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                            .Where(p => AssetDatabase.LoadAssetAtPath<Material>(p).name == name).Distinct().ToArray();
                        bool sharedName = names.Count(pair => Parent(pair.Key) == Parent(fbx) && pair.Value.Contains(name)) > 1;
                        if (candidates.Length > 1 || (sharedName && candidates.Any(p => Parent(p) == Parent(fbx))))
                        {
                            plan.report.conflicts.Add("Ambiguous existing material: " + fbx + " :: " + name + " -> " + string.Join(", ", candidates));
                            continue;
                        }
                        if (candidates.Length == 1) destination = candidates[0];
                    }
                    bool create = destination == null;
                    if (create)
                    {
                        if (!embedded[fbx].Any(m => m.name == name))
                        {
                            plan.report.unmapped.Add("No embedded source/external remap: " + fbx + " :: " + name);
                            continue;
                        }
                        if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" "))
                        {
                            plan.report.conflicts.Add("Material name cannot be preserved as a filename: " + fbx + " :: " + name);
                            continue;
                        }
                        if (File.Exists(Parent(output)) || File.Exists(output) || File.Exists(output + ".meta"))
                        {
                            plan.report.conflicts.Add("Destination exists but does not match source material: " + output);
                            continue;
                        }
                        destination = output;
                    }
                    var item = new MaterialPlan { fbx = fbx, name = name, destination = destination, create = create };
                    Material embeddedSource = embedded[fbx].FirstOrDefault(m => m.name == name);
                    if (embeddedSource != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(embeddedSource, out item.guid, out item.localId);
                    plan.materials.Add(item);
                    if (create) { plan.report.newMaterialCount++; createCount++; }
                    else { plan.report.reusedMaterialCount++; existingCount++; }
                    plan.report.materials.Add((create ? "EXTRACT " : "REUSE ") + fbx + " :: " + name + " -> " + destination);
                }
                if (new[] { "Spring", "Summer", "Desert" }.Contains(Path.GetFileNameWithoutExtension(fbx)))
                    plan.report.protectedStages.Add(fbx + ": reuse=" + existingCount + ", missing embedded to extract=" + createCount + "; existing .mat files are never overwritten");
            }

            foreach (var collision in plan.materials.Where(m => m.create).GroupBy(m => m.destination, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                plan.report.conflicts.Add("Output path collision: " + collision.Key);
            var lookup = plan.materials.ToDictionary(m => Key(m.fbx, m.name), StringComparer.Ordinal);
            foreach (string prefabPath in PrefabRoots.Where(AssetDatabase.IsValidFolder)
                .SelectMany(root => AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var prefab = new PrefabPlan { path = prefabPath, hash = Hash(prefabPath) };
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int index = 0; index < slots.Length; index++)
                    {
                        Material material = slots[index];
                        if (material != null && IsExternal(material)) { plan.report.existingExternalSlotCount++; continue; }
                        string source = material != null ? AssetDatabase.GetAssetPath(material) : MissingReferenceDescription(prefabPath, renderer, index);
                        string context = prefabPath + " | " + DisplayPath(renderer.transform) + " | " + renderer.GetType().Name
                            + " slot " + index + " | material=" + (material != null ? material.name : "<null>") + " | asset=" + source;
                        MaterialPlan item = null;
                        if (material == null)
                            item = RecoverMissing(plan, prefabPath, renderer, index, source);
                        else if (IsFbx(source) && lookup.TryGetValue(Key(source, material.name), out MaterialPlan candidate) && MatchesSource(material, candidate))
                            item = candidate;
                        if (item == null)
                        {
                            plan.report.unmapped.Add(context);
                            continue;
                        }
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string _, out long rendererId);
                        if (SerializedSlots(File.ReadAllText(prefabPath), rendererId).Count != slots.Length)
                        {
                            plan.report.conflicts.Add("Cannot preserve non-material serialization (inherited/binary renderer): " + context);
                            continue;
                        }
                        prefab.slots.Add(new SlotPlan { route = Route(root.transform, renderer.transform), displayPath = context, rendererLocalId = rendererId,
                            rendererIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer), slot = index, slotCount = slots.Length, material = item });
                        plan.report.slots.Add(context + " -> " + item.destination);
                    }
                }
                if (prefab.slots.Count == 0) continue;
                plan.prefabs.Add(prefab);
                plan.report.prefabs.Add(prefabPath + " (slots=" + prefab.slots.Count + ")");
                if (prefabPath.StartsWith(PrefabRoots[0] + "/", StringComparison.Ordinal)) plan.report.completePrefabCount++;
                else if (prefabPath.StartsWith(PrefabRoots[1] + "/", StringComparison.Ordinal)) plan.report.puzzlePrefabCount++;
                else plan.report.groundPrefabCount++;
                plan.report.replacementSlotCount += prefab.slots.Count;
            }
            CheckReferencesOutsideManagedSlots(plan);
            return plan;
        }

        private static void CheckReferencesOutsideManagedSlots(Plan plan)
        {
            // Audit only. Embedded sub-assets remain valid because no importer is changed.
            var remapped = plan.materials.Where(m => !string.IsNullOrEmpty(m.guid))
                .ToDictionary(m => m.guid + ":" + m.localId, StringComparer.Ordinal);
            if (remapped.Count == 0) return;
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                && new[] { ".prefab", ".unity", ".asset", ".mat" }.Contains(Path.GetExtension(p).ToLowerInvariant())))
            {
                string text = File.ReadAllText(path);
                if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    if (AssetDatabase.GetDependencies(path, false).Any(dependency => plan.materials.Any(m => m.fbx == dependency)))
                        plan.report.conflicts.Add("Cannot completely audit non-text asset referencing a source model: " + path);
                    continue;
                }
                var references = Regex.Matches(text, @"\{fileID: (?<id>-?\d+), guid: (?<guid>[a-fA-F0-9]{32}), type: \d+\}")
                    .Cast<Match>().GroupBy(match => match.Groups["guid"].Value + ":" + match.Groups["id"].Value);
                PrefabPlan owned = plan.prefabs.FirstOrDefault(p => p.path == path);
                foreach (var reference in references)
                {
                    if (!remapped.TryGetValue(reference.Key, out MaterialPlan source)) continue;
                    plan.report.embeddedReferenceCount += reference.Count();
                    plan.report.embeddedReferences.Add(path + " :: " + source.fbx + " :: " + source.name + " (" + reference.Count() + ")");
                    int managedCount = owned == null ? 0 : owned.slots.Count(slot => slot.material == source);
                    if (reference.Count() <= managedCount) continue;
                    string warning = path + " :: " + source.fbx + " :: " + source.name
                        + " (" + (reference.Count() - managedCount) + " references outside managed renderer slots)";
                    plan.report.outsideScopeReferences.Add(warning);
                }
            }
        }

        private sealed class SerializedSlot
        {
            public int Index, Length;
            public string Value;
        }

        private static List<SerializedSlot> SerializedSlots(string yaml, long rendererId)
        {
            Match component = Regex.Match(yaml, @"(?ms)^--- !u!(?:23|137) &" + rendererId + @"\r?\n.*?(?=^--- !u!|\z)");
            Match array = Regex.Match(component.Value, @"(?m)^  m_Materials:\r?\n(?<slots>(?:  - .*\r?\n)*)");
            if (!component.Success || !array.Success) return new List<SerializedSlot>();
            int offset = component.Index + array.Groups["slots"].Index;
            return Regex.Matches(array.Groups["slots"].Value, @"\{fileID:.*?\}").Cast<Match>()
                .Select(m => new SerializedSlot { Index = offset + m.Index, Length = m.Length, Value = m.Value }).ToList();
        }

        private static void PreserveUnrelatedSerialization(PrefabPlan prefab, byte[] original)
        {
            // PrefabUtility is the authority for saving references. Retain only its validated
            // slot PPtrs, avoiding unrelated legacy Transform/Collider serialization upgrades.
            string before = Encoding.UTF8.GetString(original);
            string saved = File.ReadAllText(prefab.path);
            var edits = new List<Tuple<int, int, string>>();
            foreach (var group in prefab.slots.GroupBy(s => s.rendererLocalId))
            {
                List<SerializedSlot> oldSlots = SerializedSlots(before, group.Key);
                List<SerializedSlot> newSlots = SerializedSlots(saved, group.Key);
                if (oldSlots.Count != group.First().slotCount || newSlots.Count != oldSlots.Count)
                    throw new InvalidOperationException("Cannot retain original serialization: " + prefab.path);
                foreach (SlotPlan slot in group)
                    edits.Add(Tuple.Create(oldSlots[slot.slot].Index, oldSlots[slot.slot].Length, newSlots[slot.slot].Value));
            }
            var result = new StringBuilder(before);
            foreach (var edit in edits.OrderByDescending(e => e.Item1))
                result.Remove(edit.Item1, edit.Item2).Insert(edit.Item1, edit.Item3);
            File.WriteAllText(prefab.path, result.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(prefab.path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool MatchesSource(Material material, MaterialPlan source) => material.name == source.name
            && AssetDatabase.GetAssetPath(material) == source.fbx
            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long id)
            && guid == source.guid && id == source.localId;

        private static Mesh RendererMesh(Renderer renderer) => renderer is SkinnedMeshRenderer skinned
            ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;

        private static MaterialPlan RecoverMissing(Plan plan, string prefab, Renderer renderer, int slot, string missing)
        {
            Mesh mesh = RendererMesh(renderer);
            if (mesh == null) return null;
            string fbx = AssetDatabase.GetAssetPath(mesh);
            string stage = Path.GetFileNameWithoutExtension(fbx);
            // An explicit empty slot is not a broken reference. Require the old PPtr's FBX
            // to agree with the exact mesh asset, and restrict repairs to the requested stages.
            if (!new[] { "Spring", "Summer", "Desert" }.Contains(stage)
                || !missing.StartsWith(fbx + " [missing reference;", StringComparison.Ordinal)
                || missing.Contains("fileID=0]")) return null;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) return null;
            Renderer[] originals = model.GetComponentsInChildren<Renderer>(true)
                .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer) && RendererMesh(r) == mesh).ToArray();
            if (originals.Length == 0 || originals.Any(r => r.sharedMaterials.Length != renderer.sharedMaterials.Length)) return null;
            Material[] candidates = originals.Select(r => r.sharedMaterials[slot]).Distinct().ToArray();
            if (candidates.Length != 1 || candidates[0] == null || !IsExternal(candidates[0])) return null;
            string destination = AssetDatabase.GetAssetPath(candidates[0]);
            MaterialPlan item = plan.materials.FirstOrDefault(m => m.fbx == fbx && !m.create && m.destination == destination);
            if (item == null) return null;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string meshGuid, out long meshId);
            plan.report.recoveries.Add(prefab + " | " + DisplayPath(renderer.transform) + " | slot " + slot
                + " | mesh=" + fbx + " [" + meshGuid + ":" + meshId + "] -> " + destination);
            if (stage == "Spring") plan.report.springRecoveredSlots++;
            else if (stage == "Summer") plan.report.summerRecoveredSlots++;
            else plan.report.desertRecoveredSlots++;
            return item;
        }

        private static bool IsFbx(string path) => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        // A missing PPtr alone cannot identify a material. Recovery additionally requires an
        // exact source mesh and unanimous original FBX renderer slot evidence.
        private static string MissingReferenceDescription(string prefab, Renderer renderer, int slot)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string _, out long id)) return "<null/unknown>";
            string yaml = File.ReadAllText(prefab);
            Match component = Regex.Match(yaml, @"(?ms)^--- !u!\d+ &" + id + @"\r?\n.*?(?=^--- !u!|\z)");
            if (!component.Success) return "<null; inherited/unknown source>";
            Match materials = Regex.Match(component.Value, @"(?m)^  m_Materials:\r?\n(?<slots>(?:  - .*\r?\n)*)");
            MatchCollection references = Regex.Matches(materials.Groups["slots"].Value, @"fileID: (?<id>-?\d+)(?:, guid: (?<guid>[a-fA-F0-9]{32}))?");
            if (slot >= references.Count) return "<null/unknown>";
            string guid = references[slot].Groups["guid"].Value;
            return AssetDatabase.GUIDToAssetPath(guid) + " [missing reference; guid=" + guid + "; fileID=" + references[slot].Groups["id"].Value + "]";
        }
        private static bool IsExternal(Material material) => AssetDatabase.GetAssetPath(material).EndsWith(".mat", StringComparison.OrdinalIgnoreCase);
        private static string Parent(string path) => Path.GetDirectoryName(path).Replace('\\', '/');
        private static string Key(string fbx, string name) => fbx + "\n" + name;
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static string Route(Transform root, Transform node)
        {
            var indexes = new List<int>();
            while (node != root) { indexes.Add(node.GetSiblingIndex()); node = node.parent; }
            indexes.Reverse();
            return string.Join("/", indexes);
        }
        private static Transform ResolveRoute(Transform root, string route)
        {
            if (route.Length == 0) return root;
            foreach (string index in route.Split('/')) root = root.GetChild(int.Parse(index));
            return root;
        }
        private static string DisplayPath(Transform node) => node.parent == null ? node.name : DisplayPath(node.parent) + "/" + node.name;
        private static void Print(Report report, string phase)
        {
            Debug.Log("[StackBlock Materials] " + phase + "\n" + JsonUtility.ToJson(report, true));
            foreach (string warning in report.unmapped) Debug.LogWarning("[StackBlock Materials] Unmapped; untouched: " + warning);
            foreach (string conflict in report.conflicts) Debug.LogWarning("[StackBlock Materials] CONFLICT: " + conflict);
        }
    }
}
