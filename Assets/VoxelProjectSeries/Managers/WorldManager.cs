using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelReyn.VoxelSeries.Part3
{
    public class WorldManager : MonoBehaviour
    {
        public Material worldMaterial;
        public VoxelColor[] WorldColors;
        private Container container;

        void Start()
        {
            if(_instance != null)
            {
                if (_instance != this)
                    Destroy(this);
            }
            else
            {
                _instance = this;
            }

            GameObject cont = new GameObject("Container");
            cont.transform.parent = transform;
            container = cont.AddComponent<Container>();
            container.Initialize(worldMaterial, Vector3.zero);

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int randomYHeight = Random.Range(1, 16);
                    for (int y = 0; y < randomYHeight; y++)
                    {
                        container[new Vector3(x, y, z)] = new Voxel() { ID = 1 };
                    }
                }
            }

            container.GenerateMesh();
            container.UploadMesh();
        }

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
    }
}