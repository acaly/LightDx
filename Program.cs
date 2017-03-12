using LightDX.InputAttributes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightDX
{
    class Program
    {
        #region Shader Code

        public static readonly string Shader = @"
struct VS_IN
{
	float4 pos : POSITION;
	float4 col : COLOR;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 col : COLOR;
};

//Texture2D faceTexture : register(t0);
//SamplerState MeshTextureSampler : register(s0);

PS_IN VS( VS_IN input )
{
	PS_IN output = (PS_IN)0;
	
	output.pos = input.pos;
	output.col = input.col;
	
	return output;
}

float4 PS( PS_IN input ) : SV_Target
{
    //float4 c1 = faceTexture.Sample(MeshTextureSampler, input.col.xy);
	return input.col;
}
";

        #endregion

        private struct Vertex
        {
            [Position]
            public Float4 Position;
            [Color]
            public Float4 Color;
        }

        private static void Main()
        {
            var form = new Form();
            form.ClientSize = new Size(800, 600);

            using (var device = LightDevice.Create(form))
            {
                var target = device.CreateDefaultTarget(false);
                target.Apply();

                var pipeline = device.CompilePipeline(Shader, false, InputTopology.Triangle);
                pipeline.Apply();

                var input = pipeline.CreateInputDataProcessor<Vertex>();

                var buffer = input.CreateImmutableBuffer(new[] {
                    new Vertex { Color = Color.Green, Position = new Float4(0.0f, 0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = Color.Red, Position = new Float4(0.5f, -0.5f, 0.5f, 1.0f) },
                    new Vertex { Color = Color.Blue, Position = new Float4(-0.5f, -0.5f, 0.5f, 1.0f) },
                });

                form.Show();
                device.RunMultithreadLoop(delegate()
                {
                    target.ClearAll(Color.BlanchedAlmond);
                    buffer.RenderAll();
                    device.Present(true);
                });
            }
        }
    }
}
