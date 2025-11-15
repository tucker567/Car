using System;
using System.Collections;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("World Grid")]
    public int tilesX = 2;
    public int tilesY = 2;

    [Header("Per-Tile World Size (meters)")]
    public float tileWorldWidth = 1000f;
    public float tileWorldLength = 1000f;

    [Header("Heightmap Resolution per Tile")]
    public int heightmapResolution = 256;
    public int verticalDepth = 20;

    [Header("Tile Prefab (optional)")]
    public GameObject tilePrefab;

    // Async + reporting
    [Header("Async Generation & Reporting")]
    public bool generateAsync = true;

    [Header("Generation Flow")]
    public bool autoGenerateAtStart = true;
    public bool setNeighbors = true;
    public bool clearExistingChildren = true;

    [Header("Global Rivers")]
    public bool useGlobalRivers = true;
    public bool enablePerTileFallbackRivers = false;

    [Header("Seed")]
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Terrain Generation Settings (moved from TerrainGenerator)")]
    public TerrainGenerationSettings terrainSettings = new TerrainGenerationSettings();

    [Header("Points of Interest")]
    [Tooltip("Prefab for a cell tower point of interest.")] public GameObject cellTowerPrefab;
    [Tooltip("If ON, will place cell towers after terrain generation.")] public bool placeCellTowers = true;
    [Tooltip("Average tiles per one tower (roughly one tower per this many tiles).")]
    [Min(1)] public int tilesPerTower = 6;
    [Tooltip("Optional vertical offset applied after sampling terrain height.")] public float cellTowerHeightOffset = 0f;
    [Tooltip("Attach a CellTowerMarker component to spawned towers if missing.")] public bool attachTowerMarkerComponent = true;
    [Tooltip("Prefix applied to spawned tower GameObject names for discovery.")] public string towerNamePrefix = "CellTower_";


    public event Action<string> OnNote;
    public event Action<float> OnProgress;
    public event Action OnGenerationComplete;

    void Note(string msg) => OnNote?.Invoke(msg);
    void Progress(float p) => OnProgress?.Invoke(Mathf.Clamp01(p));

    public void Start()
    {
        if (!autoGenerateAtStart) return;

        if (generateAsync)
            StartCoroutine(GenerateWorldAsync());
        else
            GenerateWorld();
    }

    public void GenerateWorld()
    {
        if (clearExistingChildren)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }

        if (useRandomSeed)
            seed = UnityEngine.Random.Range(0, 10000);

        // Overwrite shared seed in settings (so inspector shows same)
        terrainSettings.seed = seed;
        terrainSettings.useRandomSeed = false; // world manages determinism

        Terrain[,] terrainGrid = new Terrain[tilesX, tilesY];
        TerrainGenerator[,] genGrid = new TerrainGenerator[tilesX, tilesY];

        // 1. Create tiles (defer actual height generation)
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                Vector3 pos = new Vector3(gx * tileWorldWidth, 0f, gy * tileWorldLength);
                GameObject go = tilePrefab != null
                    ? Instantiate(tilePrefab, pos, Quaternion.identity, transform)
                    : new GameObject($"Terrain_{gx}_{gy}");

                go.name = $"Terrain_{gx}_{gy}";
                go.transform.position = pos;
                go.transform.parent = transform;

                var terrain = go.GetComponent<Terrain>() ?? go.AddComponent<Terrain>();
                var collider = go.GetComponent<TerrainCollider>() ?? go.AddComponent<TerrainCollider>();
                var gen = go.GetComponent<TerrainGenerator>() ?? go.AddComponent<TerrainGenerator>();

                // Configure per-tile core dimensions
                gen.SetDimensions(heightmapResolution, heightmapResolution, verticalDepth);
                gen.SetWorldSize(tileWorldWidth, tileWorldLength);
                gen.SetWorldOrigin(new Vector2(gx * tileWorldWidth, gy * tileWorldLength));

                // Copy settings reference (shared object)
                gen.ApplySettings(terrainSettings);

                // Rivers: allow per-tile generation only if global rivers disabled
                gen.OverridePerTileRiverEnable(enablePerTileFallbackRivers && !useGlobalRivers);

                // Provide global extents
                gen.SetGlobalExtents(tilesX * tileWorldWidth, tilesY * tileWorldLength);

                // Initialize seeds (now driven by settings)
                gen.InitializeSeed();

                terrainGrid[gx, gy] = terrain;
                genGrid[gx, gy] = gen;
            }
        }

        // 2. Global river mask (optional)
        if (useGlobalRivers && tilesX > 0 && tilesY > 0)
        {
            int samplesX = tilesX * heightmapResolution + 1;
            int samplesY = tilesY * heightmapResolution + 1;

            float[,] globalMask = GenerateGlobalRiverMaskSamples(
                samplesX, samplesY,
                seed,
                terrainSettings
            );

            // Slice per tile
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
                        for (int x = 0; x <= tileW; x++)
                            slice[x, y] = globalMask[xStart + x, yStart + y];

                    genGrid[gx, gy].SetExternalRiverMask(slice);
                }
            }
        }

        // 3. Generate tiles
        for (int gy = 0; gy < tilesY; gy++)
            for (int gx = 0; gx < tilesX; gx++)
                genGrid[gx, gy].Generate();

        // 4. Neighbor stitching
        if (setNeighbors)
        {
            for (int gy = 0; gy < tilesY; gy++)
            {
                for (int gx = 0; gx < tilesX; gx++)
                {
                    var t = terrainGrid[gx, gy];
                    Terrain left = (gx > 0) ? terrainGrid[gx - 1, gy] : null;
                    Terrain right = (gx < tilesX - 1) ? terrainGrid[gx + 1, gy] : null;
                    Terrain bottom = (gy > 0) ? terrainGrid[gx, gy - 1] : null;
                    Terrain top = (gy < tilesY - 1) ? terrainGrid[gx, gy + 1] : null;
                    t.SetNeighbors(left, top, right, bottom);
                }
            }
        }

        // 5. Flush
        for (int gy = 0; gy < tilesY; gy++)
            for (int gx = 0; gx < tilesX; gx++)
                terrainGrid[gx, gy].Flush();

        // 6. Points of Interest (Cell Towers)
        PlaceCellTowers(terrainGrid);
    }

    // Build one global river mask in height-sample space [0..samplesX-1] x [0..samplesY-1].
    // NOTE: samplesX = tilesX*tileWidthSamples + 1; samplesY = tilesY*tileHeightSamples + 1
    float[,] GenerateGlobalRiverMaskSamples(
        int samplesX,
        int samplesY,
        int baseSeed,
        TerrainGenerationSettings s
    )
    {
        float[,] mask = new float[samplesX, samplesY];
        var countRng = new System.Random(baseSeed);
        int riverCount = Mathf.Clamp(countRng.Next(s.minRivers, s.maxRivers + 1), 0, int.MaxValue);
        if (riverCount <= 0) return mask;

        var kernelCache = new System.Collections.Generic.Dictionary<int, (int dx, int dy, float f)[]>();

        int primaryLenH = samplesX;
        int primaryLenV = samplesY;
        const int pathStep = 1;

        for (int r = 0; r < riverCount; r++)
        {
            var rnd = new System.Random(baseSeed + 1000 * (r + 1));
            bool vertical = rnd.NextDouble() > 0.5;

            float wSeed = (baseSeed + r * 541) * 0.019f;
            float wSeed2 = (baseSeed + r * 937) * 0.027f;

            if (!vertical)
            {
                int startY = rnd.Next(primaryLenV / 4, 3 * primaryLenV / 4);
                float[] path = BuildGlobalPathFast(
                    primaryLenH, primaryLenV, startY, baseSeed + r * 97,
                    s.riverLowFrequency,
                    s.riverHighFrequency,
                    s.riverLowAmplitude,
                    s.riverHighAmplitude * (0.2f + s.riverWindiness * 0.8f),
                    s.riverSmoothPasses,
                    s.riverRoughness,
                    s.riverRoughnessFrequency
                );

                float wFreq = Mathf.Max(0.0001f, s.riverWidthJitterFrequency);

                for (int x = 0; x < primaryLenH; x += pathStep)
                {
                    int cYInt = Mathf.RoundToInt(path[x]);

                    float tWorld = (float)x / Mathf.Max(1, primaryLenH - 1);
                    float jitter = (Mathf.PerlinNoise(tWorld * wFreq + wSeed, wSeed) * 2f - 1f) * s.riverWidthJitter;
                    float localWidth = Mathf.Max(0f, s.riverWidth * (1f + jitter));

                    if (localWidth < 1.01f)
                    {
                        Stamp(mask, x, cYInt, 1f);
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
                                float falloff = Mathf.Pow(1f - nd, s.riverBankSoftness);
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
                    s.riverLowFrequency,
                    s.riverHighFrequency,
                    s.riverLowAmplitude,
                    s.riverHighAmplitude * (0.2f + s.riverWindiness * 0.8f),
                    s.riverSmoothPasses,
                    s.riverRoughness,
                    s.riverRoughnessFrequency
                );

                float wFreq = Mathf.Max(0.0001f, s.riverWidthJitterFrequency);

                for (int y = 0; y < primaryLenV; y += pathStep)
                {
                    int cXInt = Mathf.RoundToInt(path[y]);

                    float tWorld = (float)y / Mathf.Max(1, primaryLenV - 1);
                    float jitter = (Mathf.PerlinNoise(tWorld * wFreq + wSeed2, wSeed2) * 2f - 1f) * s.riverWidthJitter;
                    float localWidth = Mathf.Max(0f, s.riverWidth * (1f + jitter));

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
                                float falloff = Mathf.Pow(1f - nd, s.riverBankSoftness);
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

    // Async coroutine that mirrors GenerateWorld but yields and reports progress/notes.
    public IEnumerator GenerateWorldAsync()
    {
        // Compute a rough step count for progress
        int total = 0;
        total += tilesX * tilesY;                    // create tiles
        total += useGlobalRivers ? 1 : 0;            // global rivers
        total += tilesX * tilesY;                    // per-tile generate
        total += setNeighbors ? tilesX * tilesY : 0; // stitch
        total += tilesX * tilesY;                    // flush
        int cellTowerCount = (placeCellTowers && cellTowerPrefab != null)
            ? Mathf.Max(1, Mathf.RoundToInt((tilesX * tilesY) / (float)Mathf.Max(1, tilesPerTower)))
            : 0;
        total += cellTowerCount;                     // towers

        int done = 0;
        void Step(string msg, int inc)
        {
            done += inc;
            Note(msg);
            Progress(total > 0 ? (float)done / total : 0f);
        }

        Note("Preparing world...");
        yield return null;

        // Clear existing children
        if (clearExistingChildren)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
                yield return null;
            }
        }

        if (useRandomSeed)
            seed = UnityEngine.Random.Range(0, 10000);

        // Overwrite shared seed in settings (so inspector shows same)
        terrainSettings.seed = seed;
        terrainSettings.useRandomSeed = false;

        Terrain[,] terrainGrid = new Terrain[tilesX, tilesY];
        TerrainGenerator[,] genGrid = new TerrainGenerator[tilesX, tilesY];

        // 1. Create tiles
        Note("Creating terrain tiles...");
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                Vector3 pos = new Vector3(gx * tileWorldWidth, 0f, gy * tileWorldLength);
                GameObject go = tilePrefab != null
                    ? Instantiate(tilePrefab, pos, Quaternion.identity, transform)
                    : new GameObject($"Terrain_{gx}_{gy}");

                go.name = $"Terrain_{gx}_{gy}";
                go.transform.position = pos;
                go.transform.parent = transform;

                var terrain = go.GetComponent<Terrain>() ?? go.AddComponent<Terrain>();
                var collider = go.GetComponent<TerrainCollider>() ?? go.AddComponent<TerrainCollider>();
                var gen = go.GetComponent<TerrainGenerator>() ?? go.AddComponent<TerrainGenerator>();

                gen.SetDimensions(heightmapResolution, heightmapResolution, verticalDepth);
                gen.SetWorldSize(tileWorldWidth, tileWorldLength);
                gen.SetWorldOrigin(new Vector2(gx * tileWorldWidth, gy * tileWorldLength));
                gen.ApplySettings(terrainSettings);
                gen.OverridePerTileRiverEnable(enablePerTileFallbackRivers && !useGlobalRivers);
                gen.SetGlobalExtents(tilesX * tileWorldWidth, tilesY * tileWorldLength);
                gen.InitializeSeed();

                terrainGrid[gx, gy] = terrain;
                genGrid[gx, gy] = gen;

                Step($"Created tile {gx},{gy}", 1);
                yield return null;
            }
        }

        // 2. Global river mask (optional)
        if (useGlobalRivers && tilesX > 0 && tilesY > 0)
        {
            Note("Building global rivers...");
            yield return null;

            int samplesX = tilesX * heightmapResolution + 1;
            int samplesY = tilesY * heightmapResolution + 1;

            float[,] globalMask = GenerateGlobalRiverMaskSamples(
                samplesX, samplesY,
                seed,
                terrainSettings
            );

            // Slice per tile
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
                        for (int x = 0; x <= tileW; x++)
                            slice[x, y] = globalMask[xStart + x, yStart + y];

                    genGrid[gx, gy].SetExternalRiverMask(slice);
                }
            }

            Step("Rivers prepared", 1);
            yield return null;
        }

        // 3. Generate tiles
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                Note($"Generating tile {gx},{gy}...");
                genGrid[gx, gy].Generate();
                Step($"Generated tile {gx},{gy}", 1);
                yield return null;
            }
        }

        // 4. Neighbor stitching
        if (setNeighbors)
        {
            Note("Stitching neighbors...");
            for (int gy = 0; gy < tilesY; gy++)
            {
                for (int gx = 0; gx < tilesX; gx++)
                {
                    var t = terrainGrid[gx, gy];
                    Terrain left = (gx > 0) ? terrainGrid[gx - 1, gy] : null;
                    Terrain right = (gx < tilesX - 1) ? terrainGrid[gx + 1, gy] : null;
                    Terrain bottom = (gy > 0) ? terrainGrid[gx, gy - 1] : null;
                    Terrain top = (gy < tilesY - 1) ? terrainGrid[gx, gy + 1] : null;
                    t.SetNeighbors(left, top, right, bottom);
                    Step($"Stitched tile {gx},{gy}", 1);
                    yield return null;
                }
            }
        }

        // 5. Flush
        Note("Finalizing...");
        for (int gy = 0; gy < tilesY; gy++)
        {
            for (int gx = 0; gx < tilesX; gx++)
            {
                terrainGrid[gx, gy].Flush();
                Step($"Finalized tile {gx},{gy}", 1);
                yield return null;
            }
        }

        // 6. Cell Towers
        if (cellTowerCount > 0)
        {
            Note("Placing cell towers...");
            yield return null;
            foreach (var _ in PlaceCellTowersAsync(terrainGrid, cellTowerCount))
            {
                Step("Placed cell tower", 1);
                yield return null;
            }
        }

        Progress(1f);
        Note("World ready!");
        OnGenerationComplete?.Invoke();
    }

    // Synchronous placement after world generation
    void PlaceCellTowers(Terrain[,] terrainGrid)
    {
        if (!placeCellTowers)
        {
            Note("Cell towers disabled.");
            return;
        }
        if (cellTowerPrefab == null)
        {
            Note("No cell tower prefab assigned; skipping.");
            return;
        }

        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0) return;
        int desired = Mathf.Max(1, Mathf.RoundToInt(totalTiles / (float)Mathf.Max(1, tilesPerTower)));

        var rng = new System.Random(seed + 987654321);
        var chosen = new System.Collections.Generic.HashSet<int>();
        while (chosen.Count < desired && chosen.Count < totalTiles)
            chosen.Add(rng.Next(totalTiles));

        foreach (int idx in chosen)
        {
            int gx = idx % tilesX;
            int gy = idx / tilesX;
            float baseX = gx * tileWorldWidth;
            float baseZ = gy * tileWorldLength;
            double nx = rng.NextDouble() * 0.6 + 0.2; // keep away from edges
            double nz = rng.NextDouble() * 0.6 + 0.2;
            float worldX = baseX + (float)nx * tileWorldWidth;
            float worldZ = baseZ + (float)nz * tileWorldLength;
            Terrain terrain = terrainGrid[gx, gy];
            float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
            Vector3 pos = new Vector3(worldX, heightSample + cellTowerHeightOffset, worldZ);
            GameObject tower = Instantiate(cellTowerPrefab, pos, Quaternion.Euler(90f, 0f, 0f), transform);
            tower.name = $"{towerNamePrefix}{gx}_{gy}_{idx}";
            Note($"Placed cell tower at tile {gx},{gy}.");
        }
    }

    // Async placement enumerator
    System.Collections.Generic.IEnumerable<int> PlaceCellTowersAsync(Terrain[,] terrainGrid, int countBudget)
    {
        if (!placeCellTowers || cellTowerPrefab == null) yield break;
        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0) yield break;
        int desired = Mathf.Min(countBudget, Mathf.Max(1, Mathf.RoundToInt(totalTiles / (float)Mathf.Max(1, tilesPerTower))));
        var rng = new System.Random(seed + 987654321);
        var chosen = new System.Collections.Generic.HashSet<int>();
        while (chosen.Count < desired && chosen.Count < totalTiles)
            chosen.Add(rng.Next(totalTiles));
        foreach (int idx in chosen)
        {
            int gx = idx % tilesX;
            int gy = idx / tilesX;
            float baseX = gx * tileWorldWidth;
            float baseZ = gy * tileWorldLength;
            double nx = rng.NextDouble() * 0.6 + 0.2;
            double nz = rng.NextDouble() * 0.6 + 0.2;
            float worldX = baseX + (float)nx * tileWorldWidth;
            float worldZ = baseZ + (float)nz * tileWorldLength;
            Terrain terrain = terrainGrid[gx, gy];
            float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
            Vector3 pos = new Vector3(worldX, heightSample + cellTowerHeightOffset, worldZ);
            GameObject tower = Instantiate(cellTowerPrefab, pos, Quaternion.Euler(90f, 0f, 0f), transform);
            tower.name = $"{towerNamePrefix}{gx}_{gy}_{idx}";
            Note($"Placed cell tower at tile {gx},{gy}.");
            yield return 0; // dummy to count progress
        }
    }
}
