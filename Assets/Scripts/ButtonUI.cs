using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonUI : MonoBehaviour
{
    private Vector3 size;

    private void Start()
    {
        size = transform.localScale;
    }

    public void ButtonUp()
    {
        transform.localScale *= 1.2f;
    }

    public void ButtonDown()
    {
        transform.localScale = size;
    }
}
