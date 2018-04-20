﻿using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
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
    
    public enum ConstantUsage
    {
        VertexShader,
        GeometryShader,
        PixelShader,
    }

    public class Pipeline : IDisposable
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

        private bool _isBound;

        internal Pipeline(LightDevice device, IntPtr v, IntPtr g, IntPtr p, IntPtr sign, Viewport vp, InputTopology topology)
        {
            _device = device;
            device.AddComponent(this);

            _vertex = v;
            _geometry = g;
            _pixel = p;
            _signatureBlob = sign;

            _viewport = vp;
            _topology = topology;
        }

        ~Pipeline()
        {
            Dispose();
        }

        public void Dispose()
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

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public unsafe VertexDataProcessor<T> CreateVertexDataProcessor<T>()
            where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            var layoutDecl = VertexDataProcessor<T>.CreateLayoutFromType();
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

        public unsafe IndexBuffer CreateImmutableIndexBuffer(Array data, int offset = 0, int length = -1)
        {
            int realLength = length == -1 ? data.Length - offset : length;
            int indexSize = data is ushort[] ? 2 : data is uint[] ? 4 : throw new ArgumentException(nameof(data));
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(indexSize * realLength),
                Usage = 0, //default
                BindFlags = 2, //indexbuffer
                CPUAccessFlags = 0, //none. or write (65536)
                MiscFlags = 0,
                StructureByteStride = (uint)indexSize,
            };
            DataBox box = new DataBox
            {
                DataPointer = null, //the pointer is set (after pinned) in _CreateBufferMethod
                RowPitch = 0,
                SlicePitch = 0,
            };
            using (var vb = new ComScopeGuard())
            {
                if (indexSize == 2)
                {
                    StructArrayHelper<ushort>.CreateBuffer(_device.DevicePtr, &bd, &box, out vb.Ptr, ref ((ushort[])data)[0]).Check();
                }
                else
                {
                    StructArrayHelper<uint>.CreateBuffer(_device.DevicePtr, &bd, &box, out vb.Ptr, ref ((uint[])data)[0]).Check();
                }
                return new IndexBuffer(_device, vb.Move(), indexSize * 8, realLength);
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
            using (var vb = new ComScopeGuard())
            {
                Device.CreateBuffer(_device.DevicePtr, &bd, null, out vb.Ptr).Check();
                return new IndexBuffer(_device, vb.Move(), bitWidth, size);
            }
        }

        public unsafe ConstantBuffer<T> CreateConstantBuffer<T>()
            where T : struct
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
            if (_isBound)
            {
                var view = tex?.ViewPtr ?? IntPtr.Zero;
                DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)slot, 1, ref view);
            }
        }

        public void SetConstant(ConstantUsage usage, int slot, AbstractConstantBuffer pipelineConstant)
        {
            switch (usage)
            {
                case ConstantUsage.VertexShader:
                    _vsConstants[slot] = pipelineConstant;
                    if (_isBound)
                    {
                        ApplyVSConstantBuffer(slot, pipelineConstant);
                    }
                    break;
                case ConstantUsage.GeometryShader:
                    _gsConstants[slot] = pipelineConstant;
                    if (_isBound)
                    {
                        ApplyGSConstantBuffer(slot, pipelineConstant);
                    }
                    break;
                case ConstantUsage.PixelShader:
                    _psConstants[slot] = pipelineConstant;
                    if (_isBound)
                    {
                        ApplyPSConstantBuffer(slot, pipelineConstant);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(usage));
            }
        }

        private unsafe void ApplyShaders()
        {
            DeviceContext.IASetPrimitiveTopology(_device.ContextPtr, (uint)_topology);
            DeviceContext.VSSetShader(_device.ContextPtr, _vertex, IntPtr.Zero, 0);
            DeviceContext.GSSetShader(_device.ContextPtr, _geometry, IntPtr.Zero, 0);
            DeviceContext.PSSetShader(_device.ContextPtr, _pixel, IntPtr.Zero, 0);
            fixed (Viewport* ptr = &_viewport)
            {
                DeviceContext.RSSetViewports(_device.ContextPtr, 1, ptr);
            }
        }

        private void ApplyBlender()
        {
            DeviceContext.OMSetBlendState(_device.ContextPtr, _blendPtr, IntPtr.Zero, 0xFFFFFFFF);
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
            ApplyBlender();
            //TODO Samplers
            //TODO depth
            foreach (var vsK in _vsConstants)
            {
                ApplyVSConstantBuffer(vsK.Key, vsK.Value);
            }
            foreach (var res in _resources)
            {
                ApplyPSResource(res.Key, res.Value);
            }
        }
    }
}
