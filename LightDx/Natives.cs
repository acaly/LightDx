using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx.Natives
{
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
        public PixelFormat Format;
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

    internal enum PixelFormat : uint
    {
        DXGI_FORMAT_UNKNOWN = 0,
        DXGI_FORMAT_R32G32B32A32_TYPELESS = 1,
        DXGI_FORMAT_R32G32B32A32_FLOAT = 2,
        DXGI_FORMAT_R32G32B32A32_UINT = 3,
        DXGI_FORMAT_R32G32B32A32_SINT = 4,
        DXGI_FORMAT_R32G32B32_TYPELESS = 5,
        DXGI_FORMAT_R32G32B32_FLOAT = 6,
        DXGI_FORMAT_R32G32B32_UINT = 7,
        DXGI_FORMAT_R32G32B32_SINT = 8,
        DXGI_FORMAT_R16G16B16A16_TYPELESS = 9,
        DXGI_FORMAT_R16G16B16A16_FLOAT = 10,
        DXGI_FORMAT_R16G16B16A16_UNORM = 11,
        DXGI_FORMAT_R16G16B16A16_UINT = 12,
        DXGI_FORMAT_R16G16B16A16_SNORM = 13,
        DXGI_FORMAT_R16G16B16A16_SINT = 14,
        DXGI_FORMAT_R32G32_TYPELESS = 15,
        DXGI_FORMAT_R32G32_FLOAT = 16,
        DXGI_FORMAT_R32G32_UINT = 17,
        DXGI_FORMAT_R32G32_SINT = 18,
        DXGI_FORMAT_R32G8X24_TYPELESS = 19,
        DXGI_FORMAT_D32_FLOAT_S8X24_UINT = 20,
        DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS = 21,
        DXGI_FORMAT_X32_TYPELESS_G8X24_UINT = 22,
        DXGI_FORMAT_R10G10B10A2_TYPELESS = 23,
        DXGI_FORMAT_R10G10B10A2_UNORM = 24,
        DXGI_FORMAT_R10G10B10A2_UINT = 25,
        DXGI_FORMAT_R11G11B10_FLOAT = 26,
        DXGI_FORMAT_R8G8B8A8_TYPELESS = 27,
        DXGI_FORMAT_R8G8B8A8_UNORM = 28,
        DXGI_FORMAT_R8G8B8A8_UNORM_SRGB = 29,
        DXGI_FORMAT_R8G8B8A8_UINT = 30,
        DXGI_FORMAT_R8G8B8A8_SNORM = 31,
        DXGI_FORMAT_R8G8B8A8_SINT = 32,
        DXGI_FORMAT_R16G16_TYPELESS = 33,
        DXGI_FORMAT_R16G16_FLOAT = 34,
        DXGI_FORMAT_R16G16_UNORM = 35,
        DXGI_FORMAT_R16G16_UINT = 36,
        DXGI_FORMAT_R16G16_SNORM = 37,
        DXGI_FORMAT_R16G16_SINT = 38,
        DXGI_FORMAT_R32_TYPELESS = 39,
        DXGI_FORMAT_D32_FLOAT = 40,
        DXGI_FORMAT_R32_FLOAT = 41,
        DXGI_FORMAT_R32_UINT = 42,
        DXGI_FORMAT_R32_SINT = 43,
        DXGI_FORMAT_R24G8_TYPELESS = 44,
        DXGI_FORMAT_D24_UNORM_S8_UINT = 45,
        DXGI_FORMAT_R24_UNORM_X8_TYPELESS = 46,
        DXGI_FORMAT_X24_TYPELESS_G8_UINT = 47,
        DXGI_FORMAT_R8G8_TYPELESS = 48,
        DXGI_FORMAT_R8G8_UNORM = 49,
        DXGI_FORMAT_R8G8_UINT = 50,
        DXGI_FORMAT_R8G8_SNORM = 51,
        DXGI_FORMAT_R8G8_SINT = 52,
        DXGI_FORMAT_R16_TYPELESS = 53,
        DXGI_FORMAT_R16_FLOAT = 54,
        DXGI_FORMAT_D16_UNORM = 55,
        DXGI_FORMAT_R16_UNORM = 56,
        DXGI_FORMAT_R16_UINT = 57,
        DXGI_FORMAT_R16_SNORM = 58,
        DXGI_FORMAT_R16_SINT = 59,
        DXGI_FORMAT_R8_TYPELESS = 60,
        DXGI_FORMAT_R8_UNORM = 61,
        DXGI_FORMAT_R8_UINT = 62,
        DXGI_FORMAT_R8_SNORM = 63,
        DXGI_FORMAT_R8_SINT = 64,
        DXGI_FORMAT_A8_UNORM = 65,
        DXGI_FORMAT_R1_UNORM = 66,
        DXGI_FORMAT_R9G9B9E5_SHAREDEXP = 67,
        DXGI_FORMAT_R8G8_B8G8_UNORM = 68,
        DXGI_FORMAT_G8R8_G8B8_UNORM = 69,
        DXGI_FORMAT_BC1_TYPELESS = 70,
        DXGI_FORMAT_BC1_UNORM = 71,
        DXGI_FORMAT_BC1_UNORM_SRGB = 72,
        DXGI_FORMAT_BC2_TYPELESS = 73,
        DXGI_FORMAT_BC2_UNORM = 74,
        DXGI_FORMAT_BC2_UNORM_SRGB = 75,
        DXGI_FORMAT_BC3_TYPELESS = 76,
        DXGI_FORMAT_BC3_UNORM = 77,
        DXGI_FORMAT_BC3_UNORM_SRGB = 78,
        DXGI_FORMAT_BC4_TYPELESS = 79,
        DXGI_FORMAT_BC4_UNORM = 80,
        DXGI_FORMAT_BC4_SNORM = 81,
        DXGI_FORMAT_BC5_TYPELESS = 82,
        DXGI_FORMAT_BC5_UNORM = 83,
        DXGI_FORMAT_BC5_SNORM = 84,
        DXGI_FORMAT_B5G6R5_UNORM = 85,
        DXGI_FORMAT_B5G5R5A1_UNORM = 86,
        DXGI_FORMAT_B8G8R8A8_UNORM = 87,
        DXGI_FORMAT_B8G8R8X8_UNORM = 88,
        DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM = 89,
        DXGI_FORMAT_B8G8R8A8_TYPELESS = 90,
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91,
        DXGI_FORMAT_B8G8R8X8_TYPELESS = 92,
        DXGI_FORMAT_B8G8R8X8_UNORM_SRGB = 93,
        DXGI_FORMAT_BC6H_TYPELESS = 94,
        DXGI_FORMAT_BC6H_UF16 = 95,
        DXGI_FORMAT_BC6H_SF16 = 96,
        DXGI_FORMAT_BC7_TYPELESS = 97,
        DXGI_FORMAT_BC7_UNORM = 98,
        DXGI_FORMAT_BC7_UNORM_SRGB = 99,
        DXGI_FORMAT_AYUV = 100,
        DXGI_FORMAT_Y410 = 101,
        DXGI_FORMAT_Y416 = 102,
        DXGI_FORMAT_NV12 = 103,
        DXGI_FORMAT_P010 = 104,
        DXGI_FORMAT_P016 = 105,
        DXGI_FORMAT_420_OPAQUE = 106,
        DXGI_FORMAT_YUY2 = 107,
        DXGI_FORMAT_Y210 = 108,
        DXGI_FORMAT_Y216 = 109,
        DXGI_FORMAT_NV11 = 110,
        DXGI_FORMAT_AI44 = 111,
        DXGI_FORMAT_IA44 = 112,
        DXGI_FORMAT_P8 = 113,
        DXGI_FORMAT_A8P8 = 114,
        DXGI_FORMAT_B4G4R4A4_UNORM = 115,
        DXGI_FORMAT_FORCE_UINT = 0xffffffff,
    }

    internal class Native
    {
        [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
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

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
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

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int D3DGetInputSignatureBlob(IntPtr d, IntPtr len, out IntPtr r);
    }

    internal static class Device
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint CreateRenderTargetViewDelegate(IntPtr @this, IntPtr pResource, IntPtr desc, out IntPtr r);

        public static readonly CreateRenderTargetViewDelegate CreateRenderTargetView =
            CalliGenerator.GetCalliDelegate<CreateRenderTargetViewDelegate>(9);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint CreateShaderDelegate(IntPtr @this, IntPtr data, IntPtr size, IntPtr linkage, out IntPtr r);

        public static readonly CreateShaderDelegate CreateVertexShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(12);
        public static readonly CreateShaderDelegate CreateGeometryShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(13);
        public static readonly CreateShaderDelegate CreatePixelShader =
            CalliGenerator.GetCalliDelegate<CreateShaderDelegate>(15);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate uint CreateInputLayoutDelegate(IntPtr @this,
            InputElementDescription* elementDesc, uint nElementDesc,
            IntPtr signature, IntPtr sizeSignature,
            out IntPtr r);
        public static readonly CreateInputLayoutDelegate CreateInputLayout =
            CalliGenerator.GetCalliDelegate<CreateInputLayoutDelegate>(11);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate uint CreateBufferDelegate(IntPtr @this,
            void* d, void* data, out IntPtr r);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate uint CreateBufferDelegate_SetPtr<T>(IntPtr @this,
            void* d, void* data, out IntPtr r, T[] array);

        public static readonly CreateBufferDelegate CreateBuffer =
            CalliGenerator.GetCalliDelegate<CreateBufferDelegate>(3);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint CreateTexture2DDelegate(IntPtr @this, ref Texture2DDescription d,
            IntPtr data, out IntPtr r);

        public static readonly CreateTexture2DDelegate CreateTexture2D =
            CalliGenerator.GetCalliDelegate<CreateTexture2DDelegate>(5);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint CreateShaderResourceViewDelegate(IntPtr @this, IntPtr resource,
            IntPtr desc, out IntPtr r);

        public static readonly CreateShaderResourceViewDelegate CreateShaderResourceView =
            CalliGenerator.GetCalliDelegate<CreateShaderResourceViewDelegate>(7);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint CreateDepthStencilViewDelegate(IntPtr @this, IntPtr resource,
            IntPtr desc, out IntPtr r);

        public static readonly CreateDepthStencilViewDelegate CreateDepthStencilView =
            CalliGenerator.GetCalliDelegate<CreateDepthStencilViewDelegate>(10);
    }

    internal static class SwapChain
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GetBufferDelegate(IntPtr @this, int i, IntPtr guid, out IntPtr r);

        public static readonly GetBufferDelegate GetBuffer =
            CalliGenerator.GetCalliDelegate<GetBufferDelegate>(9);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint PresentDelegate(IntPtr @this, int i, int j);

        public static readonly PresentDelegate Present =
            CalliGenerator.GetCalliDelegate<PresentDelegate>(8);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GetParentDelegate(IntPtr @this, IntPtr guid, out IntPtr r);

        public static readonly GetParentDelegate GetParent =
            CalliGenerator.GetCalliDelegate<GetParentDelegate>(6);
    }

    internal static class Factory
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GetAdapterDelegate(IntPtr @this, uint index, out IntPtr r);

        public static readonly GetAdapterDelegate GetAdapter =
            CalliGenerator.GetCalliDelegate<GetAdapterDelegate>(7);
    }

    internal static class Adapter
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint GetOutputDelegate(IntPtr @this, uint index, out IntPtr r);

        public static readonly GetOutputDelegate GetOutput =
            CalliGenerator.GetCalliDelegate<GetOutputDelegate>(7);
    }

    internal static class Output
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint WaitForVerticalBlankDelegate(IntPtr @this);

        public static readonly WaitForVerticalBlankDelegate WaitForVerticalBlank =
            CalliGenerator.GetCalliDelegate<WaitForVerticalBlankDelegate>(10);
    }

    internal static class DeviceContext
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate void OMSetRenderTargetsDelegate(IntPtr @this, uint num, IntPtr* renderTarget, IntPtr depthStencil);

        public static readonly OMSetRenderTargetsDelegate OMSetRenderTargets =
            CalliGenerator.GetCalliDelegate<OMSetRenderTargetsDelegate>(33);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate void RSSetViewportsDelegate(IntPtr @this, uint num, Viewport* viewport);

        public static readonly RSSetViewportsDelegate RSSetViewports =
            CalliGenerator.GetCalliDelegate<RSSetViewportsDelegate>(44);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void IASetInputLayoutDelegate(IntPtr @this, IntPtr layout);

        public static readonly IASetInputLayoutDelegate IASetInputLayout =
            CalliGenerator.GetCalliDelegate<IASetInputLayoutDelegate>(17);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void IASetPrimitiveTopologyDelegate(IntPtr @this, uint layout);

        public static readonly IASetPrimitiveTopologyDelegate IASetPrimitiveTopology =
            CalliGenerator.GetCalliDelegate<IASetPrimitiveTopologyDelegate>(24);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate void IASetVertexBuffersDelegate(IntPtr @this,
            uint slot, uint number, void* vbs, void* strides, void* offsets);

        public static readonly IASetVertexBuffersDelegate IASetVertexBuffers =
            CalliGenerator.GetCalliDelegate<IASetVertexBuffersDelegate>(18);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void SetShaderDelegate(IntPtr @this, IntPtr vs, IntPtr ci, uint nci);

        public static readonly SetShaderDelegate VSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(11);
        public static readonly SetShaderDelegate GSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(23);
        public static readonly SetShaderDelegate PSSetShader =
            CalliGenerator.GetCalliDelegate<SetShaderDelegate>(9);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public unsafe delegate void ClearRenderTargetViewDelegate(IntPtr @this, IntPtr view, ref Float4 color);

        public static readonly ClearRenderTargetViewDelegate ClearRenderTargetView =
            CalliGenerator.GetCalliDelegate<ClearRenderTargetViewDelegate>(50);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawDelegate(IntPtr @this, uint count, uint start);
        public static readonly DrawDelegate Draw =
            CalliGenerator.GetCalliDelegate<DrawDelegate>(13);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PSSetShaderResourcesDelegate(IntPtr @this, uint slot, uint num,
            ref IntPtr view);

        public static readonly PSSetShaderResourcesDelegate PSSetShaderResources =
            CalliGenerator.GetCalliDelegate<PSSetShaderResourcesDelegate>(8);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint ClearDepthStencilViewDelegate(IntPtr @this, IntPtr view,
            uint flags, float depth, byte stencil);

        public static readonly ClearDepthStencilViewDelegate ClearDepthStencilView =
            CalliGenerator.GetCalliDelegate<ClearDepthStencilViewDelegate>(53);
    }

    internal static class Blob
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetBufferPointerDelegate(IntPtr @this);

        public static readonly GetBufferPointerDelegate GetBufferPointer =
            CalliGenerator.GetCalliDelegate<GetBufferPointerDelegate>(3);

        public static readonly GetBufferPointerDelegate GetBufferSize =
            CalliGenerator.GetCalliDelegate<GetBufferPointerDelegate>(4);
    }
}
