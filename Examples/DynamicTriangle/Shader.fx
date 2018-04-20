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

cbuffer VS_CONSTANT_BUFFER : register(b0)
{
	float fTime;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	float r = abs(fTime * 2 - 1);
	output.col = input.col * (r * 0.5 + 0.5);

	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.col;
}
