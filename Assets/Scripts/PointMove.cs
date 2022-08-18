using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointMove : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform centralAxis;
    
    public Vector3 Point = new Vector3(2.0f, 2.0f, 2.0f);

    public bool dirc = true;

    void Start()
    {
        centralAxis = GameObject.Find("Ground").GetComponent<Transform>();
        transform.position = Point;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("q"))
        {
            transform.RotateAround(centralAxis.position, Vector3.up, 90.0f);
            dirc = true;
        }
        if (Input.GetKeyDown("e"))
        {
            transform.RotateAround(centralAxis.position, Vector3.up, -90.0f);
            dirc = false;
        }
    }
}
