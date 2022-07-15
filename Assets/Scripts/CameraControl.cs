using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public Transform centralAxis;
    public float camSpeed;
    private float mouseX;
    private float mouseY;
    public Camera getCamera;

    public Vector3 offset = new Vector3(0, 1.0f, -1.0f);
    
    public float currentZoom = 7.0f;
  
    public float minZoom = 5.0f;
    public float maxZoom = 10.0f;
    
    void CamMove() {
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
            currentZoom -= Input.GetAxis("Mouse ScrollWheel");
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            getCamera.transform.position = getCamera.transform.position + offset * currentZoom;
            getCamera.transform.LookAt(centralAxis.transform);
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        getCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
    }
    
    // Update is called once per frame
    void Update()
    {
        CamMove();
    }
}
