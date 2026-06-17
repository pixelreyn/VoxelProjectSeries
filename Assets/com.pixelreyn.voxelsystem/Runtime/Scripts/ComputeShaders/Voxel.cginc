struct Voxel
{
    int voxelData;
    uint densityData;
    uint densityDataB;
    
    int getId()
    {
        return ((voxelData & (0xff << 0)) >> 0);
    }
    void setId(int value)
    {
        voxelData |= value << 0;
    }
    
    int getActiveValue()
    {
        return ((voxelData & (0xff << 8)) >> 8);
    }
    
    void setActiveValue(inout Voxel voxel, int value)
    {
        voxelData |= value << 8;
    }
    
    int IndexFromCoord(int x, int y, int z)
    {
        return x + (y * 4) + (z * 4 * 4);
    }
    
    
    bool getVoxelDensity(int x, int y, int z)
    {
        int index = IndexFromCoord(x, y, z);
        if (index >= 32)
        {
            index -= 32;
            return (densityDataB & (0x1u << (index))) != 0;
        }
        else
            return (densityData & (0x1u << index)) != 0;

    }
    

    void setVoxelDensity(int x, int y, int z, bool value)
    {
        int index = IndexFromCoord(x, y, z);

        if (index >= 32)
        {
            index -= 32;
            if (value)
                densityDataB |= 0x1u << index;
            else
                densityDataB &= ~(0x1u << index);
        }
        else
        {
            if (value)
                densityData |= 0x1u << index;
            else
                densityData &= ~(0x1u << index);
        }

    }
    
    bool isSolid()
    {
        return getId() != 0;
    }
    bool isOpaque()
    {
        return getId() != 0 && getId() != 240;
    }
    bool isTransparent()
    {
        return getId() == 240;
    }
};

struct VoxelDetails
{
    int color;
    float metallic;
    float smoothness;
};