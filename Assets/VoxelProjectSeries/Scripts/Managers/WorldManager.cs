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
    public Material[] worldMaterials;
    public Voxels[] voxelDetails;
    public WorldSettings worldSettings;

    public Transform mainCamera;
    private Vector3 lastUpdatedPosition;
    private Vector3 previouslyCheckedPosition;

    //This will contain all modified voxels, structures, whatnot for all chunks, and will effectively be our saving mechanism
    public Dictionary<Vector3, Dictionary<Vector3, Voxel>> modifiedVoxels = new Dictionary<Vector3, Dictionary<Vector3, Voxel>>();
    public ConcurrentDictionary<Vector3, ConcurrentDictionary<Vector3, Voxel>> activeVoxels = new ConcurrentDictionary<Vector3, ConcurrentDictionary<Vector3, Voxel>>();
    public ConcurrentDictionary<Vector3, Chunk> activeChunks;
    public Queue<Chunk> chunkPool;
    public Queue<MeshData> meshDataPool; 
    public Queue<Vector3> chunksNeedRegenerated = new Queue<Vector3>();
    private List<MeshData> allMeshData;
    ConcurrentQueue<Vector3> chunksNeedCreation = new ConcurrentQueue<Vector3>();
    ConcurrentQueue<Vector3> deactiveChunks = new ConcurrentQueue<Vector3>();

    public int maxChunksToProcessPerFrame = 6;
    int mainThreadID;
    Thread checkActiveVoxels;
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

        StructureManager.IntializeRandom(ComputeManager.Instance.seed);

        worldMaterials[0].SetTexture("_TextureArray", GenerateTextureArray());

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
        
        checkActiveVoxels = new Thread(CheckActiveVoxelsLoop);
        checkActiveVoxels.Priority = System.Threading.ThreadPriority.BelowNormal;
        checkActiveVoxels.Start();

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
                chunk.chunkState = Chunk.ChunkState.WaitingToMesh;
                activeChunks.TryAdd(contToMake, chunk);
                ComputeManager.Instance.GenerateVoxelData(chunk, contToMake);
                x++;
            }

        }

        for (int x = 0; x < maxChunksToProcessPerFrame; x++)
        {
            if (x < maxChunksToProcessPerFrame && chunksNeedRegenerated.Count > 0)
            {
                contToMake = chunksNeedRegenerated.Peek();
                if (activeChunks.ContainsKey(contToMake) && activeChunks[contToMake].generationState == Chunk.GeneratingState.Idle)
                {
                    chunksNeedRegenerated.Dequeue();
                    Chunk chunk = activeChunks[contToMake];
                    chunk.chunkState = Chunk.ChunkState.WaitingToMesh;
                    ComputeManager.Instance.GenerateVoxelData(chunk, contToMake);
                }
                else
                {
                    chunksNeedRegenerated.Dequeue();
                    chunksNeedRegenerated.Enqueue(contToMake);
                }
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

    void CheckActiveVoxelsLoop()
    {
        while (true && !killThreads)
        {
            foreach (var kvp in activeVoxels)
            {
                if (activeChunks.ContainsKey(kvp.Key))
                {
                    Chunk c = activeChunks[kvp.Key];
                    if (!c.needProcessBlockTicks)
                    {
                        List<Vector3> toRemove = new List<Vector3>();
                        foreach (var activeVoxelKVP in kvp.Value)
                        {
                            if (activeVoxelKVP.Value.ActiveValue < 15)
                            {
                                toRemove.Add(activeVoxelKVP.Key);
                                continue;
                            }

                            c.blockPosToUpdate.Add(activeVoxelKVP.Key);
                        }
                        if (c.blockPosToUpdate.Count > 0)
                        {
                            c.needProcessBlockTicks = true;
                            if (c.chunkState != Chunk.ChunkState.WaitingToMesh)
                            {
                                c.chunkState = Chunk.ChunkState.WaitingToMesh;
                                chunksNeedRegenerated.Enqueue(c.chunkPosition);
                            }
                        }
                        foreach (Vector3 v in toRemove)
                            kvp.Value.TryRemove(v, out var voxel);
                    }
                }
            }
            Thread.Sleep(300);
        }
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
        chunk.Initialize(worldMaterials, position);

        if (enqueue)
        {
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

    private void OnApplicationQuit()
    {
        killThreads = true;
        checkActiveChunks?.Abort();
        checkActiveVoxels?.Abort();

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
        bool initializedTexArray = false;
        Texture2DArray texArrayAlbedo = null;
        if (voxelDetails.Length > 0)
        {
            for (int i = 0; i < voxelDetails.Length; i++)
            {
                if (!initializedTexArray && voxelDetails[i].texture != null)
                {
                    Texture2D tex = voxelDetails[i].texture;
                    texArrayAlbedo = new Texture2DArray(tex.width, tex.height, voxelDetails.Length, tex.format, tex.mipmapCount > 1);
                    texArrayAlbedo.anisoLevel = tex.anisoLevel;
                    texArrayAlbedo.filterMode = tex.filterMode;
                    texArrayAlbedo.wrapMode = tex.wrapMode;
                    initializedTexArray = true;
                }
                if (voxelDetails[i].texture != null)
                    Graphics.CopyTexture(voxelDetails[i].texture, 0, 0, texArrayAlbedo, i, 0);
            }

            return texArrayAlbedo;
        }
        Debug.Log("No Textures found while trying to generate Tex2DArray");

        return null;
    }

    public static Vector3 positionToChunkCoord(Vector3 pos)
    {
        pos /= WorldSettings.chunkSize;
        pos = math.floor(pos) * WorldSettings.chunkSize;
        pos.y = 0;
        return pos;
    }

    Vector3 convertOverageVectorIntoLocal(Vector3 pos, Vector3 direction)
    {
        for (int i = 0; i < 3; i++)
        {
            if (i == 1)
                continue;

            if (direction[i] < 0)
                pos[i] = (WorldSettings.chunkSize) + pos[i];
            if (direction[i] > 0)
                pos[i] = pos[i] - (WorldSettings.chunkSize);
        }

        return pos;
    }

    public void SetVoxelAtCoord(Vector3 chunkPosition, Vector3 index, Voxel value)
    {
        bool canPlaceWithinChunk = true;
        Vector3[] neighborPos = new Vector3[3];
        int neighbor = 0;
        int2 chunkRange = new int2(4, WorldSettings.chunkSize - 1);
        float2 ind2 = new float2(index.x, index.z);

        if (math.any(ind2 < 0) || math.any(ind2 > WorldSettings.chunkSize + 3))
            canPlaceWithinChunk = false;

        if (math.any(ind2 < chunkRange[0]) || math.any(ind2 > chunkRange[1]))
        {
            for (int i = 0; i < 3; i++)
            {
                float3 mod = new float3(0, 0, 0);
                if (i == 1)
                {
                    if (math.any(ind2 < chunkRange[0]) && math.any(ind2 > chunkRange[1]))
                    {
                        mod[0] = ind2[0] < chunkRange[0] ? -1 : 1;
                        mod[2] = ind2[1] < chunkRange[0] ? -1 : 1;
                        mod *= WorldSettings.chunkSize;
                        neighborPos[neighbor++] = chunkPosition + (Vector3)mod;
                    }
                    continue;
                }

                if (index[i] < chunkRange[0] || index[i] > chunkRange[1])
                {
                    mod[i] = index[i] < chunkRange[0] ? -1 : 1;
                    mod *= WorldSettings.chunkSize;
                    neighborPos[neighbor++] = chunkPosition + (Vector3)mod;
                }

            }
        }
        if(math.all(ind2 < chunkRange[0]))
        {
            neighborPos[neighbor++] = chunkPosition + Vector3.back * WorldSettings.chunkSize + Vector3.left * WorldSettings.chunkSize;
        }
        if (math.all(ind2 > chunkRange[1]))
        {
            neighborPos[neighbor++] = chunkPosition + Vector3.forward * WorldSettings.chunkSize + Vector3.right * WorldSettings.chunkSize;
        }

        for (int i = 0; i < neighbor; i++) {
            Vector3 x = convertOverageVectorIntoLocal(index, neighborPos[i] - chunkPosition);
            if (x.x < 0 || x.z < 0 || x.x > WorldSettings.chunkSize + 4 || x.z > WorldSettings.chunkSize + 4)// == new Vector3(-32,0,-32))
            {
                continue;
            }
            PlaceWithinChunk(neighborPos[i], x, value);
        }
        
        if (canPlaceWithinChunk)
        {
            PlaceWithinChunk(chunkPosition, index, value);
        }

    }

    void PlaceWithinChunk(Vector3 chunkPosition, Vector3 localPos, Voxel value)
    {
        bool isActive = value.ID == 240 && value.ActiveValue > 15;
        if (isActive)
        {
            if (!activeVoxels.ContainsKey(chunkPosition))
                activeVoxels.TryAdd(chunkPosition, new ConcurrentDictionary<Vector3, Voxel>());
            if (!activeVoxels[chunkPosition].ContainsKey(localPos))
                activeVoxels[chunkPosition].TryAdd(localPos, value);
            else
                activeVoxels[chunkPosition][localPos] = value;
        }
        else
        {
            if (!modifiedVoxels.ContainsKey(chunkPosition))
                modifiedVoxels.Add(chunkPosition, new Dictionary<Vector3, Voxel>());
            if (!modifiedVoxels[chunkPosition].ContainsKey(localPos))
                modifiedVoxels[chunkPosition].Add(localPos, value);
            else
                modifiedVoxels[chunkPosition][localPos] = value;
        }

        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk c = activeChunks[chunkPosition];
            if (c.chunkState != Chunk.ChunkState.WaitingToMesh)
            {
                c.chunkState = Chunk.ChunkState.WaitingToMesh;
                chunksNeedRegenerated.Enqueue(c.chunkPosition);
            }
        }
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
        get { return (chunkSize + 5) *( maxHeight+ 1) * (chunkSize + 5); }
    }
}