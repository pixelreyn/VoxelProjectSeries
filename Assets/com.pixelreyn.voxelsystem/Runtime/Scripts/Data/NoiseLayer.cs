namespace PixelReyn.VoxelSystem
{
    [System.Serializable]
    public struct NoiseLayers
    {
        public float gain;
        public float frequency;
        public float lacunarity;
        public float persistence;
        public int octaves;

        public float caveScale;
        public float caveThreshold;

        public int surfaceVoxelId;
        public int subSurfaceVoxelId;
    }
}

