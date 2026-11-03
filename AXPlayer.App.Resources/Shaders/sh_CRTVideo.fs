layout(std140, set = 0, binding = 0) uniform m_CrtParameters
{
    mediump vec2 g_TexSize;
    mediump float g_Strength;
};

layout(set = 1, binding = 0) uniform texture2D m_Texture;
layout(set = 1, binding = 1) uniform sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

const mediump float PI = 3.14159265359;

void main(void)
{
    highp vec2 uv = gl_FragCoord.xy / g_TexSize;
    highp vec2 centred = uv * 2.0 - 1.0;
    highp vec2 warped = centred + centred * abs(centred.yx) * abs(centred.yx) * 0.025;
    uv = mix(uv, warped * 0.5 + 0.5, g_Strength);

    highp float edgeDistance = min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y));
    highp float edgeWidth = max(fwidth(edgeDistance), 0.0001);
    highp float edgeCoverage = smoothstep(-edgeWidth, edgeWidth, edgeDistance);
    highp vec2 sampleUv = clamp(uv, vec2(0.0), vec2(1.0));
    highp vec4 colour = texture(sampler2D(m_Texture, m_Sampler), sampleUv);

    highp float scanlinePhase = uv.y * g_TexSize.y * PI;
    highp float grillePhase = uv.x * g_TexSize.x * PI * 2.0 / 3.0;
    highp float scanlineWidth = fwidth(scanlinePhase) * 0.5;
    highp float grilleWidth = fwidth(grillePhase) * 0.5;
    highp float scanlineFilter = scanlineWidth > 0.001 ? sin(scanlineWidth) / scanlineWidth : 1.0;
    highp float grilleFilter = grilleWidth > 0.001 ? sin(grilleWidth) / grilleWidth : 1.0;
    highp float scanline = 0.94 + 0.06 * cos(scanlinePhase) * scanlineFilter;
    highp float grille = 0.94 + 0.06 * cos(grillePhase) * grilleFilter;
    highp float vignette = 1.0 - 0.22 * smoothstep(0.15, 1.4, dot(centred, centred));
    highp float effect = scanline * grille * vignette * edgeCoverage;

    colour.rgb *= mix(1.0, effect, g_Strength);

    o_Colour = colour;
}
