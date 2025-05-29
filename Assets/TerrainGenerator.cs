using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

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

    [Header("Biome Settings")]
    public List<Biome> biomes;
    public Material terrainMaterial; // Material using a texture array shader

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;

    void Update()
    {
        GenerateTerrain();
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

        for (int z = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                float height = GenerateFractalNoise(x, z) * heightMultiplier;
                int index = z * (width + 1) + x;
                vertices[index] = new Vector3(x, height, z);

                // Generate mock temperature and moisture based on position (you can improve this)
                float temp = Mathf.InverseLerp(0, width, x);
                float moisture = Mathf.InverseLerp(0, depth, z);

                int biomeIndex = GetBiomeIndex(temp, moisture);
                colors[index] = new Color((float)biomeIndex / (biomes.Count - 1), 0, 0);
                UnityEngine.Debug.Log($"BiomeIndex: {biomeIndex}, biomes.Count: {biomes.Count}");
                UnityEngine.Debug.Log($"x:{x}, z:{z}, temp:{temp}, moisture:{moisture}");
                for (int i = 0; i < biomes.Count; i++)
                {
                    var b = biomes[i];
                    UnityEngine.Debug.Log($"Biome {i}: temp range [{b.minTemperature}, {b.maxTemperature}], moisture range [{b.minMoisture}, {b.maxMoisture}]");
                }
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

    int GetBiomeIndex(float temp, float moisture)
    {
        for (int i = 0; i < biomes.Count; i++)
        {
            var b = biomes[i];
            if (temp >= b.minTemperature && temp <= b.maxTemperature &&
                moisture >= b.minMoisture && moisture <= b.maxMoisture)
            {
                return i;
            }
        }
        return 0; // default biome
    }
}

[System.Serializable]
public class Biome
{
    public string name;
    [Range(0f, 1f)] public float minTemperature;
    [Range(0f, 1f)] public float maxTemperature;
    [Range(0f, 1f)] public float minMoisture;
    [Range(0f, 1f)] public float maxMoisture;
    public Texture2D texture; // Just for reference in Inspector
}
