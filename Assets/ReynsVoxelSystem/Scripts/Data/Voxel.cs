using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Voxel
{
    public int voxelData;
    public uint densityData;
    public uint densityDataB;

    public byte ID
    {
        get
        {
            return (byte)((voxelData & (0xff << 0)) >> 0);
        }
        set
        {
            voxelData |=  value << 0;
        }
    }

    public byte ActiveValue
    {
        get
        {
            return (byte)((voxelData & (0xff << 8)) >> 8);
        }
        set
        {
            voxelData &=  ~(0xff << 8);
            voxelData |=  value << (8);
        }
    }

    int IndexFromCoord(int x, int y, int z)
    {
        return Mathf.RoundToInt(x) + (Mathf.RoundToInt(y) * 4) + (Mathf.RoundToInt(z) * 4 * 4);
    }


    public bool getVoxelDensity(int x, int y, int z)
    {
        int index = IndexFromCoord(x, y, z);
        if (index >= 32)
        {
            index -= 32;

            return (densityDataB& (0x1u << (index))) != 0;
        }
        else
            return (densityData& (0x1u << index)) != 0;

    }
    public void setVoxelDensity(int x, int y, int z, bool value)
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

    public bool isSolid
    {
        get
        {
            return ID != 0;
        }
    }
}