using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public abstract class AbstractShaderResourceBuffer : IDisposable
    {
        internal AbstractShaderResourceBuffer(LightDevice device, IntPtr pBuffer, IntPtr pView)
        {
            _device = device;
            device.AddComponent(this);

            _pBuffer = pBuffer;
            _pView = pView;
        }

        ~AbstractShaderResourceBuffer()
        {
            Dispose(false);
        }

        protected readonly LightDevice _device;
        protected IntPtr _pBuffer, _pView;
        private bool _disposed;

        internal IntPtr ViewPtr => _pView;

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
            NativeHelper.Dispose(ref _pView);
            NativeHelper.Dispose(ref _pBuffer);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public class ShaderResourceBuffer<T> : AbstractShaderResourceBuffer where T : unmanaged
    {
        private static readonly int _Size = Marshal.SizeOf<T>();

        internal ShaderResourceBuffer(LightDevice device, IntPtr pBuffer, IntPtr pView)
            : base(device, pBuffer, pView)
        {
        }

        public unsafe void Update(T[] data, int start = 0, int length = -1)
        {
            int realLength = length == -1 ? data.Length - start : length;

            SubresourceData ret;
            DeviceContext.Map(_device.ContextPtr, _pBuffer, 0,
                4 /* WRITE_DISCARD */, 0, &ret).Check();

            fixed (T* pData = &data[start])
            {
                Buffer.MemoryCopy(pData, ret.pSysMem.ToPointer(), _Size * realLength, _Size * realLength);
            }

            DeviceContext.Unmap(_device.ContextPtr, _pBuffer, 0);
        }
    }
}
