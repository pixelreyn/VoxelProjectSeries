using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawCubeModule : WorldModule
{

    public override WorldStage worldStage => WorldStage.BeforeMeshing;

    public override void OnBeforeMeshing(params object[] parameters)
    {
        var chunk = (Chunk)parameters[0];
        chunk.generationBuffer.voxelArray.Clear();
        chunk.generationBuffer.voxelArray[new Vector3(10, 10, 10)] = new Voxel() { ID = 1, densityData = 0xffffffffu, densityDataB = 0xffffffffu };
    }

}
