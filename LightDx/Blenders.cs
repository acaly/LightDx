using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public sealed class Blender
    {
        private readonly bool _alpha;

        private Blender(bool alpha)
        {
            _alpha = alpha;
        }

        internal unsafe IntPtr CreateBlenderForDevice(LightDevice device)
        {
            if (!_alpha)
            {
                return IntPtr.Zero;
            }
            BlendDescription d = new BlendDescription();
            d.RenderTarget0.BlendEnable = 1; //true
            d.RenderTarget0.SrcBlend = 5; //D3D11_BLEND_SRC_ALPHA
            d.RenderTarget0.DestBlend = 6; //D3D11_BLEND_INV_SRC_ALPHA
            d.RenderTarget0.BlendOp = 1; //D3D11_BLEND_OP_ADD
            d.RenderTarget0.SrcBlendAlpha = 2; //D3D11_BLEND_ONE
            d.RenderTarget0.DestBlendAlpha = 1; //D3D11_BLEND_ZERO
            d.RenderTarget0.BlendOpAlpha = 1; //D3D11_BLEND_OP_ADD
            d.RenderTarget0.RenderTargetWriteMask = 15; //D3D11_COLOR_WRITE_ENABLE_ALL
            Device.CreateBlendState(device.DevicePtr, new IntPtr(&d), out var ret).Check();
            return ret;
        }

        public static readonly Blender Default = new Blender(false);
        public static readonly Blender AlphaBlender = new Blender(true);
    }
}
