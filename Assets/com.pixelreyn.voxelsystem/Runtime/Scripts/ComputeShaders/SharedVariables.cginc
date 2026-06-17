#include "Voxel.cginc"

int chunkSizeX;
int chunkSizeY;
float3 chunkPosition;


RWStructuredBuffer<Voxel> voxelArray;
RWStructuredBuffer<uint> count;

uint flattenCoord(float3 idx)
{
    return round(idx.x) + (round(idx.y) * (chunkSizeX + 5)) + (round(idx.z) * (chunkSizeX + 5) * (chunkSizeY + 1));
}