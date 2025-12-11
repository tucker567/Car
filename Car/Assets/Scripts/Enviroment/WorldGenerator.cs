using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class PropGroup
{
    public SpawnCar spawnCar; // for utility functions
    public string name = "Group";
    public System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();
    [Header("Clusters")]
    [Tooltip("Manual cluster count if useTileRatio is OFF.")] public int clusterCount = 5; // fallback manual count
    [Tooltip("If ON, derive cluster count from world tile count using tilesPerCluster ratio.")] public bool useTileRatio = true;
    [Min(1), Tooltip("Average tiles per one prop cluster when useTileRatio is ON.")] public int tilesPerCluster = 12;
    public int minPerCluster = 3;
    public int maxPerCluster = 8;
    public float clusterRadius = 25f;
    public float yOffset = 0f;
    public bool randomYaw = true; // randomize Y rotation of props
    public string namePrefix = "Prop_";
    public float minDistanceBetweenClusters = 150f;
    [Header("Avoidance")]
    public bool avoidTowers = false;
    public float minDistanceFromTowers = 150f;
    public bool avoidWarehouses = false;
    public float minDistanceFromWarehouses = 150f;
    [Header("Adaptive Height")]
    [Tooltip("Choose how terrain height is sampled for each prop.")] public HeightMode heightMode = HeightMode.AverageSamples;
    [Min(1), Tooltip("Number of radial samples (used except in SingleSample mode).")]
    public int heightSamples = 5;
    [Tooltip("If a prefab's largest bound dimension >= this, treat as large building for bottom alignment.")] public float largePrefabSizeThreshold = 20f;
    [Tooltip("Extra sample radius multiplier for large prefabs (relative to their largest size).")]
    public float largePrefabExtraRadiusMultiplier = 0.5f; // 
    [Tooltip("Align pivot so bottom of large prefab touches terrain.")] public bool alignLargePrefabBottom = true;
    [Tooltip("Extra depth to sink large prefabs below sampled terrain.")] public float largePrefabAdditionalDepth = 3f;
    public enum HeightMode { SingleSample, AverageSamples, LowestSample, HighestSample }
}

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
    [Tooltip("Delay before starting generation to let UI update.")]
    public float generationStartDelaySeconds = 2f;

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

    [Header("Warehouses")]
    [Tooltip("Prefab for a warehouse point of interest.")] public GameObject warehousePrefab;
    [Tooltip("If ON, will place warehouses after terrain generation.")] public bool placeWarehouses = true;
    [Tooltip("Average tiles per one warehouse (roughly one warehouse per this many tiles).")]
    [Min(1)] public int tilesPerWarehouse = 10;
    [Tooltip("Optional vertical offset applied after sampling terrain height.")] public float warehouseHeightOffset = 0f;
    [Tooltip("Prefix applied to spawned warehouse GameObject names for discovery.")] public string warehouseNamePrefix = "Warehouse_";
    [Tooltip("Minimum distance from cell towers when placing warehouses (meters).")]
    public float minDistanceFromTowers = 250f;
    [Tooltip("Minimum distance between warehouses (meters).")]
    public float minDistanceBetweenWarehouses = 300f;
    [Tooltip("Randomize the Y rotation of placed warehouses.")]
    public bool randomizeWarehouseYaw = true;

    [Header("Prop Groups")] 
    [Tooltip("If ON, will place groups of random props after warehouses.")]
    public bool placePropGroups = true;
    [Tooltip("Groups of props to spawn in clusters across the world.")]
    public System.Collections.Generic.List<PropGroup> propGroups = new System.Collections.Generic.List<PropGroup>();

    [Header("Bunker Entrance")]
    [Tooltip("Prefab for the single bunker entrance.")] public GameObject bunkerEntrancePrefab;
    [Tooltip("If ON, spawns exactly one bunker entrance before props.")] public bool placeBunkerEntrance = true;
    [Tooltip("Vertical offset applied after sampling terrain height for bunker.")] public float bunkerEntranceHeightOffset = 0f;
    [Tooltip("Random yaw rotation for bunker entrance.")] public bool randomizeBunkerYaw = true;
    [Tooltip("Name prefix for spawned bunker entrance.")] public string bunkerEntranceNamePrefix = "BunkerEntrance_";
    [Tooltip("Normalized tile margin to keep bunker away from tile edges (0..0.5).")][Range(0f,0.5f)] public float bunkerTileEdgeMargin = 0.15f;

    // Runtime reference to spawned bunker entrance
    public Transform spawnedBunkerEntrance;


    public event Action<string> OnNote;
    public event Action<float> OnProgress;
    public event Action OnGenerationComplete;

    void Note(string msg) => OnNote?.Invoke(msg);
    void Progress(float p) => OnProgress?.Invoke(Mathf.Clamp01(p));

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

        // 7. Points of Interest (Warehouses)
        PlaceWarehouses(terrainGrid);

        // 7.5 Single Bunker Entrance
        PlaceBunkerEntrance(terrainGrid);

        // 8. Prop Groups (clusters of random props)
        PlacePropGroups(terrainGrid);
    }

    // Public delayed entry point so UI can update before heavy work starts
    public void StartWorldGenerationWithDelay()
    {
        StartCoroutine(StartWorldGenerationDelayed());
    }

    IEnumerator StartWorldGenerationDelayed()
    {
        float d = Mathf.Max(0f, generationStartDelaySeconds);
        if (d > 0f)
        {
            Note($"Starting world generation in {d:0.0}s...");
            yield return new WaitForSeconds(d);
        }
        if (generateAsync)
        {
            yield return GenerateWorldAsync();
        }
        else
        {
            GenerateWorld();
            OnGenerationComplete?.Invoke();
        }
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
        int warehouseCount = (placeWarehouses && warehousePrefab != null)
            ? Mathf.Max(1, Mathf.RoundToInt((tilesX * tilesY) / (float)Mathf.Max(1, tilesPerWarehouse)))
            : 0;
        total += warehouseCount;                    // warehouses
        int bunkerCount = (placeBunkerEntrance && bunkerEntrancePrefab != null) ? 1 : 0;
        total += bunkerCount;                      // bunker entrance
        int propClusterCount = 0;
        if (placePropGroups && propGroups != null && propGroups.Count > 0)
        {
            for (int i = 0; i < propGroups.Count; i++)
            {
                var g = propGroups[i];
                if (g != null && g.prefabs != null && g.prefabs.Count > 0 && g.clusterCount > 0)
                    propClusterCount += g.useTileRatio ? Mathf.Max(1, Mathf.RoundToInt((tilesX * tilesY) / (float)Mathf.Max(1, g.tilesPerCluster))) : g.clusterCount;
            }
        }
        total += propClusterCount;                 // prop clusters

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

        // 7. Warehouses
        if (warehouseCount > 0)
        {
            Note("Placing warehouses...");
            yield return null;
            foreach (var _ in PlaceWarehousesAsync(terrainGrid, warehouseCount))
            {
                Step("Placed warehouse", 1);
                yield return null;
            }
        }

        // 7.5 Bunker Entrance
        if (bunkerCount > 0)
        {
            Note("Placing bunker entrance...");
            yield return null;
            foreach (var _ in PlaceBunkerEntranceAsync(terrainGrid))
            {
                Step("Placed bunker entrance", 1);
                yield return null;
            }
        }

        // 8. Prop groups
        if (propClusterCount > 0)
        {
            Note("Placing prop groups...");
            yield return null;
            foreach (var _ in PlacePropGroupsAsync(terrainGrid))
            {
                Step("Placed prop cluster", 1);
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

    // Synchronous placement of warehouses with simple spacing constraints
    void PlaceWarehouses(Terrain[,] terrainGrid)
    {
        if (!placeWarehouses)
        {
            Note("Warehouses disabled.");
            return;
        }
        if (warehousePrefab == null)
        {
            Note("No warehouse prefab assigned; skipping.");
            return;
        }

        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0) return;
        int desired = Mathf.Max(1, Mathf.RoundToInt(totalTiles / (float)Mathf.Max(1, tilesPerWarehouse)));

        var rng = new System.Random(seed + 1357911);

        // Collect existing tower positions to avoid proximity
        var towerPositions = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.name.StartsWith(towerNamePrefix, StringComparison.Ordinal))
                towerPositions.Add(ch.position);
        }

        var placedWarehouses = new System.Collections.Generic.List<Vector3>();
        int attempts = 0;
        int maxAttempts = desired * 20;
        while (placedWarehouses.Count < desired && attempts < maxAttempts)
        {
            attempts++;
            int gx = rng.Next(0, Mathf.Max(1, tilesX));
            int gy = rng.Next(0, Mathf.Max(1, tilesY));
            float baseX = gx * tileWorldWidth;
            float baseZ = gy * tileWorldLength;
            double nx = rng.NextDouble() * 0.7 + 0.15; // keep away from edges a bit more
            double nz = rng.NextDouble() * 0.7 + 0.15;
            float worldX = baseX + (float)nx * tileWorldWidth;
            float worldZ = baseZ + (float)nz * tileWorldLength;
            Terrain terrain = terrainGrid[gx, gy];
            float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
            Vector3 pos = new Vector3(worldX, heightSample + warehouseHeightOffset, worldZ);

            bool tooCloseToTower = false;
            for (int i = 0; i < towerPositions.Count; i++)
            {
                if (Vector3.SqrMagnitude(pos - towerPositions[i]) < minDistanceFromTowers * minDistanceFromTowers)
                {
                    tooCloseToTower = true;
                    break;
                }
            }
            if (tooCloseToTower) continue;

            bool tooCloseToWarehouse = false;
            for (int i = 0; i < placedWarehouses.Count; i++)
            {
                if (Vector3.SqrMagnitude(pos - placedWarehouses[i]) < minDistanceBetweenWarehouses * minDistanceBetweenWarehouses)
                {
                    tooCloseToWarehouse = true;
                    break;
                }
            }
            if (tooCloseToWarehouse) continue;

            Quaternion rot = randomizeWarehouseYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
            GameObject wh = Instantiate(warehousePrefab, pos, rot, transform);
            wh.name = $"{warehouseNamePrefix}{gx}_{gy}_{placedWarehouses.Count}";
            placedWarehouses.Add(pos);
            Note($"Placed warehouse at tile {gx},{gy}.");
        }
    }

    // Synchronous placement of single bunker entrance
    void PlaceBunkerEntrance(Terrain[,] terrainGrid)
    {
        if (!placeBunkerEntrance)
        {
            Note("Bunker entrance disabled.");
            return;
        }
        if (bunkerEntrancePrefab == null)
        {
            Note("No bunker entrance prefab assigned; skipping.");
            return;
        }
        // Ensure only one (remove existing if any when regenerating)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.name.StartsWith(bunkerEntranceNamePrefix, StringComparison.Ordinal))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(ch.gameObject); else Destroy(ch.gameObject);
#else
                Destroy(ch.gameObject);
#endif
            }
        }

        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0)
        {
            Note("No tiles for bunker entrance placement.");
            return;
        }

        var rng = new System.Random(seed + 5432101);
        int gx = rng.Next(0, Mathf.Max(1, tilesX));
        int gy = rng.Next(0, Mathf.Max(1, tilesY));
        float baseX = gx * tileWorldWidth;
        float baseZ = gy * tileWorldLength;
        double nx = rng.NextDouble() * (1.0 - 2.0 * bunkerTileEdgeMargin) + bunkerTileEdgeMargin; // keep inside tile
        double nz = rng.NextDouble() * (1.0 - 2.0 * bunkerTileEdgeMargin) + bunkerTileEdgeMargin;
        float worldX = baseX + (float)nx * tileWorldWidth;
        float worldZ = baseZ + (float)nz * tileWorldLength;
        Terrain terrain = terrainGrid[gx, gy];
        float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
        Vector3 pos = new Vector3(worldX, heightSample + bunkerEntranceHeightOffset, worldZ);
        Quaternion rot = randomizeBunkerYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
        GameObject bunker = Instantiate(bunkerEntrancePrefab, pos, rot, transform);
        bunker.name = $"{bunkerEntranceNamePrefix}{gx}_{gy}";
        spawnedBunkerEntrance = bunker.transform;
        Note($"Placed bunker entrance at tile {gx},{gy}.");
    }

    // Synchronous placement of prop groups
    void PlacePropGroups(Terrain[,] terrainGrid)
    {
        if (!placePropGroups || propGroups == null || propGroups.Count == 0) return;

        // Cache tower and warehouse positions for avoidance
        var towerPositions = new System.Collections.Generic.List<Vector3>();
        var warehousePositions = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch == null) continue;
            if (!string.IsNullOrEmpty(towerNamePrefix) && ch.name.StartsWith(towerNamePrefix, StringComparison.Ordinal))
                towerPositions.Add(ch.position);
            if (!string.IsNullOrEmpty(warehouseNamePrefix) && ch.name.StartsWith(warehouseNamePrefix, StringComparison.Ordinal))
                warehousePositions.Add(ch.position);
        }

        var rng = new System.Random(seed + 24681357);

        for (int gi = 0; gi < propGroups.Count; gi++)
        {
            var g = propGroups[gi];
            if (g == null || g.prefabs == null || g.prefabs.Count == 0 || g.clusterCount <= 0)
                continue;

            var placedClusters = new System.Collections.Generic.List<Vector3>();
            int attempts = 0;
            int maxAttempts = g.clusterCount * 30;
            while (placedClusters.Count < g.clusterCount && attempts < maxAttempts)
            {
                attempts++;
                int gx = rng.Next(0, Math.Max(1, tilesX));
                int gy = rng.Next(0, Math.Max(1, tilesY));
                float baseX = gx * tileWorldWidth;
                float baseZ = gy * tileWorldLength;
                double nx = rng.NextDouble() * 0.7 + 0.15;
                double nz = rng.NextDouble() * 0.7 + 0.15;
                float worldX = baseX + (float)nx * tileWorldWidth;
                float worldZ = baseZ + (float)nz * tileWorldLength;
                Terrain terrain = terrainGrid[gx, gy];
                float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
                Vector3 center = new Vector3(worldX, heightSample + g.yOffset, worldZ);

                // Avoid towers
                if (g.avoidTowers)
                {
                    bool tooClose = false;
                    for (int i = 0; i < towerPositions.Count; i++)
                    {
                        if (Vector3.SqrMagnitude(center - towerPositions[i]) < g.minDistanceFromTowers * g.minDistanceFromTowers)
                        { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                }
                // Avoid warehouses
                if (g.avoidWarehouses)
                {
                    bool tooClose = false;
                    for (int i = 0; i < warehousePositions.Count; i++)
                    {
                        if (Vector3.SqrMagnitude(center - warehousePositions[i]) < g.minDistanceFromWarehouses * g.minDistanceFromWarehouses)
                        { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                }
                // Distance between clusters
                bool tooCloseCluster = false;
                for (int i = 0; i < placedClusters.Count; i++)
                {
                    if (Vector3.SqrMagnitude(center - placedClusters[i]) < g.minDistanceBetweenClusters * g.minDistanceBetweenClusters)
                    { tooCloseCluster = true; break; }
                }
                if (tooCloseCluster) continue;

                // Place items in cluster
                int count = Mathf.Clamp(rng.Next(g.minPerCluster, g.maxPerCluster + 1), 0, 1000);
                for (int k = 0; k < count; k++)
                {
                    if (g.prefabs.Count == 0) break;
                    var prefab = g.prefabs[rng.Next(0, g.prefabs.Count)];
                    if (prefab == null) continue;
                    // Position within circle (uniform)
                    double ang = rng.NextDouble() * Math.PI * 2.0;
                    double rad = Math.Sqrt(rng.NextDouble()) * g.clusterRadius;
                    float dx = (float)(Math.Cos(ang) * rad);
                    float dz = (float)(Math.Sin(ang) * rad);
                    float px = center.x + dx;
                    float pz = center.z + dz;
                    Vector3 rawCenter = new Vector3(px, 0f, pz);
                    float sampledHeight = SampleAdaptiveHeight(terrain, rawCenter, center.y, prefab, g);
                    Vector3 p = new Vector3(px, sampledHeight + g.yOffset, pz);
                    Quaternion rot = g.randomYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
                    var go = Instantiate(prefab, p, rot, transform);
                    if (g.alignLargePrefabBottom)
                        AdjustLargePrefabBottom(go, sampledHeight + g.yOffset, g.largePrefabSizeThreshold, g.largePrefabAdditionalDepth);
                    go.name = $"{(string.IsNullOrEmpty(g.namePrefix) ? "Prop_" : g.namePrefix)}{gi}_{placedClusters.Count}_{k}";
                }

                placedClusters.Add(center);
                Note($"Placed prop cluster {placedClusters.Count}/{g.clusterCount} for group '{g.name}'.");
            }
        }
    }

    // Async placement enumerator for prop groups (yields per cluster placed)
    System.Collections.Generic.IEnumerable<int> PlacePropGroupsAsync(Terrain[,] terrainGrid)
    {
        if (!placePropGroups || propGroups == null || propGroups.Count == 0) yield break;

        var towerPositions = new System.Collections.Generic.List<Vector3>();
        var warehousePositions = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch == null) continue;
            if (!string.IsNullOrEmpty(towerNamePrefix) && ch.name.StartsWith(towerNamePrefix, StringComparison.Ordinal))
                towerPositions.Add(ch.position);
            if (!string.IsNullOrEmpty(warehouseNamePrefix) && ch.name.StartsWith(warehouseNamePrefix, StringComparison.Ordinal))
                warehousePositions.Add(ch.position);
        }

        var rng = new System.Random(seed + 24681357);

        for (int gi = 0; gi < propGroups.Count; gi++)
        {
            var g = propGroups[gi];
            if (g == null || g.prefabs == null || g.prefabs.Count == 0 || g.clusterCount <= 0)
                continue;

            var placedClusters = new System.Collections.Generic.List<Vector3>();
            int attempts = 0;
            int maxAttempts = g.clusterCount * 30;
            while (placedClusters.Count < g.clusterCount && attempts < maxAttempts)
            {
                attempts++;
                int gx = rng.Next(0, Math.Max(1, tilesX));
                int gy = rng.Next(0, Math.Max(1, tilesY));
                float baseX = gx * tileWorldWidth;
                float baseZ = gy * tileWorldLength;
                double nx = rng.NextDouble() * 0.7 + 0.15;
                double nz = rng.NextDouble() * 0.7 + 0.15;
                float worldX = baseX + (float)nx * tileWorldWidth;
                float worldZ = baseZ + (float)nz * tileWorldLength;
                Terrain terrain = terrainGrid[gx, gy];
                float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
                Vector3 center = new Vector3(worldX, heightSample + g.yOffset, worldZ);

                if (g.avoidTowers)
                {
                    bool tooClose = false;
                    for (int i = 0; i < towerPositions.Count; i++)
                    {
                        if (Vector3.SqrMagnitude(center - towerPositions[i]) < g.minDistanceFromTowers * g.minDistanceFromTowers)
                        { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                }
                if (g.avoidWarehouses)
                {
                    bool tooClose = false;
                    for (int i = 0; i < warehousePositions.Count; i++)
                    {
                        if (Vector3.SqrMagnitude(center - warehousePositions[i]) < g.minDistanceFromWarehouses * g.minDistanceFromWarehouses)
                        { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                }
                bool tooCloseCluster = false;
                for (int i = 0; i < placedClusters.Count; i++)
                {
                    if (Vector3.SqrMagnitude(center - placedClusters[i]) < g.minDistanceBetweenClusters * g.minDistanceBetweenClusters)
                    { tooCloseCluster = true; break; }
                }
                if (tooCloseCluster) continue;

                int count = Mathf.Clamp(rng.Next(g.minPerCluster, g.maxPerCluster + 1), 0, 1000);
                for (int k = 0; k < count; k++)
                {
                    if (g.prefabs.Count == 0) break;
                    var prefab = g.prefabs[rng.Next(0, g.prefabs.Count)];
                    if (prefab == null) continue;
                    double ang = rng.NextDouble() * Math.PI * 2.0;
                    double rad = Math.Sqrt(rng.NextDouble()) * g.clusterRadius;
                    float dx = (float)(Math.Cos(ang) * rad);
                    float dz = (float)(Math.Sin(ang) * rad);
                    float px = center.x + dx;
                    float pz = center.z + dz;
                    Vector3 rawCenter = new Vector3(px, 0f, pz);
                    float sampledHeight = SampleAdaptiveHeight(terrain, rawCenter, center.y, prefab, g);
                    Vector3 p = new Vector3(px, sampledHeight + g.yOffset, pz);
                    Quaternion rot = g.randomYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
                    var go = Instantiate(prefab, p, rot, transform);
                    if (g.alignLargePrefabBottom)
                        AdjustLargePrefabBottom(go, sampledHeight + g.yOffset, g.largePrefabSizeThreshold, g.largePrefabAdditionalDepth);
                    go.name = $"{(string.IsNullOrEmpty(g.namePrefix) ? "Prop_" : g.namePrefix)}{gi}_{placedClusters.Count}_{k}";
                }

                placedClusters.Add(center);
                yield return 0; // count one cluster
            }
        }
    }

    // --- Adaptive Height Helpers ---
    float SampleAdaptiveHeight(Terrain terrain, Vector3 rawCenterXZ, float fallbackHeight, GameObject prefab, PropGroup group)
    {
        if (terrain == null) return fallbackHeight;
        // Single sample shortcut
        if (group.heightMode == PropGroup.HeightMode.SingleSample || group.heightSamples <= 1)
        {
            return terrain.SampleHeight(new Vector3(rawCenterXZ.x, 0f, rawCenterXZ.z));
        }

        // Determine sampling radius: for large prefabs expand footprint
        float radius = 0f;
        float largestSize = GetLargestPrefabDimension(prefab);
        bool isLarge = largestSize >= group.largePrefabSizeThreshold && largestSize > 0f;
        if (isLarge)
            radius = largestSize * group.largePrefabExtraRadiusMultiplier * 0.5f; // half since size ~ diameter
        else
            radius = Mathf.Min(group.clusterRadius * 0.25f, 10f); // small local variation

        int samples = Mathf.Max(2, group.heightSamples);
        float sum = 0f;
        float minH = float.MaxValue;
        float maxH = float.MinValue;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float ang = t * Mathf.PI * 2f;
            Vector3 samplePos = rawCenterXZ + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
            float h = terrain.SampleHeight(new Vector3(samplePos.x, 0f, samplePos.z));
            sum += h;
            if (h < minH) minH = h;
            if (h > maxH) maxH = h;
        }
        switch (group.heightMode)
        {
            case PropGroup.HeightMode.LowestSample: return minH;
            case PropGroup.HeightMode.HighestSample: return maxH;
            case PropGroup.HeightMode.AverageSamples: default: return sum / samples;
        }
    }

    float GetLargestPrefabDimension(GameObject prefab)
    {
        if (prefab == null) return 0f;
        var r = prefab.GetComponentInChildren<Renderer>();
        if (r == null) return 0f;
        var size = r.bounds.size;
        return Mathf.Max(size.x, size.y, size.z);
    }

    void AdjustLargePrefabBottom(GameObject instance, float targetGroundY, float sizeThreshold, float extraDepth)
    {
        if (instance == null) return;
        var r = instance.GetComponentInChildren<Renderer>();
        if (r == null) return;
        var size = r.bounds.size;
        float largest = Mathf.Max(size.x, size.y, size.z);
        if (largest < sizeThreshold) return; // only adjust large
        // Compute how far pivot is above bottom
        float bottomY = r.bounds.min.y;
        float pivotY = instance.transform.position.y;
        float diff = pivotY - bottomY; // how much to lower so bottom touches pivot level
        // Set so bottom sits on sampled terrain height
        instance.transform.position = new Vector3(instance.transform.position.x, targetGroundY + diff - Mathf.Abs(extraDepth), instance.transform.position.z);
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

    // Async placement enumerator for warehouses
    System.Collections.Generic.IEnumerable<int> PlaceWarehousesAsync(Terrain[,] terrainGrid, int countBudget)
    {
        if (!placeWarehouses || warehousePrefab == null) yield break;
        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0) yield break;
        int desired = Mathf.Min(countBudget, Mathf.Max(1, Mathf.RoundToInt(totalTiles / (float)Mathf.Max(1, tilesPerWarehouse))));

        var rng = new System.Random(seed + 1357911);
        var towerPositions = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.name.StartsWith(towerNamePrefix, StringComparison.Ordinal))
                towerPositions.Add(ch.position);
        }
        var placedWarehouses = new System.Collections.Generic.List<Vector3>();

        int attempts = 0;
        int maxAttempts = desired * 30;
        while (placedWarehouses.Count < desired && attempts < maxAttempts)
        {
            attempts++;
            int gx = rng.Next(0, Mathf.Max(1, tilesX));
            int gy = rng.Next(0, Mathf.Max(1, tilesY));
            float baseX = gx * tileWorldWidth;
            float baseZ = gy * tileWorldLength;
            double nx = rng.NextDouble() * 0.7 + 0.15;
            double nz = rng.NextDouble() * 0.7 + 0.15;
            float worldX = baseX + (float)nx * tileWorldWidth;
            float worldZ = baseZ + (float)nz * tileWorldLength;
            Terrain terrain = terrainGrid[gx, gy];
            float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
            Vector3 pos = new Vector3(worldX, heightSample + warehouseHeightOffset, worldZ);

            bool tooCloseToTower = false;
            for (int i = 0; i < towerPositions.Count; i++)
            {
                if (Vector3.SqrMagnitude(pos - towerPositions[i]) < minDistanceFromTowers * minDistanceFromTowers)
                { tooCloseToTower = true; break; }
            }
            if (tooCloseToTower) continue;

            bool tooCloseToWarehouse = false;
            for (int i = 0; i < placedWarehouses.Count; i++)
            {
                if (Vector3.SqrMagnitude(pos - placedWarehouses[i]) < minDistanceBetweenWarehouses * minDistanceBetweenWarehouses)
                { tooCloseToWarehouse = true; break; }
            }
            if (tooCloseToWarehouse) continue;

            Quaternion rot = randomizeWarehouseYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
            GameObject wh = Instantiate(warehousePrefab, pos, rot, transform);
            wh.name = $"{warehouseNamePrefix}{gx}_{gy}_{placedWarehouses.Count}";
            placedWarehouses.Add(pos);
            yield return 0; // progress unit
        }
    }

    // Async placement enumerator for single bunker entrance
    System.Collections.Generic.IEnumerable<int> PlaceBunkerEntranceAsync(Terrain[,] terrainGrid)
    {
        if (!placeBunkerEntrance || bunkerEntrancePrefab == null) yield break;
        // Remove any existing bunker entrance first
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);
            if (ch != null && ch.name.StartsWith(bunkerEntranceNamePrefix, StringComparison.Ordinal))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(ch.gameObject); else Destroy(ch.gameObject);
#else
                Destroy(ch.gameObject);
#endif
            }
        }
        int totalTiles = tilesX * tilesY;
        if (totalTiles <= 0) yield break;
        var rng = new System.Random(seed + 5432101);
        int gx = rng.Next(0, Mathf.Max(1, tilesX));
        int gy = rng.Next(0, Mathf.Max(1, tilesY));
        float baseX = gx * tileWorldWidth;
        float baseZ = gy * tileWorldLength;
        double nx = rng.NextDouble() * (1.0 - 2.0 * bunkerTileEdgeMargin) + bunkerTileEdgeMargin;
        double nz = rng.NextDouble() * (1.0 - 2.0 * bunkerTileEdgeMargin) + bunkerTileEdgeMargin;
        float worldX = baseX + (float)nx * tileWorldWidth;
        float worldZ = baseZ + (float)nz * tileWorldLength;
        Terrain terrain = terrainGrid[gx, gy];
        float heightSample = terrain != null ? terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
        Vector3 pos = new Vector3(worldX, heightSample + bunkerEntranceHeightOffset, worldZ);
        Quaternion rot = randomizeBunkerYaw ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
        GameObject bunker = Instantiate(bunkerEntrancePrefab, pos, rot, transform);
        bunker.name = $"{bunkerEntranceNamePrefix}{gx}_{gy}";
        spawnedBunkerEntrance = bunker.transform;
        Note($"Placed bunker entrance at tile {gx},{gy}.");
        yield return 0; // progress unit
    }
}
