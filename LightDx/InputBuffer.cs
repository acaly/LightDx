using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class InputBuffer : IDisposable
    {
        private readonly LightDevice _device;

        //support only one buffer
        private IntPtr _buffer;
        private IntPtr _layout;
        private uint _stride;
        private int _vertexCount;

        private bool _disposed;

        internal InputBuffer(LightDevice device, IntPtr buffer, IntPtr layout,
            int stride, int vertexCount)
        {
            _device = device;
            device.AddComponent(this);

            _buffer = buffer;
            _layout = layout;
            _stride = (uint)stride;
            _vertexCount = vertexCount;
        }

        ~InputBuffer()
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
            NativeHelper.Dispose(ref _layout);

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        internal unsafe void Bind()
        {
            DeviceContext.IASetInputLayout(_device.ContextPtr, _layout);
            uint stride = _stride, offset = 0;
            IntPtr buffer = _buffer;
            DeviceContext.IASetVertexBuffers(_device.ContextPtr, 0, 1, &buffer, &stride, &offset);
        }

        public void DrawAll()
        {
            Draw(0, _vertexCount);
        }

        public void Draw(int vertexOffset, int vertexCount)
        {
            Bind();
            DeviceContext.Draw(_device.ContextPtr, (uint)vertexCount, (uint)vertexOffset);
        }

        internal IntPtr BufferPtr => _buffer;
    }
}
