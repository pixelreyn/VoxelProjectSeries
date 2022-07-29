using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class StructureModule : WorldModule
{
	public static bool Exists = false;

	static System.Random random;
	static Dictionary<Vector3, Voxel>[] tree = new Dictionary<Vector3, Voxel>[6];

	public override WorldStage worldStage => WorldStage.BeforeMeshing;

	public override void Register()
	{
		base.Register();
		Exists = true;
	}

	public override void OnBeforeMeshing(params object[] parameters)
	{
		var chunk = (Chunk)parameters[0];

		int specialBlockCount = (int)parameters[1];
		Vector4[] seedBlocks = (Vector4[])parameters[2];
		
		//Iterate over specialBlocks and generate the requested structure or block at the location
		for (int id = 0; id < specialBlockCount; id++)
		{
			Vector3 pos = new Vector3(seedBlocks[id].x, seedBlocks[id].y, seedBlocks[id].z);
			if (chunk.generationBuffer.voxelArray[pos].ID != 0)
				continue;
			
			//If there's already a modified voxel at this point, we can assume this spawn has happened already 
			if (World.Instance.modifiedVoxels[chunk.chunkPosition].ContainsKey(pos))
				continue;
			
			switch ((int)seedBlocks[id].w)
			{
				case 243:
					SpawnRockAt(pos, chunk, chunk.generationBuffer.voxelArray);
					break;
				case 244:
					SpawnTreeAt(pos, chunk, chunk.generationBuffer.voxelArray);
					break;
				case 245:
					SpawnBushAt(pos, chunk, chunk.generationBuffer.voxelArray);
					break;
			}
		}
	}

	public override void OnShutdown()
	{
		base.OnShutdown();
		Exists = false;
	}
	

	public static void IntializeRandom(int seed)
	{
		random = new System.Random(seed);

		for (int i = 0; i < 6; i++)
		{
			tree[i] = new Dictionary<Vector3, Voxel>();
			int height = random.Next(4, 12);
			int leafMaxWidth = random.Next(6, 14);
			int leafHeight = random.Next(6, 12);
			for (int y = 0; y< height; y++)
				tree[i].Add(new Vector3(0, y, 0), new Voxel { ID = 3, densityData = uint.MaxValue, densityDataB = uint.MaxValue });

			Vector3 center = Vector3.up * (height - 2) + Vector3.up * (leafHeight / 2);
			for (int y = 0; y < leafHeight; y++)
			{
				for (int x = -leafMaxWidth; x <= leafMaxWidth; x++)
					for (int z = -leafMaxWidth; z <= leafMaxWidth; z++)
					{
						Vector3 localPos = new Vector3(x, height + y, z);
						Voxel v = new Voxel();
						if (sdSphere(center, localPos, leafMaxWidth / 2) <= 0)
							v.ID = 4;

						int c = 0;
						for (int ix = 0; ix < 4; ix++)
							for (int iy = 0; iy < 4; iy++)
								for (int iz = 0; iz < 4; iz++)
								{
									float s = sdSphere(center, localPos + new Vector3(ix, iy, iz) * 0.25f, leafMaxWidth / 2);

									if (math.distance(s, 0) < 0.125f)
									{
										v.setVoxelDensity(ix, iy, iz, true);
										c++;
									}
								}

						if (c > 1 || v.ID == 4)
							tree[i].Add(new Vector3(x, height + y, z), v);

					}
			}
		}

	}

	static float sdSphere(Vector3 center, Vector3 point, float radius)
	{
		return Vector3.Magnitude(center - point) - radius;
	}

	public static void SpawnTreeAt(Vector3 pos, Chunk chunk, IndexedArray<Voxel> cont)
	{
		int treeV = random.Next(0, 5);
		foreach (KeyValuePair<Vector3, Voxel> pair in tree[treeV])
		{
			World.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + pair.Key, pair.Value);
		}
	}
	public static void SpawnBushAt(Vector3 pos, Chunk chunk, IndexedArray<Voxel> cont)
	{
		World.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos, new Voxel { ID = 3, densityData = uint.MaxValue, densityDataB = uint.MaxValue });

		for (int j = -1; j < 2; j++)
			for (int i = 1; i < 3; i++)
				for (int k = -1; k < 2; k++)
				{
					if (i == 1 && j == 0 && k == 0)
						continue;
					World.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(j, i, k), new Voxel { ID = 4, densityData = uint.MaxValue, densityDataB = uint.MaxValue });
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
					if (posX.x > 0 && posX.x < World.WorldSettings.chunkSize && posX.z > 0 && posX.z < World.WorldSettings.chunkSize && posX.y > 1)
						lowerY = cont[posX - Vector3.up].ID == 0;
					World.Instance.SetVoxelAtCoord(chunk.chunkPosition, pos + new Vector3(x, lowerY ? y - 1 : y, z), new Voxel { ID = 5, densityData = uint.MaxValue, densityDataB = uint.MaxValue });
				}
	}
}
