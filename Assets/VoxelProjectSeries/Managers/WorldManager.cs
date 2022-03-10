using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelReyn.VoxelSeries.Part1
{
    public class WorldManager : MonoBehaviour
    {
        public Material worldMaterial;
        private Container container;

        void Start()
        {
            GameObject cont = new GameObject("Container");
            cont.transform.parent = transform;
            container = cont.AddComponent<Container>();
            container.Initialize(worldMaterial, Vector3.zero);

            container.GenerateMesh();
            container.UploadMesh();
        }
    }
}