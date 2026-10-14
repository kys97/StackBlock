using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public partial class FullFlowTests
{
    private static void AssertHintReset(PuzzleDragPreview preview)
    {
        var properties = new MaterialPropertyBlock();
        foreach (Renderer renderer in preview.Root.GetComponentsInChildren<Renderer>(true))
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                renderer.GetPropertyBlock(properties, i);
                Assert.IsTrue(properties.isEmpty, renderer.name + " slot " + i + " retains a tint");
            }
    }

    private static void AssertHintColor(PuzzleDragPreview preview, Color expected)
    {
        var properties = new MaterialPropertyBlock();
        foreach (Renderer renderer in preview.Root.GetComponentsInChildren<Renderer>(true))
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                renderer.GetPropertyBlock(properties, i);
                Color actual = properties.GetColor("_Color");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.0001f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.0001f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.0001f));
                Assert.AreEqual(renderer.sharedMaterials[i].color.a, actual.a);
            }
    }

    [UnityTest]
    public IEnumerator PlacementHintSharesDropRulesAndCleansEveryExit([Values("Spring", "Egypt")] string stage)
    {
        yield return EnterPuzzle(stage == "Spring" ? "Weather" : "Structure", stage);
        var manager = GameManager.Instance;
        Object.FindAnyObjectByType<Timer>().enabled = false;
        var piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().Where(ui =>
            manager.CanPlacePiece(PieceKey(ui), manager.Puzzle[PieceKey(ui)].Placement.WorldPosition))
            .OrderByDescending(ui => manager.Puzzle[PieceKey(ui)].Object.GetComponentsInChildren<Renderer>(true)
                .Sum(r => r.sharedMaterials.Length)).First();
        string key = PieceKey(piece);
        var target = manager.Puzzle[key];
        var originals = target.Object.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
        var snapshots = originals.Select(UnityEditor.EditorJsonUtility.ToJson).ToArray();
        var pointer = new PointerEventData(EventSystem.current) { position = ListPosition(manager) };
        Color highlight = new Color(1f, .85f, .2f, 1f);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Assert.AreEqual(highlight, typeof(BlockUI).GetField("validPlacementColor", flags).GetValue(piece));
        Assert.AreEqual(4f, typeof(BlockUI).GetField("hintBlinkSpeed", flags).GetValue(piece));
        Assert.IsFalse(manager.CanPlacePiece("not-a-piece", Vector3.zero));
        foreach (var block in manager.Puzzle.Where(p => !p.Value.Surface.activeInHierarchy))
            Assert.IsFalse(manager.CanPlacePiece(block.Key, block.Value.Placement.WorldPosition), "Predecessor condition: " + block.Key);
        Assert.AreEqual(0, manager.Score, "Read-only queries must not award points");

        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        Assert.IsNull(Preview(piece));
        Assert.AreEqual(1f, DragImage(piece).color.a, "Obsolete transparent UI blink is removed");
        pointer.position = target.ScreenPosition; piece.OnDrag(pointer);
        var preview = Preview(piece);
        Assert.IsTrue(preview.IsVisible);
        var renderer = preview.Root.GetComponentInChildren<Renderer>();
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties, 0);
        Assert.AreEqual(renderer.sharedMaterial.color, properties.GetColor("_Color"), "New hint starts at original color");

        if (stage == "Spring")
        {
            string capture = System.IO.Path.GetFullPath("../../Logs/placement-hint-base.png");
            if (System.IO.File.Exists(capture)) System.IO.File.Delete(capture);
            ScreenCapture.CaptureScreenshot(capture);
            yield return WaitFor(() => System.IO.File.Exists(capture), "Base-color capture");
        }
        Color before = renderer.sharedMaterial.color;
        yield return WaitFor(() =>
        {
            renderer.GetPropertyBlock(properties, 0);
            Color actual = properties.GetColor("_Color");
            return Vector3.Distance(new Vector3(actual.r, actual.g, actual.b), new Vector3(before.r, before.g, before.b)) > .08f;
        }, "LateUpdate animates a stationary valid preview");
        yield return new WaitForSeconds(.35f);
        if (stage == "Spring")
        {
            string capture = System.IO.Path.GetFullPath("../../Logs/placement-hint-highlight.png");
            if (System.IO.File.Exists(capture)) System.IO.File.Delete(capture);
            ScreenCapture.CaptureScreenshot(capture);
            yield return WaitFor(() => System.IO.File.Exists(capture), "Highlight capture");
        }

        // Deterministic peak on every slot, with no source or temporary material mutation.
        preview.UpdatePlacementHint(false, highlight, 4f, 0f);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        AssertHintColor(preview, highlight);
        CollectionAssert.AreEqual(snapshots, originals.Select(UnityEditor.EditorJsonUtility.ToJson));

        pointer.position = Camera.main.WorldToScreenPoint(target.Placement.WorldPosition
            + Vector3.right * (manager.CurrentStageData.SnapDistanceWorld + .3f));
        Assert.IsFalse(manager.CanPlacePiece(key, target.Placement.WorldPosition
            + Vector3.right * (manager.CurrentStageData.SnapDistanceWorld + .3f)));
        piece.OnDrag(pointer);
        AssertHintReset(preview); // Immediate, before the next frame.
        pointer.position = target.ScreenPosition; piece.OnDrag(pointer);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        AssertHintColor(preview, highlight);

        // Direction change with no new pointer event must revoke the hint as well.
        var camera = Camera.main.GetComponent<PuzzleCameraController>();
        camera.RotateRight();
        yield return WaitFor(() => !camera.IsRotating, "Direction changes during hint");
        yield return null;
        Assert.IsFalse(manager.CanPlacePiece(key, preview.AnchorWorldPosition));
        AssertHintReset(preview);
        camera.RotateLeft();
        yield return WaitFor(() => !camera.IsRotating, "Original direction restored");
        pointer.position = target.ScreenPosition; piece.OnDrag(pointer);
        preview.UpdatePlacementHint(false, highlight, 4f, 0f);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        AssertHintColor(preview, highlight);

        pointer.position = ListPosition(manager); piece.OnDrag(pointer);
        AssertHintReset(preview); Assert.IsFalse(preview.IsVisible);
        Assert.IsTrue(DragImage(piece).enabled); Assert.AreEqual(1f, DragImage(piece).color.a);
        pointer.position = target.ScreenPosition; piece.OnDrag(pointer);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        manager.PauseGame();
        Assert.IsNull(Preview(piece)); Assert.IsNull(DragImage(piece));
        AssertHintReset(preview); Assert.IsFalse(preview.IsVisible);
        Assert.IsFalse(manager.CanPlacePiece(key, target.Placement.WorldPosition));
        piece.OnBeginDrag(pointer); Assert.IsNull(Preview(piece)); Assert.IsNull(DragImage(piece));
        yield return new WaitForSecondsRealtime(.1f);
        Assert.IsTrue(preview.Root == null);
        manager.ResumeGame(); piece.OnEndDrag(pointer);
        Assert.IsFalse(target.IsComplete, "Resume must not continue the canceled drag");

        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        preview = Preview(piece);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        pointer.position = Vector2.one * -100f;
        piece.OnEndDrag(pointer); // Release without a final OnDrag must still clean up.
        AssertHintReset(preview); Assert.IsFalse(preview.IsVisible); Assert.IsFalse(target.IsComplete);
        Assert.AreEqual(0, manager.Score);
        yield return null;

        pointer.position = target.ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        preview = Preview(piece);
        preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
        piece.OnEndDrag(pointer);
        AssertHintReset(preview); Assert.IsFalse(preview.IsVisible);
        Assert.IsTrue(target.IsComplete); Assert.IsTrue(target.Object.activeSelf);
        CollectionAssert.AreEqual(snapshots, originals.Select(UnityEditor.EditorJsonUtility.ToJson));
        foreach (Renderer actual in target.Object.GetComponentsInChildren<Renderer>(true))
            Assert.IsFalse(actual.HasPropertyBlock(), "No hint on the real placed object");
        Assert.AreEqual(manager.CurrentStageData.PointsPerPiece, manager.Score);
        yield return null;

        piece = manager.PieceContent.GetComponentsInChildren<BlockUI>().First(ui =>
            manager.CanPlacePiece(PieceKey(ui), manager.Puzzle[PieceKey(ui)].Placement.WorldPosition));
        pointer.position = manager.Puzzle[PieceKey(piece)].ScreenPosition;
        piece.OnBeginDrag(pointer); piece.OnDrag(pointer);
        preview = Preview(piece);
        foreach (Renderer next in preview.Root.GetComponentsInChildren<Renderer>())
            for (int i = 0; i < next.sharedMaterials.Length; i++)
            {
                next.GetPropertyBlock(properties, i);
                Assert.AreEqual(next.sharedMaterials[i].color, properties.GetColor("_Color"), "Next piece starts fresh");
            }
        pointer.position = Vector2.one * -100f; piece.OnEndDrag(pointer);
    }
}

public class PlacementHintGeometryTests
{
    [UnityTest]
    public IEnumerator AllRenderersAndSlotsPulseWithoutChangingMaterials(
        [Values("Standard", "Hidden/HintTest/BaseColor", "Hidden/HintTest/MainColor", "Hidden/HintTest/NoColor")] string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        Assert.IsNotNull(shader);
        var source = new GameObject("Multi-renderer piece");
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
        Object.Destroy(primitive);
        var originals = new Material[3];
        string colorProperty = shaderName.EndsWith("MainColor") ? "_Tint" : shaderName.EndsWith("BaseColor") ? "_BaseColor" : "_Color";
        Color[] colors = { new Color(.15f, .3f, .65f, .7f), new Color(.55f, .1f, .2f, .8f), new Color(.2f, .65f, .1f, .9f) };
        for (int i = 0; i < originals.Length; i++)
        {
            originals[i] = new Material(shader);
            if (originals[i].HasProperty(colorProperty)) originals[i].SetColor(colorProperty, colors[i]);
            else colors[i] = Color.white;
        }
        var snapshots = originals.Select(UnityEditor.EditorJsonUtility.ToJson).ToArray();
        for (int r = 0; r < 2; r++)
        {
            var child = new GameObject("Renderer " + r);
            child.transform.SetParent(source.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterials = originals;
        }
        var skin = new GameObject("Skinned source");
        skin.transform.SetParent(source.transform, false);
        var weights = Enumerable.Range(0, mesh.vertexCount).Select(_ => new BoneWeight { boneIndex0 = 0, weight0 = 1f }).ToArray();
        mesh.boneWeights = weights; mesh.bindposes = new[] { Matrix4x4.identity };
        var skinned = skin.AddComponent<SkinnedMeshRenderer>();
        skinned.sharedMesh = mesh; skinned.bones = new[] { skin.transform }; skinned.rootBone = skin.transform;
        skinned.sharedMaterials = originals;
        source.SetActive(false);
        using (var preview = new PuzzleDragPreview(source))
        {
            preview.Show(new RaycastHit { point = Vector3.zero, normal = Vector3.up });
            var renderers = preview.Root.GetComponentsInChildren<Renderer>();
            Assert.AreEqual(3, renderers.Length, "Includes baked SkinnedMeshRenderer");
            var copies = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Assert.AreEqual(3, copies.Length, "One preview material copy per source, not per slot/frame");
            var copySnapshots = copies.Select(UnityEditor.EditorJsonUtility.ToJson).ToArray();
            var block = new MaterialPropertyBlock();
            Color highlight = new Color(.8f, 1f, .1f, .1f); // Alpha must be ignored.
            float[] increments = { 0f, Mathf.PI / 8f, Mathf.PI / 8f, Mathf.PI / 4f };
            float[] amounts = { 0f, .5f, 1f, 0f };
            for (int step = 0; step < increments.Length; step++)
            {
                preview.UpdatePlacementHint(true, highlight, 4f, increments[step]);
                foreach (Renderer renderer in renderers)
                    for (int i = 0; i < 3; i++)
                    {
                        renderer.GetPropertyBlock(block, i);
                        Color expected = Color.Lerp(colors[i], highlight, amounts[step]); expected.a = colors[i].a;
                        Color actual = block.GetColor("_Color");
                        Assert.That(Vector4.Distance(expected, actual), Is.LessThan(.0001f), shaderName + " slot " + i);
                        Assert.AreEqual(colors[i], renderer.sharedMaterials[i].color);
                    }
            }
            preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
            preview.UpdatePlacementHint(true, highlight, 4f, 0f);
            foreach (Renderer renderer in renderers)
            {
                renderer.GetPropertyBlock(block, 2);
                Assert.That(block.GetColor("_Color").g, Is.EqualTo(1f).Within(.0001f), "Zero scaled time does not advance");
            }
            preview.UpdatePlacementHint(false, highlight, 4f, 0f);
            foreach (Renderer renderer in renderers) Assert.IsFalse(renderer.HasPropertyBlock());
            preview.UpdatePlacementHint(true, highlight, 4f, Mathf.PI / 4f);
            preview.Hide();
            foreach (Renderer renderer in renderers) Assert.IsFalse(renderer.HasPropertyBlock());
            preview.UpdatePlacementHint(true, highlight, 4f, 10f);
            foreach (Renderer renderer in renderers) Assert.IsFalse(renderer.HasPropertyBlock(), "Hidden preview cannot animate");
            CollectionAssert.AreEqual(copySnapshots, copies.Select(UnityEditor.EditorJsonUtility.ToJson));
            CollectionAssert.AreEqual(snapshots, originals.Select(UnityEditor.EditorJsonUtility.ToJson));
        }
        Object.Destroy(source); Object.Destroy(mesh);
        foreach (Material material in originals) Object.Destroy(material);
        yield return null;
    }
}
