using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public Vector3 chunkPosition;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private Mesh mesh;

    public bool processLocalMods = false;
    public ChunkState chunkState = ChunkState.Idle;
    public GeneratingState generationState = GeneratingState.Idle;

    public bool needProcessBlockTicks = false;
    public List<Vector3> blockPosToUpdate = new List<Vector3>();

    public GenerationBuffer generationBuffer;
    public void Initialize(Material[] mats, Vector3 position)
    {
        ConfigureComponents();
        meshRenderer.sharedMaterials = mats;
        chunkPosition = position;
    }

    public void ClearData()
    {
        meshFilter.sharedMesh = null;
        meshCollider.sharedMesh = null;
        chunkState = ChunkState.Idle;
        generationState = GeneratingState.Idle;
        processLocalMods = false;

        if (mesh != null)
        {
            mesh.Clear();
            Destroy(mesh);
            mesh = null;
        }
    }

    public void ProcessNoiseForStructs(GenerationBuffer noiseBuffer)
    {
        generationBuffer = noiseBuffer;
        //Get our voxel data from the noise buffer and requeue it
        //ComputeManager.Instance.sw1.Start();
        generationBuffer.noiseBuffer.GetData(generationBuffer.voxelArray.array);

        int[] specialBlockCount = new int[5] { 0, 0, 0, 0, 0 };
        generationBuffer.countBuffer.GetData(specialBlockCount);

        Vector4[] seedBlocks = new Vector4[specialBlockCount[1]];
        generationBuffer.specialBlocksBuffer.GetData(seedBlocks, 0, 0, specialBlockCount[1]);
        
        //Ensure that we have a created dictionary for our modified voxels
        if (!World.Instance.modifiedVoxels.ContainsKey(chunkPosition))
        {
            World.Instance.modifiedVoxels.Add(chunkPosition, new Dictionary<float3, Voxel>());
        }

        World.onBeforeMesh?.Invoke(this, specialBlockCount[1], seedBlocks);
        processLocalMods = true;
        
        //Ensure modified voxels are added to the noiseBuffer to be meshed, must be done before active voxels are calculated so that the data exists
        foreach (var kvp in World.Instance.modifiedVoxels[chunkPosition])
        {
            noiseBuffer.voxelArray[kvp.Key] = kvp.Value;
        }

        //If something needs ticked from a module
        if (needProcessBlockTicks)
        {
            if (ActiveVoxelModule.Exists)
            {
                foreach (Vector3 v in blockPosToUpdate)
                {
                    ActiveVoxelModule.UpdateVoxel(chunkPosition, v, ActiveVoxelModule.activeVoxels[chunkPosition][v], ref noiseBuffer);
                }
            }

            blockPosToUpdate.Clear();
            needProcessBlockTicks = false;
        }

        //Add any active voxels that were updated or generated to the noisebuffer to be generated;
        if (ActiveVoxelModule.Exists && ActiveVoxelModule.activeVoxels.ContainsKey(chunkPosition))
        {
            foreach (var kvp in ActiveVoxelModule.activeVoxels[chunkPosition])
            {
                generationBuffer.voxelArray[kvp.Key] = kvp.Value;
            }
        }

        generationBuffer.noiseBuffer.SetData(generationBuffer.voxelArray.array);


        //Set our Voxels to the meshBuffer array, and our Generating state to generating so everything knows this chunk is processing it's mesh, and the chunk state back to idle, so we know it needs requeued for other mods
        chunkState = ChunkState.Idle;
        generationState = GeneratingState.Generating;
    }

    public void UploadMesh(GenerationBuffer meshBuffer)
    {

        if (meshRenderer is null)
            ConfigureComponents();

        //Get the count of vertices/tris from the shader
        int[] faceCount = new int[5] { 0, 0, 0, 0, 0 };
        meshBuffer.countBuffer.GetData(faceCount);
        MeshData meshData = World.Instance.GetMeshData();

        meshData.verts = new Vector3[faceCount[2]];
        meshData.colors = new Color[faceCount[2]];
        meshData.norms = new Vector3[faceCount[2]];
        meshData.indices = new int[faceCount[3]];
        meshData.transparentIndices = new int[faceCount[4]];

        //Get all of the meshData from the buffers to local arrays
        meshBuffer.vertexBuffer.GetData(meshData.verts, 0, 0, faceCount[2]);
        meshBuffer.indexBuffer.GetData(meshData.indices, 0, 0, faceCount[3]);
        meshBuffer.transparentIndexBuffer.GetData(meshData.transparentIndices, 0, 0, faceCount[4]);
        meshBuffer.colorBuffer.GetData(meshData.colors, 0, 0, faceCount[2]);
        if (World.WorldSettings.smoothNormals)
            meshBuffer.normalBuffer.GetData(meshData.norms, 0, 0, faceCount[2]);
        //Assign the mesh

        if (mesh is null)
            mesh = new Mesh();
        else
            mesh.Clear();

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.subMeshCount = 2;

        mesh.SetVertices(meshData.verts, 0, faceCount[2]);

        if (World.WorldSettings.smoothNormals)
            mesh.SetNormals(meshData.norms, 0, faceCount[2]);

        mesh.SetTriangles(meshData.indices, 0, faceCount[3], 0);
        mesh.SetTriangles(meshData.transparentIndices, 0, faceCount[4], 1);

        mesh.SetColors(meshData.colors, 0, faceCount[2]);

        if (!World.WorldSettings.smoothNormals)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;

        World.Instance.ClearAndRequeueMeshData(meshData);
        GenerationManager.RequeueBuffer(meshBuffer);
        generationState = GeneratingState.Idle;
    }

    private void ConfigureComponents()
    {
        meshFilter ??= GetComponent<MeshFilter>();
        meshRenderer ??= GetComponent<MeshRenderer>();
        meshCollider ??= GetComponent<MeshCollider>();
    }

    public void Dispose()
    {
        if (mesh != null)
        {
            mesh.Clear();
            Destroy(mesh);
            mesh = null;
        }
    }

    public enum ChunkState
    {
        Idle,
        WaitingToMesh,
    }

    public enum GeneratingState
    {
        Idle,
        Generating
    }

}

[System.Serializable]
public class MeshData
{
    public int[] indices;
    public int[] transparentIndices;
    public Vector3[] verts;
    public Vector3[] norms;
    public Color[] colors;
    public Mesh mesh;

    public void ClearArrays()
    {
        transparentIndices = null;
        indices = null;
        verts = null;
        norms = null;
        colors = null;
    }


}