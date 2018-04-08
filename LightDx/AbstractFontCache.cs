using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    public abstract class AbstractFontCache<T> : IDisposable
        where T : class
    {
        public struct CachedRegion
        {
            public T Bitmap;
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private struct CharacterInfo
        {
            public int BufferId;
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }
        
        public AbstractFontCache(Font font, PixelFormat format, Color foreground, Color background)
        {
            Font = font;
            PixelFormat = format;
            BackgroundColor = background;
            ForegroundColor = foreground;
            _Brush = new SolidBrush(foreground);
            var height = (int)Math.Ceiling(font.GetHeight());
            int bitmapSize;
            if (height < 85)
            {
                bitmapSize = 256;
            }
            else
            {
                bitmapSize = Round2Power(height * 3);
            }
            BufferWidth = bitmapSize;
            BufferHeight = bitmapSize;
            _GdiBitmap = new Bitmap(BufferWidth, BufferHeight, format);
            _GdiBitmapGraphics = Graphics.FromImage(_GdiBitmap);
            _GdiBitmapGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            _FormatSingle = new StringFormat();
            _FormatSingle.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 1) });
            _FormatDouble = new StringFormat();
            _FormatDouble.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 2) });
            _Empty = new CachedRegion
            {
                Bitmap = null,
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
            };
        }

        private static int Round2Power(int x)
        {
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        ~AbstractFontCache()
        {
            this.Dispose();
        }

        public bool Disposed { get; private set; }
        public Font Font { get; private set; }
        public PixelFormat PixelFormat { get; private set; }
        public Color ForegroundColor { get; private set; }
        public Color BackgroundColor { get; private set; }
        public int BufferWidth { get; private set; }
        public int BufferHeight { get; private set; }
        public TextRenderingHint RenderQuality
        {
            get => _GdiBitmapGraphics.TextRenderingHint;
            set => _GdiBitmapGraphics.TextRenderingHint = value;
        }

        private readonly Brush _Brush;
        private readonly char[] _StringBuffer = new char[2];
        private readonly StringFormat _FormatSingle, _FormatDouble;
        private readonly Bitmap _GdiBitmap;
        private readonly Graphics _GdiBitmapGraphics;
        private readonly List<T> _BufferList = new List<T>();
        private readonly Dictionary<int, CharacterInfo> _Character = new Dictionary<int, CharacterInfo>();
        private readonly Dictionary<long, int> _Kerning = new Dictionary<long, int>();
        private readonly CachedRegion _Empty;

        private T _CurrentBuffer;
        private int _CurrentBufferX, _CurrentBufferY, _CurrentBufferHeight;

        private int _DrawLastChar;

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }
            _GdiBitmapGraphics.Dispose();
            _GdiBitmap.Dispose();
            _Brush.Dispose();
            _FormatSingle.Dispose();
            _FormatDouble.Dispose();
            foreach (var b in _BufferList)
            {
                DisposeBitmap(b);
            }
            _BufferList.Clear();
            GC.SuppressFinalize(this);
        }

        public void CacheChar(int c)
        {
            if (!_Character.ContainsKey(c))
            {
                AddCharacter(c);
            }
        }

        public void CacheString(string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                if (i < str.Length - 1 && Char.IsSurrogatePair(str[i], str[i + 1]))
                {
                    CacheChar(str[i] | str[i + 1] << 16);
                    i += 1;
                }
                else
                {
                    CacheChar(str[i]);
                }
            }
        }

        public void DrawChar(int c, out CachedRegion bitmap, 
            out int kernX, out int advanceX, out int height)
        {
            if (!_Character.TryGetValue(c, out var info))
            {
                AddCharacter(c);
                info = _Character[c];
            }

            if (info.BufferId < 0)
            {
                bitmap = _Empty;
                kernX = GetKerning(c);
                advanceX = info.Width;
                height = 0;
                return;
            }

            CachedRegion ret = new CachedRegion()
            {
                Bitmap = _BufferList[info.BufferId],
                X = info.X,
                Y = info.Y,
                Width = info.Width,
                Height = info.Height
            };
            FlushCache(ret.Bitmap);

            bitmap = ret;
            kernX = GetKerning(c);
            advanceX = ret.Width;
            height = ret.Height;
        }

        public void TryDrawChar(int c, out CachedRegion bitmap,
            out int kernX, out int advanceX, out int height)
        {
            try
            {
                DrawChar(c, out bitmap, out kernX, out advanceX, out height);
            }
            catch
            {
                bitmap = _Empty;
                kernX = 0;
                advanceX = 0;
                height = 0;
            }
        }

        private int GetKerning(int c)
        {
            //TODO support kerning
            _DrawLastChar = c;
            return 0;
        }

        public void ResetDraw()
        {
            _DrawLastChar = 0;
        }

        private void AddCharacter(int c)
        {
            if (_CurrentBuffer == null)
            {
                NewBuffer();
            }

            //measure
            _StringBuffer[0] = (char)c;
            _StringBuffer[1] = (char)(c >> 16);
            if (_StringBuffer[1] != 0 &&
                !Char.IsSurrogatePair(_StringBuffer[0], _StringBuffer[1]))
            {
                throw new ArgumentException("Invalid char");
            }
            var str = new string(_StringBuffer, 0, _StringBuffer[1] != 0 ? 2 : 1);
            RectangleF rect = new RectangleF(0, 0, 1E10F, 1E10F);
            var region = _GdiBitmapGraphics.MeasureCharacterRanges(str, Font, rect,
                _StringBuffer[1] != 0 ? _FormatDouble : _FormatSingle)[0];
            var size = region.GetBounds(_GdiBitmapGraphics);
            var x = (int)Math.Ceiling(size.X);
            var y = (int)Math.Ceiling(size.Y);
            var w = (int)Math.Ceiling(size.Width);
            var h = (int)Math.Ceiling(size.Height);

            //draw
            _GdiBitmapGraphics.Clear(BackgroundColor);
            _GdiBitmapGraphics.DrawString(str, Font, _Brush, 0, 0);

            if (w <= 0 || h <= 0)
            {
                //empty char
                _Character.Add(c, new CharacterInfo
                {
                    BufferId = -1,
                    Width = MeasureWhitespace(c),
                    Height = 0,
                });
                return;
            }

            //lock
            _GdiBitmapGraphics.Flush();
            var l = _GdiBitmap.LockBits(new Rectangle(x, y, w, h),
                ImageLockMode.ReadOnly, PixelFormat);
            
            try
            {
                if (_CurrentBufferX + w <= BufferWidth &&
                    _CurrentBufferY + h <= BufferHeight)
                {
                    //current line
                    UpdateCache(_CurrentBuffer, _CurrentBufferX, _CurrentBufferY,
                        w, h, l);
                    AddCharInfo(c, w, h);
                }
                else if (w <= BufferWidth &&
                    _CurrentBufferY + _CurrentBufferHeight + h <= BufferHeight)
                {
                    //new line
                    _CurrentBufferX = 0;
                    _CurrentBufferY += _CurrentBufferHeight;
                    _CurrentBufferHeight = 0;
                    UpdateCache(_CurrentBuffer, _CurrentBufferX, _CurrentBufferY,
                        w, h, l);
                    AddCharInfo(c, w, h);
                }
                else if (w <= BufferWidth && h <= BufferHeight)
                {
                    //new buffer
                    NewBuffer();
                    UpdateCache(_CurrentBuffer, _CurrentBufferX, _CurrentBufferY,
                        w, h, l);
                    AddCharInfo(c, w, h);
                }
                else
                {
                    throw new ArgumentException("Size too large");
                }
            }
            finally
            {
                _GdiBitmap.UnlockBits(l);
            }
        }

        private int MeasureWhitespace(int c)
        {
            var sb = new StringBuilder();
            sb.Append('O');
            sb.Append((char)c);
            var d = false;
            if ((c >> 16) != 0)
            {
                d = true;
                sb.Append((char)(c >> 16));
            }
            sb.Append('O');
            var fmt = new StringFormat();
            fmt.SetMeasurableCharacterRanges(new[]
            {
                new CharacterRange(0, 1),
                new CharacterRange(d ? 3 : 2, 1),
            });
            var m = _GdiBitmapGraphics.MeasureCharacterRanges(sb.ToString(), Font,
                new RectangleF(0, 0, 1E10F, 1E10F), fmt);
            var m0 = m[0].GetBounds(_GdiBitmapGraphics);
            var m1 = m[1].GetBounds(_GdiBitmapGraphics);
            return (int)(m1.X - m0.X - m0.Width);
        }

        private void AddCharInfo(int c, int w, int h)
        {
            _Character.Add(c, new CharacterInfo
            {
                BufferId = _BufferList.Count - 1,
                X = _CurrentBufferX,
                Y = _CurrentBufferY,
                Width = w,
                Height = h,
            });
            _CurrentBufferX += w;
            _CurrentBufferHeight = Math.Max(_CurrentBufferHeight, h);
        }

        private void NewBuffer()
        {
            var b = CreateBitmap(BufferWidth, BufferHeight);
            _BufferList.Add(b);
            _CurrentBufferX = 0;
            _CurrentBufferY = 0;
            _CurrentBufferHeight = 0;
            _CurrentBuffer = b;
        }

        protected abstract T CreateBitmap(int w, int h);
        protected abstract void UpdateCache(T bitmap, int x, int y, int w, int h, BitmapData data);
        protected abstract void DisposeBitmap(T bitmap);
        protected abstract void FlushCache(T bitmap);
    }
}
