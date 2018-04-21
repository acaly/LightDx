using LightDx;
using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamicTriangle
{
    static class Program
    {
        private struct Vertex
        {
            [Position]
            public Vector4 Position;
            [Color]
            public Vector4 Color;
        }

        private struct ConstantBuffer
        {
            public Vector4 GlobalAlpha;
            public float Time;
        }

        static void SetCoordinate(LightDevice device, ref Vector4 position, double angle)
        {
            position.X = 0.5f * (float)Math.Cos(angle) * 600 / device.ScreenWidth;
            position.Y = 0.5f * (float)Math.Sin(angle) * 600 / device.ScreenHeight;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new Form();
            form.ClientSize = new Size(800, 600);

            using (var device = LightDevice.Create(form))
            {
                var target = new RenderTarget(device.GetDefaultTarget());
                target.Apply();

                Pipeline pipeline = device.CompilePipeline(InputTopology.Triangle,
                    ShaderSource.FromResource("Shader.fx", ShaderType.Vertex | ShaderType.Pixel));
                pipeline.Apply();

                var vertexData = new[] {
                    new Vertex { Position = new Vector4(0, 0, 0.5f, 1), Color = Color.Green.WithAlpha(1) },
                    new Vertex { Position = new Vector4(0, 0, 0.5f, 1), Color = Color.Red.WithAlpha(1) },
                    new Vertex { Position = new Vector4(0, 0, 0.5f, 1), Color = Color.Blue.WithAlpha(1) },
                };

                var input = pipeline.CreateVertexDataProcessor<Vertex>();
                var buffer = input.CreateDynamicBuffer(3);

                var indexBuffer = pipeline.CreateImmutableIndexBuffer(new uint[] { 0, 1, 2 });

                var constantBuffer = pipeline.CreateConstantBuffer<ConstantBuffer>();
                pipeline.SetConstant(ShaderType.Vertex, 0, constantBuffer);
                pipeline.SetConstant(ShaderType.Pixel, 0, constantBuffer);

                constantBuffer.Value.GlobalAlpha = new Vector4(1, 1, 1, 1);

                form.Show();
                
                var i = 0;
                var rand = new Random();

                var clock = Stopwatch.StartNew();
                device.RunMultithreadLoop(delegate ()
                {
                    var angle = -clock.Elapsed.TotalSeconds * Math.PI / 3;
                    var distance = Math.PI * 2 / 3;

                    SetCoordinate(device, ref vertexData[0].Position, angle);
                    SetCoordinate(device, ref vertexData[1].Position, angle - distance);
                    SetCoordinate(device, ref vertexData[2].Position, angle + distance);
                    buffer.Update(vertexData);

                    constantBuffer.Value.Time = ((float)clock.Elapsed.TotalSeconds % 2) / 2;

                    if (++i == 60)
                    {
                        i = 0;
                        constantBuffer.Value.GlobalAlpha.X = (float)rand.NextDouble() * 0.5f + 0.5f;
                        constantBuffer.Value.GlobalAlpha.Y = (float)rand.NextDouble() * 0.5f + 0.5f;
                        constantBuffer.Value.GlobalAlpha.Z = (float)rand.NextDouble() * 0.5f + 0.5f;
                    }
                    constantBuffer.Update();

                    target.ClearAll();
                    indexBuffer.DrawAll(buffer);
                    device.Present(true);
                });
            }
        }
    }
}
