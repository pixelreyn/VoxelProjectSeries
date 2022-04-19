using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public struct Voxel
{
    public int ID; 
    public float ActiveValue;
    public int CantUpdateFurther;

    public bool isSolid
    {
        get
        {
            return ID != 0;
        }
    }
}