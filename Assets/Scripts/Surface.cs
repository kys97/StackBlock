using UnityEngine;

public class Surface : MonoBehaviour
{
    [SerializeField] private string key;
    private GameManager owner;

    public void SetKey(string n)
    {
        key = n;
        owner = GameManager.Instance;
    }

    private bool TryGetBlock(out GameManager.Block block)
    {
        block = null;
        // Enable can precede SetKey; disable can follow owner destruction.
        // Never search for a replacement owner during scene teardown.
        return owner != null && owner.HasStarted && key != null
            && owner.Puzzle.TryGetValue(key, out block) && block != null
            && block.Surface == gameObject;
    }

    private void OnEnable()
    {
        if (!TryGetBlock(out GameManager.Block block))
            return;

        Camera puzzleCamera = Camera.main;
        if (owner.CurrentDirection == block.Direction && puzzleCamera != null && block.Object != null)
            block.Position(puzzleCamera.WorldToScreenPoint(block.Object.transform.position));
        else
            block.Position(new Vector2(-3000, -3000));
    }

    private void OnDisable()
    {
        if (TryGetBlock(out GameManager.Block block))
            block.Position(new Vector2(-3000, -3000));
    }
}
