using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public abstract class AbstractPipelineConstant : IDisposable
    {
        protected LightDevice _device;
        protected IntPtr _buffer;
        private bool _disposed;

        internal IntPtr BuffetPtr => _buffer;

        internal protected AbstractPipelineConstant(LightDevice device, IntPtr buffer)
        {
            _device = device;
            device.AddComponent(this);

            _buffer = buffer;
        }

        ~AbstractPipelineConstant()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _buffer);

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }
    }

    public class PipelineConstant<T> : AbstractPipelineConstant
        where T : struct
    {
        private static readonly int _Size = Marshal.SizeOf<T>();

        internal PipelineConstant(LightDevice device, IntPtr buffer)
            : base(device, buffer)
        {
        }
        
        public T Value;

        public unsafe void Update()
        {
            //Not sure why this doesn't work. Change to Map/Unmap.
            //StructArrayHelper<T>.UpdateSubresource(_device.ContextPtr, _buffer, 0, null, ref Value, 0, 0);

            SubresourceData ret;
            DeviceContext.Map(_device.ContextPtr, _buffer, 0,
                4 /* WRITE_DISCARD */, 0, &ret).Check();
            StructArrayHelper<T>.CopyStruct(ret.pSysMem, ref Value, 0, _Size);
            DeviceContext.Unmap(_device.ContextPtr, _buffer, 0);
        }
    }
}
