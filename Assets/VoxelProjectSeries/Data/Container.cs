using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace PixelReyn.VoxelSeries.Part3
{

    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Container : MonoBehaviour
    {
        public Vector3 containerPosition;

        private Dictionary<Vector3, Voxel> data;
        private MeshData meshData = new MeshData();

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;

        public void Initialize(Material mat, Vector3 position)
        {
            ConfigureComponents();
            data = new Dictionary<Vector3, Voxel>();
            meshRenderer.sharedMaterial = mat;
            containerPosition = position;
        }

        public void ClearData()
        {
            data.Clear();
        }

        public void GenerateMesh()
        {
            meshData.ClearData();

            Vector3 blockPos;
            Voxel block;

            int counter = 0;
            Vector3[] faceVertices = new Vector3[4];
            Vector2[] faceUVs = new Vector2[4];

            VoxelColor voxelColor;
            Color voxelColorAlpha;
            Vector2 voxelSmoothness;

            foreach (KeyValuePair<Vector3, Voxel> kvp in data)
            {
                //Only check on solid blocks
                if (!kvp.Value.isSolid)
                    continue;

                blockPos = kvp.Key;
                block = kvp.Value;

                voxelColor = WorldManager.Instance.WorldColors[block.ID - 1];
                voxelColorAlpha = voxelColor.color;
                voxelColorAlpha.a = 1;
                voxelSmoothness = new Vector2(voxelColor.metallic, voxelColor.smoothness);
                //Iterate over each face direction
                for (int i = 0; i < 6; i++)
                {
                    //Check if there's a solid block against this face
                    if (this[blockPos + voxelFaceChecks[i]].isSolid)
                        continue;

                    //Draw this face

                    //Collect the appropriate vertices from the default vertices and add the block position
                    for (int j = 0; j < 4; j++)
                    {
                        faceVertices[j] = voxelVertices[voxelVertexIndex[i, j]] + blockPos;
                        faceUVs[j] = voxelUVs[j];
                    }

                    for (int j = 0; j < 6; j++)
                    {
                        meshData.vertices.Add(faceVertices[voxelTris[i, j]]);
                        meshData.UVs.Add(faceUVs[voxelTris[i, j]]);
                        meshData.colors.Add(voxelColorAlpha);
                        meshData.UVs2.Add(voxelSmoothness);

                        meshData.triangles.Add(counter++);

                    }
                }

            }
        }



        public void UploadMesh()
        {
            meshData.UploadMesh();

            if (meshRenderer == null)
                ConfigureComponents();

            meshFilter.mesh = meshData.mesh;
            if (meshData.vertices.Count > 3)
                meshCollider.sharedMesh = meshData.mesh;
        }

        private void ConfigureComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
        }

        public Voxel this[Vector3 index]
        {
            get
            {
                if (data.ContainsKey(index))
                    return data[index];
                else
                    return emptyVoxel;
            }

            set
            {
                if (data.ContainsKey(index))
                    data[index] = value;
                else
                    data.Add(index, value);
            }
        }

        public static Voxel emptyVoxel = new Voxel() { ID = 0 };

        #region Mesh Data

        public struct MeshData
        {
            public Mesh mesh;
            public List<Vector3> vertices;
            public List<int> triangles;
            public List<Vector2> UVs;
            public List<Vector2> UVs2;
            public List<Color> colors;
            public bool Initialized;

            public void ClearData()
            {
                if (!Initialized)
                {
                    vertices = new List<Vector3>();
                    triangles = new List<int>();
                    UVs = new List<Vector2>();
                    UVs2 = new List<Vector2>();
                    colors = new List<Color>();

                    Initialized = true;
                    mesh = new Mesh();
                }
                else
                {
                    vertices.Clear();
                    triangles.Clear();
                    UVs.Clear();
                    UVs2.Clear();
                    colors.Clear();

                    mesh.Clear();
                }
            }
            public void UploadMesh(bool sharedVertices = false)
            {
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0, false);
                mesh.SetColors(colors);

                mesh.SetUVs(0, UVs);
                mesh.SetUVs(2, UVs2);

                mesh.Optimize();

                mesh.RecalculateNormals();

                mesh.RecalculateBounds();

                mesh.UploadMeshData(false);
            }
        }
        #endregion

        #region Static Variables
        static readonly Vector3[] voxelVertices = new Vector3[8]
        {
            new Vector3(0,0,0),//0
            new Vector3(1,0,0),//1
            new Vector3(0,1,0),//2
            new Vector3(1,1,0),//3

            new Vector3(0,0,1),//4
            new Vector3(1,0,1),//5
            new Vector3(0,1,1),//6
            new Vector3(1,1,1),//7
        };

        static readonly Vector3[] voxelFaceChecks = new Vector3[6]
        {
            new Vector3(0,0,-1),//back
            new Vector3(0,0,1),//front
            new Vector3(-1,0,0),//left
            new Vector3(1,0,0),//right
            new Vector3(0,-1,0),//bottom
            new Vector3(0,1,0)//top
        };

        static readonly int[,] voxelVertexIndex = new int[6, 4]
        {
            {0,1,2,3},
            {4,5,6,7},
            {4,0,6,2},
            {5,1,7,3},
            {0,1,4,5},
            {2,3,6,7},
        };

        static readonly Vector2[] voxelUVs = new Vector2[4]
        {
            new Vector2(0,0),
            new Vector2(0,1),
            new Vector2(1,0),
            new Vector2(1,1)
        };

        static readonly int[,] voxelTris = new int[6, 6]
        {
            {0,2,3,0,3,1},
            {0,1,2,1,3,2},
            {0,2,3,0,3,1},
            {0,1,2,1,3,2},
            {0,1,2,1,3,2},
            {0,2,3,0,3,1},
        };
        #endregion
    }

}