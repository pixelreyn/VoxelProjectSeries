using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace PixelReyn.VoxelSeries.Part3
{
    public struct Voxel
    {
        public byte ID;

        public bool isSolid
        {
            get
            {
                return ID != 0;
            }
        }
    }
}