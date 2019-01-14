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

namespace Cube
{
    class Program
    {
        private struct Vertex
        {
            [Position]
            public Vector4 Position;
            [Color]
            public Vector4 Color;
        }

        private static void SetupCubeFace(Vertex[] buffer, int offset, Vector3 center3, Vector3 u3, Vector3 v3)
        {
            var center = new Vector4(center3, 1);
            var u = new Vector4(u3 / 2, 0);
            var v = new Vector4(v3 / 2, 0);
            var pp = center + u + v;
            var pn = center + u - v;
            var np = center - u + v;
            var nn = center - u - v;
            buffer[offset + 0].Position = np;
            buffer[offset + 1].Position = pp;
            buffer[offset + 2].Position = pn;
            buffer[offset + 3].Position = pn;
            buffer[offset + 4].Position = nn;
            buffer[offset + 5].Position = np;
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
                var target = new RenderTargetList(device.GetDefaultTarget(), device.CreateDepthStencilTarget());
                target.Apply();

                Pipeline pipeline = device.CompilePipeline(InputTopology.Triangle,
                    ShaderSource.FromResource("Shader.fx", ShaderType.Vertex | ShaderType.Pixel));
                pipeline.Apply();

                var vertexConstant = pipeline.CreateConstantBuffer<Matrix4x4>();
                pipeline.SetConstant(ShaderType.Vertex, 0, vertexConstant);

                var input = pipeline.CreateVertexDataProcessor<Vertex>();
                var bufferData = new Vertex[6 * 6];
                for (int i = 0; i < bufferData.Length; ++i)
                {
                    bufferData[i].Color = Color.FromArgb(250, i / 6 * 40, 250 - i / 6 * 30).WithAlpha(1);
                }
                SetupCubeFace(bufferData, 00, new Vector3(0.5f, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
                SetupCubeFace(bufferData, 06, new Vector3(-0.5f, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1));
                SetupCubeFace(bufferData, 12, new Vector3(0, 0.5f, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1));
                SetupCubeFace(bufferData, 18, new Vector3(0, -0.5f, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0));
                SetupCubeFace(bufferData, 24, new Vector3(0, 0, 0.5f), new Vector3(0, 1, 0), new Vector3(1, 0, 0));
                SetupCubeFace(bufferData, 30, new Vector3(0, 0, -0.5f), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
                var buffer = input.CreateImmutableBuffer(bufferData);

                var camera = new Camera(new Vector3(10, 0, 0));
                camera.SetForm(form);
                var proj = device.CreatePerspectiveFieldOfView((float)Math.PI / 4).Transpose();

                vertexConstant.Value = proj * camera.GetViewMatrix();
                var pt = new Vector4(0, 0, 0, 0);
                var r = Vector4.Transform(pt, vertexConstant.Value);

                form.Show();
                device.RunMultithreadLoop(delegate ()
                {
                    target.ClearAll();

                    camera.Step();
                    vertexConstant.Value = proj * camera.GetViewMatrix();
                    vertexConstant.Update();

                    buffer.DrawAll();
                    device.Present(true);
                });
            }
        }
    }
}
