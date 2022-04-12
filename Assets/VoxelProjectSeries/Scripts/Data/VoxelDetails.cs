using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct VoxelDetails
{
	public float color;
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