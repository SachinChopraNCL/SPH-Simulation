using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SPHParticle : MonoBehaviour
{

    public Vector2 force;
    public Vector2 velocity;
    public Vector2 position;
    public Vector2 gridPosition;

    public ComputeParticleStruct[] structList;

    public SpriteRenderer spriteRenderer;

    public float magnitude;
    public float density = 0;
    public float mass;
    public float pressure = 0;

    public int particleId;
    public bool useCompute;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if(useCompute)
        {     
            force = structList[particleId].force;
            velocity = structList[particleId].velocity;
            position = structList[particleId].position;
        }
        this.transform.position = position;
        float colourVal = velocity.magnitude / 11.5f;
        magnitude = colourVal;
        colourVal = Mathf.Clamp(colourVal, 0.35f, 1f);
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, new Vector4(0, colourVal, colourVal, 1), Time.deltaTime * 0.25f);
    }
}
