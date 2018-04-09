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
        //TODO merge with InputDataProcessor
        private delegate void CopyArrayDelegate32(IntPtr ptr, uint[] array, int nbytes);
        private delegate void CopyArrayDelegate16(IntPtr ptr, ushort[] array, int nbytes);
        private static CopyArrayDelegate32 CopyArray32 = CalliGenerator.GenerateMemCopy<CopyArrayDelegate32, uint>();
        private static CopyArrayDelegate16 CopyArray16 = CalliGenerator.GenerateMemCopy<CopyArrayDelegate16, ushort>();

        internal IndexBuffer(LightDevice device, IntPtr ptr, int bitWidth, int size)
        {
            _Device = device;
            _Ptr = ptr;
            _BitWidth = bitWidth;
            _Size = size;
        }

        ~IndexBuffer()
        {
            Dispose();
        }

        private readonly LightDevice _Device;
        private IntPtr _Ptr;
        private readonly int _BitWidth;
        private readonly int _Size;

        public void Dispose()
        {
            NativeHelper.Dispose(ref _Ptr);
        }

        internal void Bind()
        {
            DeviceContext.IASetIndexBuffer(_Device.ContextPtr, _Ptr,
                _BitWidth == 16 ? 57u /* DXGI_FORMAT_R16_UINT */ : 42u /* DXGI_FORMAT_R32_UINT */, 0);
        }

        public unsafe void UpdateDynamic(Array data)
        {
            SubresourceData ret;
            DeviceContext.Map(_Device.ContextPtr, _Ptr, 0, 4 /* WRITE_DISCARD */, 0, &ret).Check();

            if (_BitWidth == 16)
            {
                CopyArray16(ret.pSysMem, (ushort[])data, 2 * data.Length);
            }
            else if (_BitWidth == 32)
            {
                CopyArray32(ret.pSysMem, (uint[])data, 4 * data.Length);
            }

            DeviceContext.Unmap(_Device.ContextPtr, _Ptr, 0);
        }

        public void DrawAll(InputBuffer vertex)
        {
            Draw(vertex, 0, _Size);
        }

        public void Draw(InputBuffer vertex, int startIndex, int indexCount)
        {
            Bind();
            vertex.Bind();
            DeviceContext.DrawIndexed(_Device.ContextPtr, (uint)indexCount, (uint)startIndex, 0);
        }
    }
}
