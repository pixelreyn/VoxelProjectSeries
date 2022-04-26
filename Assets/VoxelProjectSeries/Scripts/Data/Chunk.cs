using System.Collections;
using System.Collections.Generic;
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

    public void ProcessNoiseForStructs(NoiseBuffer noiseBuffer)
    {

        //Get our voxel data from the noise buffer and requeue it
        noiseBuffer.noiseBuffer.GetData(noiseBuffer.voxelArray.array);

        int[] specialBlockCount = new int[2] { 0, 0 };
        noiseBuffer.countBuffer.GetData(specialBlockCount);

        Vector4[] seedBlocks = new Vector4[specialBlockCount[1]];
        noiseBuffer.specialBlocksBuffer.GetData(seedBlocks, 0, 0, specialBlockCount[1]);

        bool needRegen = false;
        //Ensure that we have a created dictionary for our modified voxels
        if (!WorldManager.Instance.modifiedVoxels.ContainsKey(chunkPosition))// || (WorldManager.Instance.modifiedVoxelCalculated.ContainsKey(containerPosition) && !WorldManager.Instance.modifiedVoxelCalculated[containerPosition]))
        {
            WorldManager.Instance.modifiedVoxels.Add(chunkPosition, new Dictionary<Vector3, Voxel>());
            needRegen = true;
        }


        //If we haven't gone through and processed the special blocks that are seeded in this chunk coord, or if we're just adding the modified voxels to the dictionary, force it to recalculate
        if (needRegen || !processLocalMods)
        {
            //Iterate over specialBlocks and generate the requested structure or block at the location
            for (int id = 0; id < specialBlockCount[1]; id++)
            {
                Vector3 pos = new Vector3(seedBlocks[id].x, seedBlocks[id].y, seedBlocks[id].z);
                switch ((int)seedBlocks[id].w)
                {
                    case 243:
                        StructureManager.SpawnRockAt(pos, this, noiseBuffer.voxelArray);
                        break;
                    case 244:
                        StructureManager.SpawnTreeAt(pos, this, noiseBuffer.voxelArray);
                        break;
                    case 245:
                        StructureManager.SpawnBushAt(pos, this, noiseBuffer.voxelArray);
                        break;
                }
            }
            processLocalMods = true;
        }

        //Kick off the mesh generation process, and process our local modified voxels
        foreach (var kvp in WorldManager.Instance.modifiedVoxels[chunkPosition])
        {

            noiseBuffer.voxelArray[kvp.Key] = kvp.Value;
        }

        if (needProcessBlockTicks)
        {
            foreach (Vector3 v in blockPosToUpdate)
            {
                ActiveVoxelManager.UpdateVoxel(chunkPosition, v, WorldManager.Instance.activeVoxels[chunkPosition][v], ref noiseBuffer);
            }
            blockPosToUpdate.Clear();
            needProcessBlockTicks = false;
        }

        if (WorldManager.Instance.activeVoxels.ContainsKey(chunkPosition))
        {
            foreach (var kvp in WorldManager.Instance.activeVoxels[chunkPosition])
            {
                noiseBuffer.voxelArray[kvp.Key] = kvp.Value;
            }
        }

        //Set our Voxels to the meshBuffer array, and our Generating state to generating so everything knows this chunk is processing it's mesh, and the chunk state back to idle, so we know it needs requeued for other mods
        chunkState = ChunkState.Idle;
        generationState = GeneratingState.Generating;

        MeshBuffer meshBuffer = ComputeManager.Instance.GetMeshBuffer();
        meshBuffer.modifiedNoiseBuffer.SetData(noiseBuffer.voxelArray.array);

        ComputeManager.Instance.ClearAndRequeueBuffer(noiseBuffer);
        ComputeManager.Instance.GenerateVoxelMesh(chunkPosition, meshBuffer);
    }

    public void UploadMesh(MeshBuffer meshBuffer)
    {

        if (meshRenderer == null)
            ConfigureComponents();

        //Get the count of vertices/tris from the shader
        int[] faceCount = new int[3] { 0, 0, 0 };
        meshBuffer.countBuffer.GetData(faceCount);
        MeshData meshData = WorldManager.Instance.GetMeshData();

        meshData.verts = new Vector3[faceCount[0]];
        meshData.colors = new Color[faceCount[0]];
        meshData.norms = new Vector3[faceCount[0]];
        meshData.indices = new int[faceCount[1]];
        meshData.transparentIndices = new int[faceCount[2]];

        //Get all of the meshData from the buffers to local arrays
        meshBuffer.vertexBuffer.GetData(meshData.verts, 0, 0, faceCount[0]); 
        meshBuffer.indexBuffer.GetData(meshData.indices, 0, 0, faceCount[1]);
        meshBuffer.transparentIndexBuffer.GetData(meshData.transparentIndices, 0, 0, faceCount[2]);
        meshBuffer.colorBuffer.GetData(meshData.colors, 0, 0, faceCount[0]);
        if (WorldManager.WorldSettings.sharedVertices)
            meshBuffer.normalBuffer.GetData(meshData.norms, 0, 0, faceCount[0]);

        //Assign the mesh

        if (mesh == null)
            mesh = new Mesh();
        else
            mesh.Clear();

        mesh.subMeshCount = 2;

        mesh.SetVertices(meshData.verts, 0, faceCount[0]);

        if(WorldManager.WorldSettings.sharedVertices)
            mesh.SetNormals(meshData.norms, 0, faceCount[0]);

        mesh.SetTriangles(meshData.indices, 0, faceCount[1], 0);
        mesh.SetTriangles(meshData.transparentIndices, 0, faceCount[2], 1);

        mesh.SetColors(meshData.colors, 0, faceCount[0]);

        if (!WorldManager.WorldSettings.sharedVertices)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;

        WorldManager.Instance.ClearAndRequeueMeshData(meshData);
        ComputeManager.Instance.ClearAndRequeueBuffer(meshBuffer);
        generationState = GeneratingState.Idle;
    }

    private void ConfigureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
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