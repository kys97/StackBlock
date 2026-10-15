using System;
using System.Collections.Generic;
using UnityEngine;

// Drag-owned geometry queries only. Never moves a target, changes placement,
// edits shared materials, or adds colliders to the source objects.
public sealed class PuzzlePreviewOcclusion : IDisposable
{
    private const int QueryLayer = 2; // Ignore Raycast; queried explicitly below.
    private const float Clearance = .005f;
    private readonly GameObject root = new GameObject("Puzzle Preview Occlusion");
    private readonly List<(Renderer source, MeshCollider proxy)> proxies = new List<(Renderer, MeshCollider)>();
    private readonly HashSet<Collider> ownedColliders = new HashSet<Collider>();
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private RaycastHit[] hits = new RaycastHit[32];
    private Collider[] overlaps = new Collider[32];

    public PuzzlePreviewOcclusion(GameObject[] sources)
    {
        root.hideFlags = HideFlags.DontSave;
        root.layer = QueryLayer;
        var transforms = new Dictionary<Transform, Transform>();
        foreach (GameObject source in sources)
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh { name = "Preview occluder pose", hideFlags = HideFlags.DontSave };
                    skinned.BakeMesh(mesh, false);
                    bakedMeshes.Add(mesh);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
                else continue;
                if (mesh == null) continue;
                Transform copy = PuzzleDragPreview.CopyTransform(renderer.transform, root.transform, transforms);
                var proxy = copy.gameObject.AddComponent<MeshCollider>();
                proxy.sharedMesh = mesh;
                proxy.enabled = renderer.enabled && renderer.gameObject.activeInHierarchy;
                proxies.Add((renderer, proxy));
                ownedColliders.Add(proxy);
            }
        Physics.SyncTransforms();
    }

    public bool TryGetOffset(Camera camera, Bounds cameraBounds, Vector3 anchor, out Vector3 offset)
    {
        offset = Vector3.zero;
        bool changed = false;
        foreach (var entry in proxies)
        {
            bool enabled = entry.source != null && entry.source.enabled && entry.source.gameObject.activeInHierarchy;
            if (entry.proxy.enabled == enabled) continue;
            entry.proxy.enabled = enabled;
            changed = true;
        }
        if (changed) Physics.SyncTransforms();

        Vector3 localAnchor = camera.transform.InverseTransformPoint(anchor);
        float distance = localAnchor.magnitude;
        if (distance < .0001f || localAnchor.z <= 0f) return false;
        Vector3 direction = (anchor - camera.transform.position) / distance;

        // Include the anchor in the box so its sweep also encloses sightlines
        // from the eye to the whole preview, including an offset custom pivot.
        cameraBounds.Encapsulate(localAnchor);
        Vector3 extents = cameraBounds.extents + Vector3.one * Clearance;
        Vector3 centerOffset = cameraBounds.center - localAnchor;
        Vector3 origin = camera.transform.TransformPoint(centerOffset);
        Quaternion orientation = camera.transform.rotation;

        // A sweep can miss geometry overlapping its starting box. There is no
        // safe full-size 3D view in that case; the caller keeps its UI drag image.
        int count;
        while ((count = Physics.OverlapBoxNonAlloc(origin, extents, overlaps, orientation,
            1 << QueryLayer, QueryTriggerInteraction.Ignore)) == overlaps.Length)
            Array.Resize(ref overlaps, overlaps.Length * 2);
        for (int i = 0; i < count; i++)
            if (ownedColliders.Contains(overlaps[i])) return false;

        while ((count = Physics.BoxCastNonAlloc(origin, extents, direction, hits, orientation,
            distance, 1 << QueryLayer, QueryTriggerInteraction.Ignore)) == hits.Length)
            Array.Resize(ref hits, hits.Length * 2);
        float visibleDistance = distance;
        for (int i = 0; i < count; i++)
            if (ownedColliders.Contains(hits[i].collider))
                visibleDistance = Mathf.Min(visibleDistance, Mathf.Max(0f, hits[i].distance - Clearance));

        float localDepthShift = (visibleDistance - distance) * localAnchor.z / distance;
        if (cameraBounds.min.z + localDepthShift <= camera.nearClipPlane + Clearance) return false;
        offset = direction * (visibleDistance - distance);
        return true;
    }

    public void Dispose()
    {
        if (root != null)
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
        }
        foreach (Mesh mesh in bakedMeshes) UnityEngine.Object.Destroy(mesh);
        bakedMeshes.Clear();
        proxies.Clear();
        ownedColliders.Clear();
    }
}
