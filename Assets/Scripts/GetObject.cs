using System.Collections;
using System.Collections.Generic;
using UnityEditor.Timeline.Actions;
using UnityEngine;

public class GetObject : MonoBehaviour
{
    
    // Start is called before the first frame update
    public Camera getCamera;
    public RaycastHit Hit;
    void Start()
    {
        getCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = getCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out Hit))
            {
                 transform.position = Hit.transform.position;
            }
        }
    }
}
