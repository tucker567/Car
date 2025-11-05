using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int depth = 20;
    public int width = 256;
    public int height = 256;
    public float scale = 20f;
    public bool useRandomSeed = true;
    public int seed = 0;
    
    [Header("Sand Dune Settings")]
    [Range(1, 8)]
    public int octaves = 4;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    [Range(1f, 4f)]
    public float lacunarity = 2f;
    [Range(0f, 1f)]
    public float duneHeight = 0.3f;
    [Range(0f, 2f)]
    public float windDirection = 0.3f; // Creates asymmetrical dunes
    [Range(0.1f, 3f)]
    public float duneStretch = 1.5f; // Elongates dunes in wind direction
    
    private float offsetX;
    private float offsetY;

    // Start is called before the first frame update
    void Start()
    {
        // Generate random seed if useRandomSeed is enabled
        if (useRandomSeed)
        {
            seed = Random.Range(0, 10000);
        }
        
        // Generate random offsets based on seed
        Random.InitState(seed);
        offsetX = Random.Range(0f, 1000f);
        offsetY = Random.Range(0f, 1000f);
        
        Terrain terrain = GetComponent<Terrain>();
        terrain.terrainData = GenerateTerrain(terrain.terrainData);

    }

    // Update is called once per frame
    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        terrainData.heightmapResolution = width + 1;

        terrainData.size = new Vector3(width, depth, height);

        terrainData.SetHeights(0, 0, GenerateHeights());

        return terrainData;
    }

    // Generate height map using Perlin noise
    float[,] GenerateHeights()
    {
        float[,] heights = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heights[x, y] = CalculateHeight(x, y);
            }
        }

        return heights;
    }

    // Calculate height using multi-octave Perlin noise for sand dunes
    float CalculateHeight(int x, int y)
    {
        float xCoord = (float)x / width;
        float yCoord = (float)y / this.height;
        
        // Apply wind direction stretching
        float stretchedX = xCoord * duneStretch;
        float stretchedY = yCoord;
        
        float terrainHeight = 0f;
        float amplitude = 1f;
        float frequency = scale;
        
        // Generate multiple octaves for realistic sand dune texture
        for (int i = 0; i < octaves; i++)
        {
            float noiseValue = Mathf.PerlinNoise(
                (stretchedX * frequency) + offsetX,
                (stretchedY * frequency) + offsetY
            );
            
            terrainHeight += noiseValue * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        
        // Normalize height
        terrainHeight /= (2f - 1f / Mathf.Pow(2f, octaves - 1));
        
        // Apply sand dune characteristics
        terrainHeight = ApplyDuneShape(terrainHeight, xCoord, yCoord);
        
        return Mathf.Clamp01(terrainHeight * duneHeight);
    }
    
    // Apply sand dune specific shaping
    float ApplyDuneShape(float height, float x, float y)
    {
        // Create asymmetrical slopes (gentle windward, steep leeward)
        float windEffect = Mathf.Sin(x * Mathf.PI * 2f + windDirection) * 0.1f;
        height += windEffect;
        
        // Smooth out the terrain for that rolling sand dune look
        height = Mathf.Pow(height, 1.2f);
        
        // Add some randomness to break up patterns
        float randomness = Mathf.PerlinNoise(x * 50f + offsetX, y * 50f + offsetY) * 0.05f;
        height += randomness;
        
        return height;
    }
    
}
