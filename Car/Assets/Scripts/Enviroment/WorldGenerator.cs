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
                refGen.riverWidth, refGen.riverWindiness, refGen.riverBankSoftness
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
        float riverWidth, float riverWindiness, float riverBankSoftness
    )
    {
        float[,] mask = new float[samplesX, samplesY];

        var countRng = new System.Random(baseSeed);
        int riverCount = Mathf.Clamp(countRng.Next(minRivers, maxRivers + 1), 0, int.MaxValue);

        for (int r = 0; r < riverCount; r++)
        {
            // Each river gets its own deterministic RNG
            var rnd = new System.Random(baseSeed + 1000 * (r + 1));
            bool vertical = rnd.NextDouble() > 0.5;

            if (!vertical)
            {
                // Horizontal river across samplesX
                int len = samplesX;
                float[] path = new float[len];
                int startY = rnd.Next(samplesY / 4, 3 * samplesY / 4);
                path[0] = startY;
                float noiseScale = 0.1f;

                for (int x = 1; x < len; x++)
                {
                    float offset = (Mathf.PerlinNoise(x * noiseScale, (baseSeed + r * 123.456f) * 0.1f) * 2f - 1f) * riverWindiness;
                    path[x] = Mathf.Clamp(path[x - 1] + offset, 0, samplesY - 1);
                }

                float step = 0.2f;
                int rad = Mathf.CeilToInt(riverWidth);
                for (float fx = 0; fx <= len - 1; fx += step)
                {
                    int x0 = Mathf.FloorToInt(fx);
                    int x1 = Mathf.Min(Mathf.CeilToInt(fx), len - 1);
                    float t = Mathf.Clamp01(fx - x0);
                    float centerY = Mathf.Lerp(path[x0], path[x1], t);

                    float dirY = path[x1] - path[x0];
                    Vector2 perp = new Vector2(-dirY, 1f).normalized;

                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        for (int dy = -rad; dy <= rad; dy++)
                        {
                            float dist = new Vector2(dx, dy).magnitude;
                            if (dist > riverWidth) continue;

                            int nx = Mathf.Clamp(Mathf.RoundToInt(fx + perp.x * dx), 0, samplesX - 1);
                            int ny = Mathf.Clamp(Mathf.RoundToInt(centerY + perp.y * dy), 0, samplesY - 1);

                            float normalizedDist = dist / Mathf.Max(0.0001f, riverWidth);
                            float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), riverBankSoftness);
                            mask[nx, ny] = Mathf.Max(mask[nx, ny], falloff);
                        }
                    }
                }
            }
            else
            {
                // Vertical river across samplesY
                int len = samplesY;
                float[] path = new float[len];
                int startX = rnd.Next(samplesX / 4, 3 * samplesX / 4);
                path[0] = startX;
                float noiseScale = 0.1f;

                for (int y = 1; y < len; y++)
                {
                    float offset = (Mathf.PerlinNoise(y * noiseScale, (baseSeed + r * 321.987f) * 0.1f) * 2f - 1f) * riverWindiness;
                    path[y] = Mathf.Clamp(path[y - 1] + offset, 0, samplesX - 1);
                }

                float step = 0.2f;
                int rad = Mathf.CeilToInt(riverWidth);
                for (float fy = 0; fy <= len - 1; fy += step)
                {
                    int y0 = Mathf.FloorToInt(fy);
                    int y1 = Mathf.Min(Mathf.CeilToInt(fy), len - 1);
                    float t = Mathf.Clamp01(fy - y0);
                    float centerX = Mathf.Lerp(path[y0], path[y1], t);

                    float dirX = path[y1] - path[y0];
                    Vector2 perp = new Vector2(1f, -dirX).normalized;

                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        for (int dy = -rad; dy <= rad; dy++)
                        {
                            float dist = new Vector2(dx, dy).magnitude;
                            if (dist > riverWidth) continue;

                            int nx = Mathf.Clamp(Mathf.RoundToInt(centerX + perp.x * dx), 0, samplesX - 1);
                            int ny = Mathf.Clamp(Mathf.RoundToInt(fy + perp.y * dy), 0, samplesY - 1);

                            float normalizedDist = dist / Mathf.Max(0.0001f, riverWidth);
                            float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), riverBankSoftness);
                            mask[nx, ny] = Mathf.Max(mask[nx, ny], falloff);
                        }
                    }
                }
            }
        }

        return mask;
    }
}
