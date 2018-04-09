using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightDx
{
    public class LightDevice : IDisposable
    {
        private class MultithreadLoop
        {
            public volatile bool Stop;
        }

        private Control _Ctrl;
        private int _Width, _Height;
        private IntPtr _Device, _Swapchain, _Context;
        private IntPtr _Output;
        private IntPtr _DefaultRenderView;

        internal IntPtr DevicePtr => _Device;
        internal IntPtr ContextPtr => _Context;

        internal int WindowWidth => _Width;
        internal int WindowHeight => _Height;

        private IntPtr _DepthStencilView;

        private List<WeakReference<IDisposable>> _Components = new List<WeakReference<IDisposable>>();

        private volatile MultithreadLoop _CurrentLoop;

        private LightDevice()
        {
        }

        ~LightDevice()
        {
            Dispose();
        }

        private void TryReleaseAll()
        {
            while (_Components.Count != 0)
            {
                var index = _Components.Count - 1;
                var c = _Components[index];
                _Components.RemoveAt(index);

                IDisposable cobj;
                if (c.TryGetTarget(out cobj))
                {
                    cobj.Dispose();
                }
            }
            _Components.Clear();
            NativeHelper.Dispose(ref _DepthStencilView);
            NativeHelper.Dispose(ref _DefaultRenderView);
            NativeHelper.Dispose(ref _Output);
            NativeHelper.Dispose(ref _Context);
            NativeHelper.Dispose(ref _Swapchain);
            NativeHelper.Dispose(ref _Device);
        }

        internal void AddComponent(IDisposable obj)
        {
            _Components.Add(new WeakReference<IDisposable>(obj));
        }

        internal void RemoveComponent(IDisposable obj)
        {
            for (int i = 0; i < _Components.Count; ++i)
            {
                if (_Components[i].TryGetTarget(out var c) && c == obj)
                {
                    _Components.RemoveAt(i);
                    return;
                }
            }
        }

        public static LightDevice Create(Control ctrl)
        {
            var ret = new LightDevice();

            //initialize size
            {
                var width = ctrl.ClientSize.Width;
                var height = ctrl.ClientSize.Height;

                ret._Ctrl = ctrl;
                ret._Width = width;
                ret._Height = height;
            }

            try
            {
                //create core objects
                IntPtr swapChain, device, immediateContext;
                {
                    var d = new SwapChainDescription(ctrl.Handle, ret._Width, ret._Height);

                    uint featureLevel;
                    Native.D3D11CreateDeviceAndSwapChain(
                        IntPtr.Zero, 1, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, ref d,
                        out swapChain, out device, out featureLevel, out immediateContext).Check();

                    ret._Device = device;
                    ret._Swapchain = swapChain;
                    ret._Context = immediateContext;
                }

                //get default render target
                IntPtr renderView;
                {
                    using (var backBuffer = new ComScopeGuard())
                    {
                        SwapChain.GetBuffer(swapChain, 0, Guids.Texture2D, out backBuffer.Ptr).Check();
                        Device.CreateRenderTargetView(device, backBuffer.Ptr, IntPtr.Zero, out renderView).Check();
                    }
                    ret._DefaultRenderView = renderView;
                }

                //get DXGI.Output
                using (ComScopeGuard factory = new ComScopeGuard(),
                    adapter = new ComScopeGuard(), output = new ComScopeGuard())
                {
                    SwapChain.GetParent(swapChain, Guids.Factory, out factory.Ptr).Check();
                    Factory.GetAdapter(factory.Ptr, 0, out adapter.Ptr).Check();
                    Adapter.GetOutput(adapter.Ptr, 0, out output.Ptr).Check();

                    ret._Output = output.Move();
                }
            }
            catch (NativeException e)
            {
                ret.TryReleaseAll();
                throw e;
            }
            return ret;
        }

        private void CreateDepthStencil()
        {
            Texture2DDescription depth = new Texture2DDescription
            {
                Width = (uint)_Width,
                Height = (uint)_Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = 20, //DXGI_FORMAT_D32_FLOAT_S8X24_UINT,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = 0, //Default
                BindFlags = 64, //DepthStencil
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };
            using (ComScopeGuard depthTex = new ComScopeGuard(), depthView = new ComScopeGuard())
            {
                Device.CreateTexture2D(_Device, ref depth, IntPtr.Zero, out depthTex.Ptr).Check();
                Device.CreateDepthStencilView(_Device, depthTex.Ptr, IntPtr.Zero, out depthView.Ptr).Check();
                this._DepthStencilView = depthView.Move();
            }
        }

        private IntPtr GetDefaultDepthStencil()
        {
            if (_DepthStencilView == null)
            {
                CreateDepthStencil();
            }
            return _DepthStencilView;
        }

        public void Dispose()
        {
            TryReleaseAll();
            GC.SuppressFinalize(this);
        }

        public RenderTarget CreateDefaultTarget(bool useDepthStencil)
        {
            return new RenderTarget(this, new[] { _DefaultRenderView.AddRef() },
                useDepthStencil ? GetDefaultDepthStencil().AddRef() : IntPtr.Zero);
        }

        public Pipeline CompilePipeline(ShaderSource shader, bool useGeometryShader, InputTopology topology)
        {
            return CompilePipeline(shader.Data, true, useGeometryShader, topology);
        }

        private unsafe Pipeline CompilePipeline(byte[] shaderCode, bool useVertexShader, bool useGeometryShader, InputTopology topology)
        {
            using (ComScopeGuard vertexShader = new ComScopeGuard(), pixelShader = new ComScopeGuard(),
                geometryShader = new ComScopeGuard(), signatureBlob = new ComScopeGuard())
            {
                fixed (byte* codePtr = shaderCode)
                {
                    if (useVertexShader) using (var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, shaderCode.Length,
                            CompilerStringConstants.VS, CompilerStringConstants.vs_4_0);
                        Device.CreateVertexShader(_Device,
                            Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr), IntPtr.Zero, out vertexShader.Ptr);
                        Native.D3DGetInputSignatureBlob(
                            Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr), out signatureBlob.Ptr);
                    }
                    if (useGeometryShader) using (var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, shaderCode.Length,
                            CompilerStringConstants.GS, CompilerStringConstants.gs_4_0);
                        Device.CreateGeometryShader(_Device, Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr),
                            IntPtr.Zero, out geometryShader.Ptr);
                    }
                    using(var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, shaderCode.Length,
                            CompilerStringConstants.PS, CompilerStringConstants.ps_4_0);
                        Device.CreatePixelShader(_Device, Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr),
                            IntPtr.Zero, out pixelShader.Ptr);
                    }
                } //fixed codePtr

                return new Pipeline(this,
                    vertexShader.Move(), geometryShader.Move(), pixelShader.Move(), signatureBlob.Move(),
                    new Viewport { Width = _Width, Height = _Height, MaxDepth = 1.0f },
                    topology);
            } //using
        }

        private unsafe IntPtr Compile(byte* codePtr, int length, IntPtr p1, IntPtr p2)
        {
            using (ComScopeGuard blob = new ComScopeGuard(), msg = new ComScopeGuard())
            {
                var e = Native.D3DCompile(codePtr, length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    p1, p2, 0, 0, out blob.Ptr, out msg.Ptr);
                if (blob.Ptr == IntPtr.Zero)
                {
                    if (msg.Ptr != IntPtr.Zero)
                    {
                        var msgStr = Marshal.PtrToStringAnsi(Blob.GetBufferPointer(msg.Ptr));
                        throw new NativeException(e, msgStr);
                    }
                    else
                    {
                        throw new NativeException(e);
                    }
                }
                return blob.Move();
            }
        }

        public Texture2D CreateTexture2D(Bitmap bitmap)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), view = new ComScopeGuard())
            {
                tex.Ptr = InternalCreateTexture2D(bitmap);
                Device.CreateShaderResourceView(_Device, tex.Ptr, IntPtr.Zero, out view.Ptr).Check();
                return new Texture2D(this, tex.Move(), view.Move(), bitmap.Width, bitmap.Height);
            }
        }

        private unsafe IntPtr InternalCreateTexture2D(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                using (var b2 = bitmap.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    return InternalCreateTexture2D(b2);
                }
            }

            Texture2DDescription t2d = new Texture2DDescription
            {
                Width = (uint)bitmap.Width,
                Height = (uint)bitmap.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = 87, //DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = 1, //immutable
                BindFlags = 8, //ShaderResource
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };
            var locked = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            SubresourceData data = new SubresourceData
            {
                pSysMem = locked.Scan0,
                SysMemPitch = (uint)locked.Stride,
            };
            
            Device.CreateTexture2D(_Device, ref t2d, new IntPtr(&data), out var tex).Check();

            bitmap.UnlockBits(locked);
            return tex;
        }

        public Texture2D CreateTexture2D(int width, int height, int format)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), view = new ComScopeGuard())
            {
                tex.Ptr = InternalCreateTexture2D(width, height, format);
                Device.CreateShaderResourceView(_Device, tex.Ptr, IntPtr.Zero, out view.Ptr).Check();
                return new Texture2D(this, tex.Move(), view.Move(), width, height);
            }
        }

        private IntPtr InternalCreateTexture2D(int width, int height, int format)
        {
            Texture2DDescription t2d = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = (uint)format, //87=DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = 2, //2=dynamic
                BindFlags = 8, //ShaderResource
                CPUAccessFlags = 0x10000, //write only
                MiscFlags = 0,
            };

            Device.CreateTexture2D(_Device, ref t2d, IntPtr.Zero, out var tex).Check();
            
            return tex;
        }

        private Device.CreateBufferDelegate_SetPtr<uint> _CreateBufferMethod32 =
            CalliGenerator.GetCalliDelegate_Device_CreateBuffer
                <Device.CreateBufferDelegate_SetPtr<uint>, uint>(3, 2);
        private Device.CreateBufferDelegate_SetPtr<ushort> _CreateBufferMethod16 =
            CalliGenerator.GetCalliDelegate_Device_CreateBuffer
                <Device.CreateBufferDelegate_SetPtr<ushort>, ushort>(3, 2);

        public unsafe IndexBuffer CreateImmutableIndexBuffer(Array data, int offset = 0, int length = -1)
        {
            //TODO type check
            int realLength = length == -1 ? data.Length - offset : length;
            int indexSize = data is ushort[] ? 2 : 4;
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(indexSize * realLength),
                Usage = 0, //default
                BindFlags = 2, //indexbuffer
                CPUAccessFlags = 0, //none. or write (65536)
                MiscFlags = 0,
                StructureByteStride = (uint)indexSize,
            };
            DataBox box = new DataBox
            {
                DataPointer = null, //the pointer is set (after pinned) in _CreateBufferMethod
                RowPitch = 0,
                SlicePitch = 0,
            };
            using (var vb = new ComScopeGuard())
            {
                if (indexSize == 2)
                {
                    _CreateBufferMethod16(DevicePtr, &bd, &box, out vb.Ptr, (ushort[])data).Check();
                }
                else
                {
                    _CreateBufferMethod32(DevicePtr, &bd, &box, out vb.Ptr, (uint[])data).Check();
                }
                return new IndexBuffer(this, vb.Move(), indexSize * 8, realLength);
            }
        }

        public unsafe IndexBuffer CreateDynamicIndexBuffer(int bitWidth, int size)
        {
            //TODO check bitWidth
            BufferDescription bd = new BufferDescription()
            {
                ByteWidth = (uint)(bitWidth / 8 * size),
                Usage = 2, //dynamic
                BindFlags = 2, //indexbuffer
                CPUAccessFlags = 0x10000, //write
                MiscFlags = 0,
                StructureByteStride = (uint)bitWidth / 8,
            };
            using (var vb = new ComScopeGuard())
            {
                Device.CreateBuffer(DevicePtr, &bd, null, out vb.Ptr).Check();
                return new IndexBuffer(this, vb.Move(), bitWidth, size);
            }
        }

        public void Present(bool vsync = false)
        {
            if (vsync)
            {
                Output.WaitForVerticalBlank(_Output);
            }
            SwapChain.Present(_Swapchain, 0, 0);
        }

        public void DoEvents()
        {
            InternalDoEvents();
        }

        #region NativeMessage

        private struct NativeMessage
        {
            public IntPtr handle;
            public uint msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        private static extern int PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax, int wRemoveMsg);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        private static extern int TranslateMessage(ref NativeMessage lpMsg);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        private static extern int DispatchMessage(ref NativeMessage lpMsg);

        private static void InternalDoEvents()
        {
            NativeMessage nativeMessage;
            while (PeekMessage(out nativeMessage, IntPtr.Zero, 0, 0, 0) != 0)
            {
                if (GetMessage(out nativeMessage, IntPtr.Zero, 0, 0) == -1)
                {
                    throw new InvalidOperationException();
                }
                Message message = new Message
                {
                    HWnd = nativeMessage.handle,
                    LParam = nativeMessage.lParam,
                    Msg = (int)nativeMessage.msg,
                    WParam = nativeMessage.wParam
                };
                if (!Application.FilterMessage(ref message))
                {
                    TranslateMessage(ref nativeMessage);
                    DispatchMessage(ref nativeMessage);
                }
            }
        }
        #endregion

        public void RunLoop(Action frame)
        {
            MultithreadLoop loop = new MultithreadLoop();
            _CurrentLoop = loop;
            while (_CurrentLoop == loop && !loop.Stop && _Ctrl.Visible)
            {
                frame();
                DoEvents();
            }
        }

        public void RunMultithreadLoop(Action frame)
        {
            MultithreadLoop loop = new MultithreadLoop();
            _CurrentLoop = loop;
            var th = new Thread(delegate()
            {
                while (_CurrentLoop == loop && !loop.Stop)
                {
                    frame();
                }
            });
            th.Start();
            while (_Ctrl.Visible && th.ThreadState == ThreadState.Running)
            {
                DoEvents();
                Thread.Sleep(100);
            }
            loop.Stop = true;
            th.Join();
        }

        public void StopLoop()
        {
            var loop = _CurrentLoop;
            if (loop == null)
            {
                return;
            }
            loop.Stop = true;
        }
    }
}
