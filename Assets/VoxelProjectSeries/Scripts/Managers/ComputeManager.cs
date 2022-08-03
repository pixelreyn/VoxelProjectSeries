using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ComputeManager : MonoBehaviour
{
    public ComputeShader noiseShader;
    public ComputeShader voxelShader;

    private List<MeshBuffer> allMeshComputeBuffers = new List<MeshBuffer>();
    private Queue<MeshBuffer> availableMeshComputeBuffers = new Queue<MeshBuffer>();

    private List<NoiseBuffer> allNoiseComputeBuffers = new List<NoiseBuffer>();
    private Queue<NoiseBuffer> availableNoiseComputeBuffers = new Queue<NoiseBuffer>();

    ComputeBuffer noiseLayersArray;
    ComputeBuffer voxelColorsArray;

    private int xThreads;
    private int yThreads;
    public int numberMeshBuffers = 0;

    [Header("Noise Settings")]
    public int seed;
    public NoiseLayers[] noiseLayers;

    public void Initialize(int count = 18)
    {
        xThreads = WorldManager.WorldSettings.chunkSize / 8 + 1;
        yThreads = WorldManager.WorldSettings.maxHeight / 8;
       
        noiseLayersArray = new ComputeBuffer(noiseLayers.Length, 36);
        noiseLayersArray.SetData(noiseLayers);

        noiseShader.SetInt("chunkSizeX", WorldManager.WorldSettings.chunkSize);
        noiseShader.SetInt("chunkSizeY", WorldManager.WorldSettings.maxHeight);

        noiseShader.SetBool("generateCaves", true);
        noiseShader.SetBool("forceFloor", true);

        noiseShader.SetInt("maxHeight", WorldManager.WorldSettings.maxHeight);
        noiseShader.SetInt("oceanHeight", 42);
        noiseShader.SetInt("seed", seed);

        noiseShader.SetBuffer(0, "noiseArray", noiseLayersArray);
        noiseShader.SetInt("noiseCount", noiseLayers.Length);

        VoxelDetails[] convertedVoxelDetails = getVoxelDetails();

        voxelColorsArray = new ComputeBuffer(convertedVoxelDetails.Length, 12);
        voxelColorsArray.SetData(convertedVoxelDetails);

        voxelShader.SetBuffer(2, "voxelColors", voxelColorsArray);
        voxelShader.SetInt("chunkSizeX", WorldManager.WorldSettings.chunkSize);
        voxelShader.SetInt("chunkSizeY", WorldManager.WorldSettings.maxHeight);
        voxelShader.SetBool("sharedVertices", WorldManager.WorldSettings.sharedVertices);
        voxelShader.SetBool("useTextures", WorldManager.WorldSettings.useTextures);
        for (int i = 0; i < count; i++)
        {
            CreateNewNoiseBuffer();
            CreateNewMeshBuffer();
        }
    }

    public void GenerateVoxelData(Chunk cont, Vector3 pos)
    {

        NoiseBuffer noiseBuffer = GetNoiseBuffer();
        noiseBuffer.countBuffer.SetCounterValue(0);
        noiseBuffer.countBuffer.SetData(new uint[] { 0, 0 });
        noiseShader.SetBuffer(0, "specialBlocksBuffer", noiseBuffer.specialBlocksBuffer);
        noiseShader.SetBuffer(0, "voxelArray", noiseBuffer.noiseBuffer);
        noiseShader.SetBuffer(0, "count", noiseBuffer.countBuffer);

        noiseShader.SetVector("chunkPosition", cont.chunkPosition);
        noiseShader.SetVector("seedOffset", Vector3.zero);

        noiseShader.Dispatch(0, xThreads, yThreads, xThreads);

        AsyncGPUReadback.Request(noiseBuffer.countBuffer, (callback) =>
        {
            if (WorldManager.Instance.activeChunks.ContainsKey(pos))
            {
                WorldManager.Instance.activeChunks[pos].ProcessNoiseForStructs(noiseBuffer);
            }
            else
            {
                Debug.Log("Noise generated for inactive chunk at: " + pos);
                ClearAndRequeueBuffer(noiseBuffer);
            }

        });
    }

    public void GenerateVoxelMesh(Vector3 pos, MeshBuffer meshBuffer)
    {
        meshBuffer.countBuffer.SetData(new uint[] { 0, 0, 0 });
        voxelShader.SetVector("chunkPosition", pos);
        voxelShader.SetBuffer(0, "voxelArray", meshBuffer.modifiedNoiseBuffer);
        voxelShader.SetBuffer(0, "counter", meshBuffer.countBuffer);
        voxelShader.SetBuffer(0, "cellVertices", meshBuffer.cellVerticesBuffer);
        voxelShader.Dispatch(0, xThreads, yThreads, xThreads);

        voxelShader.SetBuffer(1, "voxelArray", meshBuffer.modifiedNoiseBuffer);
        voxelShader.SetBuffer(1, "cellVertices", meshBuffer.cellVerticesBuffer);
        voxelShader.Dispatch(1, xThreads, yThreads, xThreads);

        voxelShader.SetBuffer(2, "voxelArray", meshBuffer.modifiedNoiseBuffer);
        voxelShader.SetBuffer(2, "counter", meshBuffer.countBuffer);
        voxelShader.SetBuffer(2, "vertexBuffer", meshBuffer.vertexBuffer);
        voxelShader.SetBuffer(2, "normalBuffer", meshBuffer.normalBuffer);
        voxelShader.SetBuffer(2, "cellVertices", meshBuffer.cellVerticesBuffer);
        voxelShader.SetBuffer(2, "colorBuffer", meshBuffer.colorBuffer);
        voxelShader.SetBuffer(2, "indexBuffer", meshBuffer.indexBuffer);
        voxelShader.SetBuffer(2, "transparentIndexBuffer", meshBuffer.transparentIndexBuffer);
        voxelShader.Dispatch(2, xThreads, yThreads, xThreads);

        AsyncGPUReadback.Request(meshBuffer.countBuffer, (callback) =>
        {
            if (WorldManager.Instance.activeChunks.ContainsKey(pos))
            {
                WorldManager.Instance.activeChunks[pos].UploadMesh(meshBuffer);
            }
            else
            {
                Debug.Log("Generated mesh for inactive chunk at: " + pos);
                ClearAndRequeueBuffer(meshBuffer);
            }

        });
    }

    #region MeshBuffer Pooling
    public MeshBuffer GetMeshBuffer()
    {
        if (availableMeshComputeBuffers.Count > 0)
        {
            return availableMeshComputeBuffers.Dequeue();
        }
        else
        {
            Debug.Log("Generate chunk");
            return CreateNewMeshBuffer(false);
        }
    }

    public MeshBuffer CreateNewMeshBuffer(bool enqueue = true)
    {
        MeshBuffer buffer = new MeshBuffer();
        buffer.InitializeBuffer();
        
        allMeshComputeBuffers.Add(buffer);
        
        if (enqueue)
            availableMeshComputeBuffers.Enqueue(buffer);
        
        numberMeshBuffers++;

        return buffer;
    }

    public void ClearAndRequeueBuffer(MeshBuffer buffer)
    {
        availableMeshComputeBuffers.Enqueue(buffer);
    }
    #endregion

    #region NoiseBuffer Pooling
    public NoiseBuffer GetNoiseBuffer()
    {
        if (availableNoiseComputeBuffers.Count > 0)
        {
            return availableNoiseComputeBuffers.Dequeue();
        }
        else
        {
            return CreateNewNoiseBuffer(false);
        }
    }

    public NoiseBuffer CreateNewNoiseBuffer(bool enqueue = true)
    {
        NoiseBuffer buffer = new NoiseBuffer();
        buffer.InitializeBuffer();
        allNoiseComputeBuffers.Add(buffer);

        if (enqueue)
            availableNoiseComputeBuffers.Enqueue(buffer);

        return buffer;
    }

    public void ClearAndRequeueBuffer(NoiseBuffer buffer)
    {
        buffer.countBuffer.SetData(new int[] { 0 });
        noiseShader.SetBuffer(1, "voxelArray", buffer.noiseBuffer);
        noiseShader.Dispatch(1, xThreads, yThreads, xThreads);
        AsyncGPUReadback.Request(buffer.countBuffer, (callback) =>
        {
            availableNoiseComputeBuffers.Enqueue(buffer);
        });
    }
    #endregion

    private void OnApplicationQuit()
    {
        DisposeAllBuffers();
    }

    public void DisposeAllBuffers()
    {
        noiseLayersArray?.Dispose();
        voxelColorsArray?.Dispose();
        foreach (NoiseBuffer buffer in allNoiseComputeBuffers)
            buffer.Dispose();
        foreach (MeshBuffer buffer in allMeshComputeBuffers)
            buffer.Dispose();
    }


    static float ColorfTo32(Color32 c)
    {
        if (c.r == 0)
            c.r = 5;
        if (c.g == 0)
            c.g = 5;
        if (c.b == 0)
            c.b = 5;
        if (c.a == 0)
            c.a = 5;

        return (c.r << 24) | (c.g << 16) | (c.b << 8) | (c.a);
    }

    VoxelDetails[] getVoxelDetails()
    {
        VoxelDetails[] voxelDetails = new VoxelDetails[WorldManager.Instance.voxelDetails.Length];
        int count = 0;
        foreach (Voxels vT in WorldManager.Instance.voxelDetails)
        {
            VoxelDetails vD = new VoxelDetails();
            vD.color = WorldManager.WorldSettings.useTextures && vT.texture != null ? -1 : ColorfTo32(vT.color);
            vD.smoothness = vT.smoothness;
            vD.metallic = vT.metallic;

            voxelDetails[count++] = vD;
        }
        return voxelDetails;
    }


    private static ComputeManager _instance;

    public static ComputeManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<ComputeManager>();
            return _instance;
        }
    }
}

public class NoiseBuffer
{
    public ComputeBuffer noiseBuffer;
    public ComputeBuffer countBuffer;
    public ComputeBuffer specialBlocksBuffer;
    public IndexedArray<Voxel> voxelArray;
    public bool Initialized;
    public bool Cleared;

    public void InitializeBuffer()
    {
        specialBlocksBuffer = new ComputeBuffer(64, 16);
        countBuffer = new ComputeBuffer(2, 4);
        countBuffer.SetData(new uint[] {0, 0});

        voxelArray = new IndexedArray<Voxel>();
        noiseBuffer = new ComputeBuffer(WorldManager.WorldSettings.ChunkCount, 12);
        Initialized = true;
    }

    public void Dispose()
    {
        countBuffer?.Dispose();
        noiseBuffer?.Dispose();
        specialBlocksBuffer?.Dispose();

        Initialized = false;
    }

}

public class MeshBuffer
{
    public ComputeBuffer vertexBuffer;
    public ComputeBuffer cellVerticesBuffer;
    public ComputeBuffer normalBuffer;
    public ComputeBuffer colorBuffer;
    public ComputeBuffer indexBuffer;
    public ComputeBuffer countBuffer;
    public ComputeBuffer modifiedNoiseBuffer;
    public ComputeBuffer transparentIndexBuffer;

    public bool Initialized;
    public bool Cleared;
    public IndexedArray<Voxel> voxelArray;

    public void InitializeBuffer()
    {
        if (Initialized)
            return;

        countBuffer = new ComputeBuffer(3, 4);
        countBuffer.SetData(new uint[] { 0, 0, 0 });

        int maxTris = WorldManager.WorldSettings.chunkSize * WorldManager.WorldSettings.maxHeight * WorldManager.WorldSettings.chunkSize / 3;
        //width*height*width*faces*tris
        int maxVertices = WorldManager.WorldSettings.sharedVertices ? maxTris / 3 : maxTris;
        int maxNormals = WorldManager.WorldSettings.sharedVertices ? maxVertices * 3 : 1;
        vertexBuffer ??= new ComputeBuffer(maxVertices*3, 12);
        cellVerticesBuffer ??= new ComputeBuffer(WorldManager.WorldSettings.ChunkCount, 28);
        colorBuffer ??= new ComputeBuffer(maxVertices*3, 16);
        normalBuffer ??= new ComputeBuffer(maxNormals, 12);
        indexBuffer ??= new ComputeBuffer(maxTris*3, 4); 
        modifiedNoiseBuffer = new ComputeBuffer(WorldManager.WorldSettings.ChunkCount, 12);
        transparentIndexBuffer ??= new ComputeBuffer(maxTris*3, 4);

        Initialized = true;
    }

    public void Dispose()
    {
        vertexBuffer?.Dispose();
        cellVerticesBuffer?.Dispose();
        normalBuffer?.Dispose();
        colorBuffer?.Dispose();
        indexBuffer?.Dispose();
        transparentIndexBuffer?.Dispose();
        countBuffer?.Dispose();
        modifiedNoiseBuffer?.Dispose();

        Initialized = false;

    }
}