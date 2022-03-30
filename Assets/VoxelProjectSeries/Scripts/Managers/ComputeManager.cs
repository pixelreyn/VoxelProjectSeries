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


    static float ColorfTo32(Color32 c)
    {
        if (c.r == 0)
            c.r = 1;
        if (c.g == 0)
            c.g = 1;
        if (c.b == 0)
            c.b = 1;
        if (c.a == 0)
            c.a = 1;
        return (c.r << 24) | (c.g << 16) | (c.b << 8) | (c.a);
    }

    public void Initialize(int count = 18)
    {
        xThreads = WorldManager.WorldSettings.containerSize / 8 + 1;
        yThreads = WorldManager.WorldSettings.maxHeight / 8;
       
        noiseLayersArray = new ComputeBuffer(noiseLayers.Length, 36);
        noiseLayersArray.SetData(noiseLayers);

        noiseShader.SetInt("containerSizeX", WorldManager.WorldSettings.containerSize);
        noiseShader.SetInt("containerSizeY", WorldManager.WorldSettings.maxHeight);

        noiseShader.SetBool("generateCaves", true);
        noiseShader.SetBool("forceFloor", true);

        noiseShader.SetInt("maxHeight", WorldManager.WorldSettings.maxHeight);
        noiseShader.SetInt("oceanHeight", 42);
        noiseShader.SetInt("seed", seed);

        noiseShader.SetBuffer(0, "noiseArray", noiseLayersArray);
        noiseShader.SetInt("noiseCount", noiseLayers.Length);

        VoxelColor32[] converted = new VoxelColor32[WorldManager.Instance.WorldColors.Length];
        int cCount = 0;

        foreach(VoxelColor c in WorldManager.Instance.WorldColors)
        {
            VoxelColor32 b = new VoxelColor32();
            b.color = ColorfTo32(c.color);
            b.smoothness = c.smoothness;
            b.metallic = c.metallic;
            converted[cCount++] = b;
        }

        voxelColorsArray = new ComputeBuffer(converted.Length, 12);
        voxelColorsArray.SetData(converted);

        voxelShader.SetBuffer(0, "voxelColors", voxelColorsArray);
        voxelShader.SetInt("containerSizeX", WorldManager.WorldSettings.containerSize);
        voxelShader.SetInt("containerSizeY", WorldManager.WorldSettings.maxHeight);

        for (int i = 0; i < count; i++)
        {
            CreateNewNoiseBuffer();
            CreateNewMeshBuffer();
        }
    }

    public void GenerateVoxelData(Container cont, Vector3 pos)
    {

        NoiseBuffer noiseBuffer = GetNoiseBuffer();
        noiseBuffer.countBuffer.SetCounterValue(0);
        noiseBuffer.countBuffer.SetData(new uint[] { 0 });
        noiseShader.SetBuffer(0, "voxelArray", noiseBuffer.noiseBuffer);
        noiseShader.SetBuffer(0, "count", noiseBuffer.countBuffer);

        noiseShader.SetVector("chunkPosition", cont.containerPosition);
        noiseShader.SetVector("seedOffset", Vector3.zero);

        noiseShader.Dispatch(0, xThreads, yThreads, xThreads);

        MeshBuffer meshBuffer = GetMeshBuffer();
        meshBuffer.countBuffer.SetCounterValue(0);
        meshBuffer.countBuffer.SetData(new uint[] { 0, 0 });
        voxelShader.SetVector("chunkPosition", cont.containerPosition);

        voxelShader.SetBuffer(0, "voxelArray", noiseBuffer.noiseBuffer);
        voxelShader.SetBuffer(0, "counter", meshBuffer.countBuffer);
        voxelShader.SetBuffer(0, "vertexBuffer", meshBuffer.vertexBuffer);
        voxelShader.SetBuffer(0, "colorBuffer", meshBuffer.colorBuffer);
        voxelShader.SetBuffer(0, "indexBuffer", meshBuffer.indexBuffer);
        voxelShader.Dispatch(0, xThreads, yThreads, xThreads);

        AsyncGPUReadback.Request(meshBuffer.countBuffer, (callback) =>
        {
            if (WorldManager.Instance.activeContainers.ContainsKey(pos))
            {
                WorldManager.Instance.activeContainers[pos].UploadMesh(meshBuffer);
            }
            ClearAndRequeueBuffer(noiseBuffer);
            ClearAndRequeueBuffer(meshBuffer);

        });
    }

    private void ClearVoxelData(NoiseBuffer buffer)
    {
        buffer.countBuffer.SetData(new int[] { 0 });
        noiseShader.SetBuffer(1, "voxelArray", buffer.noiseBuffer);
        noiseShader.Dispatch(1, xThreads, yThreads, xThreads);
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
            Debug.Log("Generate container");
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
        ClearVoxelData(buffer);
        availableNoiseComputeBuffers.Enqueue(buffer);
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

public struct NoiseBuffer
{
    public ComputeBuffer noiseBuffer;
    public ComputeBuffer countBuffer;
    public bool Initialized;
    public bool Cleared;

    public void InitializeBuffer()
    {
        countBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
        countBuffer.SetCounterValue(0);
        countBuffer.SetData(new uint[] { 0 });

        //voxelArray = new IndexedArray<Voxel>();
        noiseBuffer = new ComputeBuffer(WorldManager.WorldSettings.ChunkCount, 4);
        Initialized = true;
    }

    public void Dispose()
    {
        countBuffer?.Dispose();
        noiseBuffer?.Dispose();

        Initialized = false;
    }

}

public struct MeshBuffer
{
    public ComputeBuffer vertexBuffer;
    public ComputeBuffer colorBuffer;
    public ComputeBuffer indexBuffer;
    public ComputeBuffer countBuffer;

    public bool Initialized;
    public bool Cleared;

    public void InitializeBuffer()
    {
        if (Initialized)
            return;

        countBuffer = new ComputeBuffer(2, 4, ComputeBufferType.Counter);
        countBuffer.SetCounterValue(0);
        countBuffer.SetData(new uint[] { 0, 0 });

        int maxTris = WorldManager.WorldSettings.containerSize * WorldManager.WorldSettings.maxHeight * WorldManager.WorldSettings.containerSize / 4;
        //width*height*width*faces*tris

        vertexBuffer ??= new ComputeBuffer(maxTris*3, 12);
        colorBuffer ??= new ComputeBuffer(maxTris*3, 16);;
        indexBuffer ??= new ComputeBuffer(maxTris*3, 12);

        Initialized = true;
    }

    public void Dispose()
    {
        vertexBuffer?.Dispose();
        colorBuffer?.Dispose();
        indexBuffer?.Dispose();
        countBuffer?.Dispose();

        Initialized = false;

    }
}