using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    //TODO use same baseclass for all buffers
    public abstract class AbstractConstantBuffer : IDisposable
    {
        protected LightDevice _device;
        protected IntPtr _buffer;
        private bool _disposed;

        internal IntPtr BufferPtr => _buffer;

        internal protected AbstractConstantBuffer(LightDevice device, IntPtr buffer)
        {
            _device = device;
            device.AddComponent(this);

            _buffer = buffer;
        }

        ~AbstractConstantBuffer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _buffer);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe sealed class ConstantBuffer<T> : AbstractConstantBuffer
        where T : unmanaged
    {
        private static readonly int _Size = sizeof(T);

        internal ConstantBuffer(LightDevice device, IntPtr buffer)
            : base(device, buffer)
        {
        }
        
        public T Value;

        public void Update()
        {
            //Use Map/Unmap instead of UpdateSubresource.
            SubresourceData ret;
            DeviceContext.Map(_device.ContextPtr, _buffer, 0,
                4 /* WRITE_DISCARD */, 0, &ret).Check();
            fixed (T* ptr = &Value)
            {
                Buffer.MemoryCopy(ptr, ret.pSysMem.ToPointer(), _Size, _Size);
            }
            DeviceContext.Unmap(_device.ContextPtr, _buffer, 0);
        }
    }
}
