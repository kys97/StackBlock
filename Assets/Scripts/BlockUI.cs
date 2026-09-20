using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private string key;
    [SerializeField] private GameObject move_prefab;
    private GameManager owner;
    private Image dragImage;
    private Coroutine pulse;
    private bool isValidPlacement;

    public void SetKey(string pieceKey) => key = pieceKey;

    private void Start()
    {
        owner = GameManager.Instance;
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
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragImage == null) return;
        if (!owner.CanAcceptInput) { ClearDrag(); return; }
        dragImage.transform.position = eventData.position;
        dragImage.transform.localScale = transform.localScale;
        GameManager.Block piece = owner.Puzzle[key];
        bool valid = !piece.IsComplete && piece.Surface.activeInHierarchy
            && piece.Direction == owner.CurrentDirection
            && Vector2.Distance(piece.ScreenPosition, eventData.position) <= owner.CurrentStageData.SnapDistancePixels;
        if (valid == isValidPlacement) return;
        isValidPlacement = valid;
        if (pulse != null) StopCoroutine(pulse);
        pulse = valid ? StartCoroutine(PulseDragImage()) : null;
        if (!valid) { Color color = dragImage.color; color.a = 1f; dragImage.color = color; }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragImage == null) return;
        // Rotation or expiry can occur during a drag, so validate its final position again.
        bool placed = owner != null && owner.TryCompletePiece(key, eventData.position);
        ClearDrag();
        if (placed) Destroy(gameObject);
    }

    private IEnumerator PulseDragImage()
    {
        float step = 0.1f;
        Color color = dragImage.color;
        while (true)
        {
            if (color.a <= 0.2f || color.a >= 1f) step *= -1f;
            color.a += step;
            dragImage.color = color;
            yield return new WaitForSeconds(0.07f);
        }
    }

    private void ClearDrag()
    {
        if (pulse != null) StopCoroutine(pulse);
        pulse = null;
        isValidPlacement = false;
        if (dragImage != null) Destroy(dragImage.gameObject);
        dragImage = null;
    }

    private void OnDisable() => ClearDrag();
}
