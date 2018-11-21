using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class VertexBuffer : IDisposable
    {
        private readonly LightDevice _device;
        private readonly IBufferUpdate _update;

        //support only one buffer
        private IntPtr _buffer;
        private IntPtr _layout;
        private uint _stride;
        private int _vertexCount;
        private bool _isDynamic;

        private bool _disposed;

        internal bool IsDynamic => _isDynamic;
        internal uint Stride => _stride;
        internal IntPtr BufferPtr => _buffer;
        internal int VertexCount => _vertexCount;

        internal VertexBuffer(LightDevice device, IBufferUpdate update, IntPtr buffer, IntPtr layout,
            int stride, int vertexCount, bool isDynamic)
        {
            _device = device;
            device.AddComponent(this);

            _update = update;
            _buffer = buffer;
            _layout = layout;
            _stride = (uint)stride;
            _vertexCount = vertexCount;
            _isDynamic = isDynamic;
        }

        ~VertexBuffer()
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
            NativeHelper.Dispose(ref _buffer);
            NativeHelper.Dispose(ref _layout);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal unsafe void Bind()
        {
            DeviceContext.IASetInputLayout(_device.ContextPtr, _layout);
            uint stride = _stride, offset = 0;
            IntPtr buffer = _buffer;
            DeviceContext.IASetVertexBuffers(_device.ContextPtr, 0, 1, &buffer, &stride, &offset);
        }

        public void Update(Array data, int start = 0, int length = -1)
        {
            _update.UpdateBuffer(this, data, start, length);
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
    }
}
