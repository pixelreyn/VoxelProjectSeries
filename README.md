# Voxel Project Series

This is the accompanying code for the tutorial series on Youtube at: https://www.youtube.com/watch?v=EubjobNVJdM&list=PLxI8V1bns4ExV7K6DIrP8BByNSKDCRivo
Individual tutorials can be found under the branch drop down, and the latest tutorial being merged into master


Have you ever wanted to write a system like Dual Contouring, but keep your data like a normal Minecraft clone instead of sampling gradients of a Signed Distance Field and keeping a SDF tree to make changes? Have you ever wanted to skip using a QEF system to produce a (mostly) smooth surface? Well now you can!

This system uses a standard Voxel as it's basic data type, with an ID representing the block type, but also with a hidden density field inside of it, so each voxel is *really* like a 4x4x4 grid of voxels, within a single voxel. All packed into 10 bytes of data per voxel.

General Features:

    Abstract Worlds for easily changing your world type and generation style
    Destructible/Modifable Terrain
    Basic Voxel Water (follows a simple expand and flow mechanic)
    Textured or Colored terrain generation
    Smoothed Terrain or a lower-poly-esque style


Currently there's an abstracted world system, with three world types:

    World Example
    A simple example of how the abstraction works, generates a renderDistance*renderDistance rolling hills type environment.
    Island Example
    Similar to the above as far as the world is concerned, but with a different Density shader that produces and island surounded by water
    Infinite Terrain Example
    By far the most complex of the three, this sample produces infinite terrain, with some voxel foliage and ponds that spawn below a threshold. There's also cutout caverns that are generated in this.
