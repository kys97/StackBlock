using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockObj : MonoBehaviour
{
    private string key;

    private void OnEnable()
    {
        if (GameManager.Instance.start)
        {
            int n = GameManager.Instance.Puzzle[key].block.transform.childCount;
            if (n > 0)
                for (int i = 0; i < n; i++)
                {
                    string k = GameManager.Instance.Puzzle[key].block.transform.GetChild(i).name;
                    GameManager.Instance.Puzzle[k].surface.SetActive(true);
                }
        }
    }

    public void SetKey(string n) { key = n; }
}
