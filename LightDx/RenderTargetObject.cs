using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    internal enum RenderTargetObjectSizeMode
    {
        Equal,
    }

    internal enum RenderTargetObjectType
    {
        SwapchainTarget,
        TextureTarget,
    }

    public sealed class RenderTargetObject : IDisposable
    {
        private LightDevice _device;
        private bool _disposed;

        private RenderTargetObjectType _type;
        private bool _isDepthStencil;
        private IntPtr _TexturePtr;
        private IntPtr _viewPtrTarget;
        private IntPtr _viewPtrResource;

        internal RenderTargetObjectType TargetType => _type;
        internal IntPtr ViewPtr => _viewPtrTarget;
        internal bool IsDepthStencil => _isDepthStencil;

        internal LightDevice Device => _device;
        public Vector4 ClearColor { get; set; }

        //swapchain target: no arguments

        //texture target
        private int _format;
        private RenderTargetObjectSizeMode _sizeMode;
        private Texture2D _textureObj;

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

        internal static RenderTargetObject CreateTextureTarget(LightDevice device, int format)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.TextureTarget;
            ret._isDepthStencil = false;
            ret._format = format;
            ret._sizeMode = RenderTargetObjectSizeMode.Equal;
            ret._textureObj = new Texture2D(device, IntPtr.Zero, IntPtr.Zero, 0, 0, false);
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
            NativeHelper.Dispose(ref _TexturePtr);
            NativeHelper.Dispose(ref _viewPtrTarget);
            NativeHelper.Dispose(ref _viewPtrResource);

            if (disposing)
            {
                _device.ReleaseRenderTargets -= ReleaseView;
                _device.RebuildRenderTargets -= RebuildView;

                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal void ReleaseView()
        {
            if (_textureObj != null)
            {
                _textureObj.UpdatePointer(0, 0, IntPtr.Zero, IntPtr.Zero);
            }
            NativeHelper.Dispose(ref _TexturePtr);
            NativeHelper.Dispose(ref _viewPtrTarget);
            NativeHelper.Dispose(ref _viewPtrResource);
        }

        internal void RebuildView()
        {
            if (_type == RenderTargetObjectType.SwapchainTarget)
            {
                _viewPtrTarget = _device.DefaultRenderView.AddRef();
            }
            else
            {
                int width = _device.ScreenWidth;
                int height = _device.ScreenHeight;

                if (_isDepthStencil)
                {
                    if (_sizeMode != RenderTargetObjectSizeMode.Equal)
                    {
                        throw new InvalidOperationException();
                    }
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
                    RebuildViewStencilInternal(ref desc);
                }
                else
                {
                    int texWidth = width;
                    int texHeight = height;

                    Texture2DDescription desc = new Texture2DDescription
                    {
                        Width = (uint)texWidth,
                        Height = (uint)texHeight,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = (uint)_format,
                        SampleCount = 1,
                        SampleQuality = 0,
                        Usage = 0, //Default
                        BindFlags = 0x28, //RenderTarget+ShaderResource
                        CPUAccessFlags = 0,
                        MiscFlags = 0,
                    };
                    RebuildViewTextureInternal(ref desc);
                    _textureObj.UpdatePointer(texWidth, texHeight, _TexturePtr, _viewPtrResource);
                }
            }
        }

        //Note: The following 2 methods are separated from RebuildView to avoid fat method to be modified by Mono,
        //which when doing so will corrupt the binary for unknown reason.

        private void RebuildViewStencilInternal(ref Texture2DDescription desc)
        {
            using (var depthTex = new ComScopeGuard())
            {
                Natives.Device.CreateTexture2D(_device.DevicePtr, ref desc, IntPtr.Zero, out depthTex.Ptr).Check();
                Natives.Device.CreateDepthStencilView(_device.DevicePtr, depthTex.Ptr, IntPtr.Zero, out _viewPtrTarget).Check();
                _TexturePtr = depthTex.Move();
            }
        }

        private void RebuildViewTextureInternal(ref Texture2DDescription desc)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), targetView = new ComScopeGuard(), resView = new ComScopeGuard())
            {
                Natives.Device.CreateTexture2D(_device.DevicePtr, ref desc, IntPtr.Zero, out tex.Ptr).Check();
                Natives.Device.CreateRenderTargetView(_device.DevicePtr, tex.Ptr, IntPtr.Zero, out targetView.Ptr).Check();
                Natives.Device.CreateShaderResourceView(_device.DevicePtr, tex.Ptr, IntPtr.Zero, out resView.Ptr).Check();
                _TexturePtr = tex.Move();
                _viewPtrTarget = targetView.Move();
                _viewPtrResource = resView.Move();
            }
        }

        public void Clear()
        {
            if (_isDepthStencil)
            {
                DeviceContext.ClearDepthStencilView(_device.ContextPtr, _viewPtrTarget, 1, 1.0f, 0);
            }
            else
            {
                Vector4 color = ClearColor;
                DeviceContext.ClearRenderTargetView(_device.ContextPtr, _viewPtrTarget, ref color);
            }
        }

        public Texture2D GetTexture2D()
        {
            if (_type != RenderTargetObjectType.TextureTarget || _isDepthStencil)
            {
                throw new InvalidOperationException();
            }
            return _textureObj;
        }
    }
}
