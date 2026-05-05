// Preview pixel shader aligned with FFmpeg: eq (BT.601-style Y + scaled Cb/Cr) → hue (rotate Cb/Cr)
// → lutrgb-style gamma on RGB. Constants match FlowMy.Helpers.VideoColorGrading filter order.
sampler2D implicitInputSampler : register(S0);

float2 bc : register(C0);
float2 sg : register(C1);
float2 hueCs : register(C2);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 tex = tex2D(implicitInputSampler, uv);
    float3 c = tex.rgb;
    float brightness = bc.x;
    float contrast = bc.y;
    float saturation = sg.x;
    float gamma = sg.y;
    float cosH = hueCs.x;
    float sinH = hueCs.y;

    // RGB ↔ YCbCr (BT.601 full range), same basis as libavfilter eq on YUV then vf_hue on chroma.
    float y = dot(c, float3(0.299, 0.587, 0.114));
    float cb = (c.b - y) / 1.772;
    float cr = (c.r - y) / 1.402;

    // eq: luminance LUT-style; chroma = saturation × centered chroma (see vf_eq param[1]/[2]).
    y = contrast * (y - 0.5) + 0.5 + brightness;
    cb = saturation * cb;
    cr = saturation * cr;

    // hue: rotate chroma vector (same math as vf_hue create_chrominance_lut, centered components).
    float cb2 = cb * cosH - cr * sinH;
    float cr2 = cb * sinH + cr * cosH;

    float3 outc;
    outc.r = y + 1.402 * cr2;
    outc.g = y - 0.344136 * cb2 - 0.714136 * cr2;
    outc.b = y + 1.772 * cb2;

    float invG = 1.0 / max(gamma, 0.05);
    outc = pow(saturate(outc), float3(invG, invG, invG));
    return float4(outc, tex.a);
}
