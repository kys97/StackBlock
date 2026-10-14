using System;
using System.Collections.Generic;
using UnityEngine;

// Projection only. Missing physical surfaces get temporary mesh-only proxies,
// since the existing Ground collider is a background slab below the terrain.
public sealed class PuzzleDragRaycaster : IDisposable
{
    private readonly GameObject root = new GameObject("Puzzle Drag Raycast Surfaces");
    private readonly List<(Collider collider, GameObject source)> surfaces = new List<(Collider, GameObject)>();
    private readonly bool hasPlayArea;
    private readonly Bounds playAreaLocalBounds;
    private readonly Matrix4x4 playAreaWorldToLocal;
    private readonly float playAreaCenterY;

    public PuzzleDragRaycaster(GameObject[] sources, MeshFilter playAreaMesh = null)
    {
        if (playAreaMesh != null && playAreaMesh.sharedMesh != null)
        {
            // Use the authored board's oriented bounds, not its huge background
            // child or a world AABB that expands when the board is rotated.
            hasPlayArea = true;
            playAreaLocalBounds = playAreaMesh.sharedMesh.bounds;
            playAreaWorldToLocal = playAreaMesh.transform.worldToLocalMatrix;
            playAreaCenterY = playAreaMesh.transform.TransformPoint(playAreaLocalBounds.center).y;
        }
        root.hideFlags = HideFlags.DontSave;
        root.layer = 2;
        var transforms = new Dictionary<Transform, Transform>();
        foreach (GameObject source in sources)
        {
            foreach (Collider collider in source.GetComponentsInChildren<Collider>(true))
                surfaces.Add((collider, collider.gameObject));
            foreach (MeshFilter mesh in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null || mesh.TryGetComponent<Collider>(out _)) continue;
                Transform copy = PuzzleDragPreview.CopyTransform(mesh.transform, root.transform, transforms);
                var collider = copy.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                surfaces.Add((collider, mesh.gameObject));
            }
        }
        // Newly constructed collider transforms must be ready for this pointer event.
        Physics.SyncTransforms();
    }

    public bool Raycast(Ray ray, float maxDistance, out RaycastHit closest, float? placementHeight = null)
    {
        float rayLimit = maxDistance;
        closest = default;
        bool found = false;
        foreach (var surface in surfaces)
        {
            if (surface.source == null || !surface.source.activeInHierarchy || surface.collider == null || !surface.collider.enabled) continue;
            if (surface.collider.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                closest = hit;
                maxDistance = hit.distance;
                found = true;
            }
        }
        // Collider hits delimit the puzzle area, but do not choose a piece's tier:
        // a hollow upper Surface hits the background, while another Surface can
        // occlude a small piece. Project on this piece's anchor-height plane in
        // both cases, so geometry in front/behind cannot distort the XZ position.
        // This is projection only, never snapping or a second placement rule.
        if (found && placementHeight.HasValue)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, placementHeight.Value, 0f));
            if (!plane.Raycast(ray, out float distance) || distance > rayLimit) return false;
            Vector3 point = ray.GetPoint(distance);
            if (hasPlayArea)
            {
                // Check the footprint only: elevated target anchors keep their own Y.
                Vector3 footprintPoint = new Vector3(point.x, playAreaCenterY, point.z);
                if (!playAreaLocalBounds.Contains(playAreaWorldToLocal.MultiplyPoint3x4(footprintPoint))) return false;
            }
            closest = new RaycastHit { point = point, normal = Vector3.up, distance = distance };
        }
        return found;
    }

    public void Dispose()
    {
        if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        surfaces.Clear();
    }
}
