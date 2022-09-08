using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointMove : MonoBehaviour
{
    public Vector3 Point;

    public bool cam_moved = true;
    public Vector3 dir;

    void Start()
    {
        transform.position = Camera.main.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("q"))
        {
            Left();
        }
        if (Input.GetKeyDown("e"))
        {
            Right();
        }
    }

    private void Left()
    {
        transform.position = Camera.main.transform.position;
        GameManager.Instance.camera_dir = (GameManager.Instance.camera_dir + 3) % 4;
        Point = GameManager.Instance.ground.transform.position;
        Point.y = transform.position.y;
        transform.RotateAround(Point, Vector3.up, 90.0f);
        dir = Vector3.up;
        cam_moved = false;
    }

    private void Right()
    {
        transform.position = Camera.main.transform.position;
        GameManager.Instance.camera_dir = (GameManager.Instance.camera_dir + 1) % 4;
        Point = GameManager.Instance.ground.transform.position;
        Point.y = transform.position.y;
        transform.RotateAround(Point, Vector3.down, 90.0f);
        cam_moved = false;
        dir = Vector3.down;
    }

}
