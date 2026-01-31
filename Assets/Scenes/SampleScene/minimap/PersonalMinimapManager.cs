using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using System.Collections;
using TMPro;
using FishNet.Connection;
using System.Linq;

public class PersonalMinimapManager : NetworkBehaviour
{
    [Header("Minimap Mode Selection")]
    public MinimapMode minimapMode = MinimapMode.Mode3D;
    public enum MinimapMode { Mode2D, Mode3D }

    [Header("Minimap UI References")]
    public RawImage minimapDisplay;
    public Transform minimapPanel;
    public Transform playerMarkersContainer;
    public Transform structureMarkersContainer;
    public GameObject centerMarker; // This represents the LOCAL player
    public Button toggleButton;
    public Slider zoomSlider;
    public TextMeshProUGUI coordinatesText;

    [Header("Minimap Settings")]
    [Range(64, 256)] public int mapResolution = 128;
    [Range(10, 100)] public float viewRadius = 35f;
    [Range(15f, 50f)] public float chunkUpdateDistance = 25f;
    public LayerMask terrainLayer = -1;

    [Header("3D Camera Controls")]
    [Range(50f, 500f)] public float minimapCameraHeight = 150f;
    [Range(-1000f, 1000f)] public float minimapCameraNearClip = -100f;
    [Range(100f, 2000f)] public float minimapCameraFarClip = 1000f;
    [Range(10f, 200f)] public float minimapCameraSize = 50f;
    public bool enableDynamicHeight = true;

    [Header("3D Rendering & Range Settings")]
    [Range(0, 128)] public int renderDistanceInChunks = 64;
    [Range(1f, 5f)] public float chunkRangeMultiplier = 2f;
    [Range(15f, 75f)] public float cameraAngle = 45f;
    [Range(0f, 90f)] public float cameraRotationY = 0f;
    public bool enableShadows = true;
    
    [Header("FIXED: Player Look Following Options")]
    public MinimapRotationMode rotationMode = MinimapRotationMode.RotateMapOnly;
    public enum MinimapRotationMode 
    { 
        NoRotation,        // Map never rotates, always faces north
        RotateMapOnly,     // Only the terrain rotates, markers stay in world positions
        RotateEverything   // Both terrain and markers rotate (old buggy behavior)
    }
    
    [Range(0f, 1f)] public float lookFollowStrength = 0.7f;
    [Range(0.1f, 2f)] public float lookSmoothness = 0.8f;

    [Header("3D Minimap Settings")]
    [Range(5, 50)] public int chunkSize = 16;
    [Range(1, 10)] public int chunksPerFrame = 2;
    [Range(0.1f, 5f)] public float terrainScale = 1f;
    [Range(2, 32)] public int chunkResolution = 8;
    public bool enableLOD = true;
    
    [Header("Smooth Movement Settings")]
    [Range(0.5f, 5f)] public float textureBufferSize = 2f;
    public bool enableSmoothScrolling = true;
    [Range(0.1f, 2f)] public float scrollSmoothness = 0.8f;

    [Header("Height Color Configuration")]
    [Header("Water & Low Areas")]
    public float waterHeightMax = 0.5f;
    public Color waterColor = new Color(0.2f, 0.4f, 0.8f);

    [Header("Lowlands (Grass/Plains)")]
    public float lowlandHeightMax = 2f;
    public Color lowlandColor = new Color(0.3f, 0.7f, 0.3f);

    [Header("Midlands (Hills)")]
    public float midlandHeightMax = 4f;
    public Color midlandColor = new Color(0.6f, 0.8f, 0.4f);

    [Header("Highlands (Mountains)")]
    public float highlandHeightMax = 7f;
    public Color highlandColor = new Color(0.8f, 0.6f, 0.4f);

    [Header("Peaks (Snow/Ice)")]
    public float mountainHeightMax = 15f;
    public Color mountainColor = new Color(0.9f, 0.9f, 0.9f);

    [Header("Very High Peaks")]
    public Color extremeHeightColor = new Color(1f, 1f, 1f);

    [Header("Player Markers")]
    public GameObject playerMarkerPrefab;
    public Color localPlayerColor = Color.blue;
    public Color otherPlayerColor = Color.red;
    [Range(10, 100)] public float playerVisibilityRange = 40f;

    [Header("Structure Markers")]
    public GameObject structureMarkerPrefab;
    public Sprite cityHallIcon;
    public Sprite constructionSiteIcon;
    [Range(10, 100)] public float structureVisibilityRange = 50f;

    [Header("Controls")]
    public float minZoom = 0.5f;
    public float maxZoom = 2f;
    public KeyCode toggleKey = KeyCode.M;

    [Header("Performance Settings")]
    [Range(16, 64)] public int pixelsPerFrameUpdate = 32;
    [Range(0.1f, 1f)] public float markerUpdateInterval = 0.15f;

    // Private variables
    private TerrainGeneratorMultiplayer terrainGenerator;
    private Camera minimapCamera;
    private RenderTexture renderTexture;
    
    // FIXED: Only track OTHER players, not local player
    private Dictionary<ulong, MinimapPlayerMarker> otherPlayerMarkers = new Dictionary<ulong, MinimapPlayerMarker>();
    private List<MinimapStructureMarker> structureMarkers = new List<MinimapStructureMarker>();
    private bool isMinimapActive = true;
    private float currentZoom = 1f;
    private bool isInitialized = false;

    // 2D System
    private Texture2D chunkMinimapTexture;
    private Vector3 lastChunkCenter;
    private Vector3 currentChunkCenter;
    private bool isUpdatingChunk = false;
    private Vector2 currentTextureOffset = Vector2.zero;

    // 3D System
    private Transform terrainChunksParent;
    private GameObject minimapCameraHolder;
    private Light minimapLight;
    private Dictionary<Vector2Int, MinimapTerrainChunk> activeChunks = new Dictionary<Vector2Int, MinimapTerrainChunk>();
    private Queue<MinimapTerrainChunk> chunkPool = new Queue<MinimapTerrainChunk>();
    private Coroutine chunkUpdateCoroutine;
    private Vector3 lastPlayerChunkPosition;
    private Vector3 minimapWorldOffset;
    private int minimapTerrainLayer;

    // Runtime chunk management
    private int lastRenderDistance = -1;
    private bool needsChunkRefresh = false;

    // Player look following
    private PlayerLook playerLookScript;
    private PlayerMovementMultiplayer playerMovementScript;
    private float targetCameraYRotation;
    private float currentCameraYRotation;

    // FIXED: Enhanced positioning variables
    private RectTransform minimapDisplayRect;
    private Vector2 minimapDisplaySize;
    private Vector2 minimapDisplayCenter; // Always (0,0) for center

    // Smooth movement tracking
    private Vector3 smoothPlayerPosition;
    private Vector3 lastPlayerPosition;
    private float actualViewRadius;

    // Terrain data cache
    private Vector3[] terrainVertices;
    private Color[] terrainColors;
    private int terrainWidth;
    private int terrainHeight;
    private bool terrainDataCached = false;
    private Vector3 terrainOffset;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (Owner.IsLocalClient)
        {
            

            smoothPlayerPosition = transform.position;
            lastPlayerPosition = transform.position;
            actualViewRadius = viewRadius * (1f + textureBufferSize);
            lastRenderDistance = renderDistanceInChunks;

            playerLookScript = GetComponent<PlayerLook>();
            playerMovementScript = GetComponent<PlayerMovementMultiplayer>();
            
            if (playerLookScript == null)
                
            
            if (playerMovementScript == null)
                

            StartCoroutine(InitializeMinimapWithRetries());
        }
        else
        {
            // FIXED: Non-local players disable their minimap UI completely
            if (minimapPanel != null)
                minimapPanel.gameObject.SetActive(false);
                
            
        }
    }

    private IEnumerator InitializeMinimapWithRetries()
    {
        int attempts = 0;
        while (!isInitialized && attempts < 30)
        {
            attempts++;

            if (terrainGenerator == null)
            {
                terrainGenerator = FindObjectOfType<TerrainGeneratorMultiplayer>();
                if (terrainGenerator != null)
                {
                    int terrainLayerNumber = terrainGenerator.gameObject.layer;
                    terrainLayer = 1 << terrainLayerNumber;
                    
                }
            }

            if (terrainGenerator != null)
            {
                MeshFilter meshFilter = terrainGenerator.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.mesh != null && meshFilter.mesh.vertexCount > 0)
                {
                    CacheTerrainData(meshFilter.mesh);
                    break;
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        if (terrainGenerator == null || !terrainDataCached)
        {
            
            yield break;
        }

        // FIXED: Initialize minimap display references
        InitializeMinimapDisplayReferences();

        if (SetupMinimapCamera() && SetupUI())
        {
            // FIXED: Setup center marker for local player
            SetupCenterMarker();

            if (minimapMode == MinimapMode.Mode3D)
            {
                Setup3DSystem();
                StartCoroutine(Update3DChunksRoutine());
            }
            else
            {
                InitializeSmoothTexture();
                StartCoroutine(UpdateMinimapChunk(transform.position, true));
                StartCoroutine(SmoothMovementRoutine());
            }

            StartCoroutine(UpdateMarkersRoutine());
            isInitialized = true;

            
        }
    }

    // FIXED: Initialize minimap display references for proper positioning
    private void InitializeMinimapDisplayReferences()
    {
        if (minimapDisplay != null)
        {
            minimapDisplayRect = minimapDisplay.GetComponent<RectTransform>();
            if (minimapDisplayRect != null)
            {
                minimapDisplaySize = minimapDisplayRect.rect.size;
                minimapDisplayCenter = Vector2.zero; // Always center for UI coordinates
                
            }
        }
    }

    // FIXED: Setup center marker to represent the local player
    private void SetupCenterMarker()
    {
        if (centerMarker == null)
        {
            
            return;
        }

        // FIXED: Center marker always stays in the exact center
        RectTransform centerRect = centerMarker.GetComponent<RectTransform>();
        if (centerRect != null)
        {
            centerRect.anchoredPosition = Vector2.zero; // Always center
            
            // Set color to local player color
            Image centerImage = centerMarker.GetComponent<Image>();
            if (centerImage != null)
            {
                centerImage.color = localPlayerColor;
            }
        }

        
    }

    private void CacheTerrainData(Mesh terrainMesh)
    {
        try
        {
            terrainVertices = terrainMesh.vertices;
            terrainColors = terrainMesh.colors;
            terrainWidth = terrainGenerator.width;
            terrainHeight = terrainGenerator.depth;
            terrainOffset = terrainGenerator.transform.position;
            terrainDataCached = true;

            
        }
        catch (System.Exception e)
        {
            
            terrainDataCached = false;
        }
    }

    private bool SetupMinimapCamera()
    {
        try
        {
            minimapCameraHolder = new GameObject($"MinimapCameraHolder_{Owner.ClientId}");
            
            GameObject camObj = new GameObject($"MinimapCamera_{Owner.ClientId}");
            camObj.transform.SetParent(minimapCameraHolder.transform);
            minimapCamera = camObj.AddComponent<Camera>();

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = minimapCameraSize;
            minimapCamera.nearClipPlane = minimapCameraNearClip;
            minimapCamera.farClipPlane = minimapCameraFarClip;
            
            if (minimapMode == MinimapMode.Mode3D)
            {
                Vector3 initialRotation = new Vector3(cameraAngle, cameraRotationY, 0f);
                minimapCameraHolder.transform.rotation = Quaternion.Euler(initialRotation);
                minimapCamera.transform.localRotation = Quaternion.identity;
                
                currentCameraYRotation = cameraRotationY;
                targetCameraYRotation = cameraRotationY;
                
                Vector3 initialPos = transform.position + Vector3.up * minimapCameraHeight;
                minimapCameraHolder.transform.position = initialPos;
                minimapCamera.transform.localPosition = Vector3.zero;
                
                minimapTerrainLayer = LayerMask.NameToLayer("MinimapTerrain");
                if (minimapTerrainLayer == -1)
                {
                    
                    return false;
                }
                
                minimapCamera.cullingMask = 1 << minimapTerrainLayer;
                minimapCamera.clearFlags = CameraClearFlags.SolidColor;
                minimapCamera.backgroundColor = waterColor;
                
                Setup3DLighting();
                
                
            }
            else
            {
                minimapCameraHolder.transform.position = transform.position + Vector3.up * 100f;
                minimapCameraHolder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                minimapCamera.transform.localPosition = Vector3.zero;
                minimapCamera.transform.localRotation = Quaternion.identity;
                minimapCamera.cullingMask = terrainLayer;
                minimapCamera.clearFlags = CameraClearFlags.SolidColor;
                minimapCamera.backgroundColor = waterColor;
            }

            renderTexture = new RenderTexture(mapResolution, mapResolution, 16);
            renderTexture.name = $"MinimapRT_{Owner.ClientId}";
            minimapCamera.targetTexture = renderTexture;

            if (minimapDisplay != null)
            {
                minimapDisplay.texture = renderTexture;
            }
            else
            {
                
                return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            
            return false;
        }
    }

    private void Setup3DLighting()
    {
        if (!enableShadows) return;

        GameObject lightObj = new GameObject($"MinimapLight_{Owner.ClientId}");
        lightObj.transform.SetParent(minimapCameraHolder.transform);
        minimapLight = lightObj.AddComponent<Light>();
        
        minimapLight.type = LightType.Directional;
        minimapLight.color = Color.white;
        minimapLight.intensity = 1.5f;
        minimapLight.shadows = LightShadows.Soft;
        minimapLight.shadowStrength = 0.8f;
        minimapLight.cullingMask = 1 << minimapTerrainLayer;
        minimapLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
        
        lightObj.transform.localPosition = Vector3.zero;
        lightObj.transform.localRotation = Quaternion.Euler(50f, -45f, 0f);
    }

    private void Setup3DSystem()
    {
        minimapWorldOffset = new Vector3(10000f, 0f, 10000f);

        GameObject chunksParent = new GameObject($"MinimapTerrain3D_{Owner.ClientId}");
        chunksParent.transform.position = minimapWorldOffset;
        terrainChunksParent = chunksParent.transform;

        int estimatedChunkCount = CalculateRequiredChunkCount();
        int poolSize = Mathf.Max(estimatedChunkCount + 20, 50);
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject chunkObj = new GameObject($"PooledChunk_{i}");
            chunkObj.transform.SetParent(terrainChunksParent);
            chunkObj.SetActive(false);

            MinimapTerrainChunk chunk = chunkObj.AddComponent<MinimapTerrainChunk>();
            chunk.Initialize(chunkResolution, terrainScale, enableShadows);
            chunkPool.Enqueue(chunk);
        }

        lastPlayerChunkPosition = transform.position;
        
    }

    private int CalculateRequiredChunkCount()
    {
        if (renderDistanceInChunks <= 0) return 0;
        
        int diameter = (renderDistanceInChunks * 2) + 1;
        int approximateCount = (diameter * diameter * 3) / 4;
        return approximateCount;
    }

    private bool SetupUI()
    {
        try
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleMinimap);
            }

            if (zoomSlider != null)
            {
                zoomSlider.minValue = minZoom;
                zoomSlider.maxValue = maxZoom;
                zoomSlider.value = currentZoom;
                zoomSlider.onValueChanged.AddListener(OnZoomChanged);
            }

            return true;
        }
        catch (System.Exception e)
        {
            
            return false;
        }
    }

    private IEnumerator Update3DChunksRoutine()
    {
        while (true)
        {
            if (isMinimapActive && Owner.IsLocalClient && isInitialized && minimapMode == MinimapMode.Mode3D)
            {
                Vector3 currentPos = transform.position;
                
                if (lastRenderDistance != renderDistanceInChunks)
                {
                    
                    lastRenderDistance = renderDistanceInChunks;
                    needsChunkRefresh = true;
                    
                    int requiredCount = CalculateRequiredChunkCount();
                    int currentPoolSize = chunkPool.Count + activeChunks.Count;
                    if (requiredCount > currentPoolSize)
                    {
                        int additionalChunks = requiredCount - currentPoolSize + 10;
                        for (int i = 0; i < additionalChunks; i++)
                        {
                            GameObject chunkObj = new GameObject($"PooledChunk_Runtime_{i}");
                            chunkObj.transform.SetParent(terrainChunksParent);
                            chunkObj.SetActive(false);

                            MinimapTerrainChunk chunk = chunkObj.AddComponent<MinimapTerrainChunk>();
                            chunk.Initialize(chunkResolution, terrainScale, enableShadows);
                            chunkPool.Enqueue(chunk);
                        }
                    }
                }
                
                if (minimapCameraHolder != null)
                {
                    float dynamicHeight = minimapCameraHeight;
                    if (enableDynamicHeight)
                    {
                        float terrainHeight = SampleTerrainHeightAtPosition(currentPos.x, currentPos.z);
                        dynamicHeight = Mathf.Max(minimapCameraHeight, terrainHeight + 50f);
                    }
                    
                    Vector3 targetCameraPos = minimapWorldOffset + new Vector3(currentPos.x, dynamicHeight, currentPos.z);
                    minimapCameraHolder.transform.position = targetCameraPos;
                    
                    UpdateCameraRotationWithPlayerLook();
                    
                    if (minimapCamera.orthographicSize != minimapCameraSize)
                    {
                        minimapCamera.orthographicSize = minimapCameraSize;
                    }
                    
                    if (minimapCamera.nearClipPlane != minimapCameraNearClip)
                    {
                        minimapCamera.nearClipPlane = minimapCameraNearClip;
                    }
                    
                    if (minimapCamera.farClipPlane != minimapCameraFarClip)
                    {
                        minimapCamera.farClipPlane = minimapCameraFarClip;
                    }
                }
                
                if (Vector3.Distance(currentPos, lastPlayerChunkPosition) > chunkSize * 0.3f || needsChunkRefresh)
                {
                    UpdateVisible3DChunks(currentPos);
                    lastPlayerChunkPosition = currentPos;
                    needsChunkRefresh = false;
                }
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // FIXED: Camera rotation now respects rotation mode
    private void UpdateCameraRotationWithPlayerLook()
    {
        if (minimapCameraHolder == null) return;

        float playerYRotation = transform.eulerAngles.y;
        
        switch (rotationMode)
        {
            case MinimapRotationMode.NoRotation:
                // Map never rotates, always faces north
                targetCameraYRotation = cameraRotationY;
                break;
                
            case MinimapRotationMode.RotateMapOnly:
            case MinimapRotationMode.RotateEverything:
                // Both modes rotate the camera/terrain
                float followRotation = Mathf.LerpAngle(cameraRotationY, playerYRotation, lookFollowStrength);
                targetCameraYRotation = followRotation;
                break;
        }
        
        currentCameraYRotation = Mathf.LerpAngle(currentCameraYRotation, targetCameraYRotation, lookSmoothness);
        
        Vector3 cameraRotation = new Vector3(cameraAngle, currentCameraYRotation, 0f);
        minimapCameraHolder.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    private void UpdateVisible3DChunks(Vector3 playerPosition)
    {
        StartCoroutine(UpdateVisible3DChunksCoroutine(playerPosition));
    }

    private IEnumerator UpdateVisible3DChunksCoroutine(Vector3 playerPosition)
    {
        int centerChunkX = Mathf.FloorToInt(playerPosition.x / chunkSize);
        int centerChunkZ = Mathf.FloorToInt(playerPosition.z / chunkSize);
        
        int chunkRadius = renderDistanceInChunks;

        HashSet<Vector2Int> requiredChunks = new HashSet<Vector2Int>();

        for (int x = centerChunkX - chunkRadius; x <= centerChunkX + chunkRadius; x++)
        {
            for (int z = centerChunkZ - chunkRadius; z <= centerChunkZ + chunkRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(x, z);
                Vector3 chunkWorldPos = new Vector3(x * chunkSize, 0, z * chunkSize);
                
                float distanceFromCenter = Vector3.Distance(new Vector3(playerPosition.x, 0, playerPosition.z), 
                                                          new Vector3(chunkWorldPos.x, 0, chunkWorldPos.z));
                float maxDistance = chunkRadius * chunkSize + chunkSize * 0.5f;
                
                if (distanceFromCenter <= maxDistance)
                {
                    requiredChunks.Add(chunkCoord);
                }
            }
        }

        var chunksToRemove = new List<Vector2Int>();
        foreach (var kvp in activeChunks)
        {
            if (!requiredChunks.Contains(kvp.Key))
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        foreach (var chunkCoord in chunksToRemove)
        {
            if (activeChunks.TryGetValue(chunkCoord, out MinimapTerrainChunk chunk))
            {
                ReturnChunkToPool(chunk);
                activeChunks.Remove(chunkCoord);
            }
        }

        int chunksProcessed = 0;
        foreach (var chunkCoord in requiredChunks)
        {
            if (!activeChunks.ContainsKey(chunkCoord))
            {
                if (chunkPool.Count > 0)
                {
                    MinimapTerrainChunk chunk = chunkPool.Dequeue();
                    
                    Vector3 chunkRealWorldPos = new Vector3(chunkCoord.x * chunkSize, 0, chunkCoord.y * chunkSize);
                    Vector3 chunkMinimapPos = minimapWorldOffset + chunkRealWorldPos;
                    
                    chunk.GenerateChunk(chunkCoord, chunkMinimapPos, chunkSize, this);
                    activeChunks[chunkCoord] = chunk;

                    chunksProcessed++;
                    
                    if (chunksProcessed >= chunksPerFrame)
                    {
                        yield return null;
                        chunksProcessed = 0;
                    }
                }
                else
                {
                    
                }
            }
        }
    }

    private void ReturnChunkToPool(MinimapTerrainChunk chunk)
    {
        chunk.gameObject.SetActive(false);
        chunkPool.Enqueue(chunk);
    }

    public Color SampleTerrainAtPosition(float worldX, float worldZ)
    {
        if (!terrainDataCached)
        {
            return waterColor;
        }

        float localX = worldX - terrainOffset.x;
        float localZ = worldZ - terrainOffset.z;

        int vertX = Mathf.RoundToInt(localX);
        int vertZ = Mathf.RoundToInt(localZ);

        if (vertX < 0 || vertX > terrainWidth || vertZ < 0 || vertZ > terrainHeight)
        {
            return new Color(0.1f, 0.1f, 0.3f);
        }

        int vertIndex = vertZ * (terrainWidth + 1) + vertX;

        if (vertIndex < 0 || vertIndex >= terrainVertices.Length)
        {
            return new Color(0.3f, 0.1f, 0.1f);
        }

        float height = terrainVertices[vertIndex].y;

        Color biomeBlend = Color.white;
        if (terrainColors != null && vertIndex < terrainColors.Length)
        {
            biomeBlend = terrainColors[vertIndex];

            if (biomeBlend.b > 0.5f)
                return waterColor;
        }

        Color heightColor = GetConfigurableHeightColor(height);

        if (terrainColors != null && vertIndex < terrainColors.Length)
        {
            Color biomeColor = GetBiomeColorFromMesh(biomeBlend.g);
            float blendStrength = biomeBlend.r * 0.2f;
            heightColor = Color.Lerp(heightColor, biomeColor, blendStrength);
        }

        return heightColor;
    }

    public float SampleTerrainHeightAtPosition(float worldX, float worldZ)
    {
        if (!terrainDataCached)
        {
            return 0f;
        }

        float localX = worldX - terrainOffset.x;
        float localZ = worldZ - terrainOffset.z;

        int vertX = Mathf.RoundToInt(localX);
        int vertZ = Mathf.RoundToInt(localZ);

        if (vertX < 0 || vertX > terrainWidth || vertZ < 0 || vertZ > terrainHeight)
            return 0f;

        int vertIndex = vertZ * (terrainWidth + 1) + vertX;

        if (vertIndex < 0 || vertIndex >= terrainVertices.Length)
            return 0f;

        float height = terrainVertices[vertIndex].y * terrainScale;
        return height;
    }

    private Color GetConfigurableHeightColor(float height)
    {
        if (height <= waterHeightMax)
            return waterColor;
        else if (height <= lowlandHeightMax)
            return lowlandColor;
        else if (height <= midlandHeightMax)
            return midlandColor;
        else if (height <= highlandHeightMax)
            return highlandColor;
        else if (height <= mountainHeightMax)
            return mountainColor;
        else
            return extremeHeightColor;
    }

    private Color GetBiomeColorFromMesh(float biomeIndex)
    {
        if (biomeIndex < 0.25f) return new Color(0.8f, 0.7f, 0.3f);
        else if (biomeIndex < 0.5f) return new Color(0.2f, 0.6f, 0.2f);
        else if (biomeIndex < 0.75f) return new Color(0.4f, 0.7f, 0.4f);
        else return new Color(0.5f, 0.4f, 0.3f);
    }

    private void InitializeSmoothTexture()
    {
        int bufferedResolution = Mathf.RoundToInt(mapResolution * (1f + textureBufferSize));
        chunkMinimapTexture = new Texture2D(bufferedResolution, bufferedResolution, TextureFormat.RGB24, false);

        Color[] initPixels = new Color[bufferedResolution * bufferedResolution];
        for (int i = 0; i < initPixels.Length; i++)
        {
            initPixels[i] = waterColor;
        }
        chunkMinimapTexture.SetPixels(initPixels);
        chunkMinimapTexture.Apply();

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = chunkMinimapTexture;
        }
    }

    private void Update()
    {
        if (!Owner.IsLocalClient || !isInitialized) return;

        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMinimap();
        }

        Vector3 currentPos = transform.position;
        smoothPlayerPosition = Vector3.Lerp(smoothPlayerPosition, currentPos, scrollSmoothness);

        if (minimapMode == MinimapMode.Mode2D)
        {
            if (Vector3.Distance(currentPos, lastChunkCenter) > chunkUpdateDistance && !isUpdatingChunk)
            {
                StartCoroutine(UpdateMinimapChunk(currentPos, false));
            }
            UpdateCameraPositionSmooth();
        }
        
        UpdateCoordinatesDisplay();
        lastPlayerPosition = currentPos;
    }

    private IEnumerator SmoothMovementRoutine()
    {
        while (true)
        {
            if (isMinimapActive && Owner.IsLocalClient && isInitialized && enableSmoothScrolling && minimapMode == MinimapMode.Mode2D)
            {
                UpdateTextureScrolling();
            }
            yield return null;
        }
    }

    private void UpdateTextureScrolling()
    {
        if (chunkMinimapTexture == null || minimapDisplay == null) return;

        Vector3 playerMovement = smoothPlayerPosition - currentChunkCenter;
        float textureSize = actualViewRadius * 2f;
        Vector2 targetOffset = new Vector2(
            playerMovement.x / textureSize,
            playerMovement.z / textureSize
        );

        currentTextureOffset = Vector2.Lerp(currentTextureOffset, targetOffset, scrollSmoothness);

        Rect uvRect = minimapDisplay.uvRect;
        float viewportSize = 1f / (1f + textureBufferSize);

        uvRect.width = viewportSize;
        uvRect.height = viewportSize;
        uvRect.x = (1f - viewportSize) * 0.5f + currentTextureOffset.x * 0.1f;
        uvRect.y = (1f - viewportSize) * 0.5f + currentTextureOffset.y * 0.1f;

        uvRect.x = Mathf.Clamp(uvRect.x, 0f, 1f - uvRect.width);
        uvRect.y = Mathf.Clamp(uvRect.y, 0f, 1f - uvRect.height);

        minimapDisplay.uvRect = uvRect;
    }

    private IEnumerator UpdateMinimapChunk(Vector3 centerPosition, bool isInitialGeneration)
    {
        if (isUpdatingChunk && !isInitialGeneration)
            yield break;

        isUpdatingChunk = true;
        Vector3 newChunkCenter = centerPosition;

        if (!terrainDataCached)
        {
            isUpdatingChunk = false;
            yield break;
        }

        float halfRadius = actualViewRadius;
        float minWorldX = newChunkCenter.x - halfRadius;
        float maxWorldX = newChunkCenter.x + halfRadius;
        float minWorldZ = newChunkCenter.z - halfRadius;
        float maxWorldZ = newChunkCenter.z + halfRadius;

        int textureRes = chunkMinimapTexture.width;
        int pixelsProcessed = 0;

        for (int y = 0; y < textureRes; y++)
        {
            for (int x = 0; x < textureRes; x++)
            {
                float worldX = minWorldX + (x / (float)textureRes) * (maxWorldX - minWorldX);
                float worldZ = minWorldZ + (y / (float)textureRes) * (maxWorldZ - minWorldZ);

                Color pixelColor = SampleTerrainAtPosition(worldX, worldZ);
                chunkMinimapTexture.SetPixel(x, y, pixelColor);

                pixelsProcessed++;

                if (pixelsProcessed % pixelsPerFrameUpdate == 0)
                {
                    yield return null;
                }
            }
        }

        chunkMinimapTexture.Apply();
        lastChunkCenter = newChunkCenter;
        currentChunkCenter = newChunkCenter;

        if (isInitialGeneration)
        {
            currentTextureOffset = Vector2.zero;
        }

        isUpdatingChunk = false;
    }

    private void UpdateCameraPositionSmooth()
    {
        if (minimapCameraHolder != null && minimapMode == MinimapMode.Mode2D)
        {
            Vector3 smoothPos = smoothPlayerPosition;
            minimapCameraHolder.transform.position = new Vector3(smoothPos.x, 100f, smoothPos.z);
        }
    }

    // FIXED: Enhanced multiplayer marker system - ONLY tracks OTHER players
    private IEnumerator UpdateMarkersRoutine()
    {
        while (true)
        {
            if (isMinimapActive && Owner.IsLocalClient && isInitialized)
            {
                UpdateOtherPlayerMarkers();
                UpdateStructureMarkers();
            }
            yield return new WaitForSeconds(markerUpdateInterval);
        }
    }

    // FIXED: Only create markers for OTHER players, not local player
    private void UpdateOtherPlayerMarkers()
    {
        var playersToRemove = new List<ulong>(otherPlayerMarkers.Keys);
        Vector3 localPlayerPosition = smoothPlayerPosition;

        NetworkObject[] allNetworkObjects = FindObjectsOfType<NetworkObject>();
        
        foreach (NetworkObject netObj in allNetworkObjects)
        {
            if (netObj.gameObject.CompareTag("Player") && netObj.Owner != null)
            {
                ulong playerId = (ulong)netObj.Owner.ClientId;
                
                // FIXED: Skip local player - they are represented by centerMarker
                if (playerId == (ulong)Owner.ClientId)
                {
                    continue; // Don't create marker for ourselves
                }

                PlayerIdentity identity = netObj.GetComponent<PlayerIdentity>();
                if (identity != null)
                {
                    float distance = Vector3.Distance(localPlayerPosition, netObj.transform.position);

                    

                    if (distance <= playerVisibilityRange)
                    {
                        if (!otherPlayerMarkers.ContainsKey(playerId))
                        {
                            CreateOtherPlayerMarker(playerId, netObj.gameObject, identity);
                            
                        }
                        else
                        {
                            UpdateOtherPlayerMarker(playerId, netObj.gameObject, identity);
                        }
                        playersToRemove.Remove(playerId);
                    }
                    else
                    {
                        
                    }
                }
                else
                {
                    
                }
            }
        }

        foreach (ulong playerId in playersToRemove)
        {
            RemoveOtherPlayerMarker(playerId);
            
        }
    }

    private void CreateOtherPlayerMarker(ulong playerId, GameObject playerObj, PlayerIdentity identity)
    {
        if (playerMarkerPrefab == null || playerMarkersContainer == null) 
        {
            
            return;
        }

        GameObject markerObj = Instantiate(playerMarkerPrefab, playerMarkersContainer);
        MinimapPlayerMarker marker = markerObj.GetComponent<MinimapPlayerMarker>();

        if (marker == null)
            marker = markerObj.AddComponent<MinimapPlayerMarker>();

        // FIXED: Other players always use otherPlayerColor
        marker.Initialize(playerId, identity.GetPlayerName(), otherPlayerColor, false);
        otherPlayerMarkers[playerId] = marker;
        
        // FIXED: Set parent and ensure proper setup
        RectTransform markerRect = markerObj.GetComponent<RectTransform>();
        if (markerRect != null)
        {
            markerRect.SetParent(playerMarkersContainer, false);
            markerRect.localScale = Vector3.one;
        }
        
        UpdateOtherPlayerMarker(playerId, playerObj, identity);
    }

    // FIXED: Enhanced positioning that respects rotation mode
    private void UpdateOtherPlayerMarker(ulong playerId, GameObject playerObj, PlayerIdentity identity)
    {
        if (otherPlayerMarkers.TryGetValue(playerId, out MinimapPlayerMarker marker))
        {
            Vector2 minimapPos = CalculateOtherPlayerMinimapPosition(playerObj.transform.position);
            marker.UpdatePosition(minimapPos);
            marker.UpdateName(identity.GetPlayerName());
        }
    }

    private void RemoveOtherPlayerMarker(ulong playerId)
    {
        if (otherPlayerMarkers.TryGetValue(playerId, out MinimapPlayerMarker marker))
        {
            if (marker != null && marker.gameObject != null)
                Destroy(marker.gameObject);
            otherPlayerMarkers.Remove(playerId);
        }
    }

    // FIXED: True world position calculation that respects rotation mode
    private Vector2 CalculateOtherPlayerMinimapPosition(Vector3 otherPlayerWorldPos)
    {
        if (minimapDisplayRect == null)
        {
            
            return Vector2.zero;
        }

        // Calculate relative position to local player (local player is always center)
        Vector3 relativePos = otherPlayerWorldPos - smoothPlayerPosition;
        
        // FIXED: Apply rotation based on rotation mode
        if (rotationMode == MinimapRotationMode.RotateEverything && minimapMode == MinimapMode.Mode3D)
        {
            // Only rotate markers if we're in RotateEverything mode
            float rotationAngle = -currentCameraYRotation * Mathf.Deg2Rad;
            float rotatedX = relativePos.x * Mathf.Cos(rotationAngle) - relativePos.z * Mathf.Sin(rotationAngle);
            float rotatedZ = relativePos.x * Mathf.Sin(rotationAngle) + relativePos.z * Mathf.Cos(rotationAngle);
            relativePos = new Vector3(rotatedX, relativePos.y, rotatedZ);
        }
        // For NoRotation and RotateMapOnly modes, markers stay in true world positions

        // Convert to normalized coordinates relative to view radius
        float normalizedX = relativePos.x / (viewRadius * 2f);
        float normalizedZ = relativePos.z / (viewRadius * 2f);

        // Get minimap display size
        Vector2 displaySize = minimapDisplayRect.rect.size;
        
        // Convert to pixel coordinates within the minimap display
        float pixelX = normalizedX * displaySize.x;
        float pixelZ = normalizedZ * displaySize.y;

        // FIXED: Clamp to minimap display bounds to prevent markers from going outside
        float halfWidth = displaySize.x * 0.5f;
        float halfHeight = displaySize.y * 0.5f;
        
        pixelX = Mathf.Clamp(pixelX, -halfWidth + 10f, halfWidth - 10f);
        pixelZ = Mathf.Clamp(pixelZ, -halfHeight + 10f, halfHeight - 10f);

        Vector2 finalPosition = new Vector2(pixelX, pixelZ);
        
        
        
        return finalPosition;
    }

    // FIXED: Enhanced structure marker detection
    private void UpdateStructureMarkers()
    {
        Vector3 myPosition = smoothPlayerPosition;
        var constructionSites = FindObjectsOfType<CityBlockConstructionSite>();
        var existingStructures = new HashSet<GameObject>();

        

        foreach (var site in constructionSites)
        {
            float distance = Vector3.Distance(myPosition, site.transform.position);

            

            if (distance <= structureVisibilityRange)
            {
                existingStructures.Add(site.gameObject);

                if (!HasStructureMarker(site.gameObject))
                {
                    CreateStructureMarker(site.gameObject, "Construction Site", constructionSiteIcon);
                    
                }
                else
                {
                    UpdateStructureMarkerPosition(site.gameObject);
                }
            }
        }

        var allStructures = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb.gameObject.name.Contains("City") || mb.gameObject.name.Contains("Building"))
            .Select(mb => mb.gameObject)
            .Distinct();

        foreach (var structure in allStructures)
        {
            float distance = Vector3.Distance(myPosition, structure.transform.position);
            if (distance <= structureVisibilityRange)
            {
                existingStructures.Add(structure);

                if (!HasStructureMarker(structure))
                {
                    CreateStructureMarker(structure, "Structure", cityHallIcon);
                    
                }
                else
                {
                    UpdateStructureMarkerPosition(structure);
                }
            }
        }

        for (int i = structureMarkers.Count - 1; i >= 0; i--)
        {
            if (structureMarkers[i] == null ||
                !existingStructures.Contains(structureMarkers[i].targetStructure))
            {
                if (structureMarkers[i] != null && structureMarkers[i].gameObject != null)
                    Destroy(structureMarkers[i].gameObject);
                structureMarkers.RemoveAt(i);
            }
        }
    }

    private bool HasStructureMarker(GameObject structure)
    {
        return structureMarkers.Exists(marker => marker.targetStructure == structure);
    }

    private void CreateStructureMarker(GameObject structure, string structureName, Sprite icon)
    {
        if (structureMarkerPrefab == null || structureMarkersContainer == null) return;

        GameObject markerObj = Instantiate(structureMarkerPrefab, structureMarkersContainer);
        MinimapStructureMarker marker = markerObj.GetComponent<MinimapStructureMarker>();

        if (marker == null)
            marker = markerObj.AddComponent<MinimapStructureMarker>();

        marker.Initialize(structure, structureName, icon);
        
        RectTransform markerRect = markerObj.GetComponent<RectTransform>();
        if (markerRect != null)
        {
            markerRect.SetParent(structureMarkersContainer, false);
            markerRect.localScale = Vector3.one;
        }
        
        UpdateStructureMarkerPosition(structure);
        structureMarkers.Add(marker);
    }

    private void UpdateStructureMarkerPosition(GameObject structure)
    {
        var marker = structureMarkers.Find(m => m.targetStructure == structure);
        if (marker != null)
        {
            Vector2 minimapPos = CalculateOtherPlayerMinimapPosition(structure.transform.position);
            marker.UpdatePosition(minimapPos);
        }
    }

    private void UpdateCoordinatesDisplay()
    {
        if (coordinatesText != null)
        {
            Vector3 pos = transform.position;
            coordinatesText.text = $"X: {pos.x:F0}, Z: {pos.z:F0}";
        }
    }

    public void ToggleMinimap()
    {
        isMinimapActive = !isMinimapActive;
        minimapPanel.gameObject.SetActive(isMinimapActive);

        if (minimapCamera != null)
            minimapCamera.enabled = isMinimapActive;
    }

    private void OnZoomChanged(float zoomValue)
    {
        currentZoom = zoomValue;
        if (minimapCamera != null)
        {
            minimapCamera.orthographicSize = viewRadius * currentZoom;
        }
    }

    public void ForceRefresh()
    {
        if (isInitialized)
        {
            if (minimapMode == MinimapMode.Mode3D)
            {
                needsChunkRefresh = true;
                UpdateVisible3DChunks(transform.position);
            }
            else if (!isUpdatingChunk)
            {
                StartCoroutine(UpdateMinimapChunk(transform.position, false));
            }
        }
    }

    private void OnDestroy()
    {
        if (minimapCamera != null && renderTexture != null)
        {
            minimapCamera.targetTexture = null;
        }

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            DestroyImmediate(renderTexture);
            renderTexture = null;
        }

        if (chunkMinimapTexture != null)
        {
            DestroyImmediate(chunkMinimapTexture);
            chunkMinimapTexture = null;
        }

        if (minimapCameraHolder != null)
        {
            DestroyImmediate(minimapCameraHolder);
        }

        if (terrainChunksParent != null)
        {
            DestroyImmediate(terrainChunksParent.gameObject);
        }
    }
}
