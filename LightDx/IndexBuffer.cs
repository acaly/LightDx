using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class IndexBuffer : IDisposable
    {
        internal IndexBuffer(LightDevice device, IntPtr ptr, int bitWidth, int size)
        {
            _device = device;
            device.AddComponent(this);

            _ptr = ptr;
            _bitWidth = bitWidth;
            _size = size;
        }

        ~IndexBuffer()
        {
            Dispose(false);
        }

        private readonly LightDevice _device;
        private IntPtr _ptr;
        private readonly int _bitWidth;
        private readonly int _size;
        private bool _disposed;

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
            NativeHelper.Dispose(ref _ptr);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal void Bind()
        {
            DeviceContext.IASetIndexBuffer(_device.ContextPtr, _ptr,
                _bitWidth == 16 ? 57u /* DXGI_FORMAT_R16_UINT */ : 42u /* DXGI_FORMAT_R32_UINT */, 0);
        }

        public unsafe void UpdateDynamic(Array data, int startIndex = 0, int length = -1)
        {
            int realLength = length == -1 ? data.Length - startIndex : length;
            SubresourceData ret;
            DeviceContext.Map(_device.ContextPtr, _ptr, 0, 4 /* WRITE_DISCARD */, 0, &ret).Check();

            if (_bitWidth == 16)
            {
                StructArrayHelper<ushort>.CopyArray(ret.pSysMem, (ushort[])data, 2 * startIndex, 2 * realLength);
            }
            else if (_bitWidth == 32)
            {
                StructArrayHelper<uint>.CopyArray(ret.pSysMem, (uint[])data, 4 * startIndex, 4 * realLength);
            }

            DeviceContext.Unmap(_device.ContextPtr, _ptr, 0);
        }

        public void DrawAll(VertexBuffer vertex)
        {
            Draw(vertex, 0, _size);
        }

        public void Draw(VertexBuffer vertex, int startIndex, int indexCount)
        {
            Bind();
            vertex.Bind();
            DeviceContext.DrawIndexed(_device.ContextPtr, (uint)indexCount, (uint)startIndex, 0);
        }
    }
}
