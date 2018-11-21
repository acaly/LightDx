using LightDx.Natives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightDx
{
    public sealed class LightDevice : IDisposable
    {
        private class MultithreadLoop
        {
            public volatile bool Stop;
        }

        public static uint AdapterDeviceId { get; set; }

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
        public int Dpi => _dpi;

        private RenderTargetObject _defaultRenderTarget;

        private List<WeakReference<IDisposable>> _components = new List<WeakReference<IDisposable>>();

        private volatile MultithreadLoop _currentLoop;
        private AutoResetEvent _ctrlResized = new AutoResetEvent(false);
        private AutoResetEvent _ctrlDpiChanged = new AutoResetEvent(false);

        internal RenderTargetList CurrentTarget { get; set; }
        internal Pipeline CurrentPipeline { get; set; }

        public bool AutoResize { get; set; } = true;
        public event EventHandler ResolutionChanged;
        public event EventHandler DpiChanged;
        internal event Action ReleaseRenderTargets;
        internal event Action RebuildRenderTargets;

        static LightDevice()
        {
            SetupDefaltAdapter();
            TestGetDpiForMonitor();
        }

        private LightDevice()
        {
        }

        ~LightDevice()
        {
            Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            _disposing = true;

            if (disposing)
            {
                RemoveEventHandlers();

                while (_components.Count != 0)
                {
                    var index = _components.Count - 1;
                    var c = _components[index];
                    _components.RemoveAt(index);

                    if (c.TryGetTarget(out IDisposable cobj))
                    {
                        cobj.Dispose();
                    }
                }
                _components.Clear();
            }

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

        private static unsafe bool GetAdapterDesc(IntPtr adapter, out DXGIAdapterDescription desc)
        {
            fixed (DXGIAdapterDescription* ptr = &desc)
            {
                return Adapter.GetDesc(adapter, ptr) == 0;
            }
        }

        private static IEnumerable<DXGIAdapterDescription> GetAdapterDescription(Predicate<DXGIAdapterDescription> predicate, Action<IntPtr> result)
        {
            uint code;
            using (var factory = new ComScopeGuard())
            {
                Native.CreateDXGIFactory(Guids.Factory, out factory.Ptr).Check();

                uint i = 0;
                do
                {
                    using (var adapter = new ComScopeGuard())
                    {
                        code = Factory.EnumAdapters(factory.Ptr, i++, out adapter.Ptr);
                        if (code == 0)
                        {
                            if (GetAdapterDesc(adapter.Ptr, out var desc))
                            {
                                yield return desc;
                                if (predicate != null && predicate(desc))
                                {
                                    result.Invoke(adapter.Move());
                                    yield break;
                                }
                            }
                        }
                    }
                } while (code != 0x887A0002u);
            }
        }

        public static unsafe Tuple<uint, string>[] GetAllAdapters()
        {
            return GetAdapterDescription(null, null)
                .Select(d => new Tuple<uint, string>(d.DeviceId,
                Encoding.Unicode.GetString((byte*)d.Description, 128 * 2).Trim('\0'))).ToArray();
        }

        private static void SetupDefaltAdapter()
        {
            var list = GetAdapterDescription(null, null).OrderByDescending(x => x.DedicatedVideoMemory.ToUInt64());
            AdapterDeviceId = list.First().DeviceId;
        }

        private static IntPtr GetAdapter()
        {
            uint adapterId = AdapterDeviceId;
            IntPtr ret = IntPtr.Zero;
            foreach (var i in GetAdapterDescription(d => d.DeviceId == adapterId, d => ret = d)) { }
            if (ret == IntPtr.Zero)
            {
                using (var factory = new ComScopeGuard())
                {
                    Native.CreateDXGIFactory(Guids.Factory, out factory.Ptr).Check();
                    Factory.EnumAdapters(factory.Ptr, 0, out ret).Check();
                }
            }
            return ret;
        }

        public unsafe static LightDevice Create(Control ctrl, int initWidth = -1, int initHeight = -1)
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
                ret._dpi = GetDpiForWindow(ret._form.Handle);
            }

            try
            {
                using (var adapter = new ComScopeGuard())
                {
                    //Find the adapter
                    adapter.Ptr = GetAdapter();

                    //create core objects
                    IntPtr swapChain, device, immediateContext;
                    {
                        var d = new SwapChainDescription(ctrl.Handle, ret._width, ret._height);

                        Native.D3D11CreateDeviceAndSwapChain(
                            adapter.Ptr, adapter.Ptr == IntPtr.Zero ? 1u : 0u,
                            IntPtr.Zero, 0, IntPtr.Zero, 0, 7, ref d,
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
                            Device.CreateRenderTargetView(device, backBuffer.Ptr, null, out renderView).Check();
                        }
                        ret._defaultRenderView = renderView;
                    }

                    //get DXGI.Output
                    {
                        var i = Adapter.EnumOutputs(adapter.Ptr, 0, out var output);
                        //Sometimes this can fail, but it should not affect our other functions.
                        //TODO Actually we should think of supporting multiple outputs.
                        if (i != 0x887A0002)
                        {
                            i.Check();
                        }
                        ret._output = output;
                    }

                    ret._defaultRenderTarget = RenderTargetObject.CreateSwapchainTarget(ret);
                    ret.AddEventHandlers();
                }
            }
            catch (NativeException e)
            {
                ret.Dispose(true);
                throw e;
            }
            return ret;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public unsafe void ChangeResolution(int width, int height)
        {
            _width = width;
            _height = height;

            //Clear the current render target
            DeviceContext.OMSetRenderTargets(_context, 0, null, IntPtr.Zero);

            //Release all target objects, including _defaultRenderTarget
            ReleaseRenderTargets?.Invoke();

            //Release the default render view (containing reference to the back buffer)
            NativeHelper.Dispose(ref _defaultRenderView);

            //Resize swapchain
            SwapChain.ResizeBuffers(_swapchain, 1, (uint)width, (uint)height, 28 /*R8G8B8A8_UNorm*/, 0).Check();

            //Get the new back buffer and create default view
            RebuildBackBuffer();

            //Rebuild all target objects
            RebuildRenderTargets?.Invoke();

            //Apply current RenderTarget
            CurrentTarget.Apply();

            //Invoke external event
            ResolutionChanged?.Invoke(this, EventArgs.Empty);
        }

        //Separate to avoid fat method body (workaround for the Mono.Cecil bug)
        private unsafe void RebuildBackBuffer()
        {
            using (ComScopeGuard backBuffer = new ComScopeGuard(), renderView = new ComScopeGuard())
            {
                SwapChain.GetBuffer(_swapchain, 0, Guids.Texture2D, out backBuffer.Ptr).Check();
                Device.CreateRenderTargetView(_device, backBuffer.Ptr, null, out renderView.Ptr).Check();
                _defaultRenderView = renderView.Move();
            }
        }

        public RenderTargetObject GetDefaultTarget()
        {
            return _defaultRenderTarget;
        }

        public RenderTargetObject GetDefaultTarget(Vector4 color)
        {
            _defaultRenderTarget.ClearColor = color;
            return _defaultRenderTarget;
        }

        public RenderTargetObject CreateDepthStencilTarget(int depthSize = 24, int stencilSize = 8)
        {
            if (depthSize == 24 && stencilSize == 8)
            {
                return RenderTargetObject.CreateDepthStencilTarget(this,
                    44 /* DXGI_FORMAT_R24G8_TYPELESS */,
                    45 /* DXGI_FORMAT_D24_UNORM_S8_UINT */,
                    46 /* DXGI_FORMAT_R24_UNORM_X8_TYPELESS */);
            }
            else if (depthSize == 32 && stencilSize == 0)
            {
                return RenderTargetObject.CreateDepthStencilTarget(this,
                    39 /* DXGI_FORMAT_R32_TYPELESS */,
                    40 /* DXGI_FORMAT_D32_FLOAT */,
                    41 /* DXGI_FORMAT_R32_FLOAT */);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        public RenderTargetObject CreateTextureTarget()
        {
            return RenderTargetObject.CreateTextureTarget(this, 87 /*DXGI_FORMAT_B8G8R8A8_UNORM*/);
        }

        public Pipeline CompilePipeline(InputTopology topology, params ShaderSource[] shaders)
        {
            var vs = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.Vertex));
            var gs = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.Geometry));
            var ps = shaders.FirstOrDefault(s => s.ShaderTypes.HasFlag(ShaderType.Pixel));
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
                    vertexShader.Move(), geometryShader.Move(), pixelShader.Move(), signatureBlob.Move(), topology);
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

        public Texture2D CreateTexture2D(Stream stream)
        {
            byte[] buffer = new byte[4];
            if (stream.Length > 4)
            {
                stream.Read(buffer, 0, 4);
                stream.Seek(0, SeekOrigin.Begin);
                if (DDSReader.CheckHeader(buffer))
                {
                    return DDSReader.Load(this, stream);
                }
            }
            using (var bitmap = new Bitmap(stream))
            {
                return CreateTexture2D(bitmap);
            }
        }

        public Texture2D CreateTexture2D(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                using (var b2 = bitmap.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    return CreateTexture2D(b2);
                }
            }

            System.Drawing.Imaging.BitmapData locked = null;
            try
            {
                locked = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                return CreateTexture2D(bitmap.Width, bitmap.Height, 87 /*DXGI_FORMAT_B8G8R8A8_UNORM*/,
                    locked.Scan0, locked.Stride, false);
            }
            finally
            {
                if (locked != null)
                {
                    bitmap.UnlockBits(locked);
                }
            }
        }

        public unsafe Texture2D CreateTexture2D(int width, int height, int format, IntPtr data, int stride, bool isDynamic)
        {
            using (ComScopeGuard tex = new ComScopeGuard(), view = new ComScopeGuard())
            {
                tex.Ptr = InternalCreateTexture2D(width, height, format, data, stride, isDynamic);
                Device.CreateShaderResourceView(_device, tex.Ptr, null, out view.Ptr).Check();
                return new Texture2D(this, tex.Move(), view.Move(), width, height);
            }
        }

        private unsafe IntPtr InternalCreateTexture2D(int width, int height, int format, IntPtr data, int stride, bool isDynamic)
        {
            Texture2DDescription t2d = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = (uint)format,
                SampleCount = 1,
                SampleQuality = 0,
                Usage = isDynamic ? 2u : 1u, //2:dynamic, 1:immutable
                BindFlags = 8, //ShaderResource
                CPUAccessFlags = isDynamic ? 0x10000u : 0, //0x10000u: write only
                MiscFlags = 0,
            };

            if (data != IntPtr.Zero)
            {
                SubresourceData subres = new SubresourceData
                {
                    pSysMem = data,
                    SysMemPitch = (uint)stride,
                };
                Device.CreateTexture2D(_device, ref t2d, new IntPtr(&subres), out var tex).Check();
                return tex;
            }
            else
            {
                Device.CreateTexture2D(_device, ref t2d, IntPtr.Zero, out var tex).Check();
                return tex;
            }
        }

        public void Present(bool vsync = false)
        {
            if (vsync && _output != IntPtr.Zero)
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
                CheckRenderLoopEvents();
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
                    CheckRenderLoopEvents();
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

        //Note that update mechanism is different for dpi compared with size.

        private bool _isInResize;
        private volatile int _prevClientWidth, _prevClientHeight;
        private volatile int _dpi;

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
                var dpi = GetDpiForWindow(_form.Handle);
                _prevClientWidth = newSize.Width;
                _prevClientHeight = newSize.Height;
                _ctrlResized.Set();
                if (dpi != _dpi)
                {
                    _dpi = dpi;
                    _ctrlDpiChanged.Set();
                }
            }
        }

        private void CheckRenderLoopEvents()
        {
            //WaitOne(0) checks state without blocking
            if (_ctrlResized.WaitOne(0))
            {
                if (AutoResize)
                {
                    ChangeResolution(_prevClientWidth, _prevClientHeight);
                }
            }
            if (_ctrlDpiChanged.WaitOne(0))
            {
                DpiChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Dpi awareness
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("shcore.dll")]
        private static extern uint GetDpiForMonitor(IntPtr hMonitor, uint type, out uint dpiX, out uint dpiY);

        private static bool s_GetDpiForMonitorSupported;

        private static void TestGetDpiForMonitor()
        {
            try
            {
                GetDpiForMonitor(IntPtr.Zero, 0, out var x, out var y);
                s_GetDpiForMonitorSupported = true;
            }
            catch
            {
                s_GetDpiForMonitorSupported = false;
            }
        }

        private static int GetDpiForWindow(IntPtr hWnd)
        {
            if (s_GetDpiForMonitorSupported)
            {
                var monitor = MonitorFromWindow(hWnd, 2 /*MONITOR_DEFAULTTONEAREST*/);
                if (GetDpiForMonitor(monitor, 0 /*MDT_EFFECTIVE_DPI*/, out var x, out var y) == 0)
                {
                    return (int)x;
                }
            }
            return 96; //Use default if failed
        }

        #endregion
    }
}
