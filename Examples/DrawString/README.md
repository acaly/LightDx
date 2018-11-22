DrawString
======

This project shows how to draw text with LightDx.

Instructions
------
Text rendering is extremely easy. Basically you need 2 things:
* A ```Sprite``` object for 2D rendering. Created from the ```LightDevice``` object.
* A ```TextureFontCache``` for caching characters on internal textures. Created from 
the ```LightDevice``` object with a ```System.Drawing.Font``` object, where you specify 
the font face and font size.

When you want to draw your text, 

* Make sure the ```Sprite``` is applied (it will setup the device to use its internal 
pipeline).
* Call ```Sprite.DrawString```.

Note that the metrics of the text can be obtained from the ```System.Drawing.Font``` 
object.
