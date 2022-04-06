using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public class WorldManager : MonoBehaviour
{
    public Material worldMaterial;
    public VoxelColor[] WorldColors;
    public VoxelTexture[] voxelTextures;
    public WorldSettings worldSettings;

    public Transform mainCamera;
    private Vector3 lastUpdatedPosition;
    private Vector3 previouslyCheckedPosition;

    //This will contain all modified voxels, structures, whatnot for all chunks, and will effectively be our saving mechanism
    public ConcurrentDictionary<Vector3, Dictionary<Vector3, Voxel>> modifiedVoxels = new ConcurrentDictionary<Vector3, Dictionary<Vector3, Voxel>>();
    public ConcurrentDictionary<Vector3, Chunk> activeChunks;
    public Queue<Chunk> chunkPool;
    public Queue<MeshData> meshDataPool;
    private List<MeshData> allMeshData;
    ConcurrentQueue<Vector3> chunksNeedCreation = new ConcurrentQueue<Vector3>();
    ConcurrentQueue<Vector3> deactiveChunks = new ConcurrentQueue<Vector3>();

    public int maxChunksToProcessPerFrame = 6;
    int mainThreadID;
    Thread checkActiveChunks;
    bool killThreads = false;
    bool performedFirstPass = false;
    void Start()
    {
        if (_instance != null)
        {
            if (_instance != this)
                Destroy(this);
        }
        else
        {
            _instance = this;
        }

        InitializeWorld();
    }

    private void InitializeWorld()
    {
        WorldSettings = worldSettings;

        int renderSizePlusExcess = WorldSettings.renderDistance + 3;
        int totalChunks = renderSizePlusExcess * renderSizePlusExcess;

        ComputeManager.Instance.Initialize(maxChunksToProcessPerFrame * 3);

        if(worldMaterial.shader.name.Contains("Tex") && voxelTextures.Length > 0)
        {
            Debug.Log("Trying to use Textures!");
            worldMaterial.SetTexture("_TextureArray", GenerateTextureArray());
        }

        activeChunks = new ConcurrentDictionary<Vector3, Chunk>();
        chunkPool = new Queue<Chunk>();
        meshDataPool = new Queue<MeshData>();
        allMeshData = new List<MeshData>();

        mainThreadID = Thread.CurrentThread.ManagedThreadId;

        for(int i = 0; i < maxChunksToProcessPerFrame * 3; i++)
        {
            GenerateMeshData(true);
        }

        for (int i = 0; i < totalChunks; i++)
        {
            GenerateChunk(Vector3.zero, true);
        }
        checkActiveChunks = new Thread(CheckActiveChunksLoop);
        checkActiveChunks.Priority = System.Threading.ThreadPriority.BelowNormal;
        checkActiveChunks.Start(); 

    }

    private void Update()
    {
        if (mainCamera?.transform.position != lastUpdatedPosition)
        {
            //Update position so our CheckActiveChunksLoop thread has it
            lastUpdatedPosition = positionToChunkCoord(mainCamera.transform.position);
        }

        Vector3 contToMake; 
        
        while (deactiveChunks.Count > 0 && deactiveChunks.TryDequeue(out contToMake))
        {
            deactiveChunk(contToMake);
        }
        for (int x = 0; x < maxChunksToProcessPerFrame; x++)
        {
            if (x < maxChunksToProcessPerFrame&& chunksNeedCreation.Count > 0 && chunksNeedCreation.TryDequeue(out contToMake))
            {
                Chunk chunk = GetChunk(contToMake);
                chunk.chunkPosition = contToMake;
                activeChunks.TryAdd(contToMake, chunk);
                ComputeManager.Instance.GenerateVoxelData(chunk, contToMake);
                x++;
            }

        }
    }

    void CheckActiveChunksLoop()
    {
        Profiler.BeginThreadProfiling("Chunks", "ChunkChecker");
        int halfRenderSize = WorldSettings.renderDistance / 2;
        int renderDistPlus1 = WorldSettings.renderDistance + 1;
        Vector3 pos = Vector3.zero;

        Bounds chunkBounds = new Bounds();
        chunkBounds.size = new Vector3(renderDistPlus1 * WorldSettings.chunkSize, 1, renderDistPlus1 * WorldSettings.chunkSize);
        while (true && !killThreads)
        {
            if (previouslyCheckedPosition != lastUpdatedPosition || !performedFirstPass)
            {
                previouslyCheckedPosition = lastUpdatedPosition;
                
                for (int x = -halfRenderSize; x < halfRenderSize; x++)
                    for (int z = -halfRenderSize; z < halfRenderSize; z++)
                    {
                        pos.x = x * WorldSettings.chunkSize + previouslyCheckedPosition.x;
                        pos.z = z * WorldSettings.chunkSize + previouslyCheckedPosition.z;

                        if (!activeChunks.ContainsKey(pos))
                        {
                            chunksNeedCreation.Enqueue(pos);
                        }
                    }

                chunkBounds.center = previouslyCheckedPosition;

                foreach (var kvp in activeChunks)
                {
                    if (!chunkBounds.Contains(kvp.Key))
                        deactiveChunks.Enqueue(kvp.Key);
                }
            }

            if (!performedFirstPass)
                performedFirstPass = true;

            Thread.Sleep(300);
        }
        Profiler.EndThreadProfiling();
    }

    #region Chunk Pooling
    public Chunk GetChunk(Vector3 pos)
    {
        if(chunkPool.Count > 0)
        {
            return chunkPool.Dequeue();
        }
        else
        {
            return GenerateChunk(pos, false);
        }
    }

    Chunk GenerateChunk(Vector3 position, bool enqueue = true)
    {
        if(Thread.CurrentThread.ManagedThreadId != mainThreadID)
        {
            chunksNeedCreation.Enqueue(position);
            return null;
        }
        Chunk chunk = new GameObject().AddComponent<Chunk>();
        chunk.transform.parent = transform;
        chunk.chunkPosition = position;
        chunk.Initialize(worldMaterial, position);

        if (enqueue)
        {
            chunk.gameObject.SetActive(false);
            chunkPool.Enqueue(chunk);
        }

        return chunk;
    }

    public bool deactiveChunk(Vector3 position)
    {
        if (activeChunks.ContainsKey(position))
        {
            if (activeChunks.TryRemove(position, out Chunk c))
            {
                c.ClearData();
                chunkPool.Enqueue(c);
                c.gameObject.SetActive(false);
                return true;
            }
            else
                return false;
        
        }

        return false;
    }
    #endregion

    #region MeshData Pooling
    public MeshData GetMeshData()
    {
        if (meshDataPool.Count > 0)
        {
            return meshDataPool.Dequeue();
        }
        else
        {
            return GenerateMeshData(false);
        }
    }

    MeshData GenerateMeshData(bool enqueue = true)
    {

        MeshData meshData = new MeshData();
        meshData.Initialize();

        if (enqueue)
        {
            meshDataPool.Enqueue(meshData);
        }

        return meshData;
    }

    public void ClearAndRequeueMeshData(MeshData data)
    {
        data.ClearArrays();
        meshDataPool.Enqueue(data);
    }

    #endregion

    public static Vector3 positionToChunkCoord(Vector3 pos)
    {
        pos /= WorldSettings.chunkSize;
        pos = math.floor(pos) * WorldSettings.chunkSize;
        pos.y = 0;
        return pos;
    }

    private void OnApplicationQuit()
    {
        killThreads = true;
        checkActiveChunks?.Abort();

        foreach(var c in activeChunks.Keys)
        {
            if(activeChunks.TryRemove(c, out var cont))
            {
                cont.Dispose();
            }
        }

        //Try to force cleanup of editor memory
        #if UNITY_EDITOR
            EditorUtility.UnloadUnusedAssetsImmediate();
            GC.Collect();
        #endif
    }

    public static WorldSettings WorldSettings;
    private static WorldManager _instance;

    public static WorldManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<WorldManager>();
            return _instance;
        }
    }

    public Texture2DArray GenerateTextureArray()
    {

        if (voxelTextures.Length > 0)
        {
            Texture2D tex = voxelTextures[0].texture;
            Texture2DArray texArrayAlbedo = new Texture2DArray(tex.width, tex.height, voxelTextures.Length, tex.format, tex.mipmapCount > 1);
            texArrayAlbedo.anisoLevel = tex.anisoLevel;
            texArrayAlbedo.filterMode = tex.filterMode;
            texArrayAlbedo.wrapMode = tex.wrapMode;

            for (int i = 0; i < voxelTextures.Length; i++)
            {
                Graphics.CopyTexture(voxelTextures[i].texture, 0, 0, texArrayAlbedo, i, 0);
            }

            return texArrayAlbedo;
        }
        Debug.Log("No Textures found while trying to generate Tex2DArray");

        return null;
    }
}

[System.Serializable]
public class WorldSettings
{
    public int chunkSize = 16;
    public int maxHeight = 128;
    public int renderDistance = 32;
    public bool sharedVertices = false;
    public bool useTextures = false;
    public int ChunkCount
    {
        get { return (chunkSize + 3) *( maxHeight+ 1) * (chunkSize+ 3); }
    }
}