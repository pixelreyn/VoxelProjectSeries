using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StructureManager
{

	static System.Random random;
	public static void IntializeRandom(int seed)
	{
		random = new System.Random(seed);
	}

	static float sdSphere(Vector3 center, Vector3 point, float radius)
	{
		return Vector3.Magnitude(center - point) - radius;
	}

	public static void SpawnTreeAt(Vector3 pos, Chunk chunk, IndexedArray<Voxel> cont)
	{
		int height = random.Next(4, 12);
		int leafMaxWidth = random.Next(6, 14);
		int leafHeight = random.Next(6, 12);

		for (int y = 0; y< height; y++)
			WorldManager.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(0, y, 0), new Voxel { ID = 3 });

		Vector3 center = pos + Vector3.up * (height - 2) + Vector3.up * (leafHeight / 2);
		for (int y = 0; y < leafHeight; y++)
		{
			for (int x = -leafMaxWidth; x <= leafMaxWidth; x++)
				for (int z = -leafMaxWidth; z <= leafMaxWidth; z++)
				{
					if (sdSphere(center, pos + new Vector3(x, height + y, z), leafMaxWidth / 2) <= 1)
					{
						WorldManager.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(x, height + y, z), new Voxel { ID = 4 });
					}
				}
		}
	}
	public static void SpawnBushAt(Vector3 pos, Chunk chunk, IndexedArray<Voxel> cont)
	{
		WorldManager.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos, new Voxel { ID = 3 });

		for (int j = -1; j < 2; j++)
			for (int i = 1; i < 3; i++)
				for (int k = -1; k < 2; k++)
				{
					if (i == 1 && j == 0 && k == 0)
						continue;
					WorldManager.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(j, i, k), new Voxel { ID = 4 });
				}

	}

	public static void SpawnRockAt(Vector3 pos, Chunk chunk, IndexedArray<Voxel> cont)
	{
		int w = random.Next(1, 4);
		int d = random.Next(1, 4);
		int h = random.Next(1, 2);

		Vector3 posX;
		for (int x = -w; x < w; x++)
			for (int y = 0; y < h; y++)
				for (int z = -d; z < d; z++)
				{
					posX = pos + new Vector3(x, y, z);
					bool lowerY = false;
					if (posX.x > 0 && posX.x < 16 && posX.z > 0 && posX.z < 16 && posX.y > 1)
						lowerY = cont[posX - Vector3.up].ID == 0;
					WorldManager.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(x, lowerY ? y - 1 : y, z), new Voxel { ID = 5 });
				}
	}
}