# LightDX
[![NuGet](https://img.shields.io/nuget/v/LightDx.svg)](https://www.nuget.org/packages/LightDx/)

LightDX is a graphics library in C#. It is designed to be used for those who just
want to use Direct3D for fast rendering. It supports important funtions in
Direct3D but you have to use .NET framework if you need user input, etc. Some
features includes:

* Lightweight.
No dependencies except the Framework. Less than 100KB after compiled. You can just
copy the source to your project and include them (though it requires unsafe). Even
if you don't want to directly include them, you can still have all functions in a
single small DLL. No native DLL is needed, so it should work on AnyCPU.
* Simplified API.
Unlike SharpDX or SlimDX, LightDX is not a DirectX binding. Therefore it hides
the complicated details of creating each components and only provides simplified
API, which makes it much easier to use. This may have some limits, but hopefully 
LightDX will provide everything you really need.
* Effective. LightDX utilizes calli instruction to call native COM methods, as
SharpDX does. This should be the fastest method. Other parts are also written with
efficiency in mind (at least to my best).

Please note that LightDX only supports Windows desktop application. Other platforms
are not tested.

# Usage
Nuget package ```LightDx``` is available now. (Only .NET Framework 4.7 is supported.)

# Example
See projects in the [Examples](Examples) folder.

# TODO

* Sampler.
* Constant buffer.
* Some matrix and vector math.

Any help is welcomed.
