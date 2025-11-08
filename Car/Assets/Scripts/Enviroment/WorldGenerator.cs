using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("World Grid")]
    public int tilesX = 2;
    public int tilesY = 2;

    [Header("Per-Tile World Size (meters)")]
    public float tileWorldWidth = 1000f;   // Unity X per tile
    public float tileWorldLength = 1000f;  // Unity Z per tile

    [Header("Heightmap Resolution per Tile")]
    public int heightmapResolution = 256;  // samples (width = height)
    public int verticalDepth = 20;         // Terrain vertical scale (meters)

    [Header("Tile Prefab (recommended)")]
    [Tooltip("Prefab with Terrain + TerrainCollider + TerrainGenerator configured (textures, dune settings). Optional; if null, components will be added at runtime.")]
    public GameObject tilePrefab;

    [Header("Generation")]
    public bool autoGenerateAtStart = true;
    public bool setNeighbors = true;
    public bool clearExistingChildren = true;

    [Header("Seed")]
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Rivers")]
    [Tooltip("Per-tile rivers can cause seams. Recommended OFF for tiled worlds if 'Use Global Rivers' is ON.")]
    public bool enableRivers = false;

    [Tooltip("If ON, rivers are generated once across the whole world and applied seamlessly to all tiles.")]
    public bool useGlobalRivers = true;

    public void Start()
    {
        if (autoGenerateAtStart)
        {
            GenerateWorld();
        }
    }

    public void GenerateWorld()
    {
        if (clearExistingChildren)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
                #else
                Destroy(child.gameObject);
                #endif
            }
        }

        if (useRandomSeed)
        {
            seed = Random.Range(0, 10000);
        }

        Terrain[,] terrainGrid = new Terrain[tilesX, tilesY];
        TerrainGenerator[,] genGrid = new TerrainGenerator[tilesX, tilesY];

        // 1) Create tiles and configure components, but do NOT generate yet.
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                Vector3 pos = new Vector3(gx * tileWorldWidth, 0f, gy * tileWorldLength);
                GameObject go;

                if (tilePrefab != null)
                {
                    go = Instantiate(tilePrefab, pos, Quaternion.identity, this.transform);
                }
                else
                {
                    go = new GameObject($"Terrain_{gx}_{gy}");
                    go.transform.parent = this.transform;
                    go.transform.position = pos;
                }

                go.name = $"Terrain_{gx}_{gy}";

                // Ensure components
                var terrain = go.GetComponent<Terrain>();
                if (terrain == null) terrain = go.AddComponent<Terrain>();

                var collider = go.GetComponent<TerrainCollider>();
                if (collider == null) collider = go.AddComponent<TerrainCollider>();

                var gen = go.GetComponent<TerrainGenerator>();
                if (gen == null) gen = go.AddComponent<TerrainGenerator>();

                // Configure generator for this tile
                gen.autoGenerate = false;
                gen.useRandomSeed = false; // keep a shared seed for the whole world
                gen.seed = seed;

                gen.width = heightmapResolution;
                gen.height = heightmapResolution;
                gen.depth = verticalDepth;

                gen.terrainWidth = tileWorldWidth;
                gen.terrainLength = tileWorldLength;

                gen.worldOrigin = new Vector2(gx * tileWorldWidth, gy * tileWorldLength);

                // Respect top-level switch but we'll override rivers with a global mask if useGlobalRivers is true
                gen.enableRivers = enableRivers;

                gen.globalWorldWidth = tilesX * tileWorldWidth;
                gen.globalWorldLength = tilesY * tileWorldLength;
                gen.useGlobalSeamless = true; // ensure seamless transitions

                // Initialize offsets (seed etc.) but defer Generate()
                gen.InitializeSeed();

                terrainGrid[gx, gy] = terrain;
                genGrid[gx, gy] = gen;
            }
        }

        // 2) Optionally build one global river mask across entire world and slice per tile.
        if (useGlobalRivers && tilesX > 0 && tilesY > 0)
        {
            var refGen = genGrid[0, 0];

            // Height-sample counts (note +1 for shared edges)
            int samplesX = tilesX * heightmapResolution + 1;
            int samplesY = tilesY * heightmapResolution + 1;

            float[,] globalMask = GenerateGlobalRiverMaskSamples(
                samplesX, samplesY,
                seed,
                refGen.minRivers, refGen.maxRivers,
                refGen.riverWidth, refGen.riverWindiness, refGen.riverBankSoftness,
                refGen // new argument
            );

            // Slice per tile as (width+1) x (height+1)
            for (int gy = 0; gy < tilesY; gy++)
            {
                for (int gx = 0; gx < tilesX; gx++)
                {
                    int tileW = heightmapResolution;
                    int tileH = heightmapResolution;

                    float[,] slice = new float[tileW + 1, tileH + 1];
                    int xStart = gx * tileW;
                    int yStart = gy * tileH;

                    for (int y = 0; y <= tileH; y++)
                    {
                        for (int x = 0; x <= tileW; x++)
                        {
                            slice[x, y] = globalMask[xStart + x, yStart + y];
                        }
                    }
                    genGrid[gx, gy].SetExternalRiverMask(slice);
                }
            }
        }

        // 3) Now actually generate each tile.
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                genGrid[gx, gy].Generate();
            }
        }

        // 4) Neighbor links
        if (setNeighbors)
        {
            for (int gy = 0; gy < tilesY; gy++)
            {
                for (int gx = 0; gx < tilesX; gx++)
                {
                    var t = terrainGrid[gx, gy];
                    Terrain left   = (gx > 0)           ? terrainGrid[gx - 1, gy] : null;
                    Terrain right  = (gx < tilesX - 1)  ? terrainGrid[gx + 1, gy] : null;
                    Terrain bottom = (gy > 0)           ? terrainGrid[gx, gy - 1] : null;
                    Terrain top    = (gy < tilesY - 1)  ? terrainGrid[gx, gy + 1] : null;

                    // Unity's SetNeighbors signature is (left, top, right, bottom)
                    t.SetNeighbors(left, top, right, bottom);
                }
            }
        }

        // Flush terrain data to ensure all changes are applied
        for (int gy = 0; gy < tilesY; gy++)
            for (int gx = 0; gx < tilesX; gx++)
                terrainGrid[gx, gy].Flush();
    }

    // Build one global river mask in height-sample space [0..samplesX-1] x [0..samplesY-1].
    // NOTE: samplesX = tilesX*tileWidthSamples + 1; samplesY = tilesY*tileHeightSamples + 1
    float[,] GenerateGlobalRiverMaskSamples(
        int samplesX, int samplesY, int baseSeed,
        int minRivers, int maxRivers,
        float riverWidth, float riverWindiness, float riverBankSoftness,
        TerrainGenerator refGen
    )
    {
        float[,] mask = new float[samplesX, samplesY];
        var countRng = new System.Random(baseSeed);
        int riverCount = Mathf.Clamp(countRng.Next(minRivers, maxRivers + 1), 0, int.MaxValue);
        if (riverCount <= 0) return mask;

        // Precompute kernels per distinct radius encountered (lazy)
        System.Collections.Generic.Dictionary<int, (int dx, int dy, float f)[]> kernelCache =
            new System.Collections.Generic.Dictionary<int, (int, int, float)[]>();

        int primaryLenH = samplesX;
        int primaryLenV = samplesY;

        // Step = 1 sample instead of 0.3 -> big speed-up
        const int pathStep = 1;

        for (int r = 0; r < riverCount; r++)
        {
            var rnd = new System.Random(baseSeed + 1000 * (r + 1));
            bool vertical = rnd.NextDouble() > 0.5;

            // Shared jitter seeds
            float wSeed = (baseSeed + r * 541) * 0.019f;
            float wSeed2 = (baseSeed + r * 937) * 0.027f;

            if (!vertical)
            {
                int startY = rnd.Next(primaryLenV / 4, 3 * primaryLenV / 4);
                float[] path = BuildGlobalPathFast(
                    primaryLenH, primaryLenV, startY, baseSeed + r * 97,
                    refGen.riverLowFrequency,
                    refGen.riverHighFrequency,
                    refGen.riverLowAmplitude,
                    refGen.riverHighAmplitude * (0.2f + refGen.riverWindiness * 0.8f),
                    refGen.riverSmoothPasses,
                    refGen.riverRoughness,
                    refGen.riverRoughnessFrequency
                );

                float wFreq = Mathf.Max(0.0001f, refGen.riverWidthJitterFrequency);

                for (int x = 0; x < primaryLenH; x += pathStep)
                {
                    int cYInt = Mathf.RoundToInt(path[x]);

                    float tWorld = (float)x / Mathf.Max(1, primaryLenH - 1);
                    float jitter = (Mathf.PerlinNoise(tWorld * wFreq + wSeed, wSeed) * 2f - 1f) * refGen.riverWidthJitter;
                    float localWidth = Mathf.Max(0f, riverWidth * (1f + jitter));

                    if (localWidth < 1.01f)
                    {
                        Stamp(mask, x, cYInt, 1f);
                        continue;
                    }

                    int radius = Mathf.Clamp(Mathf.RoundToInt(localWidth), 1, 128);
                    if (!kernelCache.TryGetValue(radius, out var kernel))
                    {
                        // Build kernel once
                        var list = new System.Collections.Generic.List<(int, int, float)>();
                        float rRad = radius;
                        for (int dy = -radius; dy <= radius; dy++)
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                                if (dist > rRad) continue;
                                float nd = dist / rRad;
                                float falloff = Mathf.Pow(1f - nd, riverBankSoftness);
                                if (falloff > 0.0001f)
                                    list.Add((dx, dy, falloff));
                            }
                        kernel = list.ToArray();
                        kernelCache[radius] = kernel;
                    }

                    foreach (var (dx, dy, f) in kernel)
                        Stamp(mask, x + dx, cYInt + dy, f);
                }
            }
            else
            {
                int startX = rnd.Next(primaryLenH / 4, 3 * primaryLenH / 4);
                float[] path = BuildGlobalPathFast(
                    primaryLenV, primaryLenH, startX, baseSeed + r * 173,
                    refGen.riverLowFrequency,
                    refGen.riverHighFrequency,
                    refGen.riverLowAmplitude,
                    refGen.riverHighAmplitude * (0.2f + refGen.riverWindiness * 0.8f),
                    refGen.riverSmoothPasses,
                    refGen.riverRoughness,
                    refGen.riverRoughnessFrequency
                );

                float wFreq = Mathf.Max(0.0001f, refGen.riverWidthJitterFrequency);

                for (int y = 0; y < primaryLenV; y += pathStep)
                {
                    int cXInt = Mathf.RoundToInt(path[y]);

                    float tWorld = (float)y / Mathf.Max(1, primaryLenV - 1);
                    float jitter = (Mathf.PerlinNoise(tWorld * wFreq + wSeed2, wSeed2) * 2f - 1f) * refGen.riverWidthJitter;
                    float localWidth = Mathf.Max(0f, riverWidth * (1f + jitter));

                    if (localWidth < 1.01f)
                    {
                        Stamp(mask, cXInt, y, 1f);
                        continue;
                    }

                    int radius = Mathf.Clamp(Mathf.RoundToInt(localWidth), 1, 128);
                    if (!kernelCache.TryGetValue(radius, out var kernel))
                    {
                        var list = new System.Collections.Generic.List<(int, int, float)>();
                        float rRad = radius;
                        for (int dy = -radius; dy <= radius; dy++)
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                                if (dist > rRad) continue;
                                float nd = dist / rRad;
                                float falloff = Mathf.Pow(1f - nd, riverBankSoftness);
                                if (falloff > 0.0001f)
                                    list.Add((dx, dy, falloff));
                            }
                        kernel = list.ToArray();
                        kernelCache[radius] = kernel;
                    }

                    foreach (var (dx, dy, f) in kernel)
                        Stamp(mask, cXInt + dx, y + dy, f);
                }
            }
        }

        return mask;

        void Stamp(float[,] m, int sx, int sy, float value)
        {
            if ((uint)sx >= (uint)samplesX || (uint)sy >= (uint)samplesY) return;
            if (value > m[sx, sy]) m[sx, sy] = value;
        }
    }

    // Faster global path (mirrors tile fast builder)
    float[] BuildGlobalPathFast(
        int primaryLen,
        int perpendicularLen,
        int startPos,
        int seedBase,
        float lowFreqCycles,
        float highFreqCycles,
        float lowAmp,
        float highAmp,
        int smoothPasses,
        float roughness,
        float roughnessFrequency
    )
    {
        primaryLen = Mathf.Max(2, primaryLen);
        perpendicularLen = Mathf.Max(2, perpendicularLen);

        float[] path = new float[primaryLen];

        float seedA = (seedBase * 0.149f) % 10000f;
        float seedB = (seedBase * 0.757f) % 10000f;
        float seedC = (seedBase * 0.333f) % 10000f;

        float baseNorm = Mathf.Clamp01((float)startPos / (perpendicularLen - 1));
        float lowF  = Mathf.Max(0.0001f, lowFreqCycles);
        float highF = Mathf.Max(0.0001f, highFreqCycles);
        float roughF = Mathf.Max(0.0001f, roughnessFrequency);

        for (int i = 0; i < primaryLen; i++)
        {
            float t = (float)i / (primaryLen - 1);
            float drift  = (Mathf.PerlinNoise(t * lowF  + seedA, seedA) * 2f - 1f) * lowAmp;
            float wiggle = (Mathf.PerlinNoise(t * highF + seedB, seedB) * 2f - 1f) * highAmp;
            float val = Mathf.Clamp01(baseNorm + drift + wiggle) * (perpendicularLen - 1);
            path[i] = val;
        }

        if (smoothPasses > 0)
        {
            float[] smoothed = new float[primaryLen];
            for (int i = 0; i < primaryLen; i++)
            {
                float a = path[Mathf.Max(0, i - 1)];
                float b = path[i];
                float c = path[Mathf.Min(primaryLen - 1, i + 1)];
                smoothed[i] = (a + b + c) / 3f;
            }
            path = smoothed;
        }

        if (roughness > 0f)
        {
            for (int i = 0; i < primaryLen; i++)
            {
                float t = (float)i / (primaryLen - 1);
                float rough = (Mathf.PerlinNoise(t * roughF + seedC, seedC) * 2f - 1f) * roughness;
                path[i] = Mathf.Clamp(path[i] + rough * (perpendicularLen - 1) * 0.05f, 0f, perpendicularLen - 1);
            }
        }

        return path;
    }
}
