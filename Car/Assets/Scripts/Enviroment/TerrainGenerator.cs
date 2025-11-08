using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Lifecycle")]
    public bool autoGenerate = true;   // If true, generates on Start(); otherwise call Generate() manually.
    public bool enableRivers = true;   // Disable for tiled worlds unless you accept per-tile rivers.

    [Header("Terrain Settings")]
    public int depth = 20;      // Vertical scale in world units
    public int width = 256;     // Heightmap resolution X (samples)
    public int height = 256;    // Heightmap resolution Y (samples)
    public float scale = 8f;    // Noise frequency in 1/world-units (lower = larger features)
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Terrain World Size (per tile)")]
    public float terrainWidth = 1000f;   // Tile width in world units (X)
    public float terrainLength = 1000f;  // Tile length in world units (Z)

    [Header("Tiling: world origin of this tile (meters)")]
    public Vector2 worldOrigin = Vector2.zero; // X = Unity X, Y = Unity Z

    [Header("Sand Dune Settings")]
    [Range(1, 8)]
    public int octaves = 4;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    [Range(1f, 4f)]
    public float lacunarity = 1.4f;
    [Range(0f, 3f)]
    public float duneHeight = 1f;
    [Range(0f, 1f)]
    public float windDirection = 0.6f;
    [Range(0.1f, 3f)]
    public float duneStretch = 1.5f;

    [Header("River Settings")]
    public int minRivers = 0;
    public int maxRivers = 3;
    public float riverWidth = 4f;
    public float riverDepth = 0.3f;
    public float riverWindiness = 2f;
    public float riverBankSoftness = 2f;
    public float riverTextureSpread = 1.2f;

    [Header("Biome Settings")]
    [Tooltip("Higher = smaller biome regions. Try 0.08 - 0.2 for smaller patches.")]
    public float biomeScale = 0.2f; // in 1/world-units
    [Tooltip("Below this value becomes salt-flat. 0.5 is neutral.")]
    [Range(0f, 1f)]
    public float biomeThreshold = 0.5f;
    [Tooltip("How soft the transition between biomes is (0 = hard edge).")]
    [Range(0f, 0.5f)]
    public float biomeTransition = 0.01f;
    [Tooltip("Scale applied to the random offsets when sampling biome noise (keeps mask stable).")]
    public float biomeOffsetScale = 0.01f;

    [Header("Terrain Textures")]
    public Texture2D duneTexture;
    public Texture2D saltFlatTexture;
    public Texture2D riverTexture;

    [Header("Texture Tiling")]
    public int tileSize = 5;

    [Header("Global Seamless Settings (set by WorldGenerator)")]
    public float globalWorldWidth = 1000f;   // Sum of all tile widths
    public float globalWorldLength = 1000f;  // Sum of all tile lengths
    public bool useGlobalSeamless = true;    // If true, sample noise in global space for seamless transitions

    private float offsetX;
    private float offsetY;

    void Start()
    {
        InitializeSeed();
        if (autoGenerate)
        {
            Generate();
        }
    }

    public void InitializeSeed()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, 10000);
        }

        Random.InitState(seed);
        offsetX = Random.Range(0f, 1000f);
        offsetY = Random.Range(0f, 1000f);
    }

    public void Generate()
    {
        var terrain = GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = gameObject.AddComponent<Terrain>();
        }

        // Always create a fresh TerrainData per tile
        var data = new TerrainData();
        data.heightmapResolution = Mathf.Clamp(width + 1, 33, 4097);
        data.size = new Vector3(terrainWidth, depth, terrainLength);

        // IMPORTANT: keep alphamap res consistent across tiles to avoid mapping drift
        data.alphamapResolution = Mathf.Clamp(width, 16, 2048);

        // Generate content
        float[,] heights = GenerateHeights();
        float[,] riverMask;
        if (enableRivers)
        {
            heights = GenerateRivers(heights, out riverMask);
        }
        else
        {
            riverMask = new float[width, height];
        }

        ApplyTextures(data, heights, riverMask);

        // Transpose heights before setting
        data.SetHeights(0, 0, TransposeHeightmap(heights));

        // Assign to Terrain and Collider
        terrain.terrainData = data;

        var collider = GetComponent<TerrainCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<TerrainCollider>();
        }
        collider.terrainData = data;
    }

    // Generate height map using world-space Perlin noise (seamless across tiles)
    float[,] GenerateHeights()
    {
        float[,] heights = new float[width + 1, height + 1];
        for (int sx = 0; sx <= width; sx++)
        {
            for (int sy = 0; sy <= height; sy++)
            {
                heights[sx, sy] = CalculateHeight(sx, sy);
            }
        }
        return heights;
    }

    // Rivers are still per-tile; for seamless world rivers, handle globally in a manager.
    float[,] GenerateRivers(float[,] heights, out float[,] riverMask)
    {
        riverMask = new float[width, height];
        int riverCount = Random.Range(minRivers, maxRivers + 1);

        for (int r = 0; r < riverCount; r++)
        {
            int riverSeed = seed + r * 1000;

            // Generate smooth river path across tile width (local to this tile)
            float[] riverPath = new float[width];
            int startY = Random.Range(height / 4, 3 * height / 4);
            riverPath[0] = startY;

            for (int x = 1; x < width; x++)
            {
                float offset = (Mathf.PerlinNoise(x * 0.1f, riverSeed * 0.1f) * 2f - 1f) * riverWindiness;
                riverPath[x] = Mathf.Clamp(riverPath[x - 1] + offset, 0, height - 1);
            }

            float step = 0.2f; // fine step for smooth curves
            for (float riverX = 0; riverX < width - 1; riverX += step)
            {
                int x0 = Mathf.FloorToInt(riverX);
                int x1 = Mathf.CeilToInt(riverX);
                float t = riverX - x0;
                float centerY = Mathf.Lerp(riverPath[x0], riverPath[x1], t);

                // Tangent for perpendicular carving
                float dirX = 1f;
                float dirY = 0f;
                if (x1 < width) dirY = riverPath[x1] - riverPath[x0];
                Vector2 perp = new Vector2(-dirY, dirX).normalized;

                int radius = Mathf.CeilToInt(riverWidth);
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        float dist = new Vector2(dx, dy).magnitude;
                        if (dist > riverWidth) continue;

                        int nx = Mathf.Clamp(Mathf.RoundToInt(riverX + perp.x * dx), 0, width - 1);
                        int ny = Mathf.Clamp(Mathf.RoundToInt(centerY + perp.y * dy), 0, height - 1);

                        // Smooth falloff for river banks using riverBankSoftness
                        float normalizedDist = dist / riverWidth;
                        float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), riverBankSoftness);

                        // Biome depth multiplier (deeper in salt flats) using world-space coords
                        WorldXYFromSample(nx, ny, out float xWorld, out float yWorld);
                        float xNormGlobal = xWorld / Mathf.Max(0.0001f, globalWorldWidth);
                        float yNormGlobal = yWorld / Mathf.Max(0.0001f, globalWorldLength);
                        float xNormTile = (xWorld - worldOrigin.x) / terrainWidth;
                        float yNormTile = (yWorld - worldOrigin.y) / terrainLength;
                        float xNorm = useGlobalSeamless ? xNormGlobal : xNormTile;
                        float yNorm = useGlobalSeamless ? yNormGlobal : yNormTile;

                        float biomeMask = Mathf.PerlinNoise(
                            xWorld * FreqX(biomeScale) + offsetX * biomeOffsetScale,
                            yWorld * FreqY(biomeScale) + offsetY * biomeOffsetScale
                        );
                        float riverDepthMultiplier = Mathf.Lerp(1.5f, 1f, biomeMask);
                        float finalRiverDepth = riverDepth * riverDepthMultiplier;

                        // Blend
                        heights[nx, ny] = Mathf.Lerp(heights[nx, ny], finalRiverDepth, falloff);
                        riverMask[nx, ny] = Mathf.Max(riverMask[nx, ny], falloff);
                    }
                }
            }
        }

        return heights;
    }

    // Calculate height using world-space Perlin noise with per-tile frequency (seamless across tiles)
    float CalculateHeight(int sx, int sy)
    {
        // World meters for this sample
        WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);

        // Per-tile normalized (0..1) if we ever need it
        float xNormTile = (xWorld - worldOrigin.x) / terrainWidth;
        float yNormTile = (yWorld - worldOrigin.y) / terrainLength;

        // Base domain: use world meters for seamless continuity
        // Preserve your original "scale" meaning (cycles per tile) by converting to per-meter freq
        float baseFreqX = FreqX(scale);
        float baseFreqY = FreqY(scale);

        // Biome uses same approach (cycles per tile -> per-meter)
        float biomeFreqX = FreqX(biomeScale);
        float biomeFreqY = FreqY(biomeScale);

        // --- BIOME MASK (low = salt flat, high = dunes) ---
        float biomeNoise = Mathf.PerlinNoise(
            xWorld * biomeFreqX + offsetX * biomeOffsetScale,
            yWorld * biomeFreqY + offsetY * biomeOffsetScale
        );
        float blend = Mathf.InverseLerp(biomeThreshold - biomeTransition, biomeThreshold + biomeTransition, biomeNoise);
        blend = Mathf.SmoothStep(0f, 1f, blend);

        // --- DUNE HEIGHT CALC ---
        // Stretch dunes along X in the same world domain
        float coordX = xWorld * duneStretch;
        float coordY = yWorld;

        float terrainHeight = 0f;
        float amplitude = 1f;
        float octFreq = 1f; // octave multiplier

        for (int i = 0; i < octaves; i++)
        {
            float u = coordX * (baseFreqX * octFreq) + offsetX;
            float v = coordY * (baseFreqY * octFreq) + offsetY;

            float noiseValue = Mathf.PerlinNoise(u, v);
            terrainHeight += noiseValue * amplitude;

            amplitude *= persistence;
            octFreq *= lacunarity;
        }

        // Normalize by expected amplitude sum
        terrainHeight /= (2f - 1f / Mathf.Pow(2f, octaves - 1));
        terrainHeight = ApplyDuneShape(terrainHeight, xWorld, yWorld);
        terrainHeight = Mathf.Clamp01(terrainHeight);
        terrainHeight = Mathf.SmoothStep(0f, 1f, terrainHeight);

        // --- SALT FLAT (subtle noise, per-tile cycles -> per-meter freq) ---
        float saltFlatBase = 0.08f;
        float saltFreqX = FreqX(5f);
        float saltFreqY = FreqY(5f);

        float saltFlatHeight = saltFlatBase + Mathf.PerlinNoise(
            xWorld * saltFreqX + offsetX * 2f,
            yWorld * saltFreqY + offsetY * 2f
        ) * 0.05f;

        // Blend between salt flat and dunes
        float blendedNormalized = Mathf.Lerp(saltFlatHeight, terrainHeight, blend);

        // Apply duneHeight multiplier
        float final = Mathf.Clamp01(blendedNormalized * duneHeight);
        return final;
    }

    // Apply sand dune shaping (use world meters but keep approx. one cycle per tile for the wind sway)
    float ApplyDuneShape(float height, float xWorld, float yWorld)
    {
        // One sine cycle per tile width for the slight wind effect
        float phaseX = xWorld * FreqX(1f);
        float windEffect = Mathf.Sin(phaseX * Mathf.PI * 2f + windDirection) * 0.1f;
        height += windEffect;

        // Smooth dunes
        height = Mathf.Pow(height, 1.2f);

        // Fine randomness: ~50 cycles per tile, per-meter mapping keeps it tile-size independent
        float randFreqX = FreqX(50f);
        float randFreqY = FreqY(50f);
        float randomness = Mathf.PerlinNoise(xWorld * randFreqX + offsetX, yWorld * randFreqY + offsetY) * 0.05f;
        height += randomness;

        return height;
    }

    // Texture application: sample biome with the same world-space domain/freq so splats match heights
    void ApplyTextures(TerrainData terrainData, float[,] heights, float[,] riverMask)
    {
        TerrainLayer duneLayer = new TerrainLayer { diffuseTexture = duneTexture, tileSize = new Vector2(tileSize, tileSize) };
        TerrainLayer saltLayer = new TerrainLayer { diffuseTexture = saltFlatTexture, tileSize = new Vector2(tileSize, tileSize) };
        TerrainLayer riverLayer = new TerrainLayer { diffuseTexture = riverTexture, tileSize = new Vector2(tileSize, tileSize) };

        terrainData.terrainLayers = new TerrainLayer[] { duneLayer, saltLayer, riverLayer };

        int mapWidth = terrainData.alphamapWidth;
        int mapHeight = terrainData.alphamapHeight;
        float[,,] splatmapData = new float[mapHeight, mapWidth, 3];

        float biomeFreqX = FreqX(biomeScale);
        float biomeFreqY = FreqY(biomeScale);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int hmX = Mathf.RoundToInt((float)x / (mapWidth - 1) * (width - 1));
                int hmY = Mathf.RoundToInt((float)y / (mapHeight - 1) * (height - 1));

                // Remove rotation
                float river = riverMask[hmX, hmY];

                // World meters for biome sampling
                WorldXYFromSample(hmX, hmY, out float xWorld, out float yWorld);

                float biomeNoise = Mathf.PerlinNoise(
                    xWorld * biomeFreqX + offsetX * biomeOffsetScale,
                    yWorld * biomeFreqY + offsetY * biomeOffsetScale
                );
                float biomeBlend = Mathf.InverseLerp(
                    biomeThreshold - biomeTransition,
                    biomeThreshold + biomeTransition,
                    biomeNoise
                );
                biomeBlend = Mathf.Clamp01(biomeBlend);

                splatmapData[y, x, 0] = biomeBlend * (1f - river);        // dunes
                splatmapData[y, x, 1] = (1f - biomeBlend) * (1f - river); // salt flats
                splatmapData[y, x, 2] = river;                            // river
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    // Map sample indices to world-space coordinates (meters)
    void WorldXYFromSample(int sx, int sy, out float xWorld, out float yWorld)
    {
        float nx = (width <= 0) ? 0f : (float)sx / width;   // 0..1 across tile
        float ny = (height <= 0) ? 0f : (float)sy / height;
        xWorld = worldOrigin.x + nx * terrainWidth;
        yWorld = worldOrigin.y + ny * terrainLength;
    }

    // Convert "cycles per tile" to frequency per meter
    float FreqX(float cyclesPerTile) => cyclesPerTile / Mathf.Max(0.0001f, terrainWidth);
    float FreqY(float cyclesPerTile) => cyclesPerTile / Mathf.Max(0.0001f, terrainLength);

    float[,] TransposeHeightmap(float[,] heights)
    {
        int w = heights.GetLength(0);
        int h = heights.GetLength(1);
        float[,] transposed = new float[h, w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                transposed[y, x] = heights[x, y];
        return transposed;
    }
}
