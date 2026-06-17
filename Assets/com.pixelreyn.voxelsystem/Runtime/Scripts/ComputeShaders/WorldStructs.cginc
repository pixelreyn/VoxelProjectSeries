#include "com.jimmycushnie.noisynodesHLSL/HLSL/BCCNoise4.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/BCCNoise8.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/ClassicNoise2D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/ClassicNoise3D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/SimplexNoise2D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/SimplexNoise3D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/Voronoi2D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/Voronoi3D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/Voronoi4D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/WhiteNoise2D.hlsl"
#include "com.jimmycushnie.noisynodesHLSL/HLSL/WhiteNoise3D.hlsl"

struct Biome
{
    int surfaceVoxel;
    int subsurfaceVoxel;
    int foliage;
    int foliageIds;
    int foliageCount;
    int structures;
    int structureIds;
    int structureCount;
    int noiseScale;
    int caveNoiseScale;
    float weight;
    float3 offsets;
};
