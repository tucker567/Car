using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int depth = 200;  // Increased height range for taller dunes
    public int width = 512;  // Heightmap resolution
    public int height = 512; // Heightmap resolution
    public float scale = 0.8f;  // Much smaller scale = bigger spacing between dunes
    public bool useRandomSeed = true;
    public int seed = 0;
    
    [Header("Terrain World Size")]
    public float terrainWidth = 2000f;  // Actual world width
    public float terrainLength = 2000f; // Actual world length
    
    [Header("Sand Dune Settings")]
    [Range(1, 8)]
    public int octaves = 3;  // Reduced for smoother, larger features
    [Range(0f, 1f)]
    public float persistence = 0.4f;  // Lower for smoother transitions
    [Range(1f, 4f)]
    public float lacunarity = 2f; // Standard value, not too jagged
    [Range(0f, 3f)]
    public float duneHeight = 1.8f;  // Multiplier applied to final normalized height (keep an eye on this, >1 can saturate)
    [Range(0f, 1f)]
    public float windDirection = 0.3f; // Creates asymmetrical dunes, increasing it makes windward side gentler
    [Range(0.1f, 3f)]
    public float duneStretch = 1.5f; // Elongates dunes in wind direction

    [Header("Biome Settings")]
    [Tooltip("Lower = larger biome regions. Try 0.02 - 0.08 for large patches.")]
    public float biomeScale = 0.02f; // Controls size of biome regions (smaller => bigger regions)
    [Tooltip("Below this value becomes salt-flat. 0.5 is neutral.")]
    [Range(0f,1f)]
    public float biomeThreshold = 0.5f; // Threshold for deciding biomes
    [Tooltip("How soft the transition between biomes is (0 = hard edge).")]
    [Range(0f,0.5f)]
    public float biomeTransition = 0.12f; // Width of the blend zone around threshold
    [Tooltip("Scale applied to the random offsets when sampling biome noise (keeps mask stable).")]
    public float biomeOffsetScale = 0.01f;

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

    // Generate terrain data
    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        terrainData.heightmapResolution = width + 1;

        // Set the actual world size of the terrain (not the resolution!)
        terrainData.size = new Vector3(terrainWidth, depth, terrainLength);

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

    // Calculate height using multi-octave Perlin noise for sand dunes, blended with biome mask
    float CalculateHeight(int x, int y)
    {
        float xCoord = (float)x / width;
        float yCoord = (float)y / this.height;

        // --- BIOME MASK (low = salt flat, high = dunes) ---
        // Use small offsetScale to keep biome noise stable and in an expected range
        float biomeMask = Mathf.PerlinNoise(
            xCoord * biomeScale + offsetX * biomeOffsetScale,
            yCoord * biomeScale + offsetY * biomeOffsetScale
        );

        // Smooth blend factor around threshold using transition width to avoid hard cutoffs
        float blend = Mathf.InverseLerp(biomeThreshold - biomeTransition, biomeThreshold + biomeTransition, biomeMask);
        blend = Mathf.SmoothStep(0f, 1f, blend); // now 0..1 where 0 = salt flat, 1 = dunes

        // --- DUNE HEIGHT CALC ---
        float stretchedX = xCoord * duneStretch;
        float stretchedY = yCoord;

        float terrainHeight = 0f;
        float amplitude = 1f;
        float frequency = scale;

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

        // Normalize by expected amplitude sum
        terrainHeight /= (2f - 1f / Mathf.Pow(2f, octaves - 1));
        terrainHeight = ApplyDuneShape(terrainHeight, xCoord, yCoord);
        terrainHeight = Mathf.Clamp01(terrainHeight);
        terrainHeight = Mathf.SmoothStep(0f, 1f, terrainHeight);

        // --- SALT FLAT (subtle noise) ---
        float saltFlatHeight = Mathf.PerlinNoise(
            xCoord * 5f + offsetX * 2f,
            yCoord * 5f + offsetY * 2f
        ) * 0.02f; // very small variation for salt flats (tweak as needed)

        // Blend between salt flat and dunes using the smooth blend value
        float blendedNormalized = Mathf.Lerp(saltFlatHeight, terrainHeight, blend);

        // Apply duneHeight multiplier but make sure final normalized height stays within [0,1]
        // NOTE: duneHeight > 1 may saturate the values — if you want world-unit heights, convert relative to 'depth'
        float final = blendedNormalized * duneHeight;
        final = Mathf.Clamp01(final);

        return final;
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
