using UnityEngine;

public class BlockObj : MonoBehaviour
{
    private string key;

    private void OnEnable()
    {
        if (GameManager.Instance.HasStarted)
        {
            int n = GameManager.Instance.Puzzle[key].Object.transform.childCount;
            if (n > 0)
                for (int i = 0; i < n; i++)
                {
                    string k = GameManager.Instance.Puzzle[key].Object.transform.GetChild(i).name;
                    GameManager.Instance.Puzzle[k].Surface.SetActive(true);
                }
        }
    }

    public void SetKey(string n) { key = n; }
}
