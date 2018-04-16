using LightDx;
using LightDx.InputAttributes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Triangle
{
    static class Program
    {
        private struct Vertex
        {
            [Position]
            public Float4 Position;
            [Color]
            public Float4 Color;
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

                Pipeline pipeline;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Triangle.Shader.fx"))
                {
                    pipeline = device.CompilePipeline(ShaderSource.FromStream(stream), false, InputTopology.Triangle);
                }
                pipeline.Apply();

                var input = pipeline.CreateInputDataProcessor<Vertex>();
                var buffer = input.CreateImmutableBuffer(new[] {
                    new Vertex { Color = Color.Green, Position = new Float4(0.0f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = Color.Red, Position = new Float4(0.5f, -0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = Color.Blue, Position = new Float4(-0.5f, -0.5f, 0.5f, 1.0f) },
                });

                var indexBuffer = pipeline.CreateImmutableIndexBuffer(new uint[] { 2, 0, 1 });

                form.Show();
                device.RunMultithreadLoop(delegate()
                {
                    target.ClearAll();
                    indexBuffer.DrawAll(buffer);
                    device.Present(true);
                });
            }
        }
    }
}
