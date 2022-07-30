using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnModule : WorldModule
{

    public override WorldStage worldStage => WorldStage.GenerationComplete;

    public override void OnGenerationComplete()
    {
        Debug.Log("Generation Complete, spawn player!");
        World.onGenerationComplete -= OnGenerationComplete;
    }

}
