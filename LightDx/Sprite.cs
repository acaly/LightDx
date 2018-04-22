using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    //TODO specify address and filtering options (at startup)
    //TODO support color
    public sealed class Sprite : IDisposable
    {
        private struct Vertex
        {
            [Position]
            public Vector4 Position;
            [TexCoord]
            public Vector4 TexCoord;
        }

        private struct VSConstant
        {
            public float Width;
            public float Height;
        }

        private readonly LightDevice _device;
        private readonly Pipeline _pipeline;
        private readonly VertexDataProcessor<Vertex> _vertexProcessor;
        private readonly VertexBuffer _buffer;
        private readonly ConstantBuffer<VSConstant> _constant;
        private readonly Vertex[] _array;
        private bool _disposed;

        public Sprite(LightDevice device)
        {
            _device = device;
            device.AddComponent(this);

            _pipeline = device.CompilePipeline(InputTopology.Triangle,
                ShaderSource.FromString(PipelineCode, ShaderType.Vertex | ShaderType.Pixel));
            _pipeline.SetBlender(Blender.AlphaBlender);

            _vertexProcessor = _pipeline.CreateVertexDataProcessor<Vertex>();
            _array = new[] {
                new Vertex { TexCoord = new Vector4(0, 0, 0, 0), Position = new Vector4(0, 0, 0, 0) },
                new Vertex { TexCoord = new Vector4(1, 0, 0, 0), Position = new Vector4(1, 0, 0, 0) },
                new Vertex { TexCoord = new Vector4(0, 1, 0, 0), Position = new Vector4(0, 1, 0, 0) },

                new Vertex { TexCoord = new Vector4(0, 1, 0, 0), Position = new Vector4(0, 1, 0, 0) },
                new Vertex { TexCoord = new Vector4(1, 0, 0, 0), Position = new Vector4(1, 0, 0, 0) },
                new Vertex { TexCoord = new Vector4(1, 1, 0, 0), Position = new Vector4(1, 1, 0, 0) },
            };
            _buffer = _vertexProcessor.CreateDynamicBuffer(6);

            _constant = _pipeline.CreateConstantBuffer<VSConstant>();
            _pipeline.SetConstant(ShaderType.Vertex, 0, _constant);
        }

        ~Sprite()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _pipeline.Dispose();
                _vertexProcessor.Dispose();
                _buffer.Dispose();
                _constant.Dispose();

                _device.RemoveComponent(this);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public void Apply()
        {
            _constant.Value.Width = _device.ScreenWidth;
            _constant.Value.Height = _device.ScreenHeight;
            _constant.Update();

            _pipeline.Apply();
        }

        private void CheckPipeline()
        {
            if (!_pipeline.IsActive)
            {
                throw new InvalidOperationException();
            }
        }

        public void DrawTexture(Texture2D tex, int x, int y, int w, int h)
        {
            CheckPipeline();
            DrawTextureInternal(tex, x, y, w, h, 0, 0, tex.Width, tex.Height, 0, 0, 0);
        }

        public void DrawTexture(Texture2D tex, float x, float y, float w, float h, int tx, int ty, int tw, int th)
        {
            CheckPipeline();
            DrawTextureInternal(tex, x, y, w, h, tx, ty, tw, th, 0, 0, 0);
        }

        public void DrawTexture(Texture2D tex, float x, float y, float w, float h, int tx, int ty, int tw, int th, float cx, float cy, float rotate)
        {
            CheckPipeline();
            DrawTextureInternal(tex, x, y, w, h, tx, ty, tw, th, cx, cy, rotate);
        }

        private void DrawTextureInternal(Texture2D tex, float x, float y, float w, float h, int tx, int ty, int tw, int th, float cx, float cy, float rotate)
        {
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
            _buffer.Update(_array);
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
            CheckPipeline();
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
                DrawTextureInternal(b.Bitmap, x, y, b.Width, b.Height, b.X, b.Y, b.Width, b.Height, 0, 0, 0);
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

cbuffer VS_CONSTANT_BUFFER : register(b0)
{
	float fWidth;
	float fHeight;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos.x = (input.pos.x / fWidth * 2) - 1;
	output.pos.y = 1 - (input.pos.y / fHeight * 2);
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
