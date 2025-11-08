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

    // ---------------------- RIVER CURVE SETTINGS ----------------------
    [Header("River Curve Settings")]
    [Tooltip("Number of smoothing passes (moving average) applied to the raw noise path. 0 = none, 2-5 typical.")]
    [Range(0, 12)] public int riverSmoothPasses = 4;
    [Tooltip("Low frequency (broad drift) for river center curve (cycles per tile). Smaller = broader bends.")]
    public float riverLowFrequency = 0.5f;
    [Tooltip("High frequency (tight wiggles) for river center curve (cycles per tile).")]
    public float riverHighFrequency = 4f;
    [Tooltip("Amplitude (in normalized tile height 0..1) for broad drift.")]
    [Range(0f, 0.5f)] public float riverLowAmplitude = 0.25f;
    [Tooltip("Amplitude (in normalized tile height 0..1) for finer wiggles.")]
    [Range(0f, 0.2f)] public float riverHighAmplitude = 0.05f;
    [Tooltip("Adds post-smoothing wiggle to the river centerline. 0 = off, 0.1–0.35 adds natural meander noise.")]
    [Range(0f, 1f)] public float riverRoughness = 0.2f;

    [Tooltip("Frequency (cycles per tile) for river roughness noise.")]
    public float riverRoughnessFrequency = 3f;

    [Tooltip("Relative width variation of the river along its length. 0 = constant width; 0.2–0.5 looks natural.")]
    [Range(0f, 1f)] public float riverWidthJitter = 0.25f;

    [Tooltip("Frequency (cycles per tile) for width jitter.")]
    public float riverWidthJitterFrequency = 0.8f;

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

    // NEW: externally provided world-space river mask (per-tile slice).
    // Expected size: [width, height] with indices [x, y].
    private float[,] externalRiverMask = null;

    // NEW: Public API to set/clear external river mask
    public void SetExternalRiverMask(float[,] mask)
    {
        externalRiverMask = mask;
    }
    public void ClearExternalRiverMask()
    {
        externalRiverMask = null;
    }

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

    // Helper to expand a width x height river mask to (width+1) x (height+1)
    float[,] ExpandRiverMaskIfNeeded(float[,] mask, int targetW, int targetH)
    {
        int mw = mask.GetLength(0);
        int mh = mask.GetLength(1);

        // Already the desired (samples) size
        if (mw == targetW + 1 && mh == targetH + 1)
            return mask;

        // Exact interior size: expand by duplicating last row/col
        if (mw == targetW && mh == targetH)
        {
            float[,] expanded = new float[targetW + 1, targetH + 1];
            for (int x = 0; x < targetW; x++)
                for (int y = 0; y < targetH; y++)
                    expanded[x, y] = mask[x, y];

            // Duplicate last column into new border column
            for (int y = 0; y < targetH; y++)
                expanded[targetW, y] = mask[targetW - 1, y];

            // Duplicate last row into new border row
            for (int x = 0; x < targetW + 1; x++)
                expanded[x, targetH] = expanded[x, targetH - 1];

            return expanded;
        }

        // Unsupported size: return null so we can fallback
        return null;
    }

    public void Generate()
    {
        var terrain = GetComponent<Terrain>() ?? gameObject.AddComponent<Terrain>();

        var data = new TerrainData
        {
            heightmapResolution = Mathf.Clamp(width + 1, 33, 4097),
            size = new Vector3(terrainWidth, depth, terrainLength),
            alphamapResolution = Mathf.Clamp(width, 16, 2048)
        };

        // Base heights (width+1 x height+1)
        float[,] heights = GenerateHeights();

        float[,] riverMask;

        // External mask handling (accept width x height OR width+1 x height+1)
        if (externalRiverMask != null)
        {
            var adjusted = ExpandRiverMaskIfNeeded(externalRiverMask, width, height);
            if (adjusted == null)
            {
                Debug.LogWarning(
                    $"External river mask size unsupported. Got {externalRiverMask.GetLength(0)}x{externalRiverMask.GetLength(1)}, " +
                    $"expected either {width}x{height} or {width + 1}x{height + 1}. Falling back."
                );
                externalRiverMask = null;
            }
            else
            {
                externalRiverMask = adjusted; // ensure we now have (width+1)x(height+1)
            }
        }

        if (externalRiverMask != null)
        {
            riverMask = externalRiverMask;
            heights = CarveRiversFromMask(heights, riverMask); // riverMask can be (width+1)x(height+1)
        }
        else if (enableRivers)
        {
            // Legacy per-tile generation gives mask width x height; carving only affects interior samples.
            heights = GenerateRivers(heights, out riverMask);
        }
        else
        {
            // Provide an empty mask sized to splat logic (use width+1 for consistency)
            riverMask = new float[width + 1, height + 1];
        }

        // Textures (ApplyTextures tolerates both width/height and width+1/height+1)
        ApplyTextures(data, heights, riverMask);

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

    // Precomputed falloff kernel cache (radius -> samples)
    static class RiverKernelCache
    {
        // Key: radius (int). Value: (dx, dy, falloff)
        private static readonly System.Collections.Generic.Dictionary<int, (int dx, int dy, float f)[]> _cache
            = new System.Collections.Generic.Dictionary<int, (int, int, float)[]>();

        public static (int dx, int dy, float f)[] Get(int radius, float bankSoftness)
        {
            if (_cache.TryGetValue(radius, out var arr)) return arr;
            var list = new System.Collections.Generic.List<(int, int, float)>();
            float r = radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    float nd = dist / Mathf.Max(0.0001f, r);
                    float falloff = Mathf.Pow(1f - nd, bankSoftness);
                    if (falloff > 0.0001f)
                        list.Add((dx, dy, falloff));
                }
            }
            arr = list.ToArray();
            _cache[radius] = arr;
            return arr;
        }
    }

    // Faster path builder: single smoothing pass + cached noise arrays
    float[] BuildSmoothRiverPathFast(int samplesX, int samplesY, int startY, int seedBase)
    {
        samplesX = Mathf.Max(2, samplesX);
        samplesY = Mathf.Max(2, samplesY);

        // Cache primary drift and wiggle noise in arrays (1 Perlin call each per sample)
        float[] drift = new float[samplesX];
        float[] wiggle = new float[samplesX];

        float lowF  = Mathf.Max(0.0001f, riverLowFrequency);
        float highF = Mathf.Max(0.0001f, riverHighFrequency);
        float roughF = Mathf.Max(0.0001f, riverRoughnessFrequency);

        // Seeds
        float seedA = (seedBase * 0.137f) % 10000f;
        float seedB = (seedBase * 0.713f) % 10000f;
        float seedC = (seedBase * 0.333f) % 10000f;

        float highAmp = riverHighAmplitude * (0.2f + riverWindiness * 0.8f);
        for (int i = 0; i < samplesX; i++)
        {
            float t = (float)i / (samplesX - 1);
            drift[i]  = (Mathf.PerlinNoise(t * lowF  + seedA, seedA) * 2f - 1f) * riverLowAmplitude;
            wiggle[i] = (Mathf.PerlinNoise(t * highF + seedB, seedB) * 2f - 1f) * highAmp;
        }

        float[] path = new float[samplesX];
        float baseNorm = Mathf.Clamp01((float)startY / (samplesY - 1));
        for (int i = 0; i < samplesX; i++)
            path[i] = Mathf.Clamp01(baseNorm + drift[i] + wiggle[i]) * (samplesY - 1);

        // Single smoothing pass (3-point box)
        if (riverSmoothPasses > 0)
        {
            float[] smoothed = new float[samplesX];
            for (int i = 0; i < samplesX; i++)
            {
                float a = path[Mathf.Max(0, i - 1)];
                float b = path[i];
                float c = path[Mathf.Min(samplesX - 1, i + 1)];
                smoothed[i] = (a + b + c) / 3f;
            }
            path = smoothed;
        }

        // Roughness added post-smooth
        if (riverRoughness > 0f)
        {
            for (int i = 0; i < samplesX; i++)
            {
                float t = (float)i / (samplesX - 1);
                float rough = (Mathf.PerlinNoise(t * roughF + seedC, seedC) * 2f - 1f) * riverRoughness;
                path[i] = Mathf.Clamp(path[i] + rough * (samplesY - 1) * 0.05f, 0f, samplesY - 1);
            }
        }

        return path;
    }

    // Optimized per-tile river carving
    float[,] GenerateRivers(float[,] heights, out float[,] riverMask)
    {
        // Ensure bounds sane
        int effectiveMin = Mathf.Max(0, minRivers);
        int effectiveMax = Mathf.Max(effectiveMin, maxRivers);

        riverMask = new float[width, height];
        int riverCount = Random.Range(effectiveMin, effectiveMax + 1);
        if (riverCount <= 0) return heights;

        const int pathStep = 1;

        for (int r = 0; r < riverCount; r++)
        {
            int riverSeed = seed + r * 1000;
            int startY = Random.Range(height / 4, 3 * height / 4);
            float[] path = BuildSmoothRiverPathFast(width, height, startY, riverSeed);

            float jitterSeed = (riverSeed * 1.917f) % 10000f;
            float wFreq = Mathf.Max(0.0001f, riverWidthJitterFrequency);

            for (int x = 0; x < width; x += pathStep)
            {
                int x0 = x;
                int x1 = Mathf.Min(x + pathStep, width - 1);
                float t = (float)(x - x0) / Mathf.Max(1, x1 - x0);
                float centerY = Mathf.Lerp(path[x0], path[x1], t);
                int cYInt = Mathf.RoundToInt(centerY);

                float tWorld = (float)x / Mathf.Max(1, width - 1);
                float jNoise = (Mathf.PerlinNoise(tWorld * wFreq + jitterSeed, jitterSeed) * 2f - 1f) * riverWidthJitter;
                float localWidth = riverWidth * (1f + jNoise);
                if (localWidth < 1.01f)
                {
                    int nx = Mathf.Clamp(x, 0, width - 1);
                    int ny = Mathf.Clamp(cYInt, 0, height - 1);
                    CarveSample(heights, riverMask, nx, ny, 1f);
                    continue;
                }

                int radius = Mathf.Clamp(Mathf.RoundToInt(localWidth), 1, 64);
                var kernel = RiverKernelCache.Get(radius, riverBankSoftness);

                int nextX = Mathf.Min(x + 1, width - 1);
                float dirY = path[nextX] - path[x];
                Vector2 perp = new Vector2(-dirY, 1f).normalized; // (reserved for future anisotropic stamping)

                foreach (var (dx, dy, f) in kernel)
                {
                    int nx = x + dx;
                    int ny = cYInt + dy;
                    if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;
                    CarveSample(heights, riverMask, nx, ny, f);
                }
            }
        }

        return heights;
    }

    // Legacy slower river generation retained for comparison / fallback
    [System.Obsolete("Use GenerateRivers() (optimized) instead. This version kept for reference.")]
    float[,] GenerateRiversLegacy(float[,] heights, out float[,] riverMask)
    {
        riverMask = new float[width, height];
        int riverCount = Random.Range(minRivers, maxRivers + 1);

        for (int r = 0; r < riverCount; r++)
        {
            int riverSeed = seed + r * 1000;
            int startY = Random.Range(height / 4, 3 * height / 4);
            float[] riverPath = BuildSmoothRiverPath(width, height, startY, riverSeed);

            float step = 0.2f;
            for (float riverX = 0; riverX < width - 1; riverX += step)
            {
                int x0 = Mathf.FloorToInt(riverX);
                int x1 = Mathf.Min(Mathf.CeilToInt(riverX), width - 1);
                float t = Mathf.Clamp01(riverX - x0);
                float centerY = Mathf.Lerp(riverPath[x0], riverPath[x1], t);

                float dirY = riverPath[x1] - riverPath[x0];
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

                        float normalizedDist = dist / Mathf.Max(0.0001f, riverWidth);
                        float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), riverBankSoftness);

                        WorldXYFromSample(nx, ny, out float xWorld, out float yWorld);
                        float biomeNoiseRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
                        float riverDepthMultiplier = Mathf.Lerp(1.5f, 1f, biomeNoiseRaw);
                        float finalRiverDepth = riverDepth * riverDepthMultiplier;

                        heights[nx, ny] = Mathf.Lerp(heights[nx, ny], finalRiverDepth, falloff);
                        riverMask[nx, ny] = Mathf.Max(riverMask[nx, ny], falloff);
                    }
                }
            }
        }
        return heights;
    }

    // Inline carve combining biome depth logic
    void CarveSample(float[,] heights, float[,] mask, int sx, int sy, float falloff)
    {
        WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);
        float biomeRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
        float depthMul = Mathf.Lerp(1.5f, 1f, biomeRaw);
        float targetDepth = riverDepth * depthMul;

        heights[sx, sy] = Mathf.Lerp(heights[sx, sy], targetDepth, falloff);
        mask[sx, sy] = Mathf.Max(mask[sx, sy], falloff);
    }

    // NEW: Carve rivers using a precomputed mask (supports [width,height] or [width+1,height+1]).
    float[,] CarveRiversFromMask(float[,] heights, float[,] riverMask)
    {
        int maskW = riverMask.GetLength(0);
        int maskH = riverMask.GetLength(1);

        // Heights is [width+1, height+1]
        for (int sx = 0; sx <= width; sx++)
        {
            for (int sy = 0; sy <= height; sy++)
            {
                // Sample mask, clamped (works for both mask sizes)
                int mx = Mathf.Clamp(sx, 0, maskW - 1);
                int my = Mathf.Clamp(sy, 0, maskH - 1);
                float m = riverMask[mx, my];
                if (m <= 0f) continue;

                WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);

                // Deeper in salt flats (use RAW biome noise)
                float biomeNoiseRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
                float riverDepthMultiplier = Mathf.Lerp(1.5f, 1f, biomeNoiseRaw);
                float targetDepth = riverDepth * riverDepthMultiplier;

                heights[sx, sy] = Mathf.Lerp(heights[sx, sy], targetDepth, m);
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

        int maskW = riverMask.GetLength(0);
        int maskH = riverMask.GetLength(1);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int hmX = Mathf.RoundToInt((float)x / (mapWidth - 1) * (width - 1));
                int hmY = Mathf.RoundToInt((float)y / (mapHeight - 1) * (height - 1));

                // Safe river sampling
                int rx = Mathf.Clamp(hmX, 0, maskW - 1);
                int ry = Mathf.Clamp(hmY, 0, maskH - 1);
                float river = riverMask[rx, ry];

                WorldXYFromSample(hmX, hmY, out float xWorld, out float yWorld);
                float biomeBlend = SampleBiomeBlend(xWorld, yWorld);

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

    // Optional: ensure TransposeHeightmap contains the assignment
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

    // Smooth river path builder (horizontal orientation)
    float[] BuildSmoothRiverPath(int samples, int verticalSamples, int startY, int seedBase)
    {
        samples = Mathf.Max(2, samples);
        verticalSamples = Mathf.Max(2, verticalSamples);

        float[] path = new float[samples];
        float lowFreq = Mathf.Max(0.0001f, riverLowFrequency);
        float highFreq = Mathf.Max(0.0001f, riverHighFrequency);

        float seedA = (seedBase * 0.137f) % 10000f;
        float seedB = (seedBase * 0.713f) % 10000f;

        // high-frequency amplitude still scales by windiness
        float highAmp = riverHighAmplitude * (0.2f + riverWindiness * 0.8f);

        // 1) Base curve (broad drift + finer wiggle)
        for (int x = 0; x < samples; x++)
        {
            float t = (float)x / (samples - 1);
            float drift = (Mathf.PerlinNoise(t * lowFreq + seedA, seedA) * 2f - 1f) * riverLowAmplitude;
            float wiggle = (Mathf.PerlinNoise(t * highFreq + seedB, seedB) * 2f - 1f) * highAmp;

            float yNormStart = Mathf.Clamp01((float)startY / (verticalSamples - 1));
            float yNorm = Mathf.Clamp01(yNormStart + drift + wiggle);
            path[x] = yNorm * (verticalSamples - 1);
        }

        // 2) Smoothing passes
        if (riverSmoothPasses > 0)
        {
            float[] work = new float[samples];
            for (int pass = 0; pass < riverSmoothPasses; pass++)
            {
                for (int i = 0; i < samples; i++)
                {
                    float a = path[Mathf.Max(0, i - 1)];
                    float b = path[i];
                    float c = path[Mathf.Min(samples - 1, i + 1)];
                    work[i] = (a + b + c) / 3f;
                }
                var tmp = path; path = work; work = tmp;
            }
        }

        // 3) Post-smoothing roughness (adds life back without jaggies)
        if (riverRoughness > 0f)
        {
            float seedC = (seedBase * 0.333f) % 10000f;
            float roughFreq = Mathf.Max(0.0001f, riverRoughnessFrequency);
            for (int x = 0; x < samples; x++)
            {
                float t = (float)x / (samples - 1);
                float rough = (Mathf.PerlinNoise(t * roughFreq + seedC, seedC) * 2f - 1f) * riverRoughness;
                path[x] = Mathf.Clamp(path[x] + rough * (verticalSamples - 1) * 0.05f, 0f, verticalSamples - 1);
            }
        }

        return path;
    }
}
