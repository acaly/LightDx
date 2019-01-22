struct VS_IN
{
	float4 pos : POSITION;
	uint col : COLOR;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 col : COLOR;
};

Buffer<float4> colors : register(t0);

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	output.col = colors.Load(input.col);

	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.col;
}
