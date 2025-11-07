using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int depth = 20;  // Increased height range for taller dunes
    public int width = 256;  // Heightmap resolution
    public int height = 256; // Heightmap resolution
    public float scale = 8f;  // Much smaller scale = bigger spacing between dunes
    public bool useRandomSeed = true;
    public int seed = 0;
    
    [Header("Terrain World Size")]
    public float terrainWidth = 1000f;  // Actual world width
    public float terrainLength = 1000f; // Actual world length
    
    [Header("Sand Dune Settings")]
    [Range(1, 8)]
    public int octaves = 4;  // Reduced for smoother, larger features
    [Range(0f, 1f)]
    public float persistence = 0.5f;  // Lower for smoother transitions
    [Range(1f, 4f)]
    public float lacunarity = 1.4f; // Standard value, not too jagged
    [Range(0f, 3f)]
    public float duneHeight = 1f;  // Multiplier applied to final normalized height (keep an eye on this, >1 can saturate)
    [Range(0f, 1f)]
    public float windDirection = 0.6f; // Creates asymmetrical dunes, increasing it makes windward side gentler
    [Range(0.1f, 3f)]
    public float duneStretch = 1.5f; // Elongates dunes in wind direction

    [Header("River Settings")]
    public int minRivers = 0;       // Minimum number of rivers
    public int maxRivers = 3;       // Maximum number of rivers
    public float riverWidth = 4f;   // Width of river in terrain units
   public float riverDepth = 0.3f; // or 0.4f



    [Header("Biome Settings")]
    [Tooltip("Higher = smaller biome regions. Try 0.08 - 0.2 for smaller patches.")]
    public float biomeScale = 0.2f; // Try 0.08, 0.1, or 0.2 for smaller biomes, larger the # the bigger the biomes
    [Tooltip("Below this value becomes salt-flat. 0.5 is neutral.")]
    [Range(0f,1f)]
    public float biomeThreshold = 0.5f; // Threshold for deciding biomes, larger values = more dunes
    [Tooltip("How soft the transition between biomes is (0 = hard edge).")]
    [Range(0f,0.5f)]
    public float biomeTransition = 0.01f; // Width of the blend zone around threshold, larger = smoother transition
    [Tooltip("Scale applied to the random offsets when sampling biome noise (keeps mask stable).")]
    public float biomeOffsetScale = 0.01f;

    [Header("Terrain Textures")]
    public Texture2D duneTexture;
    public Texture2D saltFlatTexture;
    public Texture2D riverTexture;

    [Header("Texture Tiling")]
    public int tileSize = 5;


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
// Generate terrain data
    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        // Set heightmap resolution and world size
        terrainData.heightmapResolution = width + 1;
        terrainData.size = new Vector3(terrainWidth, depth, terrainLength);

        // --- Generate heightmap with dunes and salt flats ---
        float[,] heights = GenerateHeights();

        // --- Carve rivers and get river mask ---
        float[,] riverMask;
        heights = GenerateRivers(heights, out riverMask);

        // Apply textures based on biome and river mask
        ApplyTextures(terrainData, heights, riverMask);

        // Apply final heights to terrain
        terrainData.SetHeights(0, 0, heights);

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

        // REMOVE this line:
        // heights = GenerateRivers(heights);

        return heights;
    }

    float[,] GenerateRivers(float[,] heights, out float[,] riverMask)
    {
        riverMask = new float[width, height]; // initialize mask

        int riverCount = Random.Range(minRivers, maxRivers + 1);

        for (int r = 0; r < riverCount; r++)
        {
            int x = 0;
            int y = Random.Range(0, height);

            for (int i = 0; i < width; i++)
            {
                for (int w = -(int)riverWidth; w <= (int)riverWidth; w++)
                {
                    int rx = Mathf.Clamp(x + w, 0, width - 1);
                    int ry = height - 1 - Mathf.Clamp(y, 0, height - 1);

                    heights[rx, ry] = Mathf.Min(heights[rx, ry], riverDepth);
                    riverMask[rx, ry] = 1f; // mark river presence
                }

                x++;
                y += Random.Range(-1, 2);
                y = Mathf.Clamp(y, 0, height - 1);
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
    
        // Apply textures based on biome mask
    void ApplyTextures(TerrainData terrainData, float[,] heights, float[,] riverMask)
    {
        TerrainLayer duneLayer = new TerrainLayer();
        duneLayer.diffuseTexture = duneTexture;
        duneLayer.tileSize = new Vector2(tileSize, tileSize);

        TerrainLayer saltLayer = new TerrainLayer();
        saltLayer.diffuseTexture = saltFlatTexture;
        saltLayer.tileSize = new Vector2(tileSize, tileSize);

        TerrainLayer riverLayer = new TerrainLayer();
        riverLayer.diffuseTexture = riverTexture;
        riverLayer.tileSize = new Vector2(tileSize, tileSize);

        terrainData.terrainLayers = new TerrainLayer[] { duneLayer, saltLayer, riverLayer };

        int mapWidth = terrainData.alphamapWidth;
        int mapHeight = terrainData.alphamapHeight;
        float[,,] splatmapData = new float[mapHeight, mapWidth, 3];

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // Original heightmap coordinates
                int hmX = Mathf.RoundToInt((float)x / (mapWidth - 1) * (width - 1));
                int hmY = Mathf.RoundToInt((float)y / (mapHeight - 1) * (height - 1));

                // --- Rotate 90° counterclockwise ---
                int rotX = height - 1 - hmY;  // new X
                int rotY = hmX;               // new Y

                // Sample river and heights using rotated coordinates
                float river = riverMask[rotX, rotY];

                // Biome noise for dunes and salt flats
                float xCoord = (float)rotX / width;
                float yCoord = (float)rotY / height;

                float biomeNoise = Mathf.PerlinNoise(
                    xCoord * biomeScale + offsetX * biomeOffsetScale,
                    yCoord * biomeScale + offsetY * biomeOffsetScale
                );

                float biomeBlend = Mathf.InverseLerp(
                    biomeThreshold - biomeTransition,
                    biomeThreshold + biomeTransition,
                    biomeNoise
                );
                biomeBlend = Mathf.Clamp01(biomeBlend);

                // --- Assign to splatmap, flip Y for top=0 ---
                int mapY = mapHeight - 1 - y;

                splatmapData[mapY, x, 0] = biomeBlend * (1f - river);      // dunes
                splatmapData[mapY, x, 1] = (1f - biomeBlend) * (1f - river); // salt flats
                splatmapData[mapY, x, 2] = river;                           // river
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmapData);
    }
}
