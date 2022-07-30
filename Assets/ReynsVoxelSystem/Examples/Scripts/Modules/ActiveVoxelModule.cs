using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class ActiveVoxelModule : WorldModule
{
    public static ConcurrentDictionary<Vector3, ConcurrentDictionary<Vector3, Voxel>> activeVoxels = new ConcurrentDictionary<Vector3, ConcurrentDictionary<Vector3, Voxel>>();
    public static bool Exists;
    
    public override WorldStage worldStage => WorldStage.Tick;
    public override void Register()
    {
        base.Register();
        Exists = true;
    }

    public override void OnTick()
    {
        foreach (var kvp in activeVoxels)
        {
            if (World.Instance.activeChunks.ContainsKey(kvp.Key))
            {
                Chunk c = World.Instance.activeChunks[kvp.Key];
                if (!c.needProcessBlockTicks)
                {
                    List<Vector3> toRemove = new List<Vector3>();
                    foreach (var activeVoxelKVP in kvp.Value)
                    {
                        if (activeVoxelKVP.Value.ActiveValue < 15)
                        {
                            toRemove.Add(activeVoxelKVP.Key);
                            continue;
                        }
                        c.blockPosToUpdate.Add(activeVoxelKVP.Key);
                    }
                    if (c.blockPosToUpdate.Count > 0)
                    {
                        c.needProcessBlockTicks = true;
                        if (c.chunkState != Chunk.ChunkState.WaitingToMesh)
                        {
                            c.chunkState = Chunk.ChunkState.WaitingToMesh;
                            World.Instance.chunksNeedRegenerated.Enqueue(c.chunkPosition);
                        }
                    }

                    foreach (Vector3 v in toRemove)
                    {
                        if(!kvp.Value.TryRemove(v, out var voxel))
                            Debug.Log("failed");
                        
                    }
                }
            }
        }
    }
    
    public static void UpdateVoxel(Vector3 chunkPos, Vector3 voxelPos, Voxel vox, ref GenerationBuffer noiseBuffer)
    {
        switch (vox.ID)
        {
            case 240:
                UpdateWater(chunkPos, voxelPos, vox, ref noiseBuffer);
                break;
        }
    }

    private static void UpdateWater(Vector3 chunkPos, Vector3 voxelPos, Voxel vox, ref GenerationBuffer noiseBuffer)
    {
        Vector3 down = voxelPos + new Vector3(0, -1, 0);

        if (vox.ActiveValue >= 15 && noiseBuffer.voxelArray[down].ID == 0 && !World.Instance.GetVoxelAtCoord(chunkPos, down, out _) && down.y > 0)
        {
            //Air below, continue to traverse down
            vox.ActiveValue = 0;
            World.Instance.SetVoxelAtCoord(chunkPos, voxelPos, vox);
            World.Instance.SetVoxelAtCoord(chunkPos, down, new Voxel() { ID = vox.ID, ActiveValue = 100});
            return;
        }
        
        //If hitting solid and not water, first hit onto the floor as well, helps prevent rerunning updates
        if (vox.ActiveValue >= 15 && noiseBuffer.voxelArray[down].ID != 0 && noiseBuffer.voxelArray[down].ID != 240)
        {
            for (int i = -1; i <= 1; i++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    if (i == 0 && k == 0) continue;
                    Vector3 modPos = voxelPos + new Vector3(i, 0, k);
                    Voxel v;
                    if (modPos.x < 0 || modPos.z < 0)
                        v = new Voxel() { };
                    else
                        v = noiseBuffer.voxelArray[voxelPos + new Vector3(i, 0, k)];

                    if (v.ID == 0)
                    {
                        if (ActiveVoxelModule.activeVoxels[chunkPos].ContainsKey(down))
                            v.ID = 240;
                    }

                    if (v.ID == 0)
                    {
                        v.ID = vox.ID;
                        v.ActiveValue = (byte)(vox.ActiveValue - 15);
                        World.Instance.SetVoxelAtCoord(chunkPos, modPos, v);

                    }
                }


            }
        }
        //This voxel has been updated, it should be removed from the active pool
        vox.ActiveValue = 0;
        activeVoxels[chunkPos][voxelPos] = vox;

        World.Instance.SetVoxelAtCoord(chunkPos, voxelPos, vox);
    }

    public override void OnShutdown()
    {
        base.OnShutdown();
        Exists = false;
        World.onTick -= OnTick;
    }
}
