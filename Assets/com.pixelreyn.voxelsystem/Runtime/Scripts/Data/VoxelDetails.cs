using UnityEngine;

namespace PixelReyn.VoxelSystem
{
	[System.Serializable]
	public struct VoxelDetails
	{
		public int color;
		public float metallic;
		public float smoothness;
	}

	[System.Serializable]
	public struct Voxels
	{
		[Header("If no texture is defined, we'll automatically fallback to color")]
		public Texture2D texture;
		public Color32 color;
		public float metallic;
		public float smoothness;
	}
}
