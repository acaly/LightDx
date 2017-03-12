using LightDX.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDX
{
    public class RenderTarget : IDisposable
    {
        private readonly LightDevice _Device;

        private bool _Disposed;

        private IntPtr[] _TargetView;
        private IntPtr _DepthStencil;

        private RenderTarget(LightDevice device)
        {
            _Device = device;
            device.AddComponent(this);
        }

        internal RenderTarget(LightDevice device, IntPtr[] targetView, IntPtr depthStencil)
        {
            _Device = device;
            device.AddComponent(this);

            _TargetView = targetView;
            _DepthStencil = depthStencil;
        }

        public RenderTarget Clone()
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }

            var ret = new RenderTarget(_Device);

            ret._TargetView = _TargetView.Select(v => v.AddRef()).ToArray();
            ret._DepthStencil = _DepthStencil.AddRef();

            return ret;
        }

        public void Merge(RenderTarget t)
        {
            if (_Disposed || t._Disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            if (_DepthStencil != null && t._DepthStencil != null)
            {
                throw new InvalidOperationException("Merge two targets both with DepthStencil");
            }

            if (t._DepthStencil != null)
            {
                _DepthStencil = t._DepthStencil.AddRef();
            }
            _TargetView = _TargetView.Concat(t._TargetView.Select(v => v.AddRef())).ToArray();

            t.Dispose();
        }

        ~RenderTarget()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }

            NativeHelper.Dispose(ref _TargetView);
            NativeHelper.Dispose(ref _DepthStencil);

            _Disposed = true;
            _Device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public unsafe void Apply()
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            fixed (IntPtr* ptr = _TargetView)
            {
                DeviceContext.OMSetRenderTargets(_Device.ContextPtr, (uint)_TargetView.Length, ptr, _DepthStencil);
            }
        }

        public void ClearAll(Float4 color)
        {
            if (_Disposed)
            {
                throw new ObjectDisposedException("RenderTarget");
            }
            foreach (var v in _TargetView)
            {
                DeviceContext.ClearRenderTargetView(_Device.ContextPtr, v, ref color);
            }
            if (_DepthStencil != IntPtr.Zero)
            {
                DeviceContext.ClearDepthStencilView(_Device.ContextPtr, _DepthStencil, 1, 1.0f, 0);
            }
        }
    }
}
