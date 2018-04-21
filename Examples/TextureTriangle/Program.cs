using LightDx;
using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TextureTriangle
{
    static class Program
    {
        private struct Vertex
        {
            [Position]
            public Vector4 Position;
            [TexCoord]
            public Vector4 TexCoord;
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

                Texture2D texture;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TextureTriangle.Hiyori.png"))
                {
                    using (var bitmap = new Bitmap(stream))
                    {
                        texture = device.CreateTexture2D(bitmap);
                        pipeline.SetResource(0, texture);
                    }
                }

                pipeline.Apply();

                var input = pipeline.CreateVertexDataProcessor<Vertex>();
                var buffer = input.CreateImmutableBuffer(new[] {
                    new Vertex { TexCoord = new Vector4(0, 0, 0, 0), Position = new Vector4(-0.5f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { TexCoord = new Vector4(1, 0, 0, 0), Position = new Vector4(0.5f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { TexCoord = new Vector4(0, 1, 0, 0), Position = new Vector4(-0.5f, -0.5f, 0.5f, 1.0f) },
                    
                    new Vertex { TexCoord = new Vector4(0, 1, 0, 0), Position = new Vector4(-0.5f, -0.5f, 0.5f, 1.0f) },
                    new Vertex { TexCoord = new Vector4(1, 0, 0, 0), Position = new Vector4(0.5f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { TexCoord = new Vector4(1, 1, 0, 0), Position = new Vector4(0.5f, -0.5f, 0.5f, 1.0f) },
                });

                form.Show();
                device.RunMultithreadLoop(delegate()
                {
                    target.ClearAll();
                    buffer.DrawAll();
                    device.Present(true);
                });
            }
        }
    }
}
