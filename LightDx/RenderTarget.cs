using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public class RenderTarget : IDisposable
    {
        private readonly LightDevice _device;

        private bool _disposed;

        private IntPtr[] _targetView;
        private IntPtr _depthStencil;

        private RenderTarget(LightDevice device)
        {
            _device = device;
            device.AddComponent(this);
        }

        internal RenderTarget(LightDevice device, IntPtr[] targetView, IntPtr depthStencil)
        {
            _device = device;
            device.AddComponent(this);

            _targetView = targetView;
            _depthStencil = depthStencil;
        }

        public RenderTarget Clone()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }

            var ret = new RenderTarget(_device);

            ret._targetView = _targetView.Select(v => v.AddRef()).ToArray();
            ret._depthStencil = _depthStencil.AddRef();

            return ret;
        }

        public void Merge(RenderTarget t)
        {
            if (_disposed || t._disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            if (_depthStencil != null && t._depthStencil != null)
            {
                throw new InvalidOperationException("Merge two targets both with DepthStencil");
            }

            if (t._depthStencil != null)
            {
                _depthStencil = t._depthStencil.AddRef();
            }
            _targetView = _targetView.Concat(t._targetView.Select(v => v.AddRef())).ToArray();

            t.Dispose();
        }

        ~RenderTarget()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            NativeHelper.Dispose(ref _targetView);
            NativeHelper.Dispose(ref _depthStencil);

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public unsafe void Apply()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            fixed (IntPtr* ptr = _targetView)
            {
                DeviceContext.OMSetRenderTargets(_device.ContextPtr, (uint)_targetView.Length, ptr, _depthStencil);
            }
        }

        public void ClearAll(Float4 color)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            foreach (var v in _targetView)
            {
                DeviceContext.ClearRenderTargetView(_device.ContextPtr, v, ref color);
            }
            if (_depthStencil != IntPtr.Zero)
            {
                DeviceContext.ClearDepthStencilView(_device.ContextPtr, _depthStencil, 1, 1.0f, 0);
            }
        }
    }
}
