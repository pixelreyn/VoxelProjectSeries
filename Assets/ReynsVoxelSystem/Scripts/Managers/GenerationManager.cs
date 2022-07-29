using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public static class GenerationManager 
{
    public static ComputeShader voxelData;
    private static ComputeShader voxelContouring;

    private static ConcurrentBag<GenerationBuffer> generationBuffers = new ConcurrentBag<GenerationBuffer>();
    private static List<GenerationBuffer> allGenerationBuffers = new List<GenerationBuffer>();

    private static ConcurrentQueue<Vector3> needGenerated = new ConcurrentQueue<Vector3> ();
    static ComputeBuffer voxelColorsArray;

    private static int xThreads;
    private static int yThreads;

    static int maxActionsPerFrame = 2;
    static bool asyncCompute = false;
    static int mainThreadID = -1;

    public static void Initialize(ComputeShader contouring, int maxActions = 2, int initialBuffers = 18)
    {
        maxActionsPerFrame = maxActions;

        voxelContouring = contouring;

        xThreads = World.WorldSettings.chunkSize / 8 + 1;
        yThreads = World.WorldSettings.maxHeight / 8;

        VoxelDetails[] convertedVoxelDetails = getVoxelDetails();
        voxelColorsArray = new ComputeBuffer(convertedVoxelDetails.Length, 12);
        voxelColorsArray.SetData(convertedVoxelDetails);

        voxelData.SetInt("chunkSizeX", World.WorldSettings.chunkSize);
        voxelData.SetInt("chunkSizeY", World.WorldSettings.maxHeight);

        voxelContouring.SetInt("chunkSizeX", World.WorldSettings.chunkSize);
        voxelContouring.SetInt("chunkSizeY", World.WorldSettings.maxHeight);
        voxelContouring.SetBool("smoothNormals", World.WorldSettings.smoothNormals);
        voxelContouring.SetBool("useTextures", World.WorldSettings.useTextures);
        voxelContouring.SetBuffer(2, "voxelColors", voxelColorsArray);

        for (int i = 0; i < initialBuffers; i++)
        {
            GenerationBuffer buffer = new GenerationBuffer();
            generationBuffers.Add(buffer);
            allGenerationBuffers.Add(buffer);
        }

        asyncCompute = SystemInfo.supportsAsyncCompute;
        mainThreadID = Thread.CurrentThread.ManagedThreadId;
    }

    public static void EnqueuePosToGenerate(Vector3 chunkPos)
    {
        needGenerated.Enqueue(chunkPos);
    }

    public static void GenerateChunkAt(Vector3 chunkPos)
    {
        if (!asyncCompute && mainThreadID != Thread.CurrentThread.ManagedThreadId) {
            needGenerated.Enqueue(chunkPos);
            return;
        }

        var genBuffer = GetGenerationBuffer();
        genBuffer.countBuffer.SetData(new uint[] { 0, 0, 0, 0, 0 });

        voxelData.SetVector("chunkPosition", chunkPos);
        voxelContouring.SetVector("chunkPosition", chunkPos);

        World.Instance.ExecuteDensityStage(genBuffer, xThreads, yThreads);
        AsyncGPUReadback.Request(genBuffer.heightMap, (callback) =>
        {
            World.Instance.activeChunks[chunkPos].ProcessNoiseForStructs(genBuffer);
            Contour(chunkPos, genBuffer);
        });
    }

    static void Contour(Vector3 chunkPos, GenerationBuffer genBuffer)
    {
        voxelContouring.SetVector("chunkPosition", chunkPos);
        voxelContouring.SetBuffer(0, "voxelArray", genBuffer.noiseBuffer);
        voxelContouring.SetBuffer(0, "count", genBuffer.countBuffer);
        voxelContouring.SetBuffer(0, "cellVertices", genBuffer.cellVerticesBuffer);
        voxelContouring.Dispatch(0, xThreads, yThreads, xThreads);

        voxelContouring.SetBuffer(1, "voxelArray", genBuffer.noiseBuffer);
        voxelContouring.SetBuffer(1, "cellVertices", genBuffer.cellVerticesBuffer);
        voxelContouring.Dispatch(1, xThreads, yThreads, xThreads);

        voxelContouring.SetBuffer(2, "voxelArray", genBuffer.noiseBuffer);
        voxelContouring.SetBuffer(2, "count", genBuffer.countBuffer);
        voxelContouring.SetBuffer(2, "vertexBuffer", genBuffer.vertexBuffer);
        voxelContouring.SetBuffer(2, "normalBuffer", genBuffer.normalBuffer);
        voxelContouring.SetBuffer(2, "cellVertices", genBuffer.cellVerticesBuffer);
        voxelContouring.SetBuffer(2, "colorBuffer", genBuffer.colorBuffer);
        voxelContouring.SetBuffer(2, "indexBuffer", genBuffer.indexBuffer);
        voxelContouring.SetBuffer(2, "transparentIndexBuffer", genBuffer.transparentIndexBuffer);
        voxelContouring.Dispatch(2, xThreads, yThreads, xThreads);

        AsyncGPUReadback.Request(genBuffer.transparentIndexBuffer, (callback) =>
        {
            if (World.Instance.activeChunks.ContainsKey(chunkPos))
            {
                World.Instance.activeChunks[chunkPos].UploadMesh(genBuffer);
            }
            else
            {
                Debug.Log("Generated mesh for inactive chunk at: " + chunkPos);
                RequeueBuffer(genBuffer);
            }

        });
    }

    //To be executed on main thread only
    public static void Tick()
    {
        if (needGenerated.Count > 0)
        {
            for (int i = 0; i < maxActionsPerFrame; i++)
            {
                //Since this would be main thread executed, the false version should just remove the generation from the queue
                if (needGenerated.TryDequeue(out var chunk) && World.Instance.activeChunks.ContainsKey(chunk))
                {
                    GenerateChunkAt(chunk);
                }
            }
        }
    }

    static GenerationBuffer GetGenerationBuffer()
    {
        if (generationBuffers.Count > 0 && generationBuffers.TryTake(out var buffer))
            return buffer;
        else
        {
            Debug.Log("New GenBuffers");
            GenerationBuffer bufferNew = new GenerationBuffer();
            allGenerationBuffers.Add(bufferNew);
            return bufferNew;
        }
    }

    public static void RequeueBuffer(GenerationBuffer ToQueue)
    {
        generationBuffers.Add(ToQueue);
    }

    public static void Shutdown()
    {
        foreach (GenerationBuffer buffer in allGenerationBuffers)
            buffer.Dispose();

        voxelColorsArray.Dispose();
    }

    static int ColorToBits(Color32 c)
    {
        return (c.r << 16) | (c.g << 8) | (c.b << 0);
    }

    static VoxelDetails[] getVoxelDetails()
    {
        VoxelDetails[] voxelDetails = new VoxelDetails[World.Instance.voxelDetails.Length];
        int count = 0;
        foreach (Voxels vT in World.Instance.voxelDetails)
        {
            VoxelDetails vD = new VoxelDetails();
            vD.color = World.WorldSettings.useTextures && vT.texture != null ? -1 : ColorToBits(vT.color);
            vD.smoothness = vT.smoothness;
            vD.metallic = vT.metallic;

            voxelDetails[count++] = vD;
        }
        return voxelDetails;
    }

    public static int getQueuedCount
    {
        get
        {
            return needGenerated.Count;
        }
    }
}

public class GenerationBuffer : IDisposable
{
    public ComputeBuffer noiseBuffer;
    public ComputeBuffer countBuffer;
    public ComputeBuffer heightMap;
    public ComputeBuffer specialBlocksBuffer;

    public ComputeBuffer vertexBuffer;
    public ComputeBuffer normalBuffer;
    public ComputeBuffer cellVerticesBuffer;
    public ComputeBuffer colorBuffer;
    public ComputeBuffer indexBuffer;
    public ComputeBuffer transparentIndexBuffer;

    public IndexedArray<Voxel> voxelArray;

    public GenerationBuffer()
    {
        specialBlocksBuffer = new ComputeBuffer(64, 16, ComputeBufferType.Raw); 
        heightMap = new ComputeBuffer(((World.WorldSettings.chunkSize + 5) * 4) * ((World.WorldSettings.chunkSize + 5) * 4), 8);
        countBuffer = new ComputeBuffer(5, 4, ComputeBufferType.Raw);
        ClearCountBuffer();

        voxelArray = new IndexedArray<Voxel>();
        noiseBuffer = new ComputeBuffer(World.WorldSettings.ChunkCount, 12);

        int maxTris = World.WorldSettings.chunkSize * World.WorldSettings.maxHeight * World.WorldSettings.chunkSize / 3;
        //width*height*width*faces*tris
        int maxVertices = World.WorldSettings.smoothNormals ? maxTris * 2 : maxTris * 4;
        int maxNormals = World.WorldSettings.smoothNormals ? maxVertices : 1;
        vertexBuffer ??= new ComputeBuffer(maxVertices, 12);
        normalBuffer ??= new ComputeBuffer(maxNormals, 12);
        cellVerticesBuffer ??= new ComputeBuffer(World.WorldSettings.ChunkCount, 32);
        colorBuffer ??= new ComputeBuffer(maxVertices, 16);
        indexBuffer ??= new ComputeBuffer(maxTris*3, 4);
        transparentIndexBuffer ??= new ComputeBuffer(maxTris*3, 4);
    }

    public void ClearCountBuffer()
    {
        countBuffer.SetData(new uint[] { 0, 0, 0, 0, 0 });
    }

    public void Dispose()
    {
        noiseBuffer?.Dispose();
        countBuffer?.Dispose();
        heightMap?.Dispose();
        specialBlocksBuffer?.Dispose();

        vertexBuffer?.Dispose();
        normalBuffer?.Dispose();
        cellVerticesBuffer?.Dispose();
        colorBuffer?.Dispose();
        indexBuffer?.Dispose();
        transparentIndexBuffer?.Dispose();
        voxelArray.Clear();
    }
}