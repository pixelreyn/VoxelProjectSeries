using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Container : MonoBehaviour
{
    public Vector3 containerPosition;

    public MeshData meshData;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public void Initialize(Material mat, Vector3 position)
    {
        ConfigureComponents();
        meshData = new MeshData();
        meshData.Initialize();
        meshRenderer.sharedMaterial = mat;
        containerPosition = position;
    }

    public void ClearData()
    {
        meshFilter.sharedMesh = null;
        meshCollider.sharedMesh = null;
        meshData.ClearData();
    }

    public void UploadMesh(MeshBuffer meshBuffer)
    {

        if (meshRenderer == null)
            ConfigureComponents();

        //Get the count of vertices/tris from the shader
        int[] faceCount = new int[2] { 0, 0 };
        meshBuffer.countBuffer.GetData(faceCount);

        //Get all of the meshData from the buffers to local arrays
        meshBuffer.vertexBuffer.GetData(meshData.verts, 0, 0, faceCount[0]);
        meshBuffer.indexBuffer.GetData(meshData.indices, 0, 0, faceCount[0]);
        meshBuffer.colorBuffer.GetData(meshData.Color, 0, 0, faceCount[0]);

        //Assign the mesh
        meshData.mesh = new Mesh();
        meshData.mesh.SetVertices(meshData.verts, 0, faceCount[0]);
        meshData.mesh.SetIndices(meshData.indices, 0, faceCount[0], MeshTopology.Triangles, 0);
        meshData.mesh.SetColors(meshData.Color, 0, faceCount[0]);

        meshData.mesh.RecalculateNormals();
        meshData.mesh.RecalculateBounds();
        meshData.mesh.Optimize();
        meshData.mesh.UploadMeshData(true);

        meshFilter.sharedMesh = meshData.mesh;
        meshCollider.sharedMesh = meshData.mesh;

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
        meshData.ClearData();
        meshData.indices = null;
        meshData.verts = null;
        meshData.Color = null;
    }

    public Voxel this[Vector3 index]
    {
        get
        {
            if (WorldManager.Instance.modifiedVoxels.ContainsKey(containerPosition))
            {
                if (WorldManager.Instance.modifiedVoxels[containerPosition].ContainsKey(index))
                {
                    return WorldManager.Instance.modifiedVoxels[containerPosition][index];
                }
                else return new Voxel() { ID = 0 };
            }
            else return new Voxel() { ID = 0 };
        }

        set
        {
            if (!WorldManager.Instance.modifiedVoxels.ContainsKey(containerPosition))
                WorldManager.Instance.modifiedVoxels.TryAdd(containerPosition, new Dictionary<Vector3, Voxel>());
            if (!WorldManager.Instance.modifiedVoxels[containerPosition].ContainsKey(index))
                WorldManager.Instance.modifiedVoxels[containerPosition].Add(index, value);
            else
                WorldManager.Instance.modifiedVoxels[containerPosition][index] = value;
        }
    }

    [System.Serializable]
    public class MeshData
    {
        public int[] indices;
        public Vector3[] verts;
        public Color[] Color;
        public Mesh mesh;

        public int arraySize;

        public void Initialize()
        {
            int maxTris = WorldManager.WorldSettings.containerSize * WorldManager.WorldSettings.maxHeight * WorldManager.WorldSettings.containerSize / 4;
            arraySize = maxTris * 3;
            mesh = new Mesh();

            indices = new int[arraySize];
            verts = new Vector3[arraySize];
            Color = new Color[arraySize];
        }

        public void ClearData()
        {
            //Completely clear the mesh reference to help prevent memory problems
            mesh.Clear();
            Destroy(mesh);
            mesh = null;
        }


    }
}

