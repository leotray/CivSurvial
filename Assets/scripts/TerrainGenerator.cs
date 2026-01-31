using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int width = 100;
    public int depth = 100;
    public float scale = 20f;
    public float heightMultiplier = 5f;

    [Header("Noise Settings")]
    public int octaves = 4;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    [Header("Height Shaping")]
    public float heightExponent = 2f;

    [Header("Biome Settings")]
    public List<Biome> biomes;
    public Material terrainMaterial;

    [Header("Biome Generation (Fixed)")]
    [Range(0.01f, 0.1f)] public float biomeScale = 0.03f; // Much smaller for larger biomes
    [Range(1, 3)] public int biomeOctaves = 2; // Fewer octaves for smoother regions
    [Range(0.3f, 0.8f)] public float biomePersistence = 0.6f;
    [Range(1.5f, 2.5f)] public float biomeLacunarity = 2f;
    [Range(0.1f, 0.5f)] public float domainWarpStrength = 0.2f; // Reduced warping
    [Range(0.2f, 0.8f)] public float biomeBlendSmoothness = 0.4f;

    [Header("River Settings")]
    public int riverCount = 2;
    public float riverWidth = 5f;
    public float riverDepth = 2f;
    public float riverCurveFrequency = 0.05f;

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnBlendThreshold = 0.1f;
    [Range(0f, 1f)] public float spawnMinHeight = 0f;
    [Range(0f, 1f)] public float spawnMaxHeight = 1f;
    public bool ignoreSpawnChance = false;
    public float spawnCheckInterval = 10f;

    [Header("Seed Settings")]
    public int seed = 0;
    private System.Random prng;

    private float noiseOffsetX;
    private float noiseOffsetY;
    private float biomeOffsetX;
    private float biomeOffsetY;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;
    private Vector3[] normals;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    public bool regenerate = false;

    private int prevWidth, prevDepth, prevSeed;

    void Start()
    {
        InitializeSeed();
        GenerateTerrain();
        SpawnBiomeObjects();
    }

    void Update()
    {
        if (regenerate)
        {
            regenerate = false;
            InitializeSeed();
            GenerateTerrain();
            SpawnBiomeObjects();
        }
    }

    void OnValidate()
    {
        if (width != prevWidth || depth != prevDepth || seed != prevSeed)
        {
            prevWidth = width;
            prevDepth = depth;
            prevSeed = seed;
            InitializeSeed();
            GenerateTerrain();
            SpawnBiomeObjects();
        }
    }

    void InitializeSeed()
    {
        prng = new System.Random(seed);
        noiseOffsetX = (float)(prng.NextDouble() * 10000);
        noiseOffsetY = (float)(prng.NextDouble() * 10000);
        biomeOffsetX = (float)(prng.NextDouble() * 10000);
        biomeOffsetY = (float)(prng.NextDouble() * 10000);
    }

    void GenerateTerrain()
    {
        mesh = new Mesh();
        if ((width + 1) * (depth + 1) > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        GetComponent<MeshFilter>().mesh = mesh;
        CreateShape();
        UpdateMesh();
    }

    void CreateShape()
    {
        int vertCountX = width + 1;
        int vertCountZ = depth + 1;

        vertices = new Vector3[vertCountX * vertCountZ];
        colors = new Color[vertices.Length];
        triangles = new int[width * depth * 6];

        float[,] falloffMap = GenerateFalloffMap(width, depth);

        for (int z = 0, index = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++, index++)
            {
                float xCoord = x + noiseOffsetX;
                float zCoord = z + noiseOffsetY;

                float baseNoise = GenerateFractalNoise(xCoord, zCoord);
                float remappedHeight = Mathf.Lerp(0f, 2f, baseNoise);
                float shapedHeight = Mathf.Pow(remappedHeight, heightExponent);
                shapedHeight = Mathf.Min(shapedHeight, 10f);

                float falloff = falloffMap[x, z];
                shapedHeight *= (1f - falloff);

                float riverCarve = GetRiverOffset(x, z);
                shapedHeight -= riverCarve;
                if (shapedHeight < 0f) shapedHeight = 0f;

                float finalHeight = shapedHeight * heightMultiplier;
                vertices[index] = new Vector3(x, finalHeight, z);

                // FIXED: Generate large irregular biome regions
                (int biomeIndex, float blendWeight) = GetLargeBiomeRegions(x, z);
                bool isRiver = riverCarve > 0.01f;

                // Maintain the same color encoding for shader compatibility
                colors[index] = isRiver
                    ? new Color(0, 0, 1) // Rivers remain blue in blue channel
                    : new Color(blendWeight, biomeIndex / (float)(biomes.Count - 1), 0);
            }
        }

        int tris = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * (width + 1) + x;

                triangles[tris + 0] = i;
                triangles[tris + 1] = i + width + 1;
                triangles[tris + 2] = i + 1;

                triangles[tris + 3] = i + 1;
                triangles[tris + 4] = i + width + 1;
                triangles[tris + 5] = i + width + 2;

                tris += 6;
            }
        }
    }

    // FIXED: Create large biome regions instead of noisy pixels
    (int, float) GetLargeBiomeRegions(int x, int z)
    {
        if (biomes == null || biomes.Count == 0)
            return (0, 1f);

        // FIXED: Use much lower frequency noise for large regions
        float tempScale = biomeScale * 0.5f; // Even larger scale for temperature
        float moistScale = biomeScale * 0.7f; // Slightly smaller scale for moisture variation

        // Generate broad temperature and moisture maps
        float temperature = GenerateTemperatureRegions(x, z, tempScale);
        float moisture = GenerateMoistureRegions(x, z, moistScale);

        // Find closest biome based on temperature/moisture
        float closestDistance = float.MaxValue;
        int closestBiome = 0;

        for (int i = 0; i < biomes.Count; i++)
        {
            var biome = biomes[i];
            float midTemp = (biome.minTemperature + biome.maxTemperature) / 2f;
            float midMoist = (biome.minMoisture + biome.maxMoisture) / 2f;

            float dist = Vector2.Distance(new Vector2(temperature, moisture), new Vector2(midTemp, midMoist));

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestBiome = i;
            }
        }

        // FIXED: Smoother blend calculation for biome edges
        float maxDistance = biomeBlendSmoothness;
        float weight = Mathf.Clamp01(1f - (closestDistance / maxDistance));

        // Add subtle edge noise for natural transitions (much less than before)
        float edgeNoise = SimplexNoise(x * biomeScale * 2f, z * biomeScale * 2f, biomeOffsetX) * 0.1f;
        weight = Mathf.Clamp01(weight + edgeNoise);

        return (closestBiome, weight);
    }

    // FIXED: Generate broad temperature regions
    float GenerateTemperatureRegions(float x, float z, float scale)
    {
        // Base temperature gradient (island effect - cooler towards edges)
        float centerX = width * 0.5f;
        float centerZ = depth * 0.5f;
        float distanceFromCenter = Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ));
        float maxDistance = Mathf.Sqrt(centerX * centerX + centerZ * centerZ);
        float baseTemp = 1f - (distanceFromCenter / maxDistance) * 0.6f; // Cooler towards edges

        // FIXED: Use very low frequency noise for large temperature regions
        float tempNoise = SimplexNoise(x * scale, z * scale, biomeOffsetX) * 0.4f;

        // Add slight variation with even lower frequency
        float tempVariation = SimplexNoise(x * scale * 0.3f, z * scale * 0.3f, biomeOffsetX + 1000f) * 0.2f;

        float finalTemp = baseTemp + tempNoise + tempVariation;
        return Mathf.Clamp01(finalTemp);
    }

    // FIXED: Generate broad moisture regions
    float GenerateMoistureRegions(float x, float z, float scale)
    {
        // Base moisture influenced by rivers
        float riverInfluence = 0f;
        for (int i = 0; i < riverCount; i++)
        {
            float xOffset = i * 10000f + seed * 5000f;
            float riverCenter = Mathf.PerlinNoise((z + xOffset) * riverCurveFrequency, i * 10f) * width;
            float distanceToRiver = Mathf.Abs(x - riverCenter);
            float riverMoisture = Mathf.InverseLerp(riverWidth * 4f, 0f, distanceToRiver) * 0.5f;
            riverInfluence = Mathf.Max(riverInfluence, riverMoisture);
        }

        // FIXED: Very broad moisture patterns
        float moistNoise = SimplexNoise(x * scale, z * scale, biomeOffsetY + 2000f) * 0.5f;

        // Add secondary moisture variation with lower frequency
        float moistVariation = SimplexNoise(x * scale * 0.4f, z * scale * 0.4f, biomeOffsetY + 5000f) * 0.3f;

        float baseMoisture = 0.3f + riverInfluence;
        float finalMoisture = baseMoisture + moistNoise + moistVariation;
        return Mathf.Clamp01(finalMoisture);
    }

    // FIXED: Use Simplex-like noise for smoother, larger regions
    float SimplexNoise(float x, float z, float offset)
    {
        // Simplified Perlin noise with multiple octaves for smoother large-scale patterns
        float noise = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < biomeOctaves; i++)
        {
            float sampleX = (x + offset) * frequency;
            float sampleZ = (z + offset) * frequency;

            float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f; // Range -1 to 1
            noise += perlin * amplitude;
            maxValue += amplitude;

            amplitude *= biomePersistence;
            frequency *= biomeLacunarity;
        }

        return noise / maxValue;
    }

    void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        normals = mesh.normals;
        mesh.RecalculateBounds();

        if (terrainMaterial != null)
            GetComponent<MeshRenderer>().material = terrainMaterial;

        var collider = GetComponent<MeshCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<MeshCollider>();

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
    }

    void SpawnBiomeObjects()
    {
        ClearSpawnedObjects();

        if (biomes == null || biomes.Count == 0) return;

        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        int spawnedCount = 0;

        for (float z = 0; z <= depth; z += spawnCheckInterval)
        {
            for (float x = 0; x <= width; x += spawnCheckInterval)
            {
                int xIndex = Mathf.Clamp(Mathf.RoundToInt(x), 0, width);
                int zIndex = Mathf.Clamp(Mathf.RoundToInt(z), 0, depth);
                int index = zIndex * (width + 1) + xIndex;

                if (index >= vertices.Length) continue;

                Color color = colors[index];
                float blend = color.r;
                int biomeIndex = Mathf.RoundToInt(color.g * (biomes.Count - 1));
                float height = vertices[index].y / heightMultiplier;

                if (biomeIndex < 0 || biomeIndex >= biomes.Count) continue;
                Biome biome = biomes[biomeIndex];

                if (biome.spawnEntries == null || biome.spawnEntries.Count == 0)
                    continue;

                if (blend < spawnBlendThreshold || height < spawnMinHeight || height > spawnMaxHeight)
                    continue;

                foreach (var entry in biome.spawnEntries)
                {
                    if (entry.prefab == null) continue;

                    float chance = entry.spawnDensity * blend * blend;
                    bool shouldSpawn = ignoreSpawnChance || (Random.value <= chance);

                    if (!shouldSpawn)
                        continue;

                    Vector3 position = vertices[index] + transform.position;
                    Vector3 normal = normals[index];

                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal) *
                                          Quaternion.Euler(0, Random.Range(0, 360f), 0);

                    GameObject spawned = Instantiate(entry.prefab, position, rotation, transform);
                    spawnedObjects.Add(spawned);
                    spawnedCount++;
                }
            }
        }

        Debug.Log($"Spawned total: {spawnedCount}");
    }

    void ClearSpawnedObjects()
    {
        foreach (Transform child in transform)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(spawnedObjects[i]);
                else
                    Destroy(spawnedObjects[i]);
#else
                Destroy(spawnedObjects[i]);
#endif
            }
        }

        spawnedObjects.Clear();
    }

    float GenerateFractalNoise(float x, float z)
    {
        float noise = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxPossibleHeight = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x / scale * frequency;
            float sampleZ = z / scale * frequency;
            float perlin = Mathf.PerlinNoise(sampleX, sampleZ);

            noise += perlin * amplitude;
            maxPossibleHeight += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return noise / maxPossibleHeight;
    }

    float[,] GenerateFalloffMap(int width, int depth)
    {
        float[,] map = new float[width + 1, depth + 1];
        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                float fx = x / (float)width * 2 - 1;
                float fz = z / (float)depth * 2 - 1;
                float value = Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fz));
                map[x, z] = value * value / (value * value + (1 - value) * (1 - value));
            }
        }
        return map;
    }

    float GetRiverOffset(int x, int z)
    {
        float totalOffset = 0f;

        for (int i = 0; i < riverCount; i++)
        {
            float xOffset = i * 10000f + seed * 5000f;
            float riverCenter = Mathf.PerlinNoise((z + xOffset) * riverCurveFrequency, i * 10f) * width;

            float distance = Mathf.Abs(x - riverCenter);
            float t = Mathf.InverseLerp(riverWidth, 0, distance);
            float depth = Mathf.SmoothStep(0, riverDepth, t);

            totalOffset += depth;
        }

        return totalOffset;
    }

    [System.Serializable]
    public class Biome
    {
        public string name;
        [Range(0f, 1f)] public float minTemperature = 0f;
        [Range(0f, 1f)] public float maxTemperature = 1f;
        [Range(0f, 1f)] public float minMoisture = 0f;
        [Range(0f, 1f)] public float maxMoisture = 1f;
        public Texture2D texture;

        [Header("Spawning")]
        public List<BiomeSpawnEntry> spawnEntries;
    }

    [System.Serializable]
    public class BiomeSpawnEntry
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float spawnDensity = 0.5f;
    }
}
