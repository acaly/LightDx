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

PS_IN VS( VS_IN input )
{
	PS_IN output = (PS_IN)0;
	
	output.pos = input.pos;
	output.tex = input.tex;
	
	return output;
}

Texture2D faceTexture : register(t0);
SamplerState textureSampler : register(s0);

float4 PS( PS_IN input ) : SV_Target
{
	return faceTexture.Sample(textureSampler, input.tex.xy);
}
