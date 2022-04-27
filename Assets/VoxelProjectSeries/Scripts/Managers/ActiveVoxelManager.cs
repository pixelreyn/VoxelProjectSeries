using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ActiveVoxelManager
{

    public static void UpdateVoxel(Vector3 chunkPos, Vector3 voxelPos, Voxel vox, ref NoiseBuffer noiseBuffer)
    {
        switch (vox.ID)
        {
            case 240:
                UpdateWater(chunkPos, voxelPos, vox, ref noiseBuffer);
                break;
        }
    }

    static void UpdateWater(Vector3 chunkPos, Vector3 voxelPos, Voxel vox, ref NoiseBuffer noiseBuffer)
    {
        Vector3 down = voxelPos + new Vector3(0, -1, 0);

        if (vox.ActiveValue >= 15 && noiseBuffer.voxelArray[down].ID == 0 && !WorldManager.Instance.activeVoxels[chunkPos].ContainsKey(down) && down.y > 0)
        {
            //Air below, continue to traverse down
            vox.ActiveValue = 0;
            WorldManager.Instance.SetVoxelAtCoord(chunkPos, voxelPos, vox);
            WorldManager.Instance.SetVoxelAtCoord(chunkPos, down, new Voxel() { ID = vox.ID, ActiveValue = 100});
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
                        if (WorldManager.Instance.activeVoxels[chunkPos].ContainsKey(down))
                            v.ID = 240;
                    }

                    if (v.ID == 0)
                    {
                        v.ID = vox.ID;
                        v.ActiveValue = (byte)(vox.ActiveValue - 15);
                        WorldManager.Instance.SetVoxelAtCoord(chunkPos, modPos, v);

                    }
                }


            }
        }

        WorldManager.Instance.SetVoxelAtCoord(chunkPos, voxelPos, vox);
    }
}