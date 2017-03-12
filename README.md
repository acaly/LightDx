# LightDX
LightDX is a graphics library in C#. The main aims are:

* Lightweight.
No dependencies except the Framework. Less than 40KB after compiled. You can just
copy the source to your project and include them (though it requires unsafe). Even
if you don't want to directly include them, you can still have all functions in a
single small dll.
* Simplified API.
Unlike SharpDX or SlimDX, LightDX is not a DirectX binding. Therefore it hides
the complicated details of creating each components and only provides simplified
API, which makes it much easier to use. This may have some limit, but hopefully 
LightDX will provide you everything you really need.
* Effective. LightDX utilizes calli instruction to call native COM methods, as
SharpDX does. This should be the fastest method. Other parts are also written with
efficiency in mind (at least to my best).

# Example
See [Program.cs](Program.cs).

# TODO
LightDX currently lacks some key features. These includes:
* Texture2D.
* Constant buffer.
* Dynamic vertex buffer.
* Index buffer.
* Font rendering.
* More semantics.
* Some matrix and vector math.

Any help is welcomed.
