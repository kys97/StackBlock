using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{ 
    public Transform centralAxis;
    public Transform cameraPoint;

    private PointMove pointMove;

    public Vector3 Point = new Vector3(1.0f, 1.0f, 0.7f);
    void CamMove()
    {
        if (cameraPoint.position != transform.position && pointMove.dirc == true)
        {
            transform.RotateAround(centralAxis.position, Vector3.up, 1.0f);
            transform.LookAt(centralAxis);
        }
        else if (cameraPoint.position != transform.position && pointMove.dirc == false)
        {
            transform.RotateAround(centralAxis.position, Vector3.up, -1.0f);
            transform.LookAt(centralAxis);
        }
        
    }

    // Start is called before the first frame update
    void Start()
    {
        centralAxis = GameObject.Find("centralAxis").GetComponent<Transform>();
        cameraPoint = GameObject.Find("cameraPoint").GetComponent<Transform>();
        pointMove = GameObject.Find("cameraPoint").GetComponent<PointMove>();
        transform.position = Point;
        transform.LookAt(centralAxis);
        
    }
        
    // Update is called once per frame
    void Update()
    {
        CamMove();
    }
}


//구버전
/*
public Transform centralAxis;
public float camSpeed;
private float mouseX;
private float mouseY;
public Camera getCamera;

public Vector3 offset = new Vector3(0, 1f, -1f);
    
public float currentZoom = 30.0f;
  
private float minZoom = 20.0f;
private float maxZoom = 60.0f;

private float scrollSpeed = 2.0f;
    
void CamMove() 
{
    // 카메라 회전
    if (Input.GetMouseButton(1))
    {
            
        mouseX += Input.GetAxis("Mouse X");
        mouseY += Input.GetAxis("Mouse Y")* -1;

        var rotation = centralAxis.rotation;
        rotation = Quaternion.Euler(new Vector3(rotation.x + mouseY, rotation.y + mouseX, 0) * camSpeed);
        centralAxis.rotation = rotation;
    }
        
    // 카메라 줌
    if (Input.GetAxis("Mouse ScrollWheel") != 0)
    {
        currentZoom = Input.GetAxis("Mouse ScrollWheel");
        if(getCamera.fieldOfView >= 20.0f && Input.GetAxis("Mouse ScrollWheel") <= 0) getCamera.fieldOfView += currentZoom + scrollSpeed;
        else if(getCamera.fieldOfView <= 60.0f && Input.GetAxis("Mouse ScrollWheel") >= 0) getCamera.fieldOfView -= currentZoom + scrollSpeed;

        if (getCamera.fieldOfView <= 20.0f) getCamera.fieldOfView = 20.0f;
        else if (getCamera.fieldOfView >= 60.0f) getCamera.fieldOfView = 60.0f;
            
        getCamera.transform.LookAt(centralAxis.transform);
    }
}*/