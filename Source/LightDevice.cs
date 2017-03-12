using LightDX.Natives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightDX
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

        internal IntPtr DevicePtr { get { return _Device; } }
        internal IntPtr ContextPtr { get { return _Context; } }

        private IntPtr _DepthStencilView;

        private List<WeakReference<IDisposable>> _Components = new List<WeakReference<IDisposable>>();

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
                IDisposable c;
                if (_Components[i].TryGetTarget(out c) && c == obj)
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
                    IntPtr backBuffer;
                    SwapChain.GetBuffer(swapChain, 0, Guids.Texture2D, out backBuffer).Check();
                    using (new ComScopeGuard(backBuffer))
                    {
                        Device.CreateRenderTargetView(device, backBuffer, IntPtr.Zero, out renderView).Check();
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
            IntPtr depthTex, depthView;
            Texture2DDescription depth = new Texture2DDescription
            {
                Width = (uint)_Width,
                Height = (uint)_Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = PixelFormat.DXGI_FORMAT_D32_FLOAT_S8X24_UINT,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = 0, //Default
                BindFlags = 64, //DepthStencil
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };
            Device.CreateTexture2D(_Device, ref depth, IntPtr.Zero, out depthTex).Check();
            using (new ComScopeGuard(depthTex))
            {
                Device.CreateDepthStencilView(_Device, depthTex, IntPtr.Zero, out depthView).Check();
            }
            this._DepthStencilView = depthView;
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

        public Pipeline CompilePipeline(string shaderCode, bool useGeometryShader, InputTopology topology)
        {
            return CompilePipeline(Encoding.ASCII.GetBytes(shaderCode), useGeometryShader, topology);
        }

        public unsafe Pipeline CompilePipeline(byte[] shaderCode, bool useGeometryShader, InputTopology topology)
        {
            using (ComScopeGuard vertexShader = new ComScopeGuard(), pixelShader = new ComScopeGuard(),
                geometryShader = new ComScopeGuard(), signatureBlob = new ComScopeGuard())
            {
                fixed (byte* codePtr = shaderCode)
                {
                    using (var blob = new ComScopeGuard())
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
                    using (var blob = new ComScopeGuard())
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

        public void Present(bool vsync = false)
        {
            SwapChain.Present(_Swapchain, 0, 0);
            if (vsync)
            {
                Output.WaitForVerticalBlank(_Output);
            }
        }

        public void DoEvents()
        {
            InternalDoEvents();
        }

        #region NativeMessage

        public struct NativeMessage
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
        public static extern int PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax, int wRemoveMsg);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        public static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        public static extern int TranslateMessage(ref NativeMessage lpMsg);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll")]
        public static extern int DispatchMessage(ref NativeMessage lpMsg);

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

        public void RunLoop(Func<bool> frame)
        {
            while (frame())
            {
                DoEvents();
            }
        }

        public void RunLoop(Action frame)
        {
            while (_Ctrl.Visible)
            {
                frame();
                DoEvents();
            }
        }

        public void RunMultithreadLoop(Action frame)
        {
            MultithreadLoop loop = new MultithreadLoop();
            var th = new Thread(delegate()
            {
                while (!loop.Stop)
                {
                    frame();
                }
            });
            th.Start();
            while (_Ctrl.Visible)
            {
                DoEvents();
                Thread.Sleep(100);
            }
            loop.Stop = true;
            th.Join();
        }
    }
}
