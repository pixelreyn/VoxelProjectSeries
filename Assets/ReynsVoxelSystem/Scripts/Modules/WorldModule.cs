using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WorldModule : MonoBehaviour
{
    public virtual WorldStage worldStage { get; }
    
    //Called before generation starts.. Not sure what this will be good for
    public virtual void OnBeforeGeneration(){}
    
    //Called after noise generation is done, but before meshing, useful for modifying created voxel data
    public virtual void OnBeforeMeshing(params object[] parameters){}
    
    //Called from your implementation of World
    public virtual void OnGenerationComplete(){}

    //Called from a background thread of World
    public virtual void OnTick(){}

    //You can override Register if you need to have your module called after multiple delegates, for instance if you need to know if Generation is complete before
    //running your tick function
    public virtual void Register()
    {
        if (worldStage == WorldStage.BeforeGeneration)
            World.onBeforeGeneration += OnBeforeGeneration;
        if (worldStage == WorldStage.BeforeMeshing)
            World.onBeforeMesh += OnBeforeMeshing;
        if (worldStage == WorldStage.Tick)
            World.onTick += OnTick;
        if (worldStage == WorldStage.GenerationComplete)
            World.onGenerationComplete += OnGenerationComplete;
        
        World.onShutdown += OnShutdown;
    }
    
    public virtual void OnShutdown()
    {
        World.onShutdown -= OnShutdown;
    }	
    private void OnDestroy()
    {
        OnShutdown();
    }
}

public enum WorldStage
{
    BeforeGeneration,
    BeforeMeshing,
    GenerationComplete,
    Tick
}