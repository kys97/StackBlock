using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointMove : MonoBehaviour
{
    // Start is called before the first frame update
    public Transform PointTransform;
    private Transform _transform;
    void Start()
    {
        PointTransform = GameObject.Find("GetObject").GetComponent<Transform>();
        _transform = GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        _transform.transform.position = PointTransform.transform.position;
    }
}
