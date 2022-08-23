using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointMove : MonoBehaviour
{
    public Vector3 Point;

    public bool dirc = true;
    public int dir = 0;

    void Start()
    {
        //현재 Main카메라 위치로 위치 옮기기
        transform.position = Camera.main.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("q"))
        {
            //Point에 Ground 위치 가져오기
            Point = GameManager.Instance.ground.transform.position;
            Point.y = transform.position.y;
            transform.RotateAround(Point, Vector3.up, 90.0f);
            dir = 1;
            dirc = true;
        }
        if (Input.GetKeyDown("e"))
        {
            //Point에 Ground 위치 가져오기
            Point = GameManager.Instance.ground.transform.position;
            Point.y = transform.position.y;
            transform.RotateAround(Point, Vector3.down, 90.0f);
            dirc = false;
            dir = 2;
        }
    }

    public void Left()
    {
        Point = GameManager.Instance.ground.transform.position;
        Point.y = transform.position.y;
        transform.RotateAround(Point, Vector3.up, 90.0f);
        dir = 1;
        dirc = true;
    }

    public void Right()
    {
        Point = GameManager.Instance.ground.transform.position;
        Point.y = transform.position.y;
        transform.RotateAround(Point, Vector3.down, 90.0f);
        dirc = false;
        dir = 2;
    }

}
