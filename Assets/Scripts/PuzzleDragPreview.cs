using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A disposable visual copy. It never instantiates puzzle behaviours or changes
// placement state, source transforms, meshes, or shared material assets.
public sealed class PuzzleDragPreview : IDisposable
{
    private static Shader previewShader;
    private readonly GameObject root;
    private readonly Vector3 sourceAnchorWorld;
    private Vector3 placementAnchorWorld;
    private Quaternion cameraRelativeRotation;
    private bool hasCameraRelativeRotation;
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
    private readonly Dictionary<Material, Material> materialCopies = new Dictionary<Material, Material>();
    private readonly List<TintSlot> tintSlots = new List<TintSlot>();
    private float hintPhase;
    private bool hintActive;

    // One cached block per slot, owned only by the temporary preview renderer.
    private sealed class TintSlot
    {
        public Renderer Renderer;
        public int Index;
        public int Property;
        public Color OriginalColor;
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
    }

    public GameObject Root => root;
    public bool IsVisible => root != null && root.activeSelf;
    public Vector3 AnchorWorldPosition => root.transform.TransformPoint(sourceAnchorWorld);

    public PuzzleDragPreview(GameObject source)
    {
        var placement = source.GetComponent<PuzzlePlacementAnchor>();
        sourceAnchorWorld = placement != null ? placement.WorldPosition : PuzzlePlacementAnchor.CalculateBottomCenter(source);
        placementAnchorWorld = sourceAnchorWorld;
        root = new GameObject("Puzzle Drag Preview");
        root.hideFlags = HideFlags.DontSave;
        root.layer = 2; // Ignore Raycast; visual copies have no colliders either.
        if (previewShader == null) previewShader = Resources.Load<Shader>("Shaders/PuzzleDragPreview");
        if (previewShader == null) throw new InvalidOperationException("Missing PuzzleDragPreview shader.");

        foreach (Renderer sourceRenderer in source.GetComponentsInChildren<Renderer>(true))
        {
            if (!sourceRenderer.enabled || !PuzzlePlacementAnchor.IsVisiblePart(sourceRenderer.transform, source.transform)) continue;
            Mesh mesh;
            if (sourceRenderer is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh { name = "Drag preview pose", hideFlags = HideFlags.DontSave };
                skinned.BakeMesh(mesh, false);
                bakedMeshes.Add(mesh);
            }
            else if (sourceRenderer is MeshRenderer && sourceRenderer.TryGetComponent(out MeshFilter filter))
                mesh = filter.sharedMesh;
            else continue;
            if (mesh == null) continue;

            Transform copy = CopyTransform(sourceRenderer.transform, root.transform, transforms);
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.gameObject.AddComponent<MeshRenderer>();
            Material[] slots = sourceRenderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = CopyMaterial(slots[i]);
            renderer.sharedMaterials = slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!TryGetColorProperty(slots[i], out int property)) continue;
                tintSlots.Add(new TintSlot
                {
                    Renderer = renderer, Index = i, Property = property,
                    OriginalColor = slots[i].GetColor(property)
                });
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        root.SetActive(false);
    }

    public PuzzleDragPreview(GameObject source, Quaternion imageViewRotation) : this(source)
    {
        // The image suffix selects the view of the authored object. Store that
        // camera-relative pose once, regardless of the player's current view.
        cameraRelativeRotation = Quaternion.Inverse(imageViewRotation) * root.transform.rotation;
        hasCameraRelativeRotation = true;
    }

    internal static Transform CopyTransform(Transform source, Transform root, Dictionary<Transform, Transform> transforms)
    {
        if (transforms.TryGetValue(source, out Transform existing)) return existing;
        // Reproduce ancestor TRS as well, including rotated, nonuniform parents.
        // lossyScale alone cannot preserve shear. All copies are independent.
        Transform parent = source.parent != null ? CopyTransform(source.parent, root, transforms) : root;
        var copy = new GameObject(source.name).transform;
        copy.gameObject.layer = 2;
        copy.gameObject.hideFlags = HideFlags.DontSave;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        transforms.Add(source, copy);
        return copy;
    }

    private Material CopyMaterial(Material source)
    {
        if (source != null && materialCopies.TryGetValue(source, out Material existing)) return existing;
        var material = new Material(previewShader) { name = "Drag Preview", hideFlags = HideFlags.DontSave };
        if (source != null)
        {
            if (TryGetColorProperty(source, out int sourceColor)
                && TryGetColorProperty(material, out int previewColor))
                material.SetColor(previewColor, source.GetColor(sourceColor));
            if (source.HasProperty("_MainTex"))
            {
                material.mainTexture = source.GetTexture("_MainTex");
                material.mainTextureScale = source.GetTextureScale("_MainTex");
                material.mainTextureOffset = source.GetTextureOffset("_MainTex");
            }
            materialCopies.Add(source, material);
        }
        materials.Add(material);
        return material;
    }

    private static bool TryGetColorProperty(Material material, out int property)
    {
        // Respect a shader's explicit main-color declaration before conventional names.
        Shader shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            if (shader.GetPropertyType(i) == ShaderPropertyType.Color
                && (shader.GetPropertyFlags(i) & ShaderPropertyFlags.MainColor) != 0)
            {
                property = shader.GetPropertyNameId(i);
                return material.HasProperty(property);
            }
        }
        foreach (string name in new[] { "_BaseColor", "_Color" })
        {
            property = Shader.PropertyToID(name);
            int index = shader.FindPropertyIndex(name);
            if (material.HasProperty(property) && index >= 0
                && shader.GetPropertyType(index) == ShaderPropertyType.Color) return true;
        }
        property = 0;
        return false;
    }

    public void UpdatePlacementHint(bool canPlace, Color highlight, float blinkSpeed, float deltaTime)
    {
        if (!canPlace || !IsVisible) { ResetPlacementHint(); return; }
        hintActive = true;
        // Called with zero elapsed time on pointer events, once with scaled deltaTime
        // in LateUpdate. Pointer frequency never changes the blink speed.
        hintPhase = Mathf.Repeat(hintPhase + Mathf.Max(0f, blinkSpeed) * Mathf.Max(0f, deltaTime), 2f * Mathf.PI);
        float blend = (1f - Mathf.Cos(hintPhase)) * 0.5f;
        foreach (TintSlot slot in tintSlots)
        {
            Color tint = Color.Lerp(slot.OriginalColor, highlight, blend);
            tint.a = slot.OriginalColor.a; // Keep existing preview opacity unchanged.
            slot.Properties.SetColor(slot.Property, tint);
            slot.Renderer.SetPropertyBlock(slot.Properties, slot.Index);
        }
    }

    private void ResetPlacementHint()
    {
        hintPhase = 0f;
        if (!hintActive) return;
        foreach (TintSlot slot in tintSlots)
            if (slot.Renderer != null) slot.Renderer.SetPropertyBlock(null, slot.Index);
        hintActive = false;
    }

    public void Show(RaycastHit hit)
    {
        // Keep the ray-projected anchor independent of the preview's visual rotation.
        placementAnchorWorld = hit.point;
        root.transform.position += hit.point - AnchorWorldPosition;
        root.SetActive(true);
    }

    public void UpdateCameraRelativeRotation(Quaternion cameraRotation)
    {
        if (!hasCameraRelativeRotation)
        {
            // Capture once after the initial pose. Hide/Show keeps this drag session.
            cameraRelativeRotation = Quaternion.Inverse(cameraRotation) * root.transform.rotation;
            hasCameraRelativeRotation = true;
            return;
        }

        root.transform.rotation = cameraRotation * cameraRelativeRotation;
        // Rotate only the disposable root, preserving every child's local TRS.
        // Rebuild translation from the last projected anchor to avoid cumulative drift.
        root.transform.position = placementAnchorWorld - root.transform.TransformVector(sourceAnchorWorld);
    }

    public void Hide()
    {
        ResetPlacementHint();
        if (root != null) root.SetActive(false);
    }

    public void Dispose()
    {
        Hide();
        if (root != null) UnityEngine.Object.Destroy(root);
        foreach (Material material in materials) UnityEngine.Object.Destroy(material);
        foreach (Mesh mesh in bakedMeshes) UnityEngine.Object.Destroy(mesh);
        materials.Clear();
        bakedMeshes.Clear();
        tintSlots.Clear();
    }
}
