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
        where T : class, IDisposable
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

        public AbstractFontCache(LightDevice device, Font font, PixelFormat format, Color foreground, Color background)
        {
            _device = device;
            device.AddComponent(this);

            Font = font;
            PixelFormat = format;
            BackgroundColor = background;
            ForegroundColor = foreground;
            _brush = new SolidBrush(foreground);
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
            _gdiBitmap = new Bitmap(BufferWidth, BufferHeight, format);
            _gdiBitmapGraphics = Graphics.FromImage(_gdiBitmap);
            _gdiBitmapGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            _formatSingle = new StringFormat();
            _formatSingle.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 1) });
            _formatDouble = new StringFormat();
            _formatDouble.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 2) });
            _empty = new CachedRegion
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
            Dispose(false);
        }

        private bool _disposed;
        protected LightDevice _device;

        public Font Font { get; private set; }
        public PixelFormat PixelFormat { get; private set; }
        public Color ForegroundColor { get; private set; }
        public Color BackgroundColor { get; private set; }
        public int BufferWidth { get; private set; }
        public int BufferHeight { get; private set; }
        public TextRenderingHint RenderQuality
        {
            get => _gdiBitmapGraphics.TextRenderingHint;
            set => _gdiBitmapGraphics.TextRenderingHint = value;
        }

        private readonly Brush _brush;
        private readonly char[] _stringBuffer = new char[2];
        private readonly StringFormat _formatSingle, _formatDouble;
        private readonly Bitmap _gdiBitmap;
        private readonly Graphics _gdiBitmapGraphics;
        private readonly List<T> _bufferList = new List<T>();
        private readonly Dictionary<int, CharacterInfo> _characters = new Dictionary<int, CharacterInfo>();
        private readonly Dictionary<long, int> _kerning = new Dictionary<long, int>();
        private readonly CachedRegion _empty;

        private T _currentBuffer;
        private int _currentBufferX, _currentBufferY, _currentBufferHeight;

        private int _drawLastChar;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _gdiBitmapGraphics.Dispose();
                _gdiBitmap.Dispose();
                _brush.Dispose();
                _formatSingle.Dispose();
                _formatDouble.Dispose();
                foreach (var b in _bufferList)
                {
                    DisposeBitmap(b);
                }
                _bufferList.Clear();

                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public void CacheChar(int c)
        {
            if (!_characters.ContainsKey(c))
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
            if (!_characters.TryGetValue(c, out var info))
            {
                AddCharacter(c);
                info = _characters[c];
            }

            if (info.BufferId < 0)
            {
                bitmap = _empty;
                kernX = GetKerning(c);
                advanceX = info.Width;
                height = 0;
                return;
            }

            CachedRegion ret = new CachedRegion()
            {
                Bitmap = _bufferList[info.BufferId],
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
                bitmap = _empty;
                kernX = 0;
                advanceX = 0;
                height = 0;
            }
        }

        private int GetKerning(int c)
        {
            //TODO support kerning
            _drawLastChar = c;
            return 0;
        }

        public void ResetDraw()
        {
            _drawLastChar = 0;
        }

        private void AddCharacter(int c)
        {
            if (_currentBuffer == null)
            {
                NewBuffer();
            }

            //measure
            _stringBuffer[0] = (char)c;
            _stringBuffer[1] = (char)(c >> 16);
            if (_stringBuffer[1] != 0 &&
                !Char.IsSurrogatePair(_stringBuffer[0], _stringBuffer[1]))
            {
                throw new ArgumentException("Invalid char");
            }
            var str = new string(_stringBuffer, 0, _stringBuffer[1] != 0 ? 2 : 1);
            RectangleF rect = new RectangleF(0, 0, 1E10F, 1E10F);
            var region = _gdiBitmapGraphics.MeasureCharacterRanges(str, Font, rect,
                _stringBuffer[1] != 0 ? _formatDouble : _formatSingle)[0];
            var size = region.GetBounds(_gdiBitmapGraphics);
            var x = (int)Math.Ceiling(size.X);
            var y = (int)Math.Ceiling(size.Y);
            var w = (int)Math.Ceiling(size.Width);
            var h = (int)Math.Ceiling(size.Height);

            //draw
            _gdiBitmapGraphics.Clear(BackgroundColor);
            _gdiBitmapGraphics.DrawString(str, Font, _brush, 0, 0);

            if (w <= 0 || h <= 0)
            {
                //empty char
                _characters.Add(c, new CharacterInfo
                {
                    BufferId = -1,
                    Width = MeasureWhitespace(c),
                    Height = 0,
                });
                return;
            }

            //lock
            _gdiBitmapGraphics.Flush();
            var l = _gdiBitmap.LockBits(new Rectangle(x, y, w, h),
                ImageLockMode.ReadOnly, PixelFormat);
            
            try
            {
                if (_currentBufferX + w <= BufferWidth &&
                    _currentBufferY + h <= BufferHeight)
                {
                    //current line
                    UpdateCache(_currentBuffer, _currentBufferX, _currentBufferY,
                        w, h, l);
                    AddCharInfo(c, w, h);
                }
                else if (w <= BufferWidth &&
                    _currentBufferY + _currentBufferHeight + h <= BufferHeight)
                {
                    //new line
                    _currentBufferX = 0;
                    _currentBufferY += _currentBufferHeight;
                    _currentBufferHeight = 0;
                    UpdateCache(_currentBuffer, _currentBufferX, _currentBufferY,
                        w, h, l);
                    AddCharInfo(c, w, h);
                }
                else if (w <= BufferWidth && h <= BufferHeight)
                {
                    //new buffer
                    NewBuffer();
                    UpdateCache(_currentBuffer, _currentBufferX, _currentBufferY,
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
                _gdiBitmap.UnlockBits(l);
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
            var m = _gdiBitmapGraphics.MeasureCharacterRanges(sb.ToString(), Font,
                new RectangleF(0, 0, 1E10F, 1E10F), fmt);
            var m0 = m[0].GetBounds(_gdiBitmapGraphics);
            var m1 = m[1].GetBounds(_gdiBitmapGraphics);
            return (int)(m1.X - m0.X - m0.Width);
        }

        private void AddCharInfo(int c, int w, int h)
        {
            _characters.Add(c, new CharacterInfo
            {
                BufferId = _bufferList.Count - 1,
                X = _currentBufferX,
                Y = _currentBufferY,
                Width = w,
                Height = h,
            });
            _currentBufferX += w;
            _currentBufferHeight = Math.Max(_currentBufferHeight, h);
        }

        private void NewBuffer()
        {
            var b = CreateBitmap(BufferWidth, BufferHeight);
            _bufferList.Add(b);
            _currentBufferX = 0;
            _currentBufferY = 0;
            _currentBufferHeight = 0;
            _currentBuffer = b;
        }

        protected abstract T CreateBitmap(int w, int h);
        protected abstract void UpdateCache(T bitmap, int x, int y, int w, int h, BitmapData data);
        protected abstract void DisposeBitmap(T bitmap);
        protected abstract void FlushCache(T bitmap);
    }
}
