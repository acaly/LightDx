using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class Texture2D : IDisposable
    {
        private readonly LightDevice _device;

        private IntPtr _texture, _view;
        internal IntPtr ViewPtr => _view;
        internal IntPtr TexturePtr => _texture;
        public int Width { get; private set; }
        public int Height { get; private set; }

        private bool _disposed;

        internal Texture2D(LightDevice device, IntPtr tex, IntPtr view, int w, int h)
        {
            _device = device;
            device.AddComponent(this);

            _texture = tex;
            _view = view;

            Width = w;
            Height = h;
        }

        ~Texture2D()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            NativeHelper.Dispose(ref _texture);
            NativeHelper.Dispose(ref _view);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
