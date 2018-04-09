using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public enum ConstantBufferUsage
    {
        VertexShader = 1,
        //other shaders not supported
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

        private Dictionary<int, AbstractPipelineConstant> _constants = new Dictionary<int, AbstractPipelineConstant>();
        private Dictionary<int, Texture2D> _resources = new Dictionary<int, Texture2D>();

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

            foreach (var c in _constants)
            {
                c.Value.Dispose();
            }
            _constants.Clear();

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public PipelineConstant<T> CreateConstantBuffer<T>(ConstantBufferUsage usage, int slot)
            where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            throw new NotImplementedException();
        }

        public unsafe InputDataProcessor<T> CreateInputDataProcessor<T>()
            where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            var layoutDecl = InputDataProcessor<T>.CreateLayoutFromType();
            using (var layout = new ComScopeGuard())
            {
                fixed (InputElementDescription* d = layoutDecl)
                {
                    Device.CreateInputLayout(_device.DevicePtr, d, (uint)layoutDecl.Length,
                        Blob.GetBufferPointer(_signatureBlob), Blob.GetBufferSize(_signatureBlob), out layout.Ptr).Check();
                }
                return new InputDataProcessor<T>(_device, layout.Move());
            }
        }

        public void SetResource(int slot, Texture2D tex)
        {
            if (tex == null)
            {
                _resources.Remove(slot);
            }
            else
            {
                _resources[slot] = tex;
            }
        }

        public void SetBlender(Blender b)
        {
            NativeHelper.Dispose(ref _blendPtr);
            _blendPtr = b.CreateBlenderForDevice(_device);
        }

        public void SetSampler(int slot, Sampler s)
        {
            throw new NotImplementedException();
        }

        public void SetDepthTest(DepthTest d)
        {
            throw new NotImplementedException();
        }

        public unsafe void Apply()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            DeviceContext.IASetPrimitiveTopology(_device.ContextPtr, (uint)_topology);
            DeviceContext.VSSetShader(_device.ContextPtr, _vertex, IntPtr.Zero, 0);
            DeviceContext.GSSetShader(_device.ContextPtr, _geometry, IntPtr.Zero, 0);
            DeviceContext.PSSetShader(_device.ContextPtr, _pixel, IntPtr.Zero, 0);
            fixed (Viewport* ptr = &_viewport)
            {
                DeviceContext.RSSetViewports(_device.ContextPtr, 1, ptr);
            }
            DeviceContext.OMSetBlendState(_device.ContextPtr, _blendPtr, IntPtr.Zero, 0xFFFFFFFF);
            //TODO Samplers
            //TODO setup constant buffer
            //TODO depth
            ApplyResources();
        }

        internal void ApplyResources()
        {
            foreach (var res in _resources)
            {
                IntPtr view = res.Value.ViewPtr;
                DeviceContext.PSSetShaderResources(_device.ContextPtr, (uint)res.Key, 1, ref view);
            }
        }
    }
}
