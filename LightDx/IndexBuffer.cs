using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class IndexBuffer : IDisposable
    {
        internal IndexBuffer(LightDevice device, IntPtr ptr, int bitWidth, int size)
        {
            _device = device;
            _ptr = ptr;
            _bitWidth = bitWidth;
            _size = size;
        }

        ~IndexBuffer()
        {
            Dispose();
        }

        private readonly LightDevice _device;
        private IntPtr _ptr;
        private readonly int _bitWidth;
        private readonly int _size;

        public void Dispose()
        {
            NativeHelper.Dispose(ref _ptr);
        }

        internal void Bind()
        {
            DeviceContext.IASetIndexBuffer(_device.ContextPtr, _ptr,
                _bitWidth == 16 ? 57u /* DXGI_FORMAT_R16_UINT */ : 42u /* DXGI_FORMAT_R32_UINT */, 0);
        }

        public unsafe void UpdateDynamic(Array data)
        {
            SubresourceData ret;
            DeviceContext.Map(_device.ContextPtr, _ptr, 0, 4 /* WRITE_DISCARD */, 0, &ret).Check();

            if (_bitWidth == 16)
            {
                StructArrayHelper<ushort>.CopyArray(ret.pSysMem, (ushort[])data, 2 * data.Length);
            }
            else if (_bitWidth == 32)
            {
                StructArrayHelper<uint>.CopyArray(ret.pSysMem, (uint[])data, 4 * data.Length);
            }

            DeviceContext.Unmap(_device.ContextPtr, _ptr, 0);
        }

        public void DrawAll(InputBuffer vertex)
        {
            Draw(vertex, 0, _size);
        }

        public void Draw(InputBuffer vertex, int startIndex, int indexCount)
        {
            Bind();
            vertex.Bind();
            DeviceContext.DrawIndexed(_device.ContextPtr, (uint)indexCount, (uint)startIndex, 0);
        }
    }
}
