using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class VertexDataProcessorGroup : IDisposable
    {
        private readonly LightDevice _device;
        internal readonly Type[] VertexTypes;
        internal readonly object[] Processors;
        private IntPtr _layout;
        private bool _disposed;

        private readonly uint[] _offsets;

        public VertexDataProcessorGroup(LightDevice device, Type[] vertexTypes, object[] processors, IntPtr layout)
        {
            _device = device;
            VertexTypes = vertexTypes;
            Processors = processors;
            _layout = layout;
            _offsets = new uint[vertexTypes.Length];
        }

        ~VertexDataProcessorGroup()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            NativeHelper.Dispose(ref _layout);

            if (disposing)
            {
                foreach (var p in Processors)
                {
                    ((IDisposable)p).Dispose();
                }
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public VertexDataProcessor<T> GetVertexDataProcessor<T>() where T : unmanaged
        {
            for (int i = 0; i < VertexTypes.Length; ++i)
            {
                if (VertexTypes[i] == typeof(T))
                {
                    return (VertexDataProcessor<T>)Processors[i];
                }
            }
            return null;
        }

        public unsafe void Bind(VertexBuffer[] buffers, uint[] offsets = null)
        {
            if (buffers.Length != VertexTypes.Length)
            {
                throw new ArgumentException();
            }
            DeviceContext.IASetInputLayout(_device.ContextPtr, _layout);

            uint[] strides = new uint[buffers.Length];
            IntPtr[] ptrs = new IntPtr[buffers.Length];
            for (int i = 0; i < buffers.Length; ++i)
            {
                strides[i] = buffers[i].Stride;
                ptrs[i] = buffers[i].BufferPtr;
            }
            uint[] realOffset = offsets ?? _offsets;

            fixed (uint* pstrides = strides)
            {
                fixed (IntPtr* pptrs = ptrs)
                {
                    fixed (uint* poffsets = realOffset)
                    {
                        DeviceContext.IASetVertexBuffers(_device.ContextPtr, 0, (uint)buffers.Length, pptrs, pstrides, poffsets);
                    }
                }
            }
        }

        public void Draw(int vertexOffset, int vertexCount)
        {
            DeviceContext.Draw(_device.ContextPtr, (uint)vertexCount, (uint)vertexOffset);
        }

        public void BindAndDrawAll(VertexBuffer[] buffers)
        {
            BindAndDraw(buffers, 0, buffers[0].VertexCount);
        }

        public void BindAndDraw(VertexBuffer[] buffers, int vertexOffset, int vertexCount)
        {
            Bind(buffers);
            Draw(vertexOffset, vertexCount);
        }
    }
}
