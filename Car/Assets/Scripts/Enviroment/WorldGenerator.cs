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
    [Tooltip("Per-tile rivers can cause seams. Recommended OFF for tiled worlds.")]
    public bool enableRivers = false;

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
                gen.enableRivers = enableRivers;

                gen.globalWorldWidth = tilesX * tileWorldWidth;
                gen.globalWorldLength = tilesY * tileWorldLength;
                gen.useGlobalSeamless = true; // ensure seamless transitions

                // Keep all other noise/biome/texture settings from prefab/component setup.
                // If needed, you can override them here similarly.

                // Initialize offsets and build
                gen.InitializeSeed();
                gen.Generate();

                terrainGrid[gx, gy] = terrain;
            }
        }

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
    }
}
