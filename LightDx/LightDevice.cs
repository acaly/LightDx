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

        private bool _disposing;
        private bool _disposed;

        private Control _ctrl;
        private Form _form;
        private int _width, _height;
        private IntPtr _device, _swapchain, _context;
        private IntPtr _output;
        private IntPtr _defaultRenderView;

        internal IntPtr DevicePtr => _device;
        internal IntPtr ContextPtr => _context;
        internal IntPtr DefaultRenderView => _defaultRenderView;

        public int ScreenWidth => _width;
        public int ScreenHeight => _height;

        private RenderTargetObject _defaultRenderTarget;

        private List<WeakReference<IDisposable>> _components = new List<WeakReference<IDisposable>>();

        private volatile MultithreadLoop _currentLoop;
        private AutoResetEvent _ctrlResized = new AutoResetEvent(false);

        internal RenderTarget CurrentTarget { get; set; }
        internal Pipeline CurrentPipeline { get; set; }

        public bool AutoResize { get; set; } = true;
        public event EventHandler ResolutionChanged;
        internal event Action ReleaseRenderTargets;
        internal event Action RebuildRenderTargets;
        
        private LightDevice()
        {
        }

        ~LightDevice()
        {
            Dispose();
        }

        private void ReleaseAll()
        {
            if (_disposed)
            {
                return;
            }
            _disposing = true;
            
            RemoveEventHandlers();

            while (_components.Count != 0)
            {
                var index = _components.Count - 1;
                var c = _components[index];
                _components.RemoveAt(index);

                IDisposable cobj;
                if (c.TryGetTarget(out cobj))
                {
                    cobj.Dispose();
                }
            }
            _components.Clear();
            NativeHelper.Dispose(ref _defaultRenderView);
            NativeHelper.Dispose(ref _output);
            NativeHelper.Dispose(ref _context);
            NativeHelper.Dispose(ref _swapchain);
            NativeHelper.Dispose(ref _device);

            _disposing = false;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal void AddComponent(IDisposable obj)
        {
            _components.Add(new WeakReference<IDisposable>(obj));
        }

        internal void RemoveComponent(IDisposable obj)
        {
            if (_disposing)
            {
                return;
            }

            //TODO make it faster?
            for (int i = 0; i < _components.Count; ++i)
            {
                if (_components[i].TryGetTarget(out var c) && c == obj)
                {
                    _components.RemoveAt(i);
                    return;
                }
            }
        }

        public static LightDevice Create(Control ctrl, int initWidth = -1, int initHeight = -1)
        {
            var ret = new LightDevice();

            //initialize size
            {
                var width = initWidth == -1 ? ctrl.ClientSize.Width : initWidth;
                var height = initHeight == -1 ? ctrl.ClientSize.Height : initHeight;

                ret._ctrl = ctrl;
                ret._form = ctrl.FindForm();
                ret._width = width;
                ret._height = height;
            }

            try
            {
                //create core objects
                IntPtr swapChain, device, immediateContext;
                {
                    var d = new SwapChainDescription(ctrl.Handle, ret._width, ret._height);
                    
                    Native.D3D11CreateDeviceAndSwapChain(
                        IntPtr.Zero, 1, IntPtr.Zero, 0, IntPtr.Zero, 0, 7, ref d,
                        out swapChain, out device, out var featureLevel, out immediateContext).Check();

                    ret._device = device;
                    ret._swapchain = swapChain;
                    ret._context = immediateContext;
                }

                //get default render target
                IntPtr renderView;
                {
                    using (var backBuffer = new ComScopeGuard())
                    {
                        SwapChain.GetBuffer(swapChain, 0, Guids.Texture2D, out backBuffer.Ptr).Check();
                        Device.CreateRenderTargetView(device, backBuffer.Ptr, IntPtr.Zero, out renderView).Check();
                    }
                    ret._defaultRenderView = renderView;
                }

                //get DXGI.Output
                using (ComScopeGuard factory = new ComScopeGuard(),
                    adapter = new ComScopeGuard(), output = new ComScopeGuard())
                {
                    SwapChain.GetParent(swapChain, Guids.Factory, out factory.Ptr).Check();
                    Factory.GetAdapter(factory.Ptr, 0, out adapter.Ptr).Check();
                    Adapter.GetOutput(adapter.Ptr, 0, out output.Ptr).Check();

                    ret._output = output.Move();
                }

                ret._defaultRenderTarget = RenderTargetObject.CreateSwapchainTarget(ret);
                ret.AddEventHandlers();
            }
            catch (NativeException e)
            {
                ret.ReleaseAll();
                throw e;
            }
            return ret;
        }

        public void Dispose()
        {
            ReleaseAll();
        }

        public void ChangeResolution(int width, int height)
        {
            _width = width;
            _height = height;

            //Release all target objects, including _defaultRenderTarget
            ReleaseRenderTargets?.Invoke();

            //Resize swapchain
            SwapChain.ResizeBuffers(_swapchain, 1, (uint)width, (uint)height, 28 /*R8G8B8A8_UNorm*/, 0);

            //Rebuild all target objects
            RebuildRenderTargets?.Invoke();

            //Apply current RenderTarget
            CurrentTarget.Apply();

            //Invoke external event
            ResolutionChanged?.Invoke(this, EventArgs.Empty);
        }

        public RenderTargetObject GetDefaultTarget()
        {
            return _defaultRenderTarget;
        }

        public RenderTargetObject CreateDefaultDepthStencilTarget()
        {
            return RenderTargetObject.CreateDepthStencilTarget(this, 20 /*DXGI_FORMAT_D32_FLOAT_S8X24_UINT*/);
        }

        public Pipeline CompilePipeline(InputTopology topology, params ShaderSource[] shaders)
        {
            var vs = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.VertexShader));
            var gs = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.GeometryShader));
            var ps = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.PixelShader));
            if (ps == null)
            {
                throw new ArgumentException("There must be one PixelShader");
            }
            return CompilePipeline(vs, gs, ps, topology);
        }

        private unsafe Pipeline CompilePipeline(ShaderSource srcVS, ShaderSource srcGS, ShaderSource srcPS, InputTopology topology)
        {
            using (ComScopeGuard vertexShader = new ComScopeGuard(), pixelShader = new ComScopeGuard(),
                geometryShader = new ComScopeGuard(), signatureBlob = new ComScopeGuard())
            {
                if (srcVS != null) fixed (byte* codePtr = srcVS.Data)
                {
                    using (var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, srcVS.Data.Length,
                            CompilerStringConstants.VS, CompilerStringConstants.vs_4_0);
                        Device.CreateVertexShader(_device,
                            Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr), IntPtr.Zero, out vertexShader.Ptr);
                        Native.D3DGetInputSignatureBlob(
                            Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr), out signatureBlob.Ptr);
                    }
                }
                if (srcGS != null) fixed (byte* codePtr = srcGS.Data)
                {
                    using (var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, srcGS.Data.Length,
                            CompilerStringConstants.GS, CompilerStringConstants.gs_4_0);
                        Device.CreateGeometryShader(_device, Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr),
                            IntPtr.Zero, out geometryShader.Ptr);
                    }
                }
                fixed (byte* codePtr = srcPS.Data)
                {
                    using (var blob = new ComScopeGuard())
                    {
                        blob.Ptr = Compile(codePtr, srcPS.Data.Length,
                            CompilerStringConstants.PS, CompilerStringConstants.ps_4_0);
                        Device.CreatePixelShader(_device, Blob.GetBufferPointer(blob.Ptr), Blob.GetBufferSize(blob.Ptr),
                            IntPtr.Zero, out pixelShader.Ptr);
                    }
                }

                return new Pipeline(this,
                    vertexShader.Move(), geometryShader.Move(), pixelShader.Move(), signatureBlob.Move(),
                    new Viewport { Width = _width, Height = _height, MaxDepth = 1.0f },
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
                Device.CreateShaderResourceView(_device, tex.Ptr, IntPtr.Zero, out view.Ptr).Check();
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
            
            Device.CreateTexture2D(_device, ref t2d, new IntPtr(&data), out var tex).Check();

            bitmap.UnlockBits(locked);
            return tex;
        }

        public Texture2D CreateTexture2D(int width, int height, int format)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), view = new ComScopeGuard())
            {
                tex.Ptr = InternalCreateTexture2D(width, height, format);
                Device.CreateShaderResourceView(_device, tex.Ptr, IntPtr.Zero, out view.Ptr).Check();
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

            Device.CreateTexture2D(_device, ref t2d, IntPtr.Zero, out var tex).Check();
            
            return tex;
        }

        public void Present(bool vsync = false)
        {
            if (vsync)
            {
                Output.WaitForVerticalBlank(_output);
            }
            SwapChain.Present(_swapchain, 0, 0);
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
            _currentLoop = loop;
            while (_currentLoop == loop && !loop.Stop && _ctrl.Visible)
            {
                CheckCtrlResize();
                frame();
                DoEvents();
            }
        }

        public void RunMultithreadLoop(Action frame)
        {
            MultithreadLoop loop = new MultithreadLoop();
            _currentLoop = loop;
            var th = new Thread(delegate()
            {
                while (_currentLoop == loop && !loop.Stop)
                {
                    CheckCtrlResize();
                    frame();
                }
            });
            th.Start();
            while (_ctrl.Visible && th.IsAlive)
            {
                DoEvents();
                Thread.Sleep(100);
            }
            loop.Stop = true;
            th.Join();
        }

        public void StopLoop()
        {
            var loop = _currentLoop;
            if (loop == null)
            {
                return;
            }
            loop.Stop = true;
        }

        #region Managed Events

        private bool _isInResize;
        private volatile int _prevClientWidth, _prevClientHeight;

        private void AddEventHandlers()
        {
            var size = _ctrl.ClientSize;
            _prevClientWidth = size.Width;
            _prevClientHeight = size.Height;

            _ctrl.ClientSizeChanged += ControlSizeChanged;
            _form.ResizeBegin += FormBeginResize;
            _form.ResizeEnd += FormEndResize;
        }

        private void RemoveEventHandlers()
        {
            _ctrl.ClientSizeChanged -= ControlSizeChanged;
            _form.ResizeBegin -= FormBeginResize;
            _form.ResizeEnd -= FormEndResize;
        }

        private void FormBeginResize(object sender, EventArgs e)
        {
            _isInResize = true;
        }

        private void FormEndResize(object sender, EventArgs e)
        {
            _isInResize = false;
            ControlSizeChanged(sender, e); //Trigger at the end
        }

        private void ControlSizeChanged(object sender, EventArgs e)
        {
            if (_isInResize || _form.WindowState == FormWindowState.Minimized)
            {
                return;
            }
            var newSize = _ctrl.ClientSize;
            if (newSize != new Size(_prevClientWidth, _prevClientHeight))
            {
                _ctrlResized.Reset();
                _prevClientWidth = newSize.Width;
                _prevClientHeight = newSize.Height;
                _ctrlResized.Set();
            }
        }

        private void CheckCtrlResize()
        {
            //WaitOne(0) checks state without blocking
            if (_ctrlResized.WaitOne(0))
            {
                if (AutoResize)
                {
                    ChangeResolution(_prevClientWidth, _prevClientHeight);
                }
            }
        }

        #endregion
    }
}
