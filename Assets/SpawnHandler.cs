using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnHandler : MonoBehaviour
{
    [SerializeField] private GameObject spawnObject;
    [SerializeField] private int numberOfParticles;
    [SerializeField] private float timeToSpawn;
    private float timer = 0;
    private int currentNumberOfParticles;
    void Update()
    {
        timer += Time.deltaTime;

        if(timer > timeToSpawn && currentNumberOfParticles < numberOfParticles)
        {
            GameObject currentParticle =  Instantiate(spawnObject, transform.position, transform.rotation);
            transform.rotation = Random.rotation;
            currentParticle.GetComponent<Rigidbody>().AddForce(-transform.up * 10);
            currentNumberOfParticles++;
            timer = 0;
        }
        
    }
}
