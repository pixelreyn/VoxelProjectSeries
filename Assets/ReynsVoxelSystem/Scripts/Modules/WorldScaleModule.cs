using UnityEngine;

public class WorldScaleModule : WorldModule
{
    public override WorldStage worldStage => WorldStage.GenerationComplete;

    [Min(1)]
    [SerializeField]
    int numberOfVoxelsPerUnit;

    public override void OnGenerationComplete()
    {
        transform.parent.localScale = new Vector3(1f / numberOfVoxelsPerUnit, 1f / numberOfVoxelsPerUnit, 1f / numberOfVoxelsPerUnit);
        World.onGenerationComplete -= OnGenerationComplete;
    }
}
