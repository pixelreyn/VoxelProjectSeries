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
    public void Initialize(Material mat, Vector3 position)
    {
        ConfigureComponents();
        meshRenderer.sharedMaterial = mat;
        chunkPosition = position;
    }

    public void ClearData()
    {
        meshFilter.sharedMesh = null;
        meshCollider.sharedMesh = null;

        mesh.Clear();
        Destroy(mesh);
        mesh = null;
    }

    public void UploadMesh(MeshBuffer meshBuffer)
    {

        if (meshRenderer == null)
            ConfigureComponents();

        //Get the count of vertices/tris from the shader
        int[] faceCount = new int[2] { 0, 0 };
        meshBuffer.countBuffer.GetData(faceCount);
        MeshData meshData = WorldManager.Instance.GetMeshData();

        meshData.verts = new Vector3[faceCount[0]];
        meshData.colors = new Color[faceCount[0]];
        meshData.norms = new Vector3[faceCount[0]];
        meshData.indices = new int[faceCount[1]];
        //Get all of the meshData from the buffers to local arrays
        meshBuffer.vertexBuffer.GetData(meshData.verts, 0, 0, faceCount[0]);
        meshBuffer.indexBuffer.GetData(meshData.indices, 0, 0, faceCount[1]);
        meshBuffer.colorBuffer.GetData(meshData.colors, 0, 0, faceCount[0]);
        if (WorldManager.WorldSettings.sharedVertices)
            meshBuffer.normalBuffer.GetData(meshData.norms, 0, 0, faceCount[0]);

        //Assign the mesh
        mesh = new Mesh();
        mesh.SetVertices(meshData.verts, 0, faceCount[0]);

        if(WorldManager.WorldSettings.sharedVertices)
            mesh.SetNormals(meshData.norms, 0, faceCount[0]);

        mesh.SetIndices(meshData.indices, 0, faceCount[1], MeshTopology.Triangles, 0);
        mesh.SetColors(meshData.colors, 0, faceCount[0]);

        if (!WorldManager.WorldSettings.sharedVertices)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();
        mesh.Optimize();
        mesh.UploadMeshData(true);

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;

        WorldManager.Instance.ClearAndRequeueMeshData(meshData);
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
    }

    private void ConfigureComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    public void Dispose()
    {
        mesh.Clear();
        Destroy(mesh);
        mesh = null;
    }

    public Voxel this[Vector3 index]
    {
        get
        {
            if (WorldManager.Instance.modifiedVoxels.ContainsKey(chunkPosition))
            {
                if (WorldManager.Instance.modifiedVoxels[chunkPosition].ContainsKey(index))
                {
                    return WorldManager.Instance.modifiedVoxels[chunkPosition][index];
                }
                else return new Voxel() { ID = 0 };
            }
            else return new Voxel() { ID = 0 };
        }

        set
        {
            if (!WorldManager.Instance.modifiedVoxels.ContainsKey(chunkPosition))
                WorldManager.Instance.modifiedVoxels.TryAdd(chunkPosition, new Dictionary<Vector3, Voxel>());
            if (!WorldManager.Instance.modifiedVoxels[chunkPosition].ContainsKey(index))
                WorldManager.Instance.modifiedVoxels[chunkPosition].Add(index, value);
            else
                WorldManager.Instance.modifiedVoxels[chunkPosition][index] = value;
        }
    }

    
}

[System.Serializable]
public class MeshData
{
    public int[] indices;
    public Vector3[] verts;
    public Vector3[] norms;
    public Color[] colors;
    public Mesh mesh;

    public int arraySize;

    public void Initialize()
    {
        int maxTris = WorldManager.WorldSettings.chunkSize * WorldManager.WorldSettings.maxHeight * WorldManager.WorldSettings.chunkSize / 4;
        arraySize = maxTris * 3;
    }
    public void ClearArrays()
    {
        indices = null;
        verts = null;
        norms = null;
        colors = null;
    }


}