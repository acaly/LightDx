# LightDX (WIP)
[![NuGet](https://img.shields.io/nuget/v/LightDx.svg)](https://www.nuget.org/packages/LightDx/)

LightDX is a graphics library in C#. It is designed to be used for those who want
to use DirectX for accelerated rendering (visualization or a simple game). It supports
most important funtions in Direct3D, but heavily relys on .NET Framework on other works.

## **Note**
This is a work in progress, so public APIs are expected to have breaking changes.

~~I'm using new features in C# 7.3, so the code or the nuget package may not work unless
you use Visual Studio 2017 Preview. If you don't want to install another VS, you can
find an older version of code or binary instead. Hopefully C# 7.3 will be published as
release version soon.~~ Now you can use the nuget package (since 0.1.6) under standard VS 2017 thanks
to the fody weaver. The source code however still only works in VS preview.

# Features
* **Lightweight.**
No dependencies except the Framework. Less than 100KB after compiled. You can just
copy the source to your project and include them (though it requires unsafe). Even
if you don't want to directly include them, you can still have everything in a
single small DLL (SharpDX can generously give you 7). No native DLL is needed, so it
works on 'AnyCPU'.
* **Clean.**
Focus on Direct3D. Use the .NET Framework as much as possible: Bitmap format
conversion, font rendering, mouse and keyboard input, vector and matrix math.
Fortunately the .NET Framework API is well designed and can be directly used here,
which avoids tons of work.
* **Easy to use.**
Unlike SharpDX or SlimDX, LightDX is not a DirectX binding. Therefore it hides
the complicated details of creating each components and only provides simplified
API, which makes it much easier to use. This may have some limits, but hopefully 
LightDX will provide everything you really need.
* **Fast.**
LightDX utilizes calli instruction to call native COM methods, as
SharpDX does. This should be the fastest method. Other parts are also written with
efficiency in mind (at least to my best).

# Limitations
Limitations in design:
* Single thread API. No multithread rendering.
* Only supports Windows desktop application. Other platforms are not tested.

Limitations in current implementation (may be fixed in the future):
* Only support Texture2D as ShaderResource.

... and many other not supported features in DX11...

# How to use
Nuget package ```LightDx``` is available now. (Only .NET Framework 4.7 is supported.)

Please check the following projects that uses LightDx:
* [Examples](Examples) folder.
* [DirectX 11 Tutorial](https://github.com/acaly/LightDx.DirectX11Tutorials).
* [ImGuiOnLightDx](https://github.com/acaly/ImGuiOnLightDx).

# TODO List
More pipeline units:
* Sampler.
* Rasterizer.

Others:
* Minimal stencil support (low priority).
* Better support for pixel formats (low priority).

If you want to use features that LightDx does not support yet, feel free to tell
me by opening an issue!
