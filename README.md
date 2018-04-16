# LightDX
[![NuGet](https://img.shields.io/nuget/v/LightDx.svg)](https://www.nuget.org/packages/LightDx/)

LightDX is a graphics library in C#. It is designed to be used for those who want
to use Direct3D only for fast rendering. It supports important funtions in
Direct3D, but you have to use .NET Framework for resource handling, user input, etc.


# Features
* Lightweight.
No dependencies except the Framework. Less than 100KB after compiled. You can just
copy the source to your project and include them (though it requires unsafe). Even
if you don't want to directly include them, you can still have all functions in a
single small DLL (SharpDX can generously give you 7). No native DLL is needed, so it
works on 'AnyCPU'.
* Simplified API.
Unlike SharpDX or SlimDX, LightDX is not a DirectX binding. Therefore it hides
the complicated details of creating each components and only provides simplified
API, which makes it much easier to use. This may have some limits, but hopefully 
LightDX will provide everything you really need.
* Effective. LightDX utilizes calli instruction to call native COM methods, as
SharpDX does. This should be the fastest method. Other parts are also written with
efficiency in mind (at least to my best).

# Limitations
Limitations in design:
* Single thread API, although DirectX 11 itself supports multithread rendering.
* Only supports Windows desktop application. Other platforms are not tested.
* Functions containing calli instructions are generated when loading. This makes it
slower at startup and takes more memory compared with precompiled, which, however,
requires special build tool and makes building the project complicated.

Limitations in current implementation (may be fixed in the future):
* Only support Texture2D as ShaderResource.
* All shader code must be in one file or string.

# Usage
Nuget package ```LightDx``` is available now. (Only .NET Framework 4.7 is supported.)

# Example
See projects in the [Examples](Examples) folder. 

See [ImGuiOnLightDx](https://github.com/acaly/ImGuiOnLightDx) for support for ImGui.NET.

# TODO List
More pipeline units:
* Sampler.
* Rasterizer.

Others:
* Handling device lost and resolution change.
* Better support for pixel formats.
* Loading DDS.
* Some matrix and vector math.

If you have another function that LightDx cannot do and is not in the list above, feel
free to open an issue!
