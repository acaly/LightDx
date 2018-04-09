using LightDx.InputAttributes;
using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class InputDataProcessor<T> : IDisposable
        where T : struct
    {
        private static int Size = Marshal.SizeOf(typeof(T));
        private readonly LightDevice _Device;
        private IntPtr _InputLayout;

        private bool _Disposed;

        private Device.CreateBufferDelegate_SetPtr<T> _CreateBufferMethod;
        private DeviceContext.UpdateSubresourceDelegate<T> _UpdateSubresourceMethod;
        private delegate void CopyArrayDelegate(IntPtr ptr, T[] array, int nbytes);
        private CopyArrayDelegate _CopyArrayMethod;

        internal InputDataProcessor(LightDevice device, IntPtr layout)
        {
            _Device = device;
            device.AddComponent(this);

            _InputLayout = layout;

            _CreateBufferMethod = CalliGenerator.GetCalliDelegate_Device_CreateBuffer
                <Device.CreateBufferDelegate_SetPtr<T>, T>(3, 2);
            _UpdateSubresourceMethod = CalliGenerator.GetCalliDelegate_PinArray
                <DeviceContext.UpdateSubresourceDelegate<T>, T>(48, 4);
            _CopyArrayMethod = CalliGenerator.GenerateMemCopy<CopyArrayDelegate, T>();
        }

        ~InputDataProcessor()
        {
            Dispose();
        }

        public unsafe InputBuffer CreateImmutableBuffer(T[] data, int offset = 0, int length = -1)
        {
            int realLength = length == -1 ? data.Length - offset : length;
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(Size * realLength),
                Usage = 0, //default
                BindFlags = 1, //vertexbuffer
                CPUAccessFlags = 0, //none. or write (65536)
                MiscFlags = 0,
                StructureByteStride = (uint)Size
            };
            DataBox box = new DataBox
            {
                DataPointer = null, //the pointer is set (after pinned) in _CreateBufferMethod
                RowPitch = 0,
                SlicePitch = 0,
            };
            using (var vb = new ComScopeGuard())
            {
                _CreateBufferMethod(_Device.DevicePtr, &bd, &box, out vb.Ptr, data).Check();
                return new InputBuffer(_Device, vb.Move(), _InputLayout.AddRef(), Size, realLength);
            }
        }

        public unsafe InputBuffer CreateDynamicBuffer(int nElement)
        {
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(Size * nElement),
                Usage = 2, //dynamic
                BindFlags = 1, //vertexbuffer
                CPUAccessFlags = 0x10000, //write
                MiscFlags = 0,
                StructureByteStride = (uint)Size
            };
            using (var vb = new ComScopeGuard())
            {
                Device.CreateBuffer(_Device.DevicePtr, &bd, null, out vb.Ptr).Check();
                return new InputBuffer(_Device, vb.Move(), _InputLayout.AddRef(), Size, nElement);
            }
        }

        public unsafe void UpdateBuffer(InputBuffer buffer, T[] data)
        {
            _UpdateSubresourceMethod(_Device.ContextPtr, buffer.BufferPtr, 0, null, data, 0, 0).Check();
        }

        public unsafe void UpdateBufferDynamic(InputBuffer buffer, T[] data)
        {
            SubresourceData ret;
            DeviceContext.Map(_Device.ContextPtr, buffer.BufferPtr, 0,
                4 /* WRITE_DISCARD */, 0, &ret).Check();

            _CopyArrayMethod(ret.pSysMem, data, Size * data.Length);

            DeviceContext.Unmap(_Device.ContextPtr, buffer.BufferPtr, 0);
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
                else if (field.FieldType == typeof(Float4))
                {
                    format = 2; //R32G32B32A32_Float
                }
                else if (field.FieldType == typeof(Float2))
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

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _InputLayout);

            _Disposed = true;
            _Device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }
    }
}
