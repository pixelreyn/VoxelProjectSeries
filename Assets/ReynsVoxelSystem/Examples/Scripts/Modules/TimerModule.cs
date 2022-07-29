using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TimerModule : WorldModule
{
    private Stopwatch timer = new Stopwatch();

    public override WorldStage worldStage => WorldStage.BeforeGeneration;

    public override void OnBeforeGeneration()
    {
        timer.Start();
    }

    public override void OnGenerationComplete()
    {
        timer.Stop();
        UnityEngine.Debug.Log($"Elapsed Generation Time: {timer.ElapsedMilliseconds}ms");
    }

    public override void Register()
    {
        base.Register();
        World.onGenerationComplete += OnGenerationComplete;
    }
}
