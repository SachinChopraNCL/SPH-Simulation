using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Instantiation : MonoBehaviour
{
    [SerializeField] private GameObject planePrefab;
    [SerializeField] private GameObject cubePrefab; 
    [SerializeField] private float width;
    [SerializeField] private float length;
    [SerializeField] private float depth;

    private bool canRotate = true;
    private bool tilt = true;
    
    private void Start()
    {
        BuildContainer();

    }


    private void Update()
    {
        if(!LeanTween.isTweening(this.gameObject))
        {
            Vector3 angle = new Vector3(0,0,0);
            tilt = !tilt;
            if (tilt)
                angle = new Vector3(0, 0, 25);
            if (!tilt)
                angle = new Vector3(0, 0, -25);

            LeanTween.rotate(this.gameObject, angle, 4.5f);
        }
    }

    private void BuildContainer()
    {
        GameObject floorPlane =  Instantiate(planePrefab, transform.position, transform.rotation);
        floorPlane.transform.localScale = new Vector3(length, 1, width);
        floorPlane.transform.SetParent(this.transform);

        Vector3 wallPosition = transform.position;
        wallPosition.x += (length * 10) * 0.5f;
        wallPosition.y += depth * 0.5f;
        GameObject positiveXWall = Instantiate(cubePrefab, wallPosition , transform.rotation);
        positiveXWall.transform.localScale = new Vector3(0.5f, depth, width * 10);
        positiveXWall.transform.SetParent(this.transform);
        
        wallPosition = transform.position;
        wallPosition.x -= (length * 10) * 0.5f;
        wallPosition.y += depth * 0.5f;
        GameObject negativeXWall = Instantiate(cubePrefab, wallPosition, transform.rotation);
        negativeXWall.transform.localScale = new Vector3(0.5f, depth, width * 10);
        negativeXWall.transform.SetParent(this.transform);

        wallPosition = transform.position;
        wallPosition.z += (width * 10) * 0.5f;
        wallPosition.y += depth * 0.5f;
        GameObject positiveZWall = Instantiate(cubePrefab, wallPosition, transform.rotation);
        positiveZWall.transform.localScale = new Vector3(length * 10, depth, 0.5f);
        positiveZWall.transform.SetParent(this.transform);

        wallPosition = transform.position;
        wallPosition.z -= (width * 10) * 0.5f;
        wallPosition.y += depth * 0.5f;
        GameObject negativeZWall = Instantiate(cubePrefab, wallPosition, transform.rotation);
        negativeZWall.transform.localScale = new Vector3(length * 10, depth, 0.5f);
        negativeZWall.transform.SetParent(this.transform);
    }
}
