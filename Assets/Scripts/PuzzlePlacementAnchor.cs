using UnityEngine;

// Geometry/configuration for one piece, independent of camera and placement rules.
[DisallowMultipleComponent]
public sealed class PuzzlePlacementAnchor : MonoBehaviour
{
    [SerializeField] private bool useCustomPlacementAnchor;
    [SerializeField] private Transform placementAnchor;
    [Tooltip("World units on the XZ plane. -1 uses the StageData default.")]
    [SerializeField, Min(-1f)] private float snapDistanceOverride = -1f;
    private Vector3 automaticLocalPosition;
    private bool initialized;

    public Vector3 WorldPosition
    {
        get
        {
            if (!initialized) Initialize();
            if (useCustomPlacementAnchor && placementAnchor != null && placementAnchor.IsChildOf(transform))
                return placementAnchor.position;
            return transform.TransformPoint(automaticLocalPosition);
        }
    }

    public float GetSnapDistance(float stageDefault) => snapDistanceOverride >= 0f ? snapDistanceOverride : stageDefault;

    public void Initialize()
    {
        automaticLocalPosition = transform.InverseTransformPoint(CalculateBottomCenter(gameObject));
        initialized = true;
    }

    public static Vector3 CalculateBottomCenter(GameObject piece)
    {
        bool found = false;
        Bounds combined = default;
        foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                || !renderer.enabled || !IsVisiblePart(renderer.transform, piece.transform)) continue;
            // Targets are inactive until placed. Derive world bounds from local bounds
            // so cached Renderer.bounds on a disabled object cannot be stale.
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                Vector3 world = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                if (!found) { combined = new Bounds(world, Vector3.zero); found = true; }
                else combined.Encapsulate(world);
            }
        }
        return found ? new Vector3(combined.center.x, combined.min.y, combined.center.z) : piece.transform.position;
    }

    internal static bool IsVisiblePart(Transform child, Transform piece)
    {
        // Only the target root itself is intentionally inactive before placement.
        for (Transform current = child; current != piece; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }
}
