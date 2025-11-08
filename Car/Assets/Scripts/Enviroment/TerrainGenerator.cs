using UnityEngine;

/*
 TerrainGenerator
 ----------------
 PURPOSE
   Procedurally builds a single terrain tile (heightmap + splat textures) that can be placed in a larger
   seamless grid (when driven by WorldGenerator). Supports dune vs salt-flat biomes, optional rivers,
   and advanced biome noise shaping (fBm, ridged, domain warp, rotation, contrast, inversion).

 KEY IDEAS
   - Height blending: A biome mask chooses between "dune" profile and "salt flat" profile.
   - Seamlessness: Noise sampled in world meters; worldOrigin offsets ensure adjacent tiles stitch.
   - Biome noise customization: Many parameters let you sculpt large-scale pattern variety.
   - Rivers (per-tile): Carved after heights; for global rivers spanning tiles you'd need a manager.

 QUICK TUNING HINTS
   Bigger features: lower 'biomeScale' and/or 'scale' (fewer cycles per tile).
   More intricate biome patchwork: increase biomeOctaves, maybe add slight biomeWarpStrength (0.15–0.3).
   Sharper biome edges: raise biomeContrast (1.5–2.5) and/or reduce biomeTransition.
   Reverse dune / salt distribution: toggle 'biomeInvert'.
   Long stretched dunes: increase 'duneStretch' (1.5–2.2) and maybe lower 'scale'.
   Subtler dunes: reduce 'duneHeight' OR raise 'saltFlatBase' section if you expose it.
*/

public class TerrainGenerator : MonoBehaviour
{
    // ---------------------- LIFECYCLE ----------------------
    [Header("Lifecycle")]
    [Tooltip("If ON, calls Generate() automatically in Start(). Turn OFF to script control generation order (e.g., after assigning seeds).")]
    public bool autoGenerate = true;
    [Tooltip("If ON, runs river carving after base height generation (per tile). Disable for seamless tiled worlds unless you accept cut rivers.")]
    public bool enableRivers = true;

    // ---------------------- CORE TERRAIN RESOLUTION / SIZE ----------------------
    [Header("Terrain Settings")]
    [Tooltip("Vertical world size (meters) of the TerrainData (affects absolute world elevation scale).")]
    public int depth = 20;
    [Tooltip("Horizontal sample resolution in X (heightmap columns minus 1). Unity stores heightmap as (width+1). Higher = smoother but heavier.")]
    public int width = 256;
    [Tooltip("Horizontal sample resolution in Y (heightmap rows minus 1). Should usually match width for square sampling.")]
    public int height = 256;
    [Tooltip("Base dune noise frequency as 'cycles per tile'. Lower = broader dunes, higher = more, smaller dunes.")]
    public float scale = 8f;
    [Tooltip("If ON a random seed is generated each InitializeSeed(). Turn OFF for deterministic builds (manually set 'seed').")]
    public bool useRandomSeed = true;
    [Tooltip("Master seed controlling dune noise + (optionally) biome noise unless 'biomeUseSeparateSeed' is ON.")]
    public int seed = 0;

    // ---------------------- TILE WORLD SIZE ----------------------
    [Header("Terrain World Size (per tile)")]
    [Tooltip("Width in meters of this tile (Unity X). Changing after generation requires re-Generate().")]
    public float terrainWidth = 1000f;
    [Tooltip("Length in meters of this tile (Unity Z).")]
    public float terrainLength = 1000f;

    // ---------------------- TILE ORIGIN FOR SEAMLESS WORLD ----------------------
    [Header("Tiling: world origin of this tile (meters)")]
    [Tooltip("World-space (X,Z) position of the tile's lower-left corner. Used to keep noise continuous across adjacent tiles.")]
    public Vector2 worldOrigin = Vector2.zero;

    // ---------------------- DUNE (MICRO / MESO SHAPE) ----------------------
    [Header("Sand Dune Settings")]
    [Tooltip("Number of octaves for dune fBm. More octaves = more detail & cost. 3–6 typical.")]
    [Range(1, 8)] public int octaves = 4;
    [Tooltip("Amplitude multiplier between dune octaves. Lower = faster decay (smoother result). 0.4–0.6 typical.")]
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Tooltip("Frequency multiplier between dune octaves. >1 increases detail frequency. 1.8–2.3 common.")]
    [Range(1f, 4f)] public float lacunarity = 1.4f;
    [Tooltip("Global multiplier for final dune+salt blended normalized height (acts like vertical exaggeration).")]
    [Range(0f, 3f)] public float duneHeight = 1f;
    [Tooltip("Horizontal phase shift for the wind sine shaping. 0..1 = fraction of full rotation. Try randomizing for variation.")]
    [Range(0f, 1f)] public float windDirection = 0.6f;
    [Tooltip("Stretches dunes along X before fBm sampling. >1 elongates dunes; <1 squashes.")]
    [Range(0.1f, 3f)] public float duneStretch = 1.5f;

    // ---------------------- RIVER (PER TILE) ----------------------
    [Header("River Settings")]
    [Tooltip("Minimum number of rivers carved on this tile (inclusive).")]
    public int minRivers = 0;
    [Tooltip("Maximum number of rivers carved (inclusive). Random count between min and max each generation.")]
    public int maxRivers = 3;
    [Tooltip("Approximate radial width (in heightmap samples) of river carving region. Larger widens rivers.")]
    public float riverWidth = 4f;
    [Tooltip("Base carve depth (in normalized 0..1 terrain height units). Actual effect scales with biome mask (deeper in salt flats).")]
    public float riverDepth = 0.3f;
    [Tooltip("Magnitude of horizontal wander per step (affects meandering). Higher = more wiggly rivers.")]
    public float riverWindiness = 2f;
    [Tooltip("Exponent for soft falloff of river banks. Higher = sharper banks; lower = smoother transition.")]
    public float riverBankSoftness = 2f;
    [Tooltip("Controls spread of river texture weighting relative to riverMask. (Currently not used directly for splats but reserved for extension.)")]
    public float riverTextureSpread = 1.2f;

    // ---------------------- BIOME (BASIC) ----------------------
    [Header("Biome Settings (basic)")]
    [Tooltip("Biome noise frequency as 'cycles per tile'. Lower = larger contiguous dune / salt areas. 0.08–0.25 typical.")]
    public float biomeScale = 0.2f;
    [Tooltip("Threshold pivot. Noise below => salt flats, above => dunes (unless inverted). 0.5 = unbiased.")]
    [Range(0f, 1f)] public float biomeThreshold = 0.5f;
    [Tooltip("Half-width of the soft band around threshold. 0 = binary switch; larger = smoother transition.")]
    [Range(0f, 0.5f)] public float biomeTransition = 0.01f;
    [Tooltip("Multiplier for random offsets fed into biome noise. Keep small to stabilize patterns across regeneration.")]
    public float biomeOffsetScale = 0.01f;

    // ---------------------- BIOME (ADVANCED SHAPING) ----------------------
    [Header("Biome Settings (advanced)")]
    [Tooltip("fBm octave count for biome mask. 1 replicates original single Perlin. More adds detail.")]
    [Range(1, 8)] public int biomeOctaves = 1;
    [Tooltip("Biome fBm amplitude decay per octave. Lower = smoother large shapes; higher retains small details.")]
    [Range(0f, 1f)] public float biomePersistence = 0.5f;
    [Tooltip("Biome fBm frequency growth per octave. Higher spreads finer detail quickly.")]
    [Range(1f, 4f)] public float biomeLacunarity = 2.0f;
    [Tooltip("If ON uses a ridged profile (turns peaks into crisp lines, valley-like patterns).")]
    public bool biomeUseRidged = false;
    [Tooltip("Rotate biome domain around (0,0) world before sampling (degrees). Adjust for directional alignment with world features).")]
    [Range(-180f, 180f)] public float biomeRotationDegrees = 0f;
    [Tooltip("Strength of domain warp (0 disables). Adds organic curling. 0.15–0.4 subtle, >1 very distorted.")]
    [Range(0f, 2f)] public float biomeWarpStrength = 0f;
    [Tooltip("Warp noise frequency (cycles per tile). Higher = tighter, more turbulent warping.")]
    [Range(0.01f, 10f)] public float biomeWarpScale = 0.5f;
    [Tooltip("Contrast (gamma-like) on final biome blend. >1 hardens boundary; <1 softens.")]
    [Range(0.25f, 4f)] public float biomeContrast = 1f;
    [Tooltip("Invert biome mask AFTER noise but BEFORE threshold/contrast (swap dunes <-> salt).")]
    public bool biomeInvert = false;
    [Tooltip("If ON uses a separate seed + offsets for biome noise (decouples from dune offsets).")]
    public bool biomeUseSeparateSeed = false;
    [Tooltip("Manual biome seed (only used if biomeUseSeparateSeed ON). 0 => auto when useRandomSeed is ON.")]
    public int biomeSeed = 0;

    // ---------------------- TEXTURES ----------------------
    [Header("Terrain Textures")]
    [Tooltip("Albedo for dune regions (layer 0).")]
    public Texture2D duneTexture;
    [Tooltip("Albedo for salt flat regions (layer 1).")]
    public Texture2D saltFlatTexture;
    [Tooltip("Albedo for river channel (layer 2).")]
    public Texture2D riverTexture;

    [Header("Texture Tiling")]
    [Tooltip("Uniform tile size (UV repeat) for all textures (meters). Adjust per-layer manually if you need variety.")]
    public int tileSize = 5;

    // ---------------------- GLOBAL SEAMLESS ----------------------
    [Header("Global Seamless Settings (set by WorldGenerator)")]
    [Tooltip("TOTAL world width (sum across tiles). Used mainly for global normalization if needed.")]
    public float globalWorldWidth = 1000f;
    [Tooltip("TOTAL world length (sum across tiles).")]
    public float globalWorldLength = 1000f;
    [Tooltip("If ON, noise sampling uses absolute world coordinates to avoid seams. (Keep ON for tiled worlds.)")]
    public bool useGlobalSeamless = true;

    // Internal random offsets
    private float offsetX;
    private float offsetY;

    // Separate biome offsets (can mirror dune offsets or be independently seeded)
    private float biomeOffsetX;
    private float biomeOffsetY;

    void Start()
    {
        InitializeSeed();
        if (autoGenerate)
            Generate();
    }

    public void InitializeSeed()
    {
        // Seed selection for dune + shared domain
        if (useRandomSeed)
            seed = Random.Range(0, 10000);

        var savedState = Random.state; // preserve RNG so other systems not affected

        Random.InitState(seed);
        offsetX = Random.Range(0f, 1000f);
        offsetY = Random.Range(0f, 1000f);

        // Biome offsets (either tied to dune offsets or independent)
        if (biomeUseSeparateSeed)
        {
            if (biomeSeed == 0 && useRandomSeed)
                biomeSeed = Random.Range(0, 10000);

            Random.InitState(biomeSeed == 0 ? seed + 12345 : biomeSeed);
            biomeOffsetX = Random.Range(0f, 1000f);
            biomeOffsetY = Random.Range(0f, 1000f);
        }
        else
        {
            biomeOffsetX = offsetX;
            biomeOffsetY = offsetY;
        }

        Random.state = savedState; // restore RNG state
    }

    public void Generate()
    {
        var terrain = GetComponent<Terrain>() ?? gameObject.AddComponent<Terrain>();

        // Always new TerrainData each generation (safe for multi-tile worlds)
        var data = new TerrainData
        {
            heightmapResolution = Mathf.Clamp(width + 1, 33, 4097),
            size = new Vector3(terrainWidth, depth, terrainLength),
            alphamapResolution = Mathf.Clamp(width, 16, 2048)
        };

        // HEIGHTS
        float[,] heights = GenerateHeights();

        // RIVERS (optional)
        float[,] riverMask;
        if (enableRivers)
            heights = GenerateRivers(heights, out riverMask);
        else
            riverMask = new float[width, height]; // all zeros

        // SPLATS
        ApplyTextures(data, heights, riverMask);

        // Unity expects [y,x]; we build [x,y]; transpose
        data.SetHeights(0, 0, TransposeHeightmap(heights));

        terrain.terrainData = data;

        var collider = GetComponent<TerrainCollider>() ?? gameObject.AddComponent<TerrainCollider>();
        collider.terrainData = data;
    }

    // Build base height map
    float[,] GenerateHeights()
    {
        float[,] heights = new float[width + 1, height + 1];
        for (int sx = 0; sx <= width; sx++)
            for (int sy = 0; sy <= height; sy++)
                heights[sx, sy] = CalculateHeight(sx, sy);
        return heights;
    }

    // Per-tile rivers; for world-scale seamless rivers you'd centralize path generation.
    float[,] GenerateRivers(float[,] heights, out float[,] riverMask)
    {
        riverMask = new float[width, height];
        int riverCount = Random.Range(minRivers, maxRivers + 1);

        for (int r = 0; r < riverCount; r++)
        {
            int riverSeed = seed + r * 1000;
            float[] riverPath = new float[width];
            int startY = Random.Range(height / 4, 3 * height / 4);
            riverPath[0] = startY;

            // Random walk with Perlin-guided offsets
            for (int x = 1; x < width; x++)
            {
                float offset = (Mathf.PerlinNoise(x * 0.1f, riverSeed * 0.1f) * 2f - 1f) * riverWindiness;
                riverPath[x] = Mathf.Clamp(riverPath[x - 1] + offset, 0, height - 1);
            }

            // Sub-sample for smooth curve carving
            float step = 0.2f;
            for (float riverX = 0; riverX < width - 1; riverX += step)
            {
                int x0 = Mathf.FloorToInt(riverX);
                int x1 = Mathf.CeilToInt(riverX);
                float t = riverX - x0;
                float centerY = Mathf.Lerp(riverPath[x0], riverPath[x1], t);

                // Perpendicular for carving radius
                float dirY = (x1 < width) ? (riverPath[x1] - riverPath[x0]) : 0f;
                Vector2 perp = new Vector2(-dirY, 1f).normalized;

                int radius = Mathf.CeilToInt(riverWidth);
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        float dist = new Vector2(dx, dy).magnitude;
                        if (dist > riverWidth) continue;

                        int nx = Mathf.Clamp(Mathf.RoundToInt(riverX + perp.x * dx), 0, width - 1);
                        int ny = Mathf.Clamp(Mathf.RoundToInt(centerY + perp.y * dy), 0, height - 1);

                        // Bank falloff -> 0 at edge, 1 at center
                        float normalizedDist = dist / riverWidth;
                        float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), riverBankSoftness);

                        // Biome-modulated depth: salt flats get deeper (using RAW noise, not inverted)
                        WorldXYFromSample(nx, ny, out float xWorld, out float yWorld);
                        float biomeNoiseRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
                        float riverDepthMultiplier = Mathf.Lerp(1.5f, 1f, biomeNoiseRaw);
                        float finalRiverDepth = riverDepth * riverDepthMultiplier;

                        // Carve (lerp toward finalRiverDepth)
                        heights[nx, ny] = Mathf.Lerp(heights[nx, ny], finalRiverDepth, falloff);
                        riverMask[nx, ny] = Mathf.Max(riverMask[nx, ny], falloff);
                    }
                }
            }
        }
        return heights;
    }

    // Combines dune fBm + salt-flat height via biome blend
    float CalculateHeight(int sx, int sy)
    {
        WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);

        float blend = SampleBiomeBlend(xWorld, yWorld); // 0 salt -> 1 dunes

        // Dune multi-octave
        float baseFreqX = FreqX(scale);
        float baseFreqY = FreqY(scale);

        float coordX = xWorld * duneStretch;
        float coordY = yWorld;

        float terrainHeight = 0f;
        float amplitude = 1f;
        float octFreq = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float u = coordX * (baseFreqX * octFreq) + offsetX;
            float v = coordY * (baseFreqY * octFreq) + offsetY;

            float noiseValue = Mathf.PerlinNoise(u, v);
            terrainHeight += noiseValue * amplitude;

            amplitude *= persistence;
            octFreq *= lacunarity;
        }

        // Normalize expected sum (geometric series approx)
        terrainHeight /= (2f - 1f / Mathf.Pow(2f, octaves - 1));
        terrainHeight = ApplyDuneShape(terrainHeight, xWorld, yWorld);
        terrainHeight = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, terrainHeight));

        // Salt flat: subtle low-amplitude noise
        float saltFlatBase = 0.08f;
        float saltFreqX = FreqX(5f);
        float saltFreqY = FreqY(5f);
        float saltFlatHeight = saltFlatBase + Mathf.PerlinNoise(
            xWorld * saltFreqX + offsetX * 2f,
            yWorld * saltFreqY + offsetY * 2f
        ) * 0.05f;

        // Blend and scale
        float blendedNormalized = Mathf.Lerp(saltFlatHeight, terrainHeight, blend);
        return Mathf.Clamp01(blendedNormalized * duneHeight);
    }

    // Macro shaping extras (wind ripple + small randomness)
    float ApplyDuneShape(float height, float xWorld, float yWorld)
    {
        float phaseX = xWorld * FreqX(1f); // exactly 1 cycle per tile width
        height += Mathf.Sin(phaseX * Mathf.PI * 2f + windDirection) * 0.1f; // gentle sway
        height = Mathf.Pow(height, 1.2f); // soften peaks / fill lows
        float randFreqX = FreqX(50f);
        float randFreqY = FreqY(50f);
        height += Mathf.PerlinNoise(xWorld * randFreqX + offsetX, yWorld * randFreqY + offsetY) * 0.05f;
        return height;
    }

    // Apply texture splats using the same biome blend logic to keep visual cohesion
    void ApplyTextures(TerrainData terrainData, float[,] heights, float[,] riverMask)
    {
        TerrainLayer duneLayer = new TerrainLayer { diffuseTexture = duneTexture, tileSize = new Vector2(tileSize, tileSize) };
        TerrainLayer saltLayer = new TerrainLayer { diffuseTexture = saltFlatTexture, tileSize = new Vector2(tileSize, tileSize) };
        TerrainLayer riverLayer = new TerrainLayer { diffuseTexture = riverTexture, tileSize = new Vector2(tileSize, tileSize) };
        terrainData.terrainLayers = new TerrainLayer[] { duneLayer, saltLayer, riverLayer };

        int mapWidth = terrainData.alphamapWidth;
        int mapHeight = terrainData.alphamapHeight;
        float[,,] splatmapData = new float[mapHeight, mapWidth, 3];

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // Map splat coordinates to heightmap indices
                int hmX = Mathf.RoundToInt((float)x / (mapWidth - 1) * (width - 1));
                int hmY = Mathf.RoundToInt((float)y / (mapHeight - 1) * (height - 1));

                float river = riverMask[hmX, hmY];
                WorldXYFromSample(hmX, hmY, out float xWorld, out float yWorld);
                float biomeBlend = SampleBiomeBlend(xWorld, yWorld);

                // Layer weights (auto normalize by assignment expectation)
                splatmapData[y, x, 0] = biomeBlend * (1f - river);        // dunes
                splatmapData[y, x, 1] = (1f - biomeBlend) * (1f - river); // salt
                splatmapData[y, x, 2] = river;                            // river
            }
        }
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    // Sample index => world meter conversion (keeps tile adjacency seamless)
    void WorldXYFromSample(int sx, int sy, out float xWorld, out float yWorld)
    {
        float nx = (width <= 0) ? 0f : (float)sx / width;
        float ny = (height <= 0) ? 0f : (float)sy / height;
        xWorld = worldOrigin.x + nx * terrainWidth;
        yWorld = worldOrigin.y + ny * terrainLength;
    }

    // Convert cycles-per-tile to frequency-per-meter
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

    // --------------------- BIOME SAMPLING HELPERS ---------------------

    Vector2 RotateWorld(Vector2 p, float degrees)
    {
        if (Mathf.Approximately(degrees, 0f)) return p;
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    // Generic (ridged) fBm returning 0..1
    float FBM(float u, float v, int oct, float pers, float lac, bool ridged)
    {
        float amp = 1f;
        float freq = 1f;
        float sum = 0f;
        float norm = 0f;
        for (int i = 0; i < oct; i++)
        {
            float n = Mathf.PerlinNoise(
                u * freq + biomeOffsetX * biomeOffsetScale,
                v * freq + biomeOffsetY * biomeOffsetScale
            );
            if (ridged)
                n = 1f - Mathf.Abs(n * 2f - 1f); // fold into ridges

            sum += n * amp;
            norm += amp;
            amp *= Mathf.Clamp01(pers);
            freq *= Mathf.Max(1f, lac);
        }
        return (norm > 0f) ? sum / norm : 0.5f;
    }

    // Raw biome noise (unthresholded, uninverted) 0..1
    float SampleBiomeNoiseRaw(float xWorld, float yWorld)
    {
        Vector2 p = RotateWorld(new Vector2(xWorld, yWorld), biomeRotationDegrees);

        float u = p.x * FreqX(biomeScale);
        float v = p.y * FreqY(biomeScale);

        // Simple domain warp for organic edges
        if (biomeWarpStrength > 0f)
        {
            float wu = p.x * FreqX(biomeWarpScale);
            float wv = p.y * FreqY(biomeWarpScale);
            float wx = Mathf.PerlinNoise(wu + biomeOffsetX, wv + biomeOffsetY) * 2f - 1f;
            float wy = Mathf.PerlinNoise(wu + biomeOffsetX + 37.7f, wv + biomeOffsetY + 59.3f) * 2f - 1f;
            u += wx * biomeWarpStrength;
            v += wy * biomeWarpStrength;
        }
        return FBM(u, v, Mathf.Max(1, biomeOctaves), biomePersistence, biomeLacunarity, biomeUseRidged);
    }

    // Final normalized blend (0 salt, 1 dunes) with threshold, inversion, contrast
    float SampleBiomeBlend(float xWorld, float yWorld)
    {
        float n = SampleBiomeNoiseRaw(xWorld, yWorld);
        if (biomeInvert) n = 1f - n;

        float blend = Mathf.InverseLerp(
            biomeThreshold - biomeTransition,
            biomeThreshold + biomeTransition,
            n
        );
        blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));

        if (!Mathf.Approximately(biomeContrast, 1f))
            blend = Mathf.Pow(blend, biomeContrast);

        return Mathf.Clamp01(blend);
    }
}
