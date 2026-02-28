static const float3 voxelVertices[8] =
{
        float3(0, 0, 0), //0
        float3(1, 0, 0), //1
        float3(1, 1, 0), //2
        float3(0, 1, 0), //3

        float3(0, 0, 1), //4
        float3(1, 0, 1), //5
        float3(1, 1, 1), //6
        float3(0, 1, 1), //7
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

static const float2 voxelUVs[4] =
{
        float2(0, 0),
        float2(0, 1),
        float2(1, 0),
        float2(1, 1)
};


static const int voxelTris[6] =
{
     0, 1, 2, 2, 1, 3 ,
};
static const int voxelTrisMapped[6][6] =
{
    {0,3,1,1,3,2},//back
    {5,6,4,4,6,7},//front
    {4,7,0,0,7,3},//left
    {1,2,5,5,2,6},//right
    {1,5,0,0,5,4},//bottom
    {3,7,2,2,7,6},//top
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