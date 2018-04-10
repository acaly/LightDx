using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    //TODO specify address and filtering options (at startup)
    //TODO make a DeviceChild class?
    //TODO use Device.WindowWidth, WindowHeight instead of 400 and 300
    //TODO support color
    public class Sprite : IDisposable
    {
        private struct Vertex
        {
            [Position]
            public Float4 Position;
            [TexCoord]
            public Float4 TexCoord;
        }

        private readonly LightDevice _device;
        private readonly Pipeline _pipeline;
        private readonly InputDataProcessor<Vertex> _input;
        private readonly InputBuffer _buffer;
        private readonly Vertex[] _array;
        private bool _disposed;

        public Sprite(LightDevice device)
        {
            _device = device;
            device.AddComponent(this);

            _pipeline = device.CompilePipeline(ShaderSource.FromString(PipelineCode), false, InputTopology.Triangle);
            _pipeline.SetBlender(Blender.AlphaBlender);

            _input = _pipeline.CreateInputDataProcessor<Vertex>();
            _array = new[] {
                new Vertex { TexCoord = new Float4(0, 0, 0, 0), Position = new Float4(0, 0, 0, 0) },
                new Vertex { TexCoord = new Float4(1, 0, 0, 0), Position = new Float4(1, 0, 0, 0) },
                new Vertex { TexCoord = new Float4(0, 1, 0, 0), Position = new Float4(0, 1, 0, 0) },

                new Vertex { TexCoord = new Float4(0, 1, 0, 0), Position = new Float4(0, 1, 0, 0) },
                new Vertex { TexCoord = new Float4(1, 0, 0, 0), Position = new Float4(1, 0, 0, 0) },
                new Vertex { TexCoord = new Float4(1, 1, 0, 0), Position = new Float4(1, 1, 0, 0) },
            };
            _buffer = _input.CreateDynamicBuffer(6);
        }

        ~Sprite()
        {
            if (!_disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pipeline.Dispose();
            _input.Dispose();
            _buffer.Dispose();

            _disposed = true;
            _device.RemoveComponent(this);
            GC.SuppressFinalize(this);
        }

        public void Apply()
        {
            _pipeline.Apply();
        }

        public void DrawTexture(Texture2D tex, int x, int y, int w, int h)
        {
            DrawTexture(tex, x, y, w, h, 0, 0, tex.Width, tex.Height, 0, 0, 0);
        }

        public void DrawTexture(Texture2D tex, float x, float y, float w, float h, int tx, int ty, int tw, int th)
        {
            DrawTexture(tex, x, y, w, h, tx, ty, tw, th, 0, 0, 0);
        }

        public void DrawTexture(Texture2D tex, float x, float y, float w, float h, int tx, int ty, int tw, int th, float cx, float cy, float rotate)
        {
            //var r = x + w;
            //var b = y + h;
            var fx = tx / (float)tex.Width;
            var fy = ty / (float)tex.Height;
            var fr = (tx + w) / (float)tex.Width;
            var fb = (ty + h) / (float)tex.Height;

            var cl = -cx;
            var ct = -cy;
            var cr = w - cx;
            var cb = h - cy;
            var s = (float)Math.Sin(rotate);
            var c = (float)Math.Cos(rotate);

            UpdatePoint(ref _array[0], x, y, cl, ct, s, c, fx, fy);
            UpdatePoint(ref _array[1], x, y, cr, ct, s, c, fr, fy);
            UpdatePoint(ref _array[2], x, y, cl, cb, s, c, fx, fb);
            UpdatePoint(ref _array[3], x, y, cl, cb, s, c, fx, fb);
            UpdatePoint(ref _array[4], x, y, cr, ct, s, c, fr, fy);
            UpdatePoint(ref _array[5], x, y, cr, cb, s, c, fr, fb);

            _pipeline.SetResource(0, tex);
            _pipeline.ApplyResources();
            _input.UpdateBufferDynamic(_buffer, _array);
            _buffer.DrawAll();
        }

        private void UpdatePoint(ref Vertex v, float x, float y, float dx, float dy, float s, float c, float tx, float ty)
        {
            v.Position.X = x + dx * c - dy * s;
            v.Position.Y = y + dy * c + dx * s;
            v.TexCoord.X = tx;
            v.TexCoord.Y = ty;
        }

        public void DrawString(TextureFontCache font, string str, float x, float y, float maxWidth)
        {
            font.CacheString(str);
            var drawX = x;
            var maxX = x + maxWidth;
            for (int i = 0; i < str.Length && drawX < maxX; ++i)
            {
                if (i < str.Length - 1 && Char.IsSurrogatePair(str[i], str[i + 1]))
                {
                    drawX = DrawChar(font, str[i] | str[i + 1] << 16, drawX, y, maxX);
                    i += 1;
                }
                else
                {
                    drawX = DrawChar(font, str[i], drawX, y, maxX);
                }
            }
        }

        private float DrawChar(TextureFontCache font, int c, float x, float y, float maxX)
        {
            font.DrawChar(c, out var b, out var k, out var ax, out var h);
            x += k;
            if (x + b.Width > maxX)
            {
                return maxX;
            }
            if (b.Bitmap != null)
            {
                //Non-space character
                DrawTexture(b.Bitmap, x, y, b.Width, b.Height, b.X, b.Y, b.Width, b.Height);
            }
            return x + ax;
        }

        private static readonly string PipelineCode = @"
struct VS_IN
{
	float4 pos : POSITION;
	float4 tex : TEXCOORD;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 tex : TEXCOORD;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos.x = (input.pos.x / 400) - 1;
	output.pos.y = 1 - (input.pos.y / 300);
	output.pos.w = 1;
	output.tex = input.tex;

	return output;
}

Texture2D faceTexture : register(t0);
SamplerState textureSampler : register(s0);

float4 PS(PS_IN input) : SV_Target
{
	return faceTexture.Sample(textureSampler, input.tex.xy);
}";
    }
}
