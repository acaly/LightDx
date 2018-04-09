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
        private readonly LightDevice _Device;

        //support only one buffer
        private IntPtr _Buffer;
        private IntPtr _Layout;
        private uint _Stride;
        private int _VertexCount;

        private bool _Disposed;

        internal InputBuffer(LightDevice device, IntPtr buffer, IntPtr layout,
            int stride, int vertexCount)
        {
            _Device = device;
            device.AddComponent(this);

            _Buffer = buffer;
            _Layout = layout;
            _Stride = (uint)stride;
            _VertexCount = vertexCount;
        }

        ~InputBuffer()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _Buffer);
            NativeHelper.Dispose(ref _Layout);

            _Disposed = true;
            _Device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        internal unsafe void Bind()
        {
            DeviceContext.IASetInputLayout(_Device.ContextPtr, _Layout);
            uint stride = _Stride, offset = 0;
            IntPtr buffer = _Buffer;
            DeviceContext.IASetVertexBuffers(_Device.ContextPtr, 0, 1, &buffer, &stride, &offset);
        }

        public void DrawAll()
        {
            Draw(0, _VertexCount);
        }

        public void Draw(int vertexOffset, int vertexCount)
        {
            Bind();
            DeviceContext.Draw(_Device.ContextPtr, (uint)vertexCount, (uint)vertexOffset);
        }

        internal IntPtr BufferPtr => _Buffer;
    }
}
