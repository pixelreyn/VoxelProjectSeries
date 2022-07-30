using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public struct Structure
{
    public int id;
    public int[] environmentsToSpawnIn;
}
[System.Serializable]
public struct Foliage
{
    public int id;
    public int[] environmentsToSpawnIn;
}

[System.Serializable]
public struct Biome
{
    public int surfaceVoxel;
    public int subsurfaceVoxel;
    public int foliage;
    public int structures;
    [HideInInspector]
    public int foliageIds;
    [HideInInspector]
    public int foliageCount;
    [HideInInspector]
    public int structureIds;
    [HideInInspector]
    public int structureCount;
    
    public int noiseScale;
    public int caveNoiseScale;
    public float weight;
    public Vector3 offset;
    
    public void SetFoliageIds(Foliage[] ids)
    {
        if (ids.Length < 8)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                foliageIds |= ids[i].id << (4 * i);
            }
        }
        foliageCount = ids.Length;
    }

    public bool HasEnvironment(int id)
    {
        for (int i = 0; i < foliageCount; i++)
        {
            int val = foliageIds & (0xf << (4 * i)) >> (4 * i);
            if (val == id)
                return true;
        }
        return false;
    }
    public void SetStructureIds(Structure[] ids)
    {
        if (ids.Length < 8)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                structureIds |= ids[i].id << (4 * i);
            }
        }
        structureCount = ids.Length;
    }

    public bool StructHasEnvironment(int id)
    {
        for (int i = 0; i < structureCount; i++)
        {
            int val = structureIds & (0xf << (4 * i)) >> (4 * i);
            if (val == id)
                return true;
        }
        return false;
    }
}