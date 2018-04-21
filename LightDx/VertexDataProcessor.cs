using LightDx.InputAttributes;
using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal interface IBufferUpdate
    {
        void UpdateBuffer(VertexBuffer buffer, Array data, int start, int length);
    }

    public sealed class VertexDataProcessor<T> : IDisposable
        where T : unmanaged
    {
        private class BufferUpdate : IBufferUpdate
        {
            private LightDevice _device;

            public BufferUpdate(LightDevice device)
            {
                _device = device;
            }

            public void UpdateBuffer(VertexBuffer buffer, Array data, int start, int length)
            {
                UpdateBufferInternal(buffer, (T[])data, start, length);
            }

            private unsafe void UpdateBufferInternal(VertexBuffer buffer, T[] data, int start, int length)
            {
                int realLength = length == -1 ? data.Length - start : length;
                if (buffer.IsDynamic)
                {
                    SubresourceData ret;
                    DeviceContext.Map(_device.ContextPtr, buffer.BufferPtr, 0,
                        4 /* WRITE_DISCARD */, 0, &ret).Check();

                    fixed (T* pData = &data[start])
                    {
                        Buffer.MemoryCopy(pData, ret.pSysMem.ToPointer(), _Size * realLength, _Size * realLength);
                    }

                    DeviceContext.Unmap(_device.ContextPtr, buffer.BufferPtr, 0);
                }
                else
                {
                    var box = stackalloc uint[6] { 0, 0, 0, (uint)(realLength * _Size), 1, 1 };
                    fixed (T* pData = &data[start])
                    {
                        DeviceContext.UpdateSubresource(_device.ContextPtr, buffer.BufferPtr, 0, box, pData, 0, 0);
                    }
                }
            }
        }

        private static int _Size = Marshal.SizeOf(typeof(T));
        private readonly LightDevice _device;
        private IntPtr _inputLayout;
        private BufferUpdate _bufferUpdate;

        private bool _disposed;

        internal VertexDataProcessor(LightDevice device, IntPtr layout)
        {
            _device = device;
            device.AddComponent(this);

            _inputLayout = layout;
            _bufferUpdate = new BufferUpdate(device);
        }

        ~VertexDataProcessor()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public unsafe VertexBuffer CreateImmutableBuffer(T[] data, int offset = 0, int length = -1)
        {
            int realLength = length == -1 ? data.Length - offset : length;
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(_Size * realLength),
                Usage = 0, //default
                BindFlags = 1, //vertexbuffer
                CPUAccessFlags = 0, //none. or write (65536)
                MiscFlags = 0,
                StructureByteStride = (uint)_Size
            };
            fixed (T* pData = &data[offset])
            {
                DataBox box = new DataBox
                {
                    DataPointer = pData,
                    RowPitch = 0,
                    SlicePitch = 0,
                };
                using (var vb = new ComScopeGuard())
                {
                    Device.CreateBuffer(_device.DevicePtr, &bd, &box, out vb.Ptr).Check();
                    return new VertexBuffer(_device, _bufferUpdate, vb.Move(), _inputLayout.AddRef(), _Size, realLength, false);
                }
            }
        }
        
        public unsafe VertexBuffer CreateDynamicBuffer(int nElement)
        {
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(_Size * nElement),
                Usage = 2, //dynamic
                BindFlags = 1, //vertexbuffer
                CPUAccessFlags = 0x10000, //write
                MiscFlags = 0,
                StructureByteStride = (uint)_Size
            };
            using (var vb = new ComScopeGuard())
            {
                Device.CreateBuffer(_device.DevicePtr, &bd, null, out vb.Ptr).Check();
                return new VertexBuffer(_device, _bufferUpdate, vb.Move(), _inputLayout.AddRef(), _Size, nElement, true);
            }
        }

        internal static InputElementDescription[] CreateLayoutFromType()
        {
            var type = typeof(T);
            List<InputElementDescription> fieldList = new List<InputElementDescription>();
            foreach (var field in type.GetFields())
            {
                var attr = field.GetCustomAttribute<InputAttribute>();
                if (attr == null) continue;
                int offset = Marshal.OffsetOf(type, field.Name).ToInt32();
                int format;
                if (field.FieldType == typeof(float))
                {
                    format = 41; //DXGI_FORMAT_R32_FLOAT
                }
                else if (field.FieldType == typeof(Vector4))
                {
                    format = 2; //R32G32B32A32_Float
                }
                else if (field.FieldType == typeof(Vector2))
                {
                    format = 16; //DXGI_FORMAT_R32G32_FLOAT
                }
                else if (field.FieldType == typeof(uint))
                {
                    format = 28; //DXGI_FORMAT_R8G8B8A8_UNORM
                }
                else
                {
                    throw new ArgumentException("Unknown input field type: " + field.FieldType.Name);
                }
                fieldList.Add(new InputElementDescription
                {
                    SemanticName = attr.SemanticName,
                    SemanticIndex = attr.SemanticIndex,
                    Format = format,
                    AlignedByteOffset = offset,
                });
            }
            fieldList.Sort((InputElementDescription a, InputElementDescription b) =>
                a.AlignedByteOffset.CompareTo(b.AlignedByteOffset));
            return fieldList.ToArray();
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _inputLayout);

            if (disposing)
            {
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
