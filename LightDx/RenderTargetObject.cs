using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal enum RenderTargetObjectSizeMode
    {
        Equal,
        Ratio,
        Fixed,
    }

    internal enum RenderTargetObjectType
    {
        SwapchainTarget,
        TextureTarget,
    }

    public class RenderTargetObject : IDisposable
    {
        private LightDevice _device;
        private bool _disposed;

        private RenderTargetObjectType _type;
        private bool _isDepthStencil;
        private IntPtr _viewPtr;

        internal RenderTargetObjectType TargetType => _type;
        internal IntPtr ViewPtr => _viewPtr;
        internal bool IsDepthStencil => _isDepthStencil;

        internal LightDevice Device => _device;
        public Float4 ClearColor { get; set; }

        //swapchain target: no arguments

        //texture target
        private int _format;
        private float _ratioX, _ratioY;
        private RenderTargetObjectSizeMode _sizeMode;

        internal static RenderTargetObject CreateSwapchainTarget(LightDevice device)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.SwapchainTarget;
            ret._isDepthStencil = false;
            ret.RebuildView();
            return ret;
        }

        internal static RenderTargetObject CreateDepthStencilTarget(LightDevice device, int format)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.TextureTarget;
            ret._isDepthStencil = true;
            ret._format = format;
            ret._sizeMode = RenderTargetObjectSizeMode.Equal;
            ret.RebuildView();
            return ret;
        }

        private RenderTargetObject(LightDevice device)
        {
            _device = device;
            device.AddComponent(this);
            device.ReleaseRenderTargets += ReleaseView;
            device.RebuildRenderTargets += RebuildView;
        }

        ~RenderTargetObject()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            NativeHelper.Dispose(ref _viewPtr);
            _device.ReleaseRenderTargets -= ReleaseView;
            _device.RebuildRenderTargets -= RebuildView;

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        internal void ReleaseView()
        {
            NativeHelper.Dispose(ref _viewPtr);
        }

        internal void RebuildView()
        {
            if (_type == RenderTargetObjectType.SwapchainTarget)
            {
                _viewPtr = _device.DefaultRenderView.AddRef();
            }
            else
            {
                int width = _device.ScreenWidth;
                int height = _device.ScreenHeight;

                if (_isDepthStencil)
                {
                    Texture2DDescription desc = new Texture2DDescription
                    {
                        Width = (uint)width,
                        Height = (uint)height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = (uint)_format,
                        SampleCount = 1,
                        SampleQuality = 0,
                        Usage = 0, //Default
                        BindFlags = 64, //DepthStencil
                        CPUAccessFlags = 0,
                        MiscFlags = 0,
                    };
                    using (ComScopeGuard depthTex = new ComScopeGuard(), depthView = new ComScopeGuard())
                    {
                        Natives.Device.CreateTexture2D(_device.DevicePtr, ref desc, IntPtr.Zero, out depthTex.Ptr).Check();
                        Natives.Device.CreateDepthStencilView(_device.DevicePtr, depthTex.Ptr, IntPtr.Zero, out depthView.Ptr).Check();
                        _viewPtr = depthView.Move();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public void Clear()
        {
            if (_isDepthStencil)
            {
                DeviceContext.ClearDepthStencilView(_device.ContextPtr, _viewPtr, 1, 1.0f, 0);
            }
            else
            {
                Float4 color = ClearColor;
                DeviceContext.ClearRenderTargetView(_device.ContextPtr, _viewPtr, ref color);
            }
        }
    }
}
