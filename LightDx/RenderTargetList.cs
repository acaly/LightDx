using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class RenderTargetList
    {
        private RenderTargetObject[] _renderTargets;
        private IntPtr[] _viewPtr; //The COM objects will be freed by RenderTargetObjects.
        private RenderTargetObject _depthStencil;

        public RenderTargetList(params RenderTargetObject[] renderTargetObjects)
        {
            if (renderTargetObjects.Length < 1)
            {
                throw new ArgumentException(nameof(renderTargetObjects));
            }
            LightDevice device = renderTargetObjects[0].Device;
            int renderTarget = 0, depthStencil = 0;
            if (renderTargetObjects[0].IsDepthStencil)
            {
                depthStencil += 1;
            }
            else
            {
                renderTarget += 1;
            }
            for (int i = 1; i < renderTargetObjects.Length; ++i)
            {
                if (renderTargetObjects[i].Device != device)
                {
                    throw new ArgumentException("target not from same device");
                }
                if (renderTargetObjects[i].IsDepthStencil)
                {
                    depthStencil += 1;
                }
                else
                {
                    renderTarget += 1;
                }
            }
            if (renderTarget == 0 || depthStencil > 1)
            {
                throw new ArgumentException("invalid target type");
            }
            _renderTargets = renderTargetObjects.Where(t => !t.IsDepthStencil).ToArray();
            _depthStencil = renderTargetObjects.FirstOrDefault(t => t.IsDepthStencil);
            _viewPtr = new IntPtr[_renderTargets.Length];
        }

        public unsafe void Apply()
        {
            var device = _renderTargets[0].Device;
            for (int i = 0; i < _renderTargets.Length; ++i)
            {
                _viewPtr[i] = _renderTargets[i].ViewPtr;
            }
            fixed (IntPtr* ptr = _viewPtr)
            {
                DeviceContext.OMSetRenderTargets(device.ContextPtr, (uint)_renderTargets.Length,
                    ptr, _depthStencil?.ViewPtr ?? IntPtr.Zero);
            }
            device.CurrentTarget = this;
        }

        public void ClearAll()
        {
            foreach (var t in _renderTargets)
            {
                t.Clear();
            }
            _depthStencil?.Clear();
        }
    }
}
