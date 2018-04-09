using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class TextureFontCache : AbstractFontCache<Texture2D>
    {
        public TextureFontCache(LightDevice device, Font font)
            : base(font, PixelFormat.Format32bppArgb, Color.Black, Color.Transparent)
        {
            _device = device;
        }

        private readonly LightDevice _device;
        private readonly Dictionary<Texture2D, Tuple<IntPtr, int, int>> _memCache =
            new Dictionary<Texture2D, Tuple<IntPtr, int, int>>();
        private readonly HashSet<Texture2D> _dirtyCache = new HashSet<Texture2D>();
        private const int PixelSize = 1;

        protected override Texture2D CreateBitmap(int w, int h)
        {
            var ret = _device.CreateTexture2D(w, h, 65); //A8_UNORM
            _memCache.Add(ret,
                new Tuple<IntPtr, int, int>(Marshal.AllocHGlobal(w * h * PixelSize),
                w * PixelSize, h));
            return ret;
        }

        protected override void DisposeBitmap(Texture2D bitmap)
        {
            Marshal.FreeHGlobal(_memCache[bitmap].Item1);
            _memCache.Remove(bitmap);
            bitmap.Dispose();
        }

        protected override unsafe void UpdateCache(Texture2D bitmap,
            int x, int y, int w, int h, BitmapData data)
        {
            var p = _memCache[bitmap];
            byte* destScan0 = ((byte*)p.Item1) + p.Item2 * y + PixelSize * x;
            for (int b = 0; b < h; ++b)
            {
                byte* src = ((byte*)data.Scan0) + data.Stride * b + 3;
                byte* dest = destScan0 + p.Item2 * b;
                for (int a = 0; a < w; ++a)
                {
                    *dest = *src;
                    dest += 1;
                    src += 4;
                }
            }
            _dirtyCache.Add(bitmap);
        }

        protected override unsafe void FlushCache(Texture2D bitmap)
        {
            if (_dirtyCache.Contains(bitmap))
            {
                _dirtyCache.Remove(bitmap);

                SubresourceData d;
                DeviceContext.Map(_device.ContextPtr, bitmap.TexturePtr, 0, 4, 0, &d).Check();

                var p = _memCache[bitmap];
                for (int i = 0; i < p.Item3; ++i)
                {
                    System.Buffer.MemoryCopy((byte*)p.Item1.ToPointer() + p.Item2 * i,
                        (byte*)d.pSysMem + d.SysMemPitch * i, p.Item2, p.Item2);
                }

                DeviceContext.Unmap(_device.ContextPtr, bitmap.TexturePtr, 0);
            }
        }
    }
}
