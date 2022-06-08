using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Voxel
{
    public int voxelData;

    public byte ID
    {
        get
        {
            return GetVoxelData(0);
        }
        set
        {
            UpdateVoxelData(0, value);
        }
    }

    public byte ActiveValue
    {
        get
        {
            return GetVoxelData(1);
        }
        set
        {
            UpdateVoxelData(1, value);
        }
    }
    //From root coord up - technically a range of 0-100
    public byte terrainHeight
    {
        get
        {
            return GetVoxelData(2);
        }
        set
        {
            UpdateVoxelData(2, value);
        }
    }



    byte GetVoxelData(byte component)
    {
        byte shift = (byte)(8 * component);
        return (byte)((voxelData & (0xff << shift)) >> shift);
    }

    void UpdateVoxelData(int component, byte value)
    {
        voxelData |=  value << (8 * component);
    }

    public bool isSolid
    {
        get
        {
            return ID != 0;
        }
    }
}