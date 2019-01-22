using LightDx;
using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BufferResource
{
    class Program
    {
        private struct Vertex
        {
            [Position]
            public Vector4 Position;
            [Color(Format = 42 /* DXGI_FORMAT_R32_UINT */)]
            public uint Color;
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
                var target = new RenderTargetList(device.GetDefaultTarget());
                target.Apply();

                Pipeline pipeline = device.CompilePipeline(InputTopology.Triangle,
                    ShaderSource.FromResource("Shader.fx", ShaderType.Vertex | ShaderType.Pixel));
                pipeline.Apply();

                var input = pipeline.CreateVertexDataProcessor<Vertex>();
                var buffer = input.CreateImmutableBuffer(new[] {
                    new Vertex { Color = 0, Position = new Vector4(0.0f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = 1, Position = new Vector4(0.5f, -0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = 2, Position = new Vector4(-0.5f, -0.5f, 0.5f, 1.0f) },
                });

                var indexBuffer = pipeline.CreateImmutableIndexBuffer(new uint[] { 2, 0, 1 });

                var srBuffer = pipeline.CreateShaderResourceBuffer(new[]
                {
                    Color.Blue.WithAlpha(1),
                    Color.Red.WithAlpha(1),
                    Color.Green.WithAlpha(1),
                }, false);
                pipeline.SetResource(ShaderType.Vertex, 0, srBuffer);
                
                form.Show();
                device.RunMultithreadLoop(delegate ()
                {
                    target.ClearAll();
                    indexBuffer.DrawAll(buffer);
                    device.Present(true);
                });
            }
        }
    }
}
