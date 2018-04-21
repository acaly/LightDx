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
    public sealed class TextureFontCache : AbstractFontCache<Texture2D>
    {
        private class MemoryCacheItem : IDisposable
        {
            public MemoryCacheItem(IntPtr ptr, int stride, int height)
            {
                Ptr = ptr;
                Stride = stride;
                Height = height;
            }

            ~MemoryCacheItem()
            {
                Dispose();
            }

            public IntPtr Ptr;
            public int Stride;
            public int Height;

            public void Dispose()
            {
                Marshal.FreeHGlobal(Ptr);
                Ptr = IntPtr.Zero;
            }
        }

        public TextureFontCache(LightDevice device, Font font)
            : base(device, font, PixelFormat.Format32bppArgb, Color.Black, Color.Transparent)
        {
        }
        
        private readonly Dictionary<Texture2D, MemoryCacheItem> _memCache = new Dictionary<Texture2D, MemoryCacheItem>();
        private readonly HashSet<Texture2D> _dirtyCache = new HashSet<Texture2D>();
        private const int PixelSize = 1;

        protected override Texture2D CreateBitmap(int w, int h)
        {
            var ret = _device.CreateTexture2D(w, h, 65 /*A8_UNORM*/, IntPtr.Zero, 0, true);
            //Possible memory leak (only when the new fails). Won't fix.
            _memCache.Add(ret,
                new MemoryCacheItem(Marshal.AllocHGlobal(w * h * PixelSize), w * PixelSize, h));
            return ret;
        }

        protected override void DisposeBitmap(Texture2D bitmap)
        {
            _memCache[bitmap].Dispose();
            _memCache.Remove(bitmap);
            bitmap.Dispose();
        }

        protected override unsafe void UpdateCache(Texture2D bitmap,
            int x, int y, int w, int h, BitmapData data)
        {
            var p = _memCache[bitmap];
            byte* destScan0 = ((byte*)p.Ptr) + p.Stride * y + PixelSize * x;
            for (int b = 0; b < h; ++b)
            {
                byte* src = ((byte*)data.Scan0) + data.Stride * b + 3;
                byte* dest = destScan0 + p.Stride * b;
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
                for (int i = 0; i < p.Height; ++i)
                {
                    System.Buffer.MemoryCopy((byte*)p.Ptr.ToPointer() + p.Stride * i,
                        (byte*)d.pSysMem + d.SysMemPitch * i, p.Stride, p.Stride);
                }

                DeviceContext.Unmap(_device.ContextPtr, bitmap.TexturePtr, 0);
            }
        }
    }
}
