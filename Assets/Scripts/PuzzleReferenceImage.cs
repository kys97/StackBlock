using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PuzzleReferenceImage : MonoBehaviour
{
    private Image referenceImage;
    private GameSession session;
    private void OnEnable()
    {
        if (session == null) return;
        session.SelectionChanged += RefreshImage;
        RefreshImage();
    }

    private void Start()
    {
        referenceImage = GetComponent<Image>();
        referenceImage.preserveAspect = true;
        referenceImage.raycastTarget = false;
        session = GameManager.Instance.Session;
        session.SelectionChanged += RefreshImage;
        RefreshImage();
    }

    private void OnDisable()
    {
        if (session != null) session.SelectionChanged -= RefreshImage;
    }

    private void RefreshImage()
    {
        if (session == null)
        {
            referenceImage.enabled = false;
            return;
        }

        referenceImage.sprite = session.CurrentStageData.PreviewImage;
        referenceImage.enabled = referenceImage.sprite != null;
        if (referenceImage.sprite == null)
            Debug.LogWarning("No reference image for " + session.Topic + "/" + session.Stage, this);
    }
}
