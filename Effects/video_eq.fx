sampler2D implicitInputSampler : register(S0);

float2 bc : register(C0);
float2 sg : register(C1);
float2 hueCs : register(C2);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 c = tex2D(implicitInputSampler, uv);
    float b = bc.x;
    float contrast = bc.y;
    float sat = sg.x;
    float gamma = sg.y;

    c.rgb = c.rgb + b;
    c.rgb = (c.rgb - 0.5) * contrast + 0.5;

    float yl = dot(c.rgb, float3(0.299, 0.587, 0.114));
    c.rgb = lerp(float3(yl, yl, yl), c.rgb, sat);

    float3 axis = float3(0.577350269, 0.577350269, 0.577350269);
    float yn = dot(c.rgb, axis);
    float3 u = c.rgb - axis * yn;
    float3 v = cross(axis, u);
    c.rgb = u * hueCs.x + v * hueCs.y + axis * yn;

    float invG = 1.0 / max(gamma, 0.05);
    c.r = pow(max(c.r, 0.0), invG);
    c.g = pow(max(c.g, 0.0), invG);
    c.b = pow(max(c.b, 0.0), invG);

    return saturate(c);
}
