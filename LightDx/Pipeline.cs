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
        private readonly LightDevice _Device;
        private bool _Disposed;

        private IntPtr _Vertex, _Geometry, _Pixel;
        private IntPtr _SignatureBlob;

        private IntPtr _BlendPtr;

        private readonly InputTopology _Topology;

        //only 1 viewport
        private Viewport _Viewport;

        private Dictionary<int, AbstractPipelineConstant> _Constants = new Dictionary<int, AbstractPipelineConstant>();
        private Dictionary<int, Texture2D> _Resources = new Dictionary<int, Texture2D>();

        internal Pipeline(LightDevice device, IntPtr v, IntPtr g, IntPtr p, IntPtr sign, Viewport vp, InputTopology topology)
        {
            _Device = device;
            device.AddComponent(this);

            _Vertex = v;
            _Geometry = g;
            _Pixel = p;
            _SignatureBlob = sign;

            _Viewport = vp;
            _Topology = topology;
        }

        ~Pipeline()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }

            NativeHelper.Dispose(ref _Vertex);
            NativeHelper.Dispose(ref _Geometry);
            NativeHelper.Dispose(ref _Pixel);
            NativeHelper.Dispose(ref _SignatureBlob);
            NativeHelper.Dispose(ref _BlendPtr);

            foreach (var c in _Constants)
            {
                c.Value.Dispose();
            }
            _Constants.Clear();

            _Disposed = true;
            _Device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public PipelineConstant<T> CreateConstantBuffer<T>(ConstantBufferUsage usage, int slot)
            where T : struct
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            throw new NotImplementedException();
        }

        public unsafe InputDataProcessor<T> CreateInputDataProcessor<T>()
            where T : struct
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            var layoutDecl = InputDataProcessor<T>.CreateLayoutFromType();
            using (var layout = new ComScopeGuard())
            {
                fixed (InputElementDescription* d = layoutDecl)
                {
                    Device.CreateInputLayout(_Device.DevicePtr, d, (uint)layoutDecl.Length,
                        Blob.GetBufferPointer(_SignatureBlob), Blob.GetBufferSize(_SignatureBlob), out layout.Ptr).Check();
                }
                return new InputDataProcessor<T>(_Device, layout.Move());
            }
        }

        public void SetResource(int slot, Texture2D tex)
        {
            if (tex == null)
            {
                _Resources.Remove(slot);
            }
            else
            {
                _Resources[slot] = tex;
            }
        }

        public void SetBlender(Blender b)
        {
            NativeHelper.Dispose(ref _BlendPtr);
            _BlendPtr = b.CreateBlenderForDevice(_Device);
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
            if (_Disposed)
            {
                throw new ObjectDisposedException("Pipeline");
            }
            DeviceContext.IASetPrimitiveTopology(_Device.ContextPtr, (uint)_Topology);
            DeviceContext.VSSetShader(_Device.ContextPtr, _Vertex, IntPtr.Zero, 0);
            DeviceContext.GSSetShader(_Device.ContextPtr, _Geometry, IntPtr.Zero, 0);
            DeviceContext.PSSetShader(_Device.ContextPtr, _Pixel, IntPtr.Zero, 0);
            fixed (Viewport* ptr = &_Viewport)
            {
                DeviceContext.RSSetViewports(_Device.ContextPtr, 1, ptr);
            }
            DeviceContext.OMSetBlendState(_Device.ContextPtr, _BlendPtr, IntPtr.Zero, 0xFFFFFFFF);
            //TODO Samplers
            //TODO setup constant buffer
            //TODO depth
            ApplyResources();
        }

        internal void ApplyResources()
        {
            foreach (var res in _Resources)
            {
                IntPtr view = res.Value.ViewPtr;
                DeviceContext.PSSetShaderResources(_Device.ContextPtr, (uint)res.Key, 1, ref view);
            }
        }
    }
}
