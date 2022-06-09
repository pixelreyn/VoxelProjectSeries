static const float3 voxelVertices[8] =
{
        float3(0, 0, 0), //0
        float3(1, 0, 0), //1
        float3(0, 1, 0), //2
        float3(1, 1, 0), //3

        float3(0, 0, 1), //4
        float3(1, 0, 1), //5
        float3(0, 1, 1), //6
        float3(1, 1, 1), //7
};

static const float3 voxelFaceChecks[6] =
{
        float3(0, 0, -1), //back
        float3(0, 0, 1), //front
        float3(-1, 0, 0), //left
        float3(1, 0, 0), //right
        float3(0, -1, 0), //bottom
        float3(0, 1, 0) //top
};

static const int voxelVertexIndex[6][4] =
{
    { 0, 1, 2, 3 },
    { 4, 5, 6, 7 },
    { 4, 0, 6, 2 },
    { 5, 1, 7, 3 },
    { 0, 1, 4, 5 },
    { 2, 3, 6, 7 },
};

static const float2 voxelUVs[4] =
{
        float2(0, 0),
        float2(0, 1),
        float2(1, 0),
        float2(1, 1)
};

static const int voxelTris[6][6] =
{
    { 0, 2, 3, 0, 3, 1 },
    { 0, 1, 2, 1, 3, 2 },
    { 0, 2, 3, 0, 3, 1 },
    { 0, 1, 2, 1, 3, 2 },
    { 0, 1, 2, 1, 3, 2 },
    { 0, 2, 3, 0, 3, 1 },
};
static const int voxelTrisMapped[6][6] =
{
    {0,2,1,1,2,3},//back
    {4,5,6,5,7,6},//front
    {0,6,2,6,0,4},//left
    {1,3,7,7,5,1},//right
    {0,5,4,0,1,5},//bottom
    {2,6,7,2,7,3},//top
};

static const float3 axis[3] =
{
    float3(-1, 0, 0), float3(0, -1, 0), float3(0, 0, -1),
};

static const float3 CellCentersByAxis[3][4] =
{
    { float3(0, -1, -1), float3(0, 0, -1), float3(0, 0, 0), float3(0, -1, 0) },
    { float3(-1, 0, -1), float3(0, 0, -1), float3(0, 0, 0), float3(-1, 0, 0) },
    { float3(-1, -1, 0), float3(0, -1, 0), float3(0, 0, 0), float3(-1, 0, 0) }
};