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
        private int _texWidth, _texHeight;

        internal RenderTargetObjectType TargetType => _type;
        internal IntPtr ViewPtr => _viewPtrTarget;
        internal bool IsDepthStencil => _isDepthStencil;

        internal LightDevice Device => _device;
        public Vector4 ClearColor { get; set; }

        //swapchain target: no arguments

        //texture target
        private int _formatTexture, _formatTarget, _formatResource;
        private Texture2D _textureObj;

        internal static RenderTargetObject CreateSwapchainTarget(LightDevice device)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.SwapchainTarget;
            ret._isDepthStencil = false;
            ret.RebuildView();
            return ret;
        }

        internal static RenderTargetObject CreateDepthStencilTarget(LightDevice device,
            int formatTexture, int formatTarget, int formatResource)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.TextureTarget;
            ret._isDepthStencil = true;
            ret._formatTexture = formatTexture;
            ret._formatTarget = formatTarget;
            ret._formatResource = formatResource;
            ret.RebuildView();
            return ret;
        }

        internal static RenderTargetObject CreateTextureTarget(LightDevice device, int format)
        {
            var ret = new RenderTargetObject(device);
            ret._type = RenderTargetObjectType.TextureTarget;
            ret._isDepthStencil = false;
            ret._formatTexture = format;
            ret._formatTarget = format;
            ret._formatResource = format;
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
                _texWidth = _device.ScreenWidth;
                _texHeight = _device.ScreenHeight;

                Texture2DDescription desc = new Texture2DDescription
                {
                    Width = (uint)_texWidth,
                    Height = (uint)_texHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = (uint)_formatTexture,
                    SampleCount = 1,
                    SampleQuality = 0,
                    Usage = 0, //Default
                    CPUAccessFlags = 0,
                    MiscFlags = 0,
                };
                if (_isDepthStencil)
                {
                    desc.BindFlags = 64 + 8; //DepthStencil+ShaderResource
                    RebuildViewStencilInternal(ref desc);
                }
                else
                {
                    desc.BindFlags = 32 + 8; //RenderTarget+ShaderResource
                    RebuildViewTextureInternal(ref desc);
                }
            }
        }

        //Note: The following 2 methods are separated from RebuildView to avoid fat method to be modified by Mono.Cecil,
        //which when doing so will corrupt the binary for unknown reason.

        private unsafe void RebuildViewStencilInternal(ref Texture2DDescription desc)
        {
            using (var depthTex = new ComScopeGuard())
            {
                int* depthStencilDesc = stackalloc int[6]
                {
                    _formatTarget,
                    3, //D3D11_DSV_DIMENSION_TEXTURE2D
                    0, //Flags = 0
                    0, //MipSlice = 0
                    0,
                    0,
                };
                Natives.Device.CreateTexture2D(_device.DevicePtr, ref desc, IntPtr.Zero, out depthTex.Ptr).Check();
                Natives.Device.CreateDepthStencilView(_device.DevicePtr, depthTex.Ptr, depthStencilDesc, out _viewPtrTarget).Check();
                _TexturePtr = depthTex.Move();
            }
            RebuildResourceView();
        }

        private unsafe void RebuildViewTextureInternal(ref Texture2DDescription desc)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), targetView = new ComScopeGuard(), resView = new ComScopeGuard())
            {
                int* renderTargetDesc = stackalloc int[5]
                {
                    _formatTarget,
                    4, //D3D11_RTV_DIMENSION_TEXTURE2D
                    0, //MipSlice = 0
                    0, //Not used in tex2d
                    0, //Not used in tex2d
                };
                Natives.Device.CreateTexture2D(_device.DevicePtr, ref desc, IntPtr.Zero, out tex.Ptr).Check();
                Natives.Device.CreateRenderTargetView(_device.DevicePtr, tex.Ptr, renderTargetDesc, out targetView.Ptr).Check();
                _TexturePtr = tex.Move();
                _viewPtrTarget = targetView.Move();
            }
            RebuildResourceView();
        }

        private unsafe void RebuildResourceView()
        {
            if (_textureObj == null)
            {
                return;
            }
            int* resourceDesc = stackalloc int[5]
            {
                _formatResource,
                4, //D3D11_SRV_DIMENSION_TEXTURE2D
                0, //MostDetailedMip = 0
                1, //MipLevels = 1
                0, //Not used in tex2d
            };
            Natives.Device.CreateShaderResourceView(_device.DevicePtr, _TexturePtr, resourceDesc, out _viewPtrResource).Check();
            _textureObj.UpdatePointer(_texWidth, _texHeight, _TexturePtr, _viewPtrResource);
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
            if (_type != RenderTargetObjectType.TextureTarget)
            {
                throw new InvalidOperationException();
            }
            if (_textureObj == null)
            {
                _textureObj = new Texture2D(_device, IntPtr.Zero, IntPtr.Zero, 0, 0, false);
                RebuildResourceView();
            }
            return _textureObj;
        }
    }
}
