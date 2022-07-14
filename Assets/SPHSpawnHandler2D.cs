using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ComputeParticleStruct
{
    public Vector2 force;
    public Vector2 velocity;
    public Vector2 position;
    public Vector2 gridPosition;
    public float density;
    public float pressure;
    public int particleId;
}


[System.Serializable]
public struct GridStruct
{
    public int gridVal0;
    public int gridVal1;
    public int gridVal2;
    public int gridVal3;
    public int gridVal4;
    public int particleCount;
}



public class SPHSpawnHandler2D : MonoBehaviour
{
    public bool runFlat;
    public bool runCompute;
    public bool runOptimisedNeighbor;
    public bool runBrute;
    [SerializeField] private int numberOfParticles;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private int damWidth;
    [SerializeField] private float smoothingLength;
    [SerializeField] private float pressureConstant;
    [SerializeField] private float dampingFactor;
    [SerializeField] private float viscosityConstant;
    [SerializeField] private float particleMass;
    [SerializeField] private float restingDensity;
    [SerializeField] private GameObject SPHParticle;
    [SerializeField] private GameObject wall;

    private Vector2 thisPressure;
    private Vector2 thisViscosity;

    private float lengthSquared;
    private float densityKernel;
    private float pressureKernel;
    private float viscosityKernel;

    // CPU Implementation
    private List<SPHParticle> particleList;

    [SerializeField] private List<SPHParticle>[,] particleGrid;


    // GPU Implementation
    [SerializeField] private ComputeShader forceShader;
    [SerializeField] private ComputeShader positionShader;
    [SerializeField] private ComputeParticleStruct[] particleStructs;
    [SerializeField] private int maxDepth;
    [SerializeField] private GridStruct[] computeGrid;
    private int densityLoc;
    private int forceLoc;
    private int positionKernel;
    private ComputeBuffer gridBuffer;
    private ComputeBuffer positionBuffer;


    private void Start()
    {

        height = numberOfParticles / damWidth;


        particleGrid = new List<SPHParticle>[width, height];
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                particleGrid[i, j] = new List<SPHParticle>();
            }
        }

        InitKernels();
        InitWalls();
        particleList = new List<SPHParticle>();


        particleStructs = new ComputeParticleStruct[numberOfParticles];

        for (int i = 0; i < numberOfParticles; i++)
        {
            particleStructs[i].force = new Vector2(0, 0);
            particleStructs[i].velocity = new Vector2(0, 0);
            particleStructs[i].position = new Vector2(0, 0);
        }

        float x = ((width / 2) - (damWidth / 2)) * smoothingLength;
        float y = 0;
        int id = 0;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < damWidth; j++)
            {
                float offset = Random.Range(-smoothingLength * 0.01f, smoothingLength * 0.01f);
                GameObject particleGameObject = Instantiate(SPHParticle, new Vector2(x + offset, y), Quaternion.identity);
                particleGameObject.transform.localScale = new Vector2(smoothingLength * 3f, smoothingLength * 3f);

                SPHParticle particle = particleGameObject.GetComponent<SPHParticle>();

                particle.mass = particleMass;
                particle.force = new Vector2(0, 0);
                particle.velocity = new Vector2(0, 0);
                particle.position = new Vector2(x + offset, y);
                particle.gridPosition = new Vector2(0, 0);
                particle.particleId = id;
                particle.useCompute = runFlat;

                if (runFlat)
                {
                    particleStructs[id].position = new Vector2(x + offset, y);
                    particleStructs[id].velocity = new Vector2(0, 0);
                    particleStructs[id].force = new Vector2(0, 0);
                    particleStructs[id].gridPosition = new Vector2(0, 0);
                    particleStructs[id].pressure = 0;
                    particleStructs[id].particleId = id;
                    particle.structList = particleStructs;
                }

                particleList.Add(particle);
                x += smoothingLength;

                id++;
            }
            y += smoothingLength;
            x = ((width / 2) - (damWidth / 2)) * smoothingLength;
        }

        computeGrid = new GridStruct[width * height];

        for (int i = 0; i < width * height; i++)
        {
            computeGrid[i].particleCount = 0;
            computeGrid[i].gridVal0 = 0;
            computeGrid[i].gridVal1 = 0;
            computeGrid[i].gridVal2 = 0;
            computeGrid[i].gridVal3 = 0;
            computeGrid[i].gridVal4 = 0;
        }

        if (runFlat)
        {
            positionKernel = positionShader.FindKernel("UpdateParticlePosition");
            positionBuffer = new ComputeBuffer(particleStructs.Length, 10 * sizeof(float) + sizeof(int));
            positionShader.SetFloat("height", height);
            positionShader.SetFloat("width", width);
            positionShader.SetFloat("smoothingLength", smoothingLength);

            if (runCompute)
            {
                densityLoc = forceShader.FindKernel("UpdateDensity");
                forceLoc = forceShader.FindKernel("UpdatePressure");
                gridBuffer = new ComputeBuffer(width * height, 6 * sizeof(int));
                forceShader.SetFloat("height", height);
                forceShader.SetFloat("width", width);
                forceShader.SetInt("particleCount", numberOfParticles);
                forceShader.SetFloat("smoothingLength", smoothingLength);
            }


        }
    }

    private void Update()
    {
        if (runFlat)
        {
            for (int i = 0; i < width * height; i++)
            {
                computeGrid[i].particleCount = 0;
                computeGrid[i].gridVal0 = 0;
                computeGrid[i].gridVal1 = 0;
                computeGrid[i].gridVal2 = 0;
                computeGrid[i].gridVal3 = 0;
                computeGrid[i].gridVal4 = 0;
            }

         
            for (int i = 0; i < particleStructs.Length; i++)
            {
                Vector2 gridPosition = particleStructs[i].position / smoothingLength;
                particleStructs[i].gridPosition.x = (int)gridPosition.x;
                particleStructs[i].gridPosition.y = (int)gridPosition.y;
                int index = (int)gridPosition.x + ((int)gridPosition.y * width);
 
                if(computeGrid[index].particleCount == 0 )
                    computeGrid[index].gridVal0 = particleStructs[i].particleId;
                if(computeGrid[index].particleCount == 1)
                    computeGrid[index].gridVal1 = particleStructs[i].particleId;
                if (computeGrid[index].particleCount == 2)
                    computeGrid[index].gridVal2 = particleStructs[i].particleId;
                if (computeGrid[index].particleCount == 3)
                    computeGrid[index].gridVal3 = particleStructs[i].particleId;
                if (computeGrid[index].particleCount == 4)
                    computeGrid[index].gridVal4 = particleStructs[i].particleId;
                computeGrid[index].particleCount++;
            }

            if (runCompute)
            {
                RunForceShader();
                RunPositionShader();
            }
            else
            {
                CalculateComputeDensity();
                CalculateComputeForces();
                RunPositionShader();

            }
        }
        else
        {
            
            if(runBrute)
            {
                OldCalculateDensity();
                OldCalculateForces();
            }
            
            if(runOptimisedNeighbor)
            {
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        particleGrid[i, j] = new List<SPHParticle>();
                    }
                }

                foreach (SPHParticle particle in particleList)
                {
                    Vector2 gridPosition = particle.position / smoothingLength;
                    particle.gridPosition.x = (int)gridPosition.x;
                    particle.gridPosition.y = (int)gridPosition.y;
                    particleGrid[(int)particle.gridPosition.x, (int)particle.gridPosition.y].Add(particle);

                }
                CalculateDensity();
                CalculateForces();
            }
      
            foreach (SPHParticle particle in particleList)
            {
                particle.velocity += Time.deltaTime * particle.force / particle.density;
                particle.position += Time.deltaTime * particle.velocity;
                ApplyBoundaryForce(particle);
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            ApplyRandomForces();
        }
    }

    private void InitKernels()
    {
        lengthSquared = smoothingLength * smoothingLength;
        densityKernel = 315.0f / (65.0f * Mathf.PI * Mathf.Pow(smoothingLength, 9.0f));
        pressureKernel = -45.0f / (Mathf.PI * Mathf.Pow(smoothingLength, 6.0f));
        viscosityKernel = 45.0f / (Mathf.PI * Mathf.Pow(smoothingLength, 6.0f));
    }

    private void InitWalls()
    {
        for (int i = 0; i < width; i++)
        {
            GameObject wallObject = Instantiate(wall, new Vector2(i * smoothingLength, 0 - smoothingLength), Quaternion.identity);
            wallObject.transform.localScale = new Vector2(smoothingLength * 3.5f, smoothingLength * 3.5f);
        }

        for (int i = 0; i < height; i++)
        {
            GameObject wallObject = Instantiate(wall, new Vector2(-smoothingLength, (-smoothingLength) + i * smoothingLength), Quaternion.identity);
            wallObject.transform.localScale = new Vector2(smoothingLength * 3.5f, smoothingLength * 3.5f);

            wallObject = Instantiate(wall, new Vector2(width * smoothingLength, (-smoothingLength) + i * smoothingLength), Quaternion.identity);
            wallObject.transform.localScale = new Vector2(smoothingLength * 3.5f, smoothingLength * 3.5f);
        }
    }


    private void RunForceShader()
    {
        positionBuffer.SetData(particleStructs);
        gridBuffer.SetData(computeGrid);
        forceShader.SetBuffer(densityLoc, "particleBuffer", positionBuffer);
        forceShader.SetBuffer(densityLoc, "gridBuffer", gridBuffer);
        forceShader.Dispatch(densityLoc, particleStructs.Length, 1, 1);
       

        forceShader.SetBuffer(forceLoc , "particleBuffer", positionBuffer);
        forceShader.SetBuffer(forceLoc, "gridBuffer", gridBuffer);
        forceShader.Dispatch(forceLoc, particleStructs.Length, 1, 1);
        positionBuffer.GetData(particleStructs);
    }

    private void RunPositionShader()
    {
        positionBuffer.SetData(particleStructs);
        positionShader.SetBuffer(positionKernel, "particleBuffer", positionBuffer);
        positionShader.SetFloat("deltaTime", Time.deltaTime);
        positionShader.Dispatch(positionKernel, particleStructs.Length, 1, 1);
        positionBuffer.GetData(particleStructs);
    }

    private void ApplyRandomForces()
    {
        foreach (SPHParticle particle in particleList)
        {
            Vector2 newForce = new Vector2(Random.Range(-2, 2f), Random.Range(-2, 2f));
            particle.force = newForce;
        }
    }

    #region GPU implementation

    private void CalculateComputeDensity()
    {
        for (int i = 0; i < particleStructs.Length; i++)
        {
            particleStructs[i].density = 0;

            int x = (int)particleStructs[i].gridPosition.x;
            int y = (int)particleStructs[i].gridPosition.y;

            DensityLocalCompute(particleStructs[i], computeGrid[y * width + x]);
            if (x < width - 1)
                DensityLocalCompute(particleStructs[i], computeGrid[y * width + (x + 1)]);
            if (x > 0)
                DensityLocalCompute(particleStructs[i], computeGrid[y * width + (x - 1)]);

            if (y < height - 1)
                DensityLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + x]);
            if (x < width - 1 && y < height - 1)
                DensityLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + (x + 1)]);
            if (x > 0 && y < height - 1)
                DensityLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + (x - 1)]);

            if (y > 0)
                DensityLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + x]);
            if (x < width - 1 && y > 0)
                DensityLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + (x + 1)]);
            if (x > 0 && y > 0)
                DensityLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + (x - 1)]);
        }
    }

    private void DensityLocalCompute(ComputeParticleStruct thisParticle, GridStruct grid)
    {

        for (int i = 0; i < grid.particleCount; i++)
        {
            int index = 0;

            if (i == 0) index = grid.gridVal0;
            if (i == 1) index = grid.gridVal1;
            if (i == 2) index = grid.gridVal2;
            if (i == 3) index = grid.gridVal3;
            if (i == 4) index = grid.gridVal4;


            Vector2 position = particleStructs[index].position - particleStructs[thisParticle.particleId].position;
            float magnitude = position.magnitude;

            if (magnitude < smoothingLength)
            {
                particleStructs[thisParticle.particleId].density += particleMass * densityKernel * Mathf.Pow(lengthSquared - magnitude, 3.0f);
            }
        }
        particleStructs[thisParticle.particleId].pressure = pressureConstant * (particleStructs[thisParticle.particleId].density - restingDensity);
    }

    private void CalculateComputeForces()
    {
        for (int i = 0; i < particleStructs.Length; i++)
        {
            int x = (int)particleStructs[i].gridPosition.x;
            int y = (int)particleStructs[i].gridPosition.y;

            thisPressure = new Vector2(0, 0);
            thisViscosity = new Vector2(0, 0);

            ForcesLocalCompute(particleStructs[i], computeGrid[y * width + x]);
            if (x < width - 1)
                ForcesLocalCompute(particleStructs[i], computeGrid[y * width + (x + 1)]);
            if (x > 0)
                ForcesLocalCompute(particleStructs[i], computeGrid[y * width + (x - 1)]);

            if (y < height - 1)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + x]);
            if (x < width - 1 && y < height - 1)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + (x + 1)]);
            if (x > 0 && y < height - 1)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y + 1) * width + (x - 1)]);

            if (y > 0)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + x]);
            if (x < width - 1 && y > 0)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + (x + 1)]);
            if (x > 0 && y > 0)
                ForcesLocalCompute(particleStructs[i], computeGrid[(y - 1) * width + (x - 1)]);

            Vector2 gravity = new Vector2(0, -30f * particleMass) * particleStructs[i].density;
            particleStructs[i].force = thisPressure + thisViscosity + gravity;
        }
    }

    private void ForcesLocalCompute(ComputeParticleStruct thisParticle, GridStruct grid)
    { 
        for (int i = 0; i < grid.particleCount; i++)
        {
            int index = 0;

            if (i == 0) index = grid.gridVal0;
            if (i == 1) index = grid.gridVal1;
            if (i == 2) index = grid.gridVal2;
            if (i == 3) index = grid.gridVal3;
            if (i == 4) index = grid.gridVal4;

            if (thisParticle.particleId == index) continue;

            Vector2 position = particleStructs[index].position - particleStructs[thisParticle.particleId].position;
            float magnitude = position.magnitude;

            if (magnitude < smoothingLength)
            {
                thisPressure += -position.normalized * particleMass * (particleStructs[thisParticle.particleId].pressure + particleStructs[index].pressure) / (2.0f * particleStructs[index].density) * pressureKernel * Mathf.Pow(smoothingLength - magnitude, 2.0f);
                thisViscosity += viscosityConstant * particleMass * (particleStructs[index].velocity - particleStructs[thisParticle.particleId].velocity) / particleStructs[index].density * viscosityKernel * (smoothingLength - magnitude);
            }
        }

    }

    #endregion


    #region CPU implementation

    private void CalculateDensity()
    {
       foreach(SPHParticle thisParticle in particleList)
       {
            thisParticle.density = 0;

            int x = (int)thisParticle.gridPosition.x;
            int y = (int)thisParticle.gridPosition.y;

            DensityLocal(thisParticle, particleGrid[x, y]);
            if(x < width - 1)
                DensityLocal(thisParticle, particleGrid[x + 1, y]);
            if (x > 0)
                DensityLocal(thisParticle, particleGrid[x - 1, y]);
            
            if(y < height - 1)
                DensityLocal(thisParticle, particleGrid[x, y + 1]);
            if(x < width - 1 && y < height - 1)
                DensityLocal(thisParticle, particleGrid[x + 1, y + 1]);
            if(x > 0 &&  y < height - 1)
                DensityLocal(thisParticle, particleGrid[x - 1, y + 1]);

            if(y > 0)
                DensityLocal(thisParticle, particleGrid[x, y - 1]);
            if(x < width - 1 && y > 0)
                DensityLocal(thisParticle, particleGrid[x + 1, y - 1]);
            if(x > 0 && y > 0)
                DensityLocal(thisParticle, particleGrid[x - 1, y - 1]);

            particleStructs[thisParticle.particleId].density = thisParticle.density;
       }
    }

    private void DensityLocal(SPHParticle thisParticle, List<SPHParticle> gridList)
    {
        foreach (SPHParticle otherParticle in gridList)
        {
            Vector2 position = otherParticle.position - thisParticle.position;
            float magnitude = position.magnitude;

            if (magnitude < smoothingLength)
            {
                thisParticle.density += thisParticle.mass * densityKernel * Mathf.Pow(lengthSquared - magnitude, 3.0f);
            }

        }
        thisParticle.pressure = pressureConstant * (thisParticle.density - restingDensity);

    }

    private void CalculateForces()
    {
        foreach(SPHParticle thisParticle in particleList)
        {
            int x = (int)thisParticle.gridPosition.x;
            int y = (int)thisParticle.gridPosition.y;

            thisPressure = new Vector2(0, 0);
            thisViscosity = new Vector2(0, 0);

            ForcesLocal(thisParticle, particleGrid[x, y]);
            if (x < width - 1)
                ForcesLocal(thisParticle, particleGrid[x + 1, y]);
            if (x > 0)
                ForcesLocal(thisParticle, particleGrid[x - 1, y]);

            if (y < height - 1)
                ForcesLocal(thisParticle, particleGrid[x, y + 1]);
            if (x < width - 1 && y < height - 1)
                ForcesLocal(thisParticle, particleGrid[x + 1, y + 1]);
            if (x > 0 && y < height - 1)
                ForcesLocal(thisParticle, particleGrid[x - 1, y + 1]);

            if (y > 0)
                ForcesLocal(thisParticle, particleGrid[x, y - 1]);
            if (x < width - 1 && y > 0)
                ForcesLocal(thisParticle, particleGrid[x + 1, y - 1]);
            if (x > 0 && y > 0)
                ForcesLocal(thisParticle, particleGrid[x - 1, y - 1]);

            Vector2 gravity = ApplyGravity(thisParticle) * thisParticle.density;
            thisParticle.force = thisPressure + thisViscosity + gravity;
            particleStructs[thisParticle.particleId].force = thisPressure + thisViscosity + gravity;
        }
    }

    private void ForcesLocal(SPHParticle thisParticle, List<SPHParticle> gridList)
    {
        foreach (SPHParticle otherParticle in gridList)
        {

            if (thisParticle == otherParticle) continue;

            Vector2 position = otherParticle.position - thisParticle.position;
            float magnitude = position.magnitude;

            if (magnitude < smoothingLength)
            {
                thisPressure += -position.normalized * thisParticle.mass * (thisParticle.pressure + otherParticle.pressure) / (2.0f * otherParticle.density) * pressureKernel * Mathf.Pow(smoothingLength - magnitude, 2.0f);
                thisViscosity += viscosityConstant * thisParticle.mass * (otherParticle.velocity - thisParticle.velocity) / otherParticle.density * viscosityKernel * (smoothingLength - magnitude);
            }
        }

 
    }

    private Vector2 ApplyGravity(SPHParticle particle)
    {
        return new Vector2(0, -9.8f * particle.mass);
    }

    private void ApplyBoundaryForce(SPHParticle particle)
    {
        if(particle.position.y < 0)
        {
            particle.position.y = 0;
            particle.velocity.y = -particle.velocity.y * dampingFactor;
        }

        if (particle.position.y > (smoothingLength * height) - 0.2f)
        {
            particle.position.y = (smoothingLength * height) - 0.2f;
            particle.velocity.y = -particle.velocity.y * dampingFactor;
        }

        if (particle.position.x < 0)
        {
            particle.position.x = 0;
            particle.velocity.x = -particle.velocity.x * dampingFactor;
        }

        if (particle.position.x > (smoothingLength * width) - smoothingLength)
        {
            particle.position.x = (smoothingLength * width) - smoothingLength;
            particle.velocity.x = -particle.velocity.x * dampingFactor;
        }
    }
    #endregion

    #region Old computations - unoptimised

    private void OldCalculateDensity()
    {
        foreach (SPHParticle thisParticle in particleList)
        {
            thisParticle.density = 0;

            foreach (SPHParticle otherParticle in particleList)
            {
                Vector2 position = otherParticle.position - thisParticle.position;
                float magnitude = position.magnitude;

                if (magnitude < smoothingLength)
                {
                    thisParticle.density += thisParticle.mass * densityKernel * Mathf.Pow(lengthSquared - magnitude, 3.0f);
                }

            }
            thisParticle.pressure = pressureConstant * (thisParticle.density - restingDensity);

        }
    }

    private void OldCalculateForces()
    {
        foreach (SPHParticle thisParticle in particleList)
        {
            Vector2 pressure = new Vector2(0, 0);
            Vector2 viscosity = new Vector2(0, 0);

            foreach (SPHParticle otherParticle in particleList)
            {

                if (thisParticle == otherParticle) continue;

                Vector2 position = otherParticle.position - thisParticle.position;
                float magnitude = position.magnitude;

                if (magnitude < smoothingLength)
                {
                    pressure += -position.normalized * thisParticle.mass * (thisParticle.pressure + otherParticle.pressure) / (2.0f * otherParticle.density) * pressureKernel * Mathf.Pow(smoothingLength - magnitude, 2.0f);
                    viscosity += viscosityConstant * thisParticle.mass * (otherParticle.velocity - thisParticle.velocity) / otherParticle.density * viscosityKernel * (smoothingLength - magnitude);
                }
            }

            Vector2 gravity = ApplyGravity(thisParticle) * thisParticle.density;
            thisParticle.force = pressure + viscosity + gravity;

        }
    }
    #endregion Calculations - Unoptimised
}
