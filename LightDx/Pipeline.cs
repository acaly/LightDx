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
    public enum InputTopology
    {
        Point = 1,
        Triangle = 4,
    }
    
    [Flags]
    public enum ShaderType
    {
        Vertex = 1,
        Geometry = 2,
        Pixel = 4,
    }

    public sealed class Pipeline : IDisposable
    {
        private readonly LightDevice _device;
        private bool _disposed;

        private IntPtr _vertex, _geometry, _pixel;
        private IntPtr _signatureBlob;

        private IntPtr _blendPtr;

        private readonly InputTopology _topology;

        //only 1 viewport
        private Viewport _viewport;

        private Dictionary<int, AbstractConstantBuffer> _vsConstants = new Dictionary<int, AbstractConstantBuffer>();
        private Dictionary<int, AbstractConstantBuffer> _gsConstants = new Dictionary<int, AbstractConstantBuffer>();
        private Dictionary<int, AbstractConstantBuffer> _psConstants = new Dictionary<int, AbstractConstantBuffer>();
        private Dictionary<int, Texture2D> _resources = new Dictionary<int, Texture2D>();
        private Dictionary<int, AbstractShaderResourceBuffer> _vsBuffers = new Dictionary<int, AbstractShaderResourceBuffer>();
        private Dictionary<int, AbstractShaderResourceBuffer> _gsBuffers = new Dictionary<int, AbstractShaderResourceBuffer>();
        private Dictionary<int, AbstractShaderResourceBuffer> _psBuffers = new Dictionary<int, AbstractShaderResourceBuffer>();

        private bool _isBound;

        public bool IsActive => _isBound;

        internal Pipeline(LightDevice device, IntPtr v, IntPtr g, IntPtr p, IntPtr sign, InputTopology topology)
        {
            _device = device;
            device.AddComponent(this);
            _device.ResolutionChanged += DeviceBufferResized;

            _vertex = v;
            _geometry = g;
            _pixel = p;
            _signatureBlob = sign;
            
            _viewport = new Viewport
            {
                Width = _device.ScreenWidth,
                Height = _device.ScreenHeight,
                MaxDepth = 1.0f,
            };
            _topology = topology;
        }

        ~Pipeline()
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

            NativeHelper.Dispose(ref _vertex);
            NativeHelper.Dispose(ref _geometry);
            NativeHelper.Dispose(ref _pixel);
            NativeHelper.Dispose(ref _signatureBlob);
            NativeHelper.Dispose(ref _blendPtr);

            if (disposing)
            {
                _device.ResolutionChanged -= DeviceBufferResized;
                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void DeviceBufferResized(object sender, EventArgs e)
        {
            _viewport = new Viewport
            {
                Width = _device.ScreenWidth,
                Height = _device.ScreenHeight,
                MaxDepth = 1.0f,
            };
            if (_isBound)
            {
                ApplyViewport();
            }
        }

        public unsafe VertexDataProcessor<T> CreateVertexDataProcessor<T>()
            where T : unmanaged
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            var layoutDecl = VertexDataProcessor<T>.CreateLayoutFromType(0);
            using (var layout = new ComScopeGuard())
            {
                fixed (InputElementDescription* d = layoutDecl)
                {
                    Device.CreateInputLayout(_device.DevicePtr, d, (uint)layoutDecl.Length,
                        Blob.GetBufferPointer(_signatureBlob), Blob.GetBufferSize(_signatureBlob), out layout.Ptr).Check();
                }
                return new VertexDataProcessor<T>(_device, layout.Move());
            }
        }

        public unsafe VertexDataProcessorGroup CreateVertexDataProcessors(Type[] types)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            var desc = Enumerable.Empty<InputElementDescription>();
            var processors = new object[types.Length];
            var deviceTypeList = new[] { typeof(LightDevice) };
            var deviceObject = new object[] { _device };
            for (int i = 0; i < types.Length; ++i)
            {
                desc = desc.Concat(InputElementDescriptionFactory.Create(types[i], i));
                var processorType = typeof(VertexDataProcessor<>).MakeGenericType(types[i]);
                var ctor = processorType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, deviceTypeList, null);
                processors[i] = ctor.Invoke(deviceObject);
            }
            var list = desc.ToArray();

            using (var layout = new ComScopeGuard())
            {
                fixed (InputElementDescription* d = list)
                {
                    Device.CreateInputLayout(_device.DevicePtr, d, (uint)list.Length,
                        Blob.GetBufferPointer(_signatureBlob), Blob.GetBufferSize(_signatureBlob), out layout.Ptr).Check();
                }
                return new VertexDataProcessorGroup(_device, types, processors, layout.Move());
            }
        }

        public unsafe IndexBuffer CreateImmutableIndexBuffer<T>(T[] data, int offset = 0, int length = -1) where T : unmanaged
        {
            int realLength = length == -1 ? data.Length - offset : length;
            int indexSize = sizeof(T);
            if (indexSize != 2 && indexSize != 4)
            {
                throw new ArgumentException("Invalid index size: " + indexSize);
            }
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(indexSize * realLength),
                Usage = 0, //default
                BindFlags = 2, //indexbuffer
                CPUAccessFlags = 0, //none. or write (65536)
                MiscFlags = 0,
                StructureByteStride = (uint)indexSize,
            };
            fixed (T* pData = &data[offset])
            {
                DataBox box = new DataBox
                {
                    DataPointer = pData,
                    RowPitch = 0,
                    SlicePitch = 0,
                };
                using (var ib = new ComScopeGuard())
                {
                    Device.CreateBuffer(_device.DevicePtr, &bd, &box, out ib.Ptr).Check();
                    return new IndexBuffer(_device, ib.Move(), indexSize * 8, realLength);
                }
            }
        }

        public unsafe IndexBuffer CreateDynamicIndexBuffer(int bitWidth, int size)
        {
            if (bitWidth != 16 && bitWidth != 32)
            {
                throw new ArgumentOutOfRangeException(nameof(bitWidth));
            }
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(bitWidth / 8 * size),
                Usage = 2, //dynamic
                BindFlags = 2, //indexbuffer
                CPUAccessFlags = 0x10000, //write
                MiscFlags = 0,
                StructureByteStride = (uint)bitWidth / 8,
            };
            using (var ib = new ComScopeGuard())
            {
                Device.CreateBuffer(_device.DevicePtr, &bd, null, out ib.Ptr).Check();
                return new IndexBuffer(_device, ib.Move(), bitWidth, size);
            }
        }

        public unsafe ConstantBuffer<T> CreateConstantBuffer<T>()
            where T : unmanaged
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }

            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (((uint)Marshal.SizeOf<T>() + 15) & ~15u), //multiples of 16
                Usage = 2, //dynamic
                BindFlags = 4, //constantbuffer
                CPUAccessFlags = 0x10000, //write
                MiscFlags = 0,
                StructureByteStride = 0,// (uint)(Marshal.SizeOf<T>()),
            };
            using (var cb = new ComScopeGuard())
            {
                Device.CreateBuffer(_device.DevicePtr, &bd, null, out cb.Ptr).Check();
                return new ConstantBuffer<T>(_device, cb.Move());
            }
        }

        public unsafe ShaderResourceBuffer<T> CreateShaderResourceBuffer<T>(T[] data, bool isStructured) where T : unmanaged
        {
            var elementSize = Marshal.SizeOf<T>();
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(elementSize * data.Length),
                Usage = 1, //immutable
                BindFlags = 8, //shader resource
                CPUAccessFlags = 0, //none
                MiscFlags = isStructured ? 64u : 0u, //D3D11_RESOURCE_MISC_BUFFER_STRUCTURED
                StructureByteStride = (uint)elementSize,
            };
            using (var buffer = new ComScopeGuard())
            {
                fixed (T* pData = &data[0])
                {
                    DataBox box = new DataBox
                    {
                        DataPointer = pData,
                        RowPitch = 0,
                        SlicePitch = 0,
                    };
                    Device.CreateBuffer(_device.DevicePtr, &bd, &box, out buffer.Ptr).Check();
                }
                using (var view = new ComScopeGuard())
                {
                    int* viewDesc = stackalloc int[5]
                    {
                        isStructured ? 0 : InputElementDescriptionFactory.GetFormatFromType(typeof(T)),
                        isStructured ? 11 : 1, //D3D_SRV_DIMENSION_BUFFEREX or D3D_SRV_DIMENSION_BUFFER
                        0, //FirstElement = 0
                        data.Length, //NumElements
                        0, //Not used
                    };
                    Device.CreateShaderResourceView(_device.DevicePtr, buffer.Ptr, viewDesc, out view.Ptr).Check();
                    return new ShaderResourceBuffer<T>(_device, buffer.Move(), view.Move());
                }
            }
        }

        public void SetBlender(Blender b)
        {
            NativeHelper.Dispose(ref _blendPtr);
            _blendPtr = b.CreateBlenderForDevice(_device);
            if (_isBound)
            {
                ApplyBlender();
            }
        }

        public void SetSampler(int slot, Sampler s)
        {
            throw new NotImplementedException();
        }

        public void SetDepthTest(DepthTest d)
        {
            throw new NotImplementedException();
        }

        public void SetResource(int slot, Texture2D tex)
        {
            _resources[slot] = tex;
            _psBuffers.Remove(slot);
            if (_isBound)
            {
                var view = tex?.ViewPtr ?? IntPtr.Zero;
                DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
            }
        }

        public void SetResource(ShaderType usage, int slot, AbstractShaderResourceBuffer buffer)
        {
            if (usage.HasFlag(ShaderType.Vertex))
            {
                _vsBuffers[slot] = buffer;
                if (_isBound)
                {
                    var view = buffer?.ViewPtr ?? IntPtr.Zero;
                    DeviceContext.VSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
                }
            }
            if (usage.HasFlag(ShaderType.Geometry))
            {
                _gsBuffers[slot] = buffer;
                if (_isBound)
                {
                    var view = buffer?.ViewPtr ?? IntPtr.Zero;
                    DeviceContext.GSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
                }
            }
            if (usage.HasFlag(ShaderType.Pixel))
            {
                _resources.Remove(slot);
                _psBuffers[slot] = buffer;
                if (_isBound)
                {
                    var view = buffer?.ViewPtr ?? IntPtr.Zero;
                    DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
                }
            }
        }

        public void SetConstant(ShaderType usage, int slot, AbstractConstantBuffer pipelineConstant)
        {
            if (usage.HasFlag(ShaderType.Vertex))
            {
                _vsConstants[slot] = pipelineConstant;
                if (_isBound)
                {
                    ApplyVSConstantBuffer(slot, pipelineConstant);
                }
            }
            if (usage.HasFlag(ShaderType.Geometry))
            {
                _gsConstants[slot] = pipelineConstant;
                if (_isBound)
                {
                    ApplyGSConstantBuffer(slot, pipelineConstant);
                }
            }
            if (usage.HasFlag(ShaderType.Pixel))
            {
                _psConstants[slot] = pipelineConstant;
                if (_isBound)
                {
                    ApplyPSConstantBuffer(slot, pipelineConstant);
                }
            }
        }

        private unsafe void ApplyViewport()
        {
            fixed (Viewport* ptr = &_viewport)
            {
                DeviceContext.RSSetViewports(_device.ContextPtr, 1, ptr);
            }
        }

        private void ApplyShaders()
        {
            DeviceContext.IASetPrimitiveTopology(_device.ContextPtr, (uint)_topology);
            DeviceContext.VSSetShader(_device.ContextPtr, _vertex, IntPtr.Zero, 0);
            DeviceContext.GSSetShader(_device.ContextPtr, _geometry, IntPtr.Zero, 0);
            DeviceContext.PSSetShader(_device.ContextPtr, _pixel, IntPtr.Zero, 0);
        }

        private void ApplyBlender()
        {
            DeviceContext.OMSetBlendState(_device.ContextPtr, _blendPtr, IntPtr.Zero, 0xFFFFFFFF);
        }

        private void ApplyVSResource(int slot, AbstractShaderResourceBuffer buffer)
        {
            IntPtr view = buffer?.ViewPtr ?? IntPtr.Zero;
            DeviceContext.VSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
        }

        private void ApplyGSResource(int slot, AbstractShaderResourceBuffer buffer)
        {
            IntPtr view = buffer?.ViewPtr ?? IntPtr.Zero;
            DeviceContext.GSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
        }

        private void ApplyPSResource(int slot, AbstractShaderResourceBuffer buffer)
        {
            IntPtr view = buffer?.ViewPtr ?? IntPtr.Zero;
            DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
        }

        private void ApplyPSResource(int slot, Texture2D tex)
        {
            IntPtr view = tex?.ViewPtr ?? IntPtr.Zero;
            DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
        }

        private void ApplyVSConstantBuffer(int slot, AbstractConstantBuffer pipelineConstant)
        {
            var buffer = pipelineConstant?.BufferPtr ?? IntPtr.Zero;
            DeviceContext.VSSetConstantBuffers(_device.ContextPtr, (uint)slot, 1, ref buffer);
        }

        private void ApplyGSConstantBuffer(int slot, AbstractConstantBuffer pipelineConstant)
        {
            var buffer = pipelineConstant?.BufferPtr ?? IntPtr.Zero;
            DeviceContext.GSSetConstantBuffers(_device.ContextPtr, (uint)slot, 1, ref buffer);
        }

        private void ApplyPSConstantBuffer(int slot, AbstractConstantBuffer pipelineConstant)
        {
            var buffer = pipelineConstant?.BufferPtr ?? IntPtr.Zero;
            DeviceContext.PSSetConstantBuffers(_device.ContextPtr, (uint)slot, 1, ref buffer);
        }

        public void Apply()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }

            var prev = _device.CurrentPipeline;
            if (prev == this)
            {
                return;
            }
            _device.CurrentPipeline = this;

            if (prev != null)
            {
                prev._isBound = false;
            }
            _isBound = true;

            ApplyShaders();
            ApplyViewport();
            ApplyBlender();
            //TODO Samplers
            //TODO depth
            foreach (var vsK in _vsConstants)
            {
                ApplyVSConstantBuffer(vsK.Key, vsK.Value);
            }
            foreach (var gsK in _gsConstants)
            {
                ApplyGSConstantBuffer(gsK.Key, gsK.Value);
            }
            foreach (var psK in _psConstants)
            {
                ApplyPSConstantBuffer(psK.Key, psK.Value);
            }
            foreach (var res in _resources)
            {
                ApplyPSResource(res.Key, res.Value);
            }
            foreach (var res in _vsBuffers)
            {
                ApplyVSResource(res.Key, res.Value);
            }
            foreach (var res in _gsBuffers)
            {
                ApplyGSResource(res.Key, res.Value);
            }
            foreach (var res in _psBuffers)
            {
                ApplyPSResource(res.Key, res.Value);
            }
        }
    }
}
