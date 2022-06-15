using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public class IndexedArray<T> where T : struct
{
    private bool initialized = false;

    [SerializeField]
    [HideInInspector]
    public T[] array;

    [SerializeField]
    [HideInInspector]
    private Vector2Int size;

    public IndexedArray()
    {
        Create(WorldManager.WorldSettings.chunkSize, WorldManager.WorldSettings.maxHeight);
    }

    public IndexedArray(int sizeX, int sizeY)
    {
        Create(sizeX, sizeY);
    }

    private void Create(int sizeX, int sizeY)
    {
        size = new Vector2Int(sizeX + 5, sizeY + 1);
        array = new T[Count];
        initialized = true;
    }


    int IndexFromCoord(Vector3 idx)
    {
        return Mathf.RoundToInt(idx.x) + (Mathf.RoundToInt(idx.y) * size.x) + (Mathf.RoundToInt(idx.z) * size.x * size.y);
    }


    public void Clear()
    {
        if (!initialized)
            return;

        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.x; z++)
                    array[x + (y * size.x) + (z * size.x * size.y)] = default(T);
    }

    public int Count
    {
        get { return size.x * size.y * size.x; }
    }

    public T[] GetData
    {
        get
        {
            return array;
        }
    }

    public T this[Vector3 coord]
    {
        get
        {
            if (coord.x < 0 || coord.x > size.x ||
            coord.y < 0 || coord.y > size.y ||
            coord.z < 0 || coord.z > size.x)
            {
                Debug.LogError($"Coordinates out of bounds! {coord}");
                return default(T);
            }
            return array[IndexFromCoord(coord)];
        }
        set
        {
            if (coord.x < 0 || coord.x >= size.x ||
            coord.y < 0 || coord.y >= size.y ||
            coord.z < 0 || coord.z >= size.x)
            {
                Debug.LogError($"Coordinates out of bounds! {coord}");
                return;
            }
            array[IndexFromCoord(coord)] = value;
        }
    }

}
