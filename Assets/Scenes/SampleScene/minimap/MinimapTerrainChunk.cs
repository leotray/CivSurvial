using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MinimapTerrainChunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh chunkMesh;

    private int resolution;
    private float heightScale;
    private bool shadowsEnabled;

    private Vector3[] vertices;
    private Vector3[] normals;
    private Color[] colors;
    private Vector2[] uvs; // NEW: Add UV coordinates for smoothing
    private int[] triangles;
    private bool isInitialized = false;

    public void Initialize(int chunkResolution, float terrainScale, bool enableShadows = true)
    {
        if (isInitialized) return;

        resolution = chunkResolution;
        heightScale = terrainScale;
        shadowsEnabled = enableShadows;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        gameObject.layer = LayerMask.NameToLayer("MinimapTerrain");

        Material chunkMaterial = Resources.Load<Material>("MinimapChunkMaterial");
        if (chunkMaterial == null)
        {
            chunkMaterial = new Material(Shader.Find("Custom/MinimapVertexColor"));
            chunkMaterial.name = "MinimapChunkMaterial_Auto";
            chunkMaterial.SetFloat("_ShadowStrength", 0.7f);
            chunkMaterial.SetColor("_AmbientColor", new Color(0.2f, 0.2f, 0.3f, 1f));
            chunkMaterial.SetFloat("_SmoothnessFactor", 1.2f); // NEW: Set smoothness
        }

        meshRenderer.material = chunkMaterial;

        if (shadowsEnabled)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
        }
        else
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        int vertexCount = (resolution + 1) * (resolution + 1);
        int triangleCount = resolution * resolution * 6;

        vertices = new Vector3[vertexCount];
        normals = new Vector3[vertexCount];
        colors = new Color[vertexCount];
        uvs = new Vector2[vertexCount]; // NEW: Initialize UVs
        triangles = new int[triangleCount];

        chunkMesh = new Mesh();
        chunkMesh.name = "MinimapTerrainChunk3D";
        chunkMesh.MarkDynamic();
        meshFilter.mesh = chunkMesh;

        GenerateTriangleIndices();

        isInitialized = true;
    }

    private void GenerateTriangleIndices()
    {
        int triangleIndex = 0;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int vertexIndex = y * (resolution + 1) + x;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + resolution + 1;
                triangles[triangleIndex + 2] = vertexIndex + 1;

                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + resolution + 1;
                triangles[triangleIndex + 5] = vertexIndex + resolution + 2;

                triangleIndex += 6;
            }
        }
    }

    public void GenerateChunk(Vector2Int chunkCoord, Vector3 worldPosition, int chunkSize, PersonalMinimapManager minimapManager)
    {
        if (!isInitialized) return;

        gameObject.SetActive(false);
        transform.position = worldPosition;

        float stepSize = (float)chunkSize / resolution;
        int vertexIndex = 0;

        Vector3 realWorldBase = new Vector3(chunkCoord.x * chunkSize, 0, chunkCoord.y * chunkSize);

        // NEW: Enhanced vertex generation with smooth color interpolation
        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float realWorldX = realWorldBase.x + x * stepSize;
                float realWorldZ = realWorldBase.z + y * stepSize;

                float height = minimapManager.SampleTerrainHeightAtPosition(realWorldX, realWorldZ);

                // NEW: Sample multiple points for smoother color interpolation
                Color vertexColor = GetSmoothedColorAtPosition(realWorldX, realWorldZ, stepSize, minimapManager);

                vertices[vertexIndex] = new Vector3(x * stepSize, height, y * stepSize);
                colors[vertexIndex] = vertexColor;

                // NEW: Generate UV coordinates for smooth blending
                uvs[vertexIndex] = new Vector2((float)x / resolution, (float)y / resolution);

                vertexIndex++;
            }
        }

        CalculateUltraSmoothNormals();

        chunkMesh.vertices = vertices;
        chunkMesh.normals = normals;
        chunkMesh.colors = colors;
        chunkMesh.uv = uvs; // NEW: Apply UVs
        chunkMesh.triangles = triangles;
        chunkMesh.RecalculateBounds();
        chunkMesh.RecalculateTangents();

        gameObject.SetActive(true);
    }

    // NEW: Enhanced color sampling for ultra-smooth appearance
    private Color GetSmoothedColorAtPosition(float worldX, float worldZ, float stepSize, PersonalMinimapManager minimapManager)
    {
        // Sample center point
        Color centerColor = minimapManager.SampleTerrainAtPosition(worldX, worldZ);

        // Sample surrounding points for smoother blending
        float offset = stepSize * 0.25f;
        Color[] samples = new Color[5];
        samples[0] = centerColor;
        samples[1] = minimapManager.SampleTerrainAtPosition(worldX + offset, worldZ);
        samples[2] = minimapManager.SampleTerrainAtPosition(worldX - offset, worldZ);
        samples[3] = minimapManager.SampleTerrainAtPosition(worldX, worldZ + offset);
        samples[4] = minimapManager.SampleTerrainAtPosition(worldX, worldZ - offset);

        // Blend all samples for smoother result
        Color blendedColor = Color.black;
        float[] weights = { 0.4f, 0.15f, 0.15f, 0.15f, 0.15f };

        for (int i = 0; i < samples.Length; i++)
        {
            blendedColor += samples[i] * weights[i];
        }

        return blendedColor;
    }

    // NEW: Ultra-smooth normal calculation
    private void CalculateUltraSmoothNormals()
    {
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }

        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = y * (resolution + 1) + x;
                Vector3 normal = CalculateUltraSmoothVertexNormal(x, y);
                normals[index] = normal.normalized;
            }
        }
    }

    // NEW: Ultra-smooth normal calculation using weighted averaging
    private Vector3 CalculateUltraSmoothVertexNormal(int x, int y)
    {
        Vector3 normal = Vector3.zero;
        float totalWeight = 0f;

        Vector3 center = vertices[y * (resolution + 1) + x];

        // Sample in a larger radius for ultra-smooth normals
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx <= resolution && ny >= 0 && ny <= resolution)
                {
                    Vector3 neighbor = vertices[ny * (resolution + 1) + nx];
                    Vector3 diff = neighbor - center;

                    // Weight based on distance (closer points have more influence)
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float weight = 1.0f / (1.0f + distance);

                    Vector3 crossUp = Vector3.Cross(diff, Vector3.up);
                    Vector3 localNormal = Vector3.Cross(crossUp, diff);

                    if (localNormal.magnitude > 0.001f)
                    {
                        normal += localNormal.normalized * weight;
                        totalWeight += weight;
                    }
                }
            }
        }

        if (totalWeight > 0)
        {
            normal = (normal / totalWeight).normalized;
        }
        else
        {
            normal = Vector3.up;
        }

        return normal;
    }

    public void ReturnToPool()
    {
        gameObject.SetActive(false);
    }
}
