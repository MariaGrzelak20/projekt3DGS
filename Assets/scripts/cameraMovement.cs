using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cameraMovement : MonoBehaviour
{
    /// <summary>
    /// Camera movement using mouse
    ///  normal camera movement - rotate camera
    ///  camera movement while holding ppm - move in space
    /// </summary>
    // Start is called before the first frame update
    Vector3 movement;
    Vector3 cameraRotation;

    private void Start()
    {
        movement = transform.localPosition;
    }
    void Update()
    {
        if (Input.GetKey(KeyCode.W)) 
        {
        
        }
        if (Input.GetMouseButton(0)) { }
    }
}
