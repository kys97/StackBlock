using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private string key;
    [SerializeField] private GameObject move_prefab;
    [Header("Placement Hint")]
    [SerializeField] private Color validPlacementColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Smooth pulse speed in radians/second. 4 gives one cycle about every 1.57 seconds.")]
    [SerializeField, Min(0.1f)] private float hintBlinkSpeed = 4f;
    private GameManager owner;
    private Image dragImage;
    private Camera puzzleCamera;
    private PuzzleCameraController cameraController;
    private Camera uiCamera;
    private RectTransform pieceListArea;
    private GameObject[] previewSurfaces;
    private PuzzleDragRaycaster dragRaycaster;
    private MeshFilter playAreaMesh;
    private PuzzleDragPreview dragPreview;
    private GameObject[] previewOccluders;
    private PuzzlePreviewOcclusion previewOcclusion;
    private Vector2 dragPosition;
    private bool positionDirty;
    private bool hasPlacementHit;

    public void SetKey(string pieceKey) => key = pieceKey;

    private void Start()
    {
        owner = GameManager.Instance;
        owner.PauseChanged += OnPauseChanged;
        puzzleCamera = Camera.main;
        if (puzzleCamera != null) cameraController = puzzleCamera.GetComponent<PuzzleCameraController>();
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        pieceListArea = scroll != null
            ? (scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform)
            : (RectTransform)owner.PieceContent.transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var surfaces = new List<GameObject> { owner.Ground };
        foreach (GameManager.Block piece in owner.Puzzle.Values)
            surfaces.Add(piece.Surface);
        previewSurfaces = surfaces.ToArray();
        var occluders = new List<GameObject> { owner.Ground };
        foreach (GameManager.Block piece in owner.Puzzle.Values)
            if (piece.Object != owner.Puzzle[key].Object) occluders.Add(piece.Object);
        previewOccluders = occluders.ToArray();
        // All stage Ground roots hold the board mesh; their children include the background slab.
        playAreaMesh = owner.Ground.GetComponent<MeshFilter>();
        Vector2 size = owner.Puzzle[key].Image.bounds.size;
        transform.localScale = size / Mathf.Max(size.x, size.y);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || !owner.CanAcceptInput || owner.Puzzle[key].IsComplete || dragImage != null) return;
        owner.DragCanvas.SetActive(true);
        dragImage = Instantiate(move_prefab, owner.DragCanvas.transform).GetComponent<Image>();
        dragImage.sprite = owner.Puzzle[key].Image;
        dragImage.transform.position = transform.position;
        dragImage.transform.localScale = transform.localScale;
        dragImage.raycastTarget = false;
        dragPosition = eventData.position;
        positionDirty = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragImage == null) return;
        if (!owner.CanAcceptInput) { ClearDrag(); return; }
        RecordPointer(eventData.position);
        dragImage.transform.position = dragPosition;
        dragImage.transform.localScale = transform.localScale;
        UpdateDragVisual(0f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragImage == null) return;
        // Release can contain the last pointer movement without an OnDrag callback.
        if (owner == null || !owner.CanAcceptInput) { ClearDrag(); return; }
        RecordPointer(eventData.position);
        UpdateDragVisual(0f);
        bool placed = hasPlacementHit && dragPreview != null && dragPreview.IsVisible
            && owner.TryCompletePiece(key, dragPreview.AnchorWorldPosition);
        ClearDrag();
        if (placed) Destroy(gameObject);
    }

    private void ClearDrag()
    {
        dragPreview?.Dispose();
        dragPreview = null;
        dragRaycaster?.Dispose();
        dragRaycaster = null;
        previewOcclusion?.Dispose();
        previewOcclusion = null;
        if (dragImage != null)
        {
            dragImage.gameObject.SetActive(false);
            Destroy(dragImage.gameObject);
        }
        dragImage = null;
        positionDirty = false;
        hasPlacementHit = false;
    }

    private void LateUpdate()
    {
        if (dragImage == null) return;
        // Expiry/scene exit cancels even if the pointer has stopped moving.
        if (owner == null || !owner.CanAcceptInput) { ClearDrag(); return; }
        // PuzzleCameraController finishes its pose in Update, before this LateUpdate.
        // Refresh rotation even with a stationary pointer or an in-progress turn.
        UpdateDragVisual(Time.deltaTime);
    }

    private void UpdateDragVisual(float deltaTime)
    {
        bool overList = RectTransformUtility.RectangleContainsScreenPoint(pieceListArea, dragPosition, uiCamera);
        if (overList)
        {
            hasPlacementHit = false;
            dragPreview?.Hide();
            dragImage.enabled = true;
        }
        else if (positionDirty && (cameraController == null || !cameraController.IsRotating
            || dragPreview == null || !dragPreview.HasPlacementPosition))
        {
            // Leaving the list must show a preview even without a board hit,
            // including the first exit during a camera turn. Only board hits
            // can participate in placement or hint evaluation.
            RaycastHit hit = default;
            hasPlacementHit = (cameraController == null || !cameraController.IsRotating)
                && TryGetPreviewHit(out hit);
            if (hasPlacementHit || TryGetFloatingPreviewHit(out hit))
            {
                if (dragPreview == null)
                {
                    GameManager.Block piece = owner.Puzzle[key];
                    dragPreview = cameraController != null && piece.Direction >= 0 && piece.Direction < 4
                        ? new PuzzleDragPreview(piece.Object, cameraController.GetDirectionRotation(piece.Direction))
                        : new PuzzleDragPreview(piece.Object);
                }
                dragPreview.Show(hit);
                dragImage.enabled = false;
            }
            else
            {
                dragPreview?.Hide();
                dragImage.enabled = true;
            }
        }
        // Consume movement even during rotation. Ending a turn cannot apply an old
        // pointer event; a new screen position is required before projecting again.
        positionDirty = false;
        if (dragPreview != null)
        {
            // Outside the positionDirty / IsRotating gate: only position projection freezes.
            dragPreview.UpdateCameraRelativeRotation(puzzleCamera.transform.rotation);
            if (dragPreview.HasPlacementPosition)
            {
                if (previewOcclusion == null) previewOcclusion = new PuzzlePreviewOcclusion(previewOccluders);
                Vector3 offset = Vector3.zero;
                bool canDisplay = dragPreview.TryGetCameraBounds(puzzleCamera, out Bounds bounds)
                    && previewOcclusion.TryGetOffset(puzzleCamera, bounds, dragPreview.AnchorWorldPosition, out offset);
                dragPreview.ApplyVisibilityOffset(offset, canDisplay);
                dragImage.enabled = !canDisplay;
            }
            dragPreview.UpdatePlacementHint(hasPlacementHit && dragPreview.IsVisible
                && owner.CanPlacePiece(key, dragPreview.AnchorWorldPosition),
                validPlacementColor, hintBlinkSpeed, deltaTime);
        }
    }

    private void RecordPointer(Vector2 position)
    {
        if (position == dragPosition) return;
        dragPosition = position;
        positionDirty = true;
    }

    private bool TryGetPreviewHit(out RaycastHit closest)
    {
        closest = default;
        if (puzzleCamera == null || !puzzleCamera.pixelRect.Contains(dragPosition)) return false;
        if (dragRaycaster == null) dragRaycaster = new PuzzleDragRaycaster(previewSurfaces, playAreaMesh);
        return dragRaycaster.Raycast(puzzleCamera.ScreenPointToRay(dragPosition), puzzleCamera.farClipPlane,
            out closest, owner.Puzzle[key].Placement.WorldPosition.y);
    }

    private bool TryGetFloatingPreviewHit(out RaycastHit hit)
    {
        hit = default;
        if (puzzleCamera == null || !puzzleCamera.pixelRect.Contains(dragPosition)) return false;
        // A camera-facing plane at the piece's usual depth gives the preview
        // a finite world position outside the board, without changing its scale
        // or projecting a nearly horizontal ray far behind the map.
        var plane = new Plane(puzzleCamera.transform.forward, owner.Puzzle[key].Placement.WorldPosition);
        Ray ray = puzzleCamera.ScreenPointToRay(dragPosition);
        if (!plane.Raycast(ray, out float distance) || distance > puzzleCamera.farClipPlane) return false;
        hit = new RaycastHit { point = ray.GetPoint(distance), distance = distance };
        return true;
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused) ClearDrag();
    }

    private void OnEnable()
    {
        if (owner != null) owner.PauseChanged += OnPauseChanged;
    }

    private void OnDisable()
    {
        if (owner != null) owner.PauseChanged -= OnPauseChanged;
        ClearDrag();
    }
}
