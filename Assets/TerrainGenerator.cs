using System.Collections.Generic;
using UnityEngine;

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
    public Vector2 noiseOffset;

    [Header("Biome Settings")]
    public List<Biome> biomes;
    public Material terrainMaterial;

    [Header("River Settings")]
    public int riverCount = 2;
    public float riverWidth = 5f;
    public float riverDepth = 2f;
    public float riverCurveFrequency = 0.05f;

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnBlendThreshold = 0.1f;
    [Range(0f, 1f)] public float spawnMinHeight = 0f;
    public bool ignoreSpawnChance = false;
    public float spawnCheckInterval = 10f;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;
    private Vector3[] normals;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    public bool regenerate = false;

    void Start()
    {
        GenerateTerrain();
        SpawnBiomeObjects();
    }

    void Update()
    {
        if (regenerate)
        {
            regenerate = false;
            GenerateTerrain();
            SpawnBiomeObjects();
        }
    }

    void GenerateTerrain()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        CreateShape();
        UpdateMesh();
    }

    void CreateShape()
    {
        vertices = new Vector3[(width + 1) * (depth + 1)];
        colors = new Color[vertices.Length];
        triangles = new int[width * depth * 6];

        float[,] falloffMap = GenerateFalloffMap(width, depth);

        for (int z = 0, index = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++, index++)
            {
                float xCoord = x + noiseOffset.x;
                float zCoord = z + noiseOffset.y;

                float baseHeight = GenerateFractalNoise(xCoord, zCoord);
                float falloff = falloffMap[x, z];
                float finalHeight = Mathf.Clamp01(baseHeight - falloff) * heightMultiplier;

                float riverCarve = GetRiverOffset(x, z);
                finalHeight -= riverCarve;

                vertices[index] = new Vector3(x, finalHeight, z);

                float temp = Mathf.InverseLerp(0, width, x);
                float moisture = Mathf.InverseLerp(0, depth, z);

                (int biomeIndex, float blendWeight) = GetBiomeBlend(temp, moisture);

                bool isRiver = riverCarve > 0.01f;
                colors[index] = isRiver ?
                    new Color(0, 0, 1) :
                    new Color(blendWeight, biomeIndex / (float)(biomes.Count - 1), 0);
            }
        }

        int vert = 0;
        int tris = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;

                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }
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
        {
            GetComponent<MeshRenderer>().material = terrainMaterial;
        }

        // Add or update MeshCollider
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<MeshCollider>();

        collider.sharedMesh = null; // Force update
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
                int xIndex = Mathf.RoundToInt(x);
                int zIndex = Mathf.RoundToInt(z);
                int index = zIndex * (width + 1) + xIndex;

                if (index >= vertices.Length) continue;

                Color color = colors[index];
                float blend = color.r;
                int biomeIndex = Mathf.RoundToInt(color.g * (biomes.Count - 1));
                float height = vertices[index].y;

                if (biomeIndex < 0 || biomeIndex >= biomes.Count) continue;
                Biome biome = biomes[biomeIndex];

                if (biome.spawnPrefabs == null || biome.spawnPrefabs.Count == 0)
                    continue;

                if (blend < spawnBlendThreshold || height < spawnMinHeight)
                    continue;

                float chance = biome.spawnDensity * blend * blend;
                bool shouldSpawn = ignoreSpawnChance || (Random.value <= chance);

                if (!shouldSpawn)
                    continue;

                GameObject prefab = biome.spawnPrefabs[Random.Range(0, biome.spawnPrefabs.Count)];
                Vector3 position = vertices[index] + transform.position;
                Vector3 normal = normals != null && index < normals.Length ? normals[index] : Vector3.up;

                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal) *
                                      Quaternion.Euler(0, Random.Range(0, 360f), 0);

                GameObject spawned = Instantiate(prefab, position, rotation, transform);
                spawnedObjects.Add(spawned);
                spawnedCount++;
            }
        }

        Debug.Log($"Spawned total: {spawnedCount}");
    }

    void ClearSpawnedObjects()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
    }

    float GenerateFractalNoise(float x, float z)
    {
        float noise = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x / scale * frequency;
            float sampleZ = z / scale * frequency;
            float perlin = Mathf.PerlinNoise(sampleX, sampleZ);

            noise += perlin * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Mathf.Clamp01(noise);
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
            float xOffset = i * 100f;
            float riverCenter = Mathf.PerlinNoise((z + xOffset) * riverCurveFrequency, i * 10f) * width;

            float distance = Mathf.Abs(x - riverCenter);
            float t = Mathf.InverseLerp(riverWidth, 0, distance);
            float depth = Mathf.SmoothStep(0, riverDepth, t);

            totalOffset += depth;
        }

        return totalOffset;
    }

    (int, float) GetBiomeBlend(float temp, float moisture)
    {
        float closestDistance = float.MaxValue;
        int closestBiome = 0;
        for (int i = 0; i < biomes.Count; i++)
        {
            var b = biomes[i];
            float midTemp = (b.minTemperature + b.maxTemperature) / 2f;
            float midMoist = (b.minMoisture + b.maxMoisture) / 2f;
            float dist = Vector2.Distance(new Vector2(temp, moisture), new Vector2(midTemp, midMoist));
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestBiome = i;
            }
        }

        float maxDistance = 0.3f;
        float weight = Mathf.Clamp01(1f - (closestDistance / maxDistance));
        return (closestBiome, weight);
    }
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
    [Range(0f, 1f)] public float spawnDensity;
    public List<GameObject> spawnPrefabs;
}
