using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

public abstract class World : MonoBehaviour
{
    public Material[] worldMaterials;
    public Voxels[] voxelDetails;
    public WorldSettings worldSettings;

    public ComputeShader contouringShader;
    public ComputeShader densityGenerationShader;
    public int maxChunksToProcessPerFrame;

    //This will contain all modified voxels, structures, whatnot for all chunks, and will effectively be our saving mechanism
    public Dictionary<Vector3, Dictionary<float3, Voxel>> modifiedVoxels = new Dictionary<Vector3, Dictionary<float3, Voxel>>();
    public ConcurrentDictionary<Vector3, Chunk> activeChunks = new ConcurrentDictionary<Vector3, Chunk>();
    public Queue<Vector3> chunksNeedRegenerated = new Queue<Vector3>();
    public Queue<MeshData> meshDataPool = new Queue<MeshData>();

    public static WorldSettings WorldSettings;

    Thread tickThread;
    protected bool killThreads = false;
    private void Start()
    {
        if(_instance == null)
            _instance = this;
        else
        {
            if (_instance != this)
                Destroy(this.gameObject);
            return;
        }

        WorldSettings = worldSettings;

        worldMaterials[0].SetTexture("_TextureArray", GenerateTextureArray());

        for (int i = 0; i < maxChunksToProcessPerFrame * 3; i++)
        {
            GenerateMeshData(true);
        }

        tickThread = new Thread(TickLoop);
        tickThread.Priority = System.Threading.ThreadPriority.BelowNormal;
        tickThread.Start();

        foreach (var module in FindObjectsOfType<WorldModule>())
        {
            module.Register();
        }
        
        InitializeShaders();

        onBeforeGeneration?.Invoke();
        OnStart(); 
    }

    public abstract void OnStart();

    public void InitializeShaders()
    {
        GenerationManager.voxelData = densityGenerationShader;
        InitializeDensityShader();
        GenerationManager.Initialize(contouringShader, maxChunksToProcessPerFrame);
    }

    public abstract void InitializeDensityShader();
    public abstract void ExecuteDensityStage(GenerationBuffer genBuffer, int xThreads, int yThreads);

    public void Update()
    {
        GenerationManager.Tick();
        DoUpdate();
        
        for (int x = 0; x < maxChunksToProcessPerFrame * 2; x++)
        {
            if (x < maxChunksToProcessPerFrame && chunksNeedRegenerated.Count > 0)
            {
                var contToMake = chunksNeedRegenerated.Peek();
                if (activeChunks.ContainsKey(contToMake) && activeChunks[contToMake].generationState == Chunk.GeneratingState.Idle)
                {
                    chunksNeedRegenerated.Dequeue();
                    Chunk chunk = activeChunks[contToMake];
                    chunk.chunkState = Chunk.ChunkState.WaitingToMesh; 
                    GenerationManager.GenerateChunkAt(contToMake);
                }
                else
                {
                    chunksNeedRegenerated.Dequeue();
                    chunksNeedRegenerated.Enqueue(contToMake);
                }
            }
        }
        
    }

    public void TickLoop()
    {
        while (true && !killThreads)
        {
            onTick?.Invoke();
            DoTick();
            Thread.Sleep(WorldSettings.tickTime);
        }
    }

    public virtual void DoTick(){}
    
    public abstract void DoUpdate();

    #region MeshData Pooling
    public MeshData GetMeshData()
    {
        if (meshDataPool.Count > 0)
        {
            return meshDataPool.Dequeue();
        }
        else
        {
            Debug.Log("New MeshData");
            return GenerateMeshData(false);
        }
    }

    protected MeshData GenerateMeshData(bool enqueue = true)
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

    #region Voxel Data
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

    void PlaceWithinChunk(Vector3 chunkPosition, Vector3 localPos, Voxel value)
    {
        bool isActive = value.ID == 240 && value.ActiveValue > 15;
        if (isActive && ActiveVoxelModule.Exists)
        {
            if (!ActiveVoxelModule.activeVoxels.ContainsKey(chunkPosition))
                ActiveVoxelModule.activeVoxels.TryAdd(chunkPosition, new ConcurrentDictionary<Vector3, Voxel>());
            if (!ActiveVoxelModule.activeVoxels[chunkPosition].ContainsKey(localPos))
                ActiveVoxelModule.activeVoxels[chunkPosition].TryAdd(localPos, value);
            else
                ActiveVoxelModule.activeVoxels[chunkPosition][localPos] = value;
        }
        else
        {
            if (!modifiedVoxels.ContainsKey(chunkPosition))
                modifiedVoxels.Add(chunkPosition, new Dictionary<float3, Voxel>());
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
    
    public bool GetVoxelAtCoord(Vector3 chunkPosition, Vector3 voxelPosition, out Voxel value)
    {
        //Get the neighbor coords
        var neighbors = GetNeighborCoordsFromChunkVoxel(chunkPosition, voxelPosition);
        bool canExistWithinChunk = CanExistWithinChunk(voxelPosition);

        bool voxelExists = false;
        value = new Voxel();
        //Check separate module dicts before modified
        if (ActiveVoxelModule.Exists)
        {
            if (canExistWithinChunk)
            {
                if (activeChunks.ContainsKey(chunkPosition) && ActiveVoxelModule.activeVoxels.ContainsKey(chunkPosition) && ActiveVoxelModule.activeVoxels[chunkPosition].ContainsKey(voxelPosition))
                {
                    voxelExists = true;
                    value = ActiveVoxelModule.activeVoxels[chunkPosition][voxelPosition];
                }
            }

            for (int i = 0; i < neighbors.Item2; i++)
            {
                Vector3 x = convertOverageVectorIntoLocal(voxelPosition, neighbors.Item1[i] - chunkPosition);
                if (x.x < 0 || x.z < 0 || x.x > WorldSettings.chunkSize + 4 || x.z > WorldSettings.chunkSize + 4)// == new Vector3(-32,0,-32))
                {
                    continue;
                }
                if (!activeChunks.ContainsKey(neighbors.Item1[i]))
                    continue;
                if (!ActiveVoxelModule.activeVoxels.ContainsKey(neighbors.Item1[i]))
                    continue;
                if (!ActiveVoxelModule.activeVoxels[neighbors.Item1[i]].ContainsKey(x))
                    continue;
                voxelExists = true;
                value = ActiveVoxelModule.activeVoxels[neighbors.Item1[i]][x];
                break;
            }
        }

        if (voxelExists)
            return voxelExists;

        if (canExistWithinChunk)
        {
            if (activeChunks.ContainsKey(chunkPosition) && modifiedVoxels.ContainsKey(chunkPosition) && modifiedVoxels[chunkPosition].ContainsKey(voxelPosition))
            {
                voxelExists = true;
                value = modifiedVoxels[chunkPosition][voxelPosition];
            }
        }

        for (int i = 0; i < neighbors.Item2; i++)
        {
            Vector3 x = convertOverageVectorIntoLocal(voxelPosition, neighbors.Item1[i] - chunkPosition);
            if (x.x < 0 || x.z < 0 || x.x > WorldSettings.chunkSize + 4 || x.z > WorldSettings.chunkSize + 4)// == new Vector3(-32,0,-32))
            {
                continue;
            }
            if (!activeChunks.ContainsKey(neighbors.Item1[i]))
                continue;
            if (!modifiedVoxels.ContainsKey(neighbors.Item1[i]))
                continue;
            if (!modifiedVoxels[neighbors.Item1[i]].ContainsKey(x))
                continue;
            voxelExists = true;
            value = modifiedVoxels[neighbors.Item1[i]][x];
            break;
        }

        return voxelExists;

    }

    (Vector3[], int) GetNeighborCoordsFromChunkVoxel(Vector3 chunkPosition, Vector3 voxelPosition)
    {

        Vector3[] neighborPos = new Vector3[3];
        int neighborCount = 0;
        int2 chunkRange = new int2(4, WorldSettings.chunkSize -1);
        float2 ind2 = new float2(voxelPosition.x, voxelPosition.z);

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
                        neighborPos[neighborCount++] = chunkPosition + (Vector3)mod;
                    }
                    continue;
                }

                if (voxelPosition[i] < chunkRange[0] || voxelPosition[i] > chunkRange[1])
                {
                    mod[i] = voxelPosition[i] < chunkRange[0] ? -1 : 1;
                    mod *= WorldSettings.chunkSize;
                    neighborPos[neighborCount++] = chunkPosition + (Vector3)mod;
                }

            }
        }
        if (math.all(ind2 < chunkRange[0]))
        {
            neighborPos[neighborCount++] = chunkPosition + Vector3.back * WorldSettings.chunkSize + Vector3.left * WorldSettings.chunkSize;
        }
        if (math.all(ind2 > chunkRange[1]))
        {
            neighborPos[neighborCount++] = chunkPosition + Vector3.forward * WorldSettings.chunkSize + Vector3.right * WorldSettings.chunkSize;
        }

        return (neighborPos, neighborCount);
    }

    bool CanExistWithinChunk(Vector3 voxelPosition)
    {
        float2 ind2 = new float2(voxelPosition.x, voxelPosition.z);
        if (math.any(ind2 < 0) || math.any(ind2 > WorldSettings.chunkSize + 3))
            return false;
        return true;
    }

    public void SetVoxelAtCoord(Vector3 chunkPosition, Vector3 voxelPosition, Voxel value)
    {

        bool canPlaceWithinChunk = CanExistWithinChunk(voxelPosition);

        var neighbors = GetNeighborCoordsFromChunkVoxel(chunkPosition, voxelPosition);


        for (int i = 0; i < neighbors.Item2; i++)
        {
            Vector3 x = convertOverageVectorIntoLocal(voxelPosition, neighbors.Item1[i] - chunkPosition);
            if (x.x < 0 || x.z < 0 || x.x > WorldSettings.chunkSize + 4 || x.z > WorldSettings.chunkSize + 4)// == new Vector3(-32,0,-32))
            {
                continue;
            }
            PlaceWithinChunk(neighbors.Item1[i], x, value);
        }

        if (canPlaceWithinChunk)
        {
            PlaceWithinChunk(chunkPosition, voxelPosition, value);
        }

    }
    #endregion

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

    private void OnApplicationQuit()
    {
        ShutDown();
    }

    private void OnDestroy()
    {
        ShutDown();
    }

    private void ShutDown()
    {
        GenerationManager.Shutdown();
        onShutdown?.Invoke();
        tickThread?.Abort();
    }

    private static World _instance;
    public static World Instance
    {
        get
        {
            _instance ??= FindObjectOfType<World>();
            
            return _instance;
        }
    }
    public delegate void BeforeGeneration();
    public static BeforeGeneration onBeforeGeneration;
    public delegate void BeforeMeshing(params object[] info);
    public static BeforeMeshing onBeforeMesh;
    public delegate void GenerationComplete();
    public static GenerationComplete onGenerationComplete;
    public delegate void Tick();
    public static Tick onTick;
    public delegate void Shutdown();
    public static Shutdown onShutdown;
}

[System.Serializable]
public class WorldSettings
{
    public int tickTime = 300;
    public int chunkSize = 16;
    public int maxHeight = 128;
    public int renderDistance = 32;
    public bool smoothNormals = false;
    public bool useTextures = false;
    public bool debug = false;

    public int ChunkCount
    {
        get { return (chunkSize + 5) *(maxHeight+ 1) * (chunkSize+ 5); }
    }
}