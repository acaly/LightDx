using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx.Natives
{
    internal unsafe struct DXGIAdapterDescription
    {
        public fixed char Description[128];
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public ulong AdapterLuid;
    }

    internal struct SwapChainDescription
    {
        //DXGI_MODE_DESC BufferDesc;
        public uint BufferWidth;
        public uint BufferHeight;
        public uint RefreshRateNumerator;
        public uint RefreshRateDenominator;
        public uint BufferFormat;
        public uint ScanlineOrdering;
        public uint Scaling;
        //DXGI_SAMPLE_DESC SampleDesc;
        public uint SampleCount;
        public uint SampleQuality;
        //DXGI_USAGE BufferUsage;
        public uint BufferUsage;
        //UINT BufferCount;
        public uint BufferCount;
        //HWND OutputWindow;
        public IntPtr OutputWindow;
        //BOOL Windowed;
        public int Windowed;
        //DXGI_SWAP_EFFECT SwapEffect;
        public uint SwapEffect;
        //UINT Flags;
        public uint Flags;

        public SwapChainDescription(IntPtr hWnd, int width, int height)
        {
            BufferWidth = (uint)width;
            BufferHeight = (uint)height;
            RefreshRateNumerator = 60;
            RefreshRateDenominator = 1;
            BufferFormat = 28; //R8G8B8A8_UNorm
            ScanlineOrdering = 0;
            Scaling = 0;
            SampleCount = 1;
            SampleQuality = 0;
            BufferUsage = 32; //RenderTargetOutput
            BufferCount = 1;
            OutputWindow = hWnd;
            Windowed = 1;
            SwapEffect = 0; //Discard
            Flags = 0;
        }
    }

    internal struct Viewport
    {
        public float TopLeftX;
        public float TopLeftY;
        public float Width;
        public float Height;
        public float MinDepth;
        public float MaxDepth;
    }

    internal struct InputElementDescription
    {
        public IntPtr SemanticName;
        public int SemanticIndex;
        public int Format;
        public int Slot;
        public int AlignedByteOffset;
        public int Classification;
        public int InstanceDataStepRate;
    }

    internal struct BufferDescription
    {
        public uint ByteWidth;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
        public uint StructureByteStride;
    }

    internal unsafe struct DataBox
    {
        public void* DataPointer;
        public int RowPitch;
        public int SlicePitch;
    }

    internal struct Texture2DDescription
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public uint SampleCount;
        public uint SampleQuality;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    internal unsafe struct SubresourceData
    {
        public IntPtr pSysMem;
        public uint SysMemPitch;
        public uint SysMemSlicePitch;
    }

    internal struct RenderTargetBlendDescription
    {
        public int BlendEnable;
        public int SrcBlend;
        public int DestBlend;
        public int BlendOp;
        public int SrcBlendAlpha;
        public int DestBlendAlpha;
        public int BlendOpAlpha;
        public byte RenderTargetWriteMask; //should be byte, use int for padding?
    }

    internal struct BlendDescription
    {
        public int AlphaToCoverageEnable;
        public int IndependentBlendEnable;
        public RenderTargetBlendDescription RenderTarget0;
        public RenderTargetBlendDescription RenderTarget1;
        public RenderTargetBlendDescription RenderTarget2;
        public RenderTargetBlendDescription RenderTarget3;
        public RenderTargetBlendDescription RenderTarget4;
        public RenderTargetBlendDescription RenderTarget5;
        public RenderTargetBlendDescription RenderTarget6;
        public RenderTargetBlendDescription RenderTarget7;
    }
    
    internal class Native
    {
        [DllImport("d3d11.dll")]
        public static extern uint D3D11CreateDeviceAndSwapChain(
            IntPtr pAdapter, //null
            uint DriverType, //hardware(1)
            IntPtr Software, //null
            uint Flags, //debug(2) (? or 0)
            IntPtr pFeatureLevels, //null
            uint FeatureLevels, //0
            uint SDKVersion, // D3D11_SDK_VERSION(7)
            ref SwapChainDescription pSwapChainDesc,
            out IntPtr ppSwapChain,
            out IntPtr ppDevice,
            out uint pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("d3dcompiler_47.dll")]
        public unsafe static extern uint D3DCompile(
            void* pSrcData,
            int SrcDataSize,
            IntPtr pSourceName,
            IntPtr pDefines,
            IntPtr pInclude,
            IntPtr pEntrypoint,
            IntPtr pTarget,
            uint Flags1,
            uint Flags2,
            out IntPtr ppCode,
            out IntPtr ppErrorMsgs);

        [DllImport("d3dcompiler_47.dll")]
        public static extern int D3DGetInputSignatureBlob(IntPtr d, IntPtr len, out IntPtr r);

        [DllImport("DXGI.dll")]
        public static extern uint CreateDXGIFactory(IntPtr guid, out IntPtr r);
    }

    internal static class Device
    {
        public unsafe delegate uint CreateRenderTargetViewDelegate(IntPtr @this,
            IntPtr pResource, int* desc, out IntPtr r);
        public static readonly CreateRenderTargetViewDelegate CreateRenderTargetView =
            CalliGenerator.GetCalliDelegate<CreateRenderTargetViewDelegate>(9);
        
        public delegate uint CreateShaderDelegate(IntPtr @this,
            IntPtr data, IntPtr size, IntPtr linkage, out IntPtr r);
        public static readonly CreateShaderDelegate CreateVertexShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(12);
        public static readonly CreateShaderDelegate CreateGeometryShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(13);
        public static readonly CreateShaderDelegate CreatePixelShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(15);
        
        public unsafe delegate uint CreateInputLayoutDelegate(IntPtr @this,
            InputElementDescription* elementDesc, uint nElementDesc,
            IntPtr signature, IntPtr sizeSignature, out IntPtr r);
        public static readonly CreateInputLayoutDelegate CreateInputLayout =
            CalliGenerator.GetCalliDelegate<CreateInputLayoutDelegate>(11);
        
        public unsafe delegate uint CreateBufferDelegate(IntPtr @this,
            void* d, void* data, out IntPtr r);
        public static readonly CreateBufferDelegate CreateBuffer =
            CalliGenerator.GetCalliDelegate<CreateBufferDelegate>(3);
        
        public delegate uint CreateTexture2DDelegate(IntPtr @this,
            ref Texture2DDescription d, IntPtr data, out IntPtr r);
        public static readonly CreateTexture2DDelegate CreateTexture2D =
            CalliGenerator.GetCalliDelegate<CreateTexture2DDelegate>(5);
        
        public unsafe delegate uint CreateShaderResourceViewDelegate(IntPtr @this,
            IntPtr resource, int* desc, out IntPtr r);
        public static readonly CreateShaderResourceViewDelegate CreateShaderResourceView =
            CalliGenerator.GetCalliDelegate<CreateShaderResourceViewDelegate>(7);
        
        public unsafe delegate uint CreateDepthStencilViewDelegate(IntPtr @this,
            IntPtr resource, int* desc, out IntPtr r);
        public static readonly CreateDepthStencilViewDelegate CreateDepthStencilView =
            CalliGenerator.GetCalliDelegate<CreateDepthStencilViewDelegate>(10);
        
        public delegate uint CreateBlendStateDelegate(IntPtr @this,
            IntPtr option, out IntPtr r);
        public static readonly CreateBlendStateDelegate CreateBlendState =
            CalliGenerator.GetCalliDelegate<CreateBlendStateDelegate>(20);
    }

    internal static class SwapChain
    {
        public delegate uint GetBufferDelegate(IntPtr @this,
            int i, IntPtr guid, out IntPtr r);
        public static readonly GetBufferDelegate GetBuffer =
            CalliGenerator.GetCalliDelegate<GetBufferDelegate>(9);
        
        public delegate uint PresentDelegate(IntPtr @this,
            int i, int j);
        public static readonly PresentDelegate Present =
            CalliGenerator.GetCalliDelegate<PresentDelegate>(8);

        public delegate uint ResizeBuffersDelegate(IntPtr @this,
            uint count, uint width, uint height, uint format, uint flags);
        public static readonly ResizeBuffersDelegate ResizeBuffers =
            CalliGenerator.GetCalliDelegate<ResizeBuffersDelegate>(13);
    }

    internal static class Factory
    {
        public delegate uint EnumAdaptersDelegate(IntPtr @this,
            uint index, out IntPtr r);
        public static readonly EnumAdaptersDelegate EnumAdapters =
            CalliGenerator.GetCalliDelegate<EnumAdaptersDelegate>(7);
        
    }

    internal static class Adapter
    {
        public delegate uint EnumOutputsDelegate(IntPtr @this,
            uint index, out IntPtr r);
        public static readonly EnumOutputsDelegate EnumOutputs =
            CalliGenerator.GetCalliDelegate<EnumOutputsDelegate>(7);

        public unsafe delegate uint GetDescDelegate(IntPtr @this, void* desc);
        public static GetDescDelegate GetDesc = CalliGenerator.GetCalliDelegate<GetDescDelegate>(8);
    }

    internal static class Output
    {
        public delegate uint WaitForVerticalBlankDelegate(IntPtr @this);
        public static readonly WaitForVerticalBlankDelegate WaitForVerticalBlank =
            CalliGenerator.GetCalliDelegate<WaitForVerticalBlankDelegate>(10);
    }

    internal static class DeviceContext
    {
        public unsafe delegate void OMSetRenderTargetsDelegate(IntPtr @this,
            uint num, IntPtr* renderTarget, IntPtr depthStencil);
        public static readonly OMSetRenderTargetsDelegate OMSetRenderTargets =
            CalliGenerator.GetCalliDelegate<OMSetRenderTargetsDelegate>(33);
        
        public unsafe delegate void RSSetViewportsDelegate(IntPtr @this,
            uint num, Viewport* viewport);
        public static readonly RSSetViewportsDelegate RSSetViewports =
            CalliGenerator.GetCalliDelegate<RSSetViewportsDelegate>(44);
        
        public delegate void IASetInputLayoutDelegate(IntPtr @this, IntPtr layout);
        public static readonly IASetInputLayoutDelegate IASetInputLayout =
            CalliGenerator.GetCalliDelegate<IASetInputLayoutDelegate>(17);
        
        public delegate void IASetPrimitiveTopologyDelegate(IntPtr @this, uint layout);
        public static readonly IASetPrimitiveTopologyDelegate IASetPrimitiveTopology =
            CalliGenerator.GetCalliDelegate<IASetPrimitiveTopologyDelegate>(24);
        
        public unsafe delegate void IASetVertexBuffersDelegate(IntPtr @this,
            uint slot, uint number, void* vbs, void* strides, void* offsets);
        public static readonly IASetVertexBuffersDelegate IASetVertexBuffers =
            CalliGenerator.GetCalliDelegate<IASetVertexBuffersDelegate>(18);
        
        public unsafe delegate void IASetIndexBufferDelegate(IntPtr @this,
            IntPtr buffer, uint format, uint offset);
        public static readonly IASetIndexBufferDelegate IASetIndexBuffer =
            CalliGenerator.GetCalliDelegate<IASetIndexBufferDelegate>(19);
        
        public delegate void SetShaderDelegate(IntPtr @this,
            IntPtr vs, IntPtr ci, uint nci);
        public static readonly SetShaderDelegate VSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(11);
        public static readonly SetShaderDelegate GSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(23);
        public static readonly SetShaderDelegate PSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(9);
        
        public unsafe delegate void ClearRenderTargetViewDelegate(IntPtr @this,
            IntPtr view, ref Vector4 color);
        public static readonly ClearRenderTargetViewDelegate ClearRenderTargetView =
            CalliGenerator.GetCalliDelegate<ClearRenderTargetViewDelegate>(50);
        
        public delegate void DrawDelegate(IntPtr @this,
            uint count, uint start);
        public static readonly DrawDelegate Draw =
            CalliGenerator.GetCalliDelegate<DrawDelegate>(13);

        public delegate void DrawIndexedDelegate(IntPtr @this,
            uint count, uint start, int vertexBase);
        public static readonly DrawIndexedDelegate DrawIndexed =
            CalliGenerator.GetCalliDelegate<DrawIndexedDelegate>(12);

        public delegate void SetShaderResourcesDelegate(IntPtr @this,
            uint slot, uint num, ref IntPtr view);
        public static readonly SetShaderResourcesDelegate VSSetShaderResources =
            CalliGenerator.GetCalliDelegate<SetShaderResourcesDelegate>(25);
        public static readonly SetShaderResourcesDelegate GSSetShaderResources =
            CalliGenerator.GetCalliDelegate<SetShaderResourcesDelegate>(31);
        public static readonly SetShaderResourcesDelegate PSSetShaderResources =
            CalliGenerator.GetCalliDelegate<SetShaderResourcesDelegate>(8);

        public delegate uint ClearDepthStencilViewDelegate(IntPtr @this,
            IntPtr view, uint flags, float depth, byte stencil);
        public static readonly ClearDepthStencilViewDelegate ClearDepthStencilView =
            CalliGenerator.GetCalliDelegate<ClearDepthStencilViewDelegate>(53);
        
        public unsafe delegate uint MapDelegate(IntPtr @this,
            IntPtr r, int subres, int t, int f, void* ret);
        public static readonly MapDelegate Map =
            CalliGenerator.GetCalliDelegate<MapDelegate>(14);
        
        public unsafe delegate void UnmapDelegate(IntPtr @this,
            IntPtr r, int subres);
        public static readonly UnmapDelegate Unmap =
            CalliGenerator.GetCalliDelegate<UnmapDelegate>(15);

        public unsafe delegate void UpdateSubresourceDelegate(IntPtr @this,
            IntPtr r, int subres, uint* box, void* data, int p1, int p2);
        public static readonly UpdateSubresourceDelegate UpdateSubresource =
            CalliGenerator.GetCalliDelegate<UpdateSubresourceDelegate>(48);

        public delegate void OMSetBlendStateDelegate(IntPtr @this,
            IntPtr b, IntPtr color, uint mask);
        public static readonly OMSetBlendStateDelegate OMSetBlendState =
            CalliGenerator.GetCalliDelegate<OMSetBlendStateDelegate>(35);

        public delegate void SetConstantBuffersDelegate(IntPtr @this, uint start, uint num, ref IntPtr ptr);
        public static readonly SetConstantBuffersDelegate VSSetConstantBuffers =
            CalliGenerator.GetCalliDelegate<SetConstantBuffersDelegate>(7);
        public static readonly SetConstantBuffersDelegate PSSetConstantBuffers =
           CalliGenerator.GetCalliDelegate<SetConstantBuffersDelegate>(16);
        public static readonly SetConstantBuffersDelegate GSSetConstantBuffers =
           CalliGenerator.GetCalliDelegate<SetConstantBuffersDelegate>(22);
    }

    internal static class Blob
    {
        public delegate IntPtr GetBufferPointerDelegate(IntPtr @this);
        public static readonly GetBufferPointerDelegate GetBufferPointer =
            CalliGenerator.GetCalliDelegate<GetBufferPointerDelegate>(3);
        public static readonly GetBufferPointerDelegate GetBufferSize =
            CalliGenerator.GetCalliDelegate<GetBufferPointerDelegate>(4);
    }
}
