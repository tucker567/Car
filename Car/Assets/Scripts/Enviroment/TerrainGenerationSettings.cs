using UnityEngine;

[System.Serializable]
public class TerrainGenerationSettings
{
    [Header("Lifecycle")]
    [Tooltip("If ON, calls Generate() automatically in Start() of each TerrainGenerator.")]
    public bool autoGenerate = true;
    [Tooltip("Per-tile river carving (ignored if a global river mask is applied).")]
    public bool enableRivers = true;

    [Header("Dune Base Noise")]
    public float scale = 8f;
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Dune fBm Detail")]
    [Range(1, 8)] public int octaves = 4;
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Range(1f, 4f)] public float lacunarity = 1.4f;
    [Range(0f, 3f)] public float duneHeight = 1f;
    [Range(0f, 1f)] public float windDirection = 0.6f;
    [Range(0.1f, 3f)] public float duneStretch = 1.5f;

    [Header("River Counts / Shape")]
    public int minRivers = 1;
    public int maxRivers = 5;
    public float riverWidth = 100f;
    public float riverDepth = 0f;
    public float riverWindiness = 10f;
    public float riverBankSoftness = 12f;
    public float riverTextureSpread = 4.5f;

    [Header("River Curve Params")]
    [Range(0, 12)] public int riverSmoothPasses = 4;
    public float riverLowFrequency = 0.5f;
    public float riverHighFrequency = 4f;
    [Range(0f, 0.5f)] public float riverLowAmplitude = 0.25f;
    [Range(0f, 0.2f)] public float riverHighAmplitude = 0.05f;
    [Range(0f, 1f)] public float riverRoughness = 0.2f;
    public float riverRoughnessFrequency = 3f;
    [Range(0f, 1f)] public float riverWidthJitter = 0.25f;
    public float riverWidthJitterFrequency = 0.8f;

    [Header("Biome (basic)")]
    public float biomeScale = 0.2f;
    [Range(0f, 1f)] public float biomeThreshold = 0.5f;
    [Range(0f, 0.5f)] public float biomeTransition = 0.01f;
    public float biomeOffsetScale = 0.01f;

    [Header("Biome (advanced)")]
    [Range(1, 8)] public int biomeOctaves = 1;
    [Range(0f, 1f)] public float biomePersistence = 0.5f;
    [Range(1f, 4f)] public float biomeLacunarity = 2.0f;
    public bool biomeUseRidged = false;
    [Range(-180f, 180f)] public float biomeRotationDegrees = 0f;
    [Range(0f, 2f)] public float biomeWarpStrength = 0f;
    [Range(0.01f, 10f)] public float biomeWarpScale = 0.5f;
    [Range(0.25f, 4f)] public float biomeContrast = 1f;
    public bool biomeInvert = true;
    public bool biomeUseSeparateSeed = false;
    public int biomeSeed = 0;

    [Header("Textures")]
    public Texture2D duneTexture;
    public Texture2D saltFlatTexture;
    public Texture2D riverTexture;

    [Header("Texture Tiling")]
    public int tileSize = 5;

    [Header("Seamless")]
    public bool useGlobalSeamless = true;
}
