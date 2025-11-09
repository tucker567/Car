// NOTE: Public tweakable fields removed; settings now supplied by WorldGenerator via TerrainGenerationSettings.
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    // Internal dimensions (were public)
    int depth = 30;
    int width = 256;
    int height = 256;

    // Per-tile world size
    float terrainWidth = 1000f;
    float terrainLength = 1000f;

    // Origin set by world generator
    Vector2 worldOrigin = Vector2.zero;

    // Global extents (set by world generator)
    float globalWorldWidth = 1000f;
    float globalWorldLength = 1000f;

    // Settings bundle (new)
    [SerializeField, HideInInspector] TerrainGenerationSettings settings;
    public TerrainGenerationSettings Settings => settings;

    // Effective per-tile river enable (can be forced off even if settings.enableRivers true)
    bool effectiveEnableRivers => _overrideRiversEnabled ?? settings.enableRivers;
    bool? _overrideRiversEnabled = null;

    // Internal random offsets
    float offsetX;
    float offsetY;

    // Biome offsets
    float biomeOffsetX;
    float biomeOffsetY;

    // External river mask
    float[,] externalRiverMask = null;

    public void ApplySettings(TerrainGenerationSettings s) => settings = s;
    public void SetDimensions(int w, int h, int d) { width = w; height = h; depth = d; }
    public void SetWorldSize(float w, float l) { terrainWidth = w; terrainLength = l; }
    public void SetWorldOrigin(Vector2 origin) { worldOrigin = origin; }
    public void SetGlobalExtents(float gw, float gl) { globalWorldWidth = gw; globalWorldLength = gl; }
    public void OverridePerTileRiverEnable(bool enabled) { _overrideRiversEnabled = enabled; }

    public void SetExternalRiverMask(float[,] mask) => externalRiverMask = mask;
    public void ClearExternalRiverMask() => externalRiverMask = null;

    void Start()
    {
        if (settings == null)
        {
            Debug.LogWarning("TerrainGenerator missing settings; creating default.");
            settings = new TerrainGenerationSettings();
        }
        InitializeSeed();
        if (settings.autoGenerate)
            Generate();
    }

    public void InitializeSeed()
    {
        if (settings.useRandomSeed)
            settings.seed = Random.Range(0, 10000);

        var saved = Random.state;

        Random.InitState(settings.seed);
        offsetX = Random.Range(0f, 1000f);
        offsetY = Random.Range(0f, 1000f);

        if (settings.biomeUseSeparateSeed)
        {
            if (settings.biomeSeed == 0 && settings.useRandomSeed)
                settings.biomeSeed = Random.Range(0, 10000);

            Random.InitState(settings.biomeSeed == 0 ? settings.seed + 12345 : settings.biomeSeed);
            biomeOffsetX = Random.Range(0f, 1000f);
            biomeOffsetY = Random.Range(0f, 1000f);
        }
        else
        {
            biomeOffsetX = offsetX;
            biomeOffsetY = offsetY;
        }

        Random.state = saved;
    }

    float[,] ExpandRiverMaskIfNeeded(float[,] mask, int targetW, int targetH)
    {
        int mw = mask.GetLength(0);
        int mh = mask.GetLength(1);
        if (mw == targetW + 1 && mh == targetH + 1) return mask;
        if (mw == targetW && mh == targetH)
        {
            float[,] expanded = new float[targetW + 1, targetH + 1];
            for (int x = 0; x < targetW; x++)
                for (int y = 0; y < targetH; y++)
                    expanded[x, y] = mask[x, y];
            for (int y = 0; y < targetH; y++)
                expanded[targetW, y] = mask[targetW - 1, y];
            for (int x = 0; x < targetW + 1; x++)
                expanded[x, targetH] = expanded[x, targetH - 1];
            return expanded;
        }
        return null;
    }

    public void Generate()
    {
        if (settings == null)
        {
            Debug.LogError("Cannot Generate: settings not assigned.");
            return;
        }

        var terrain = GetComponent<Terrain>() ?? gameObject.AddComponent<Terrain>();

        var data = new TerrainData
        {
            heightmapResolution = Mathf.Clamp(width + 1, 33, 4097),
            size = new Vector3(terrainWidth, depth, terrainLength),
            alphamapResolution = Mathf.Clamp(width, 16, 2048)
        };

        float[,] heights = GenerateHeights();
        float[,] riverMask;

        if (externalRiverMask != null)
        {
            var adjusted = ExpandRiverMaskIfNeeded(externalRiverMask, width, height);
            if (adjusted == null)
            {
                Debug.LogWarning($"External river mask size mismatch. Got {externalRiverMask.GetLength(0)}x{externalRiverMask.GetLength(1)}, expected {width}x{height} or {width + 1}x{height + 1}. Ignoring.");
                externalRiverMask = null;
            }
            else
            {
                externalRiverMask = adjusted;
            }
        }

        if (externalRiverMask != null)
        {
            riverMask = externalRiverMask;
            heights = CarveRiversFromMask(heights, riverMask);
        }
        else if (effectiveEnableRivers)
        {
            heights = GenerateRivers(heights, out riverMask);
        }
        else
        {
            riverMask = new float[width + 1, height + 1];
        }

        ApplyTextures(data, heights, riverMask);
        data.SetHeights(0, 0, TransposeHeightmap(heights));
        terrain.terrainData = data;

        var collider = GetComponent<TerrainCollider>() ?? gameObject.AddComponent<TerrainCollider>();
        collider.terrainData = data;
    }

    float[,] GenerateHeights()
    {
        float[,] heights = new float[width + 1, height + 1];
        for (int sx = 0; sx <= width; sx++)
            for (int sy = 0; sy <= height; sy++)
                heights[sx, sy] = CalculateHeight(sx, sy);
        return heights;
    }

    static class RiverKernelCache
    {
        private static readonly System.Collections.Generic.Dictionary<int, (int dx, int dy, float f)[]> _cache
            = new System.Collections.Generic.Dictionary<int, (int, int, float)[]>();
        public static (int dx, int dy, float f)[] Get(int radius, float bankSoftness)
        {
            if (_cache.TryGetValue(radius, out var arr)) return arr;
            var list = new System.Collections.Generic.List<(int, int, float)>();
            float r = radius;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    float nd = dist / Mathf.Max(0.0001f, r);
                    float falloff = Mathf.Pow(1f - nd, bankSoftness);
                    if (falloff > 0.0001f)
                        list.Add((dx, dy, falloff));
                }
            arr = list.ToArray();
            _cache[radius] = arr;
            return arr;
        }
    }

    float[] BuildSmoothRiverPathFast(int samplesX, int samplesY, int startY, int seedBase)
    {
        samplesX = Mathf.Max(2, samplesX);
        samplesY = Mathf.Max(2, samplesY);

        float[] drift = new float[samplesX];
        float[] wiggle = new float[samplesX];

        float lowF = Mathf.Max(0.0001f, settings.riverLowFrequency);
        float highF = Mathf.Max(0.0001f, settings.riverHighFrequency);
        float roughF = Mathf.Max(0.0001f, settings.riverRoughnessFrequency);

        float seedA = (seedBase * 0.137f) % 10000f;
        float seedB = (seedBase * 0.713f) % 10000f;
        float seedC = (seedBase * 0.333f) % 10000f;

        float highAmp = settings.riverHighAmplitude * (0.2f + settings.riverWindiness * 0.8f);
        for (int i = 0; i < samplesX; i++)
        {
            float t = (float)i / (samplesX - 1);
            drift[i] = (Mathf.PerlinNoise(t * lowF + seedA, seedA) * 2f - 1f) * settings.riverLowAmplitude;
            wiggle[i] = (Mathf.PerlinNoise(t * highF + seedB, seedB) * 2f - 1f) * highAmp;
        }

        float[] path = new float[samplesX];
        float baseNorm = Mathf.Clamp01((float)startY / (samplesY - 1));
        for (int i = 0; i < samplesX; i++)
            path[i] = Mathf.Clamp01(baseNorm + drift[i] + wiggle[i]) * (samplesY - 1);

        if (settings.riverSmoothPasses > 0)
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

        if (settings.riverRoughness > 0f)
        {
            for (int i = 0; i < samplesX; i++)
            {
                float t = (float)i / (samplesX - 1);
                float rough = (Mathf.PerlinNoise(t * roughF + seedC, seedC) * 2f - 1f) * settings.riverRoughness;
                path[i] = Mathf.Clamp(path[i] + rough * (samplesY - 1) * 0.05f, 0f, samplesY - 1);
            }
        }

        return path;
    }

    float[,] GenerateRivers(float[,] heights, out float[,] riverMask)
    {
        int effectiveMin = Mathf.Max(0, settings.minRivers);
        int effectiveMax = Mathf.Max(effectiveMin, settings.maxRivers);

        riverMask = new float[width, height];
        int riverCount = Random.Range(effectiveMin, effectiveMax + 1);
        if (riverCount <= 0) return heights;

        const int pathStep = 1;

        for (int r = 0; r < riverCount; r++)
        {
            int riverSeed = settings.seed + r * 1000;
            int startY = Random.Range(height / 4, 3 * height / 4);
            float[] path = BuildSmoothRiverPathFast(width, height, startY, riverSeed);

            float jitterSeed = (riverSeed * 1.917f) % 10000f;
            float wFreq = Mathf.Max(0.0001f, settings.riverWidthJitterFrequency);

            for (int x = 0; x < width; x += pathStep)
            {
                int x0 = x;
                int x1 = Mathf.Min(x + pathStep, width - 1);
                float t = (float)(x - x0) / Mathf.Max(1, x1 - x0);
                float centerY = Mathf.Lerp(path[x0], path[x1], t);
                int cYInt = Mathf.RoundToInt(centerY);

                float tWorld = (float)x / Mathf.Max(1, width - 1);
                float jNoise = (Mathf.PerlinNoise(tWorld * wFreq + jitterSeed, jitterSeed) * 2f - 1f) * settings.riverWidthJitter;
                float localWidth = settings.riverWidth * (1f + jNoise);
                if (localWidth < 1.01f)
                {
                    int nx = Mathf.Clamp(x, 0, width - 1);
                    int ny = Mathf.Clamp(cYInt, 0, height - 1);
                    CarveSample(heights, riverMask, nx, ny, 1f);
                    continue;
                }

                int radius = Mathf.Clamp(Mathf.RoundToInt(localWidth), 1, 64);
                var kernel = RiverKernelCache.Get(radius, settings.riverBankSoftness);

                int nextX = Mathf.Min(x + 1, width - 1);
                float dirY = path[nextX] - path[x];
                Vector2 perp = new Vector2(-dirY, 1f).normalized; // reserved

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

    void CarveSample(float[,] heights, float[,] mask, int sx, int sy, float falloff)
    {
        WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);
        float biomeRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
        float depthMul = Mathf.Lerp(1.5f, 1f, biomeRaw);
        float targetDepth = settings.riverDepth * depthMul;

        heights[sx, sy] = Mathf.Lerp(heights[sx, sy], targetDepth, falloff);
        mask[sx, sy] = Mathf.Max(mask[sx, sy], falloff);
    }

    float[,] CarveRiversFromMask(float[,] heights, float[,] riverMask)
    {
        int maskW = riverMask.GetLength(0);
        int maskH = riverMask.GetLength(1);

        for (int sx = 0; sx <= width; sx++)
        {
            for (int sy = 0; sy <= height; sy++)
            {
                int mx = Mathf.Clamp(sx, 0, maskW - 1);
                int my = Mathf.Clamp(sy, 0, maskH - 1);
                float m = riverMask[mx, my];
                if (m <= 0f) continue;

                WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);
                float biomeNoiseRaw = SampleBiomeNoiseRaw(xWorld, yWorld);
                float riverDepthMultiplier = Mathf.Lerp(1.5f, 1f, biomeNoiseRaw);
                float targetDepth = settings.riverDepth * riverDepthMultiplier;

                heights[sx, sy] = Mathf.Lerp(heights[sx, sy], targetDepth, m);
            }
        }
        return heights;
    }

    float CalculateHeight(int sx, int sy)
    {
        WorldXYFromSample(sx, sy, out float xWorld, out float yWorld);
        float blend = SampleBiomeBlend(xWorld, yWorld); // 0 salt -> 1 dunes

        float baseFreqX = FreqX(settings.scale);
        float baseFreqY = FreqY(settings.scale);

        float coordX = xWorld * settings.duneStretch;
        float coordY = yWorld;

        float terrainHeight = 0f;
        float amplitude = 1f;
        float octFreq = 1f;

        for (int i = 0; i < settings.octaves; i++)
        {
            float u = coordX * (baseFreqX * octFreq) + offsetX;
            float v = coordY * (baseFreqY * octFreq) + offsetY;
            float noiseValue = Mathf.PerlinNoise(u, v);
            terrainHeight += noiseValue * amplitude;
            amplitude *= settings.persistence;
            octFreq *= settings.lacunarity;
        }

        terrainHeight /= (2f - 1f / Mathf.Pow(2f, settings.octaves - 1));
        terrainHeight = ApplyDuneShape(terrainHeight, xWorld, yWorld);
        terrainHeight = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, terrainHeight));

        float saltFlatBase = 0.08f;
        float saltFreqX = FreqX(5f);
        float saltFreqY = FreqY(5f);
        float saltFlatHeight = saltFlatBase + Mathf.PerlinNoise(
            xWorld * saltFreqX + offsetX * 2f,
            yWorld * saltFreqY + offsetY * 2f) * 0.05f;

        float blendedNormalized = Mathf.Lerp(saltFlatHeight, terrainHeight, blend);
        return Mathf.Clamp01(blendedNormalized * settings.duneHeight);
    }

    float ApplyDuneShape(float height, float xWorld, float yWorld)
    {
        float phaseX = xWorld * FreqX(1f);
        height += Mathf.Sin(phaseX * Mathf.PI * 2f + settings.windDirection) * 0.1f;
        height = Mathf.Pow(height, 1.2f);
        float randFreqX = FreqX(50f);
        float randFreqY = FreqY(50f);
        height += Mathf.PerlinNoise(xWorld * randFreqX + offsetX, yWorld * randFreqY + offsetY) * 0.05f;
        return height;
    }

    void ApplyTextures(TerrainData terrainData, float[,] heights, float[,] riverMask)
    {
        TerrainLayer duneLayer = new TerrainLayer { diffuseTexture = settings.duneTexture, tileSize = new Vector2(settings.tileSize, settings.tileSize) };
        TerrainLayer saltLayer = new TerrainLayer { diffuseTexture = settings.saltFlatTexture, tileSize = new Vector2(settings.tileSize, settings.tileSize) };
        TerrainLayer riverLayer = new TerrainLayer { diffuseTexture = settings.riverTexture, tileSize = new Vector2(settings.tileSize, settings.tileSize) };
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

    void WorldXYFromSample(int sx, int sy, out float xWorld, out float yWorld)
    {
        float nx = (width <= 0) ? 0f : (float)sx / width;
        float ny = (height <= 0) ? 0f : (float)sy / height;
        xWorld = worldOrigin.x + nx * terrainWidth;
        yWorld = worldOrigin.y + ny * terrainLength;
    }

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

    Vector2 RotateWorld(Vector2 p, float degrees)
    {
        if (Mathf.Approximately(degrees, 0f)) return p;
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    float FBM(float u, float v, int oct, float pers, float lac, bool ridged)
    {
        float amp = 1f;
        float freq = 1f;
        float sum = 0f;
        float norm = 0f;
        for (int i = 0; i < oct; i++)
        {
            float n = Mathf.PerlinNoise(
                u * freq + biomeOffsetX * settings.biomeOffsetScale,
                v * freq + biomeOffsetY * settings.biomeOffsetScale
            );
            if (ridged) n = 1f - Mathf.Abs(n * 2f - 1f);
            sum += n * amp;
            norm += amp;
            amp *= Mathf.Clamp01(pers);
            freq *= Mathf.Max(1f, lac);
        }
        return (norm > 0f) ? sum / norm : 0.5f;
    }

    float SampleBiomeNoiseRaw(float xWorld, float yWorld)
    {
        Vector2 p = RotateWorld(new Vector2(xWorld, yWorld), settings.biomeRotationDegrees);
        float u = p.x * FreqX(settings.biomeScale);
        float v = p.y * FreqY(settings.biomeScale);

        if (settings.biomeWarpStrength > 0f)
        {
            float wu = p.x * FreqX(settings.biomeWarpScale);
            float wv = p.y * FreqY(settings.biomeWarpScale);
            float wx = Mathf.PerlinNoise(wu + biomeOffsetX, wv + biomeOffsetY) * 2f - 1f;
            float wy = Mathf.PerlinNoise(wu + biomeOffsetX + 37.7f, wv + biomeOffsetY + 59.3f) * 2f - 1f;
            u += wx * settings.biomeWarpStrength;
            v += wy * settings.biomeWarpStrength;
        }
        return FBM(u, v, Mathf.Max(1, settings.biomeOctaves), settings.biomePersistence, settings.biomeLacunarity, settings.biomeUseRidged);
    }

    float SampleBiomeBlend(float xWorld, float yWorld)
    {
        float n = SampleBiomeNoiseRaw(xWorld, yWorld);
        if (settings.biomeInvert) n = 1f - n;
        float blend = Mathf.InverseLerp(
            settings.biomeThreshold - settings.biomeTransition,
            settings.biomeThreshold + settings.biomeTransition,
            n
        );
        blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
        if (!Mathf.Approximately(settings.biomeContrast, 1f))
            blend = Mathf.Pow(blend, settings.biomeContrast);
        return Mathf.Clamp01(blend);
    }
}
