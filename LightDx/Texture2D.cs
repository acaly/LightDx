using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class Texture2D : IDisposable
    {
        private readonly LightDevice _Device;

        private IntPtr _Texture, _View;
        internal IntPtr ViewPtr { get { return _View; } }

        private bool _Disposed;

        internal Texture2D(LightDevice device, IntPtr tex, IntPtr view)
        {
            _Device = device;
            device.AddComponent(this);

            _Texture = tex;
            _View = view;
        }

        ~Texture2D()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }

            NativeHelper.Dispose(ref _Texture);
            NativeHelper.Dispose(ref _View);

            _Disposed = true;
            _Device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }
    }
}
