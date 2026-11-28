layout(std140, set = 0, binding = 0) uniform m_RetroTvParameters
{
    mediump vec2 g_TexSize;
    mediump float g_Strength;
    mediump float g_Curvature;
    mediump float g_Noise;
    mediump float g_Time;
};

layout(set = 1, binding = 0) uniform highp texture2D m_Texture;
layout(set = 1, binding = 1) uniform highp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

const highp float PI = 3.14159265359;
const highp vec3 luma_weights = vec3(0.2126, 0.7152, 0.0722);

highp vec4 sampleTexture(highp vec2 uv)
{
    return texture(sampler2D(m_Texture, m_Sampler), clamp(uv, vec2(0.0), vec2(1.0)));
}

// The effect buffers are rendered with nearest-neighbour filtering, so the curved
// screen has to be sampled manually to avoid hard, blocky steps in the picture.
highp vec4 sampleBilinear(highp vec2 uv)
{
    highp vec2 size = g_TexSize;
    highp vec2 position = clamp(uv, vec2(0.0), vec2(1.0)) * size - 0.5;
    highp vec2 base = floor(position);
    highp vec2 fraction = position - base;
    highp vec2 max_base = size - 1.0;

    highp vec2 p00 = clamp(base, vec2(0.0), max_base);
    highp vec2 p10 = clamp(base + vec2(1.0, 0.0), vec2(0.0), max_base);
    highp vec2 p01 = clamp(base + vec2(0.0, 1.0), vec2(0.0), max_base);
    highp vec2 p11 = clamp(base + vec2(1.0, 1.0), vec2(0.0), max_base);

    highp vec4 c00 = sampleTexture((p00 + 0.5) / size);
    highp vec4 c10 = sampleTexture((p10 + 0.5) / size);
    highp vec4 c01 = sampleTexture((p01 + 0.5) / size);
    highp vec4 c11 = sampleTexture((p11 + 0.5) / size);

    highp vec4 top = mix(c00, c10, fraction.x);
    highp vec4 bottom = mix(c01, c11, fraction.x);
    return mix(top, bottom, fraction.y);
}

highp float hash21(highp vec2 p)
{
    highp vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

void main(void)
{
    highp vec2 uv = gl_FragCoord.xy / g_TexSize;
    highp vec4 source = sampleTexture(uv);

    if (g_Strength <= 0.0)
    {
        o_Colour = source;
        return;
    }

    highp float strength = clamp(g_Strength, 0.0, 1.0);
    highp vec2 centred = uv * 2.0 - 1.0;
    highp float radiusSquared = dot(centred, centred);

    // The physical tube: a rounded rectangle with a little bezel around it.
    highp vec2 screen = abs(centred) - vec2(0.96);
    highp float cornerRadius = 0.12;
    highp float screenDistance = length(max(screen + cornerRadius, vec2(0.0))) + min(max(screen.x, screen.y), 0.0) - cornerRadius;
    highp float screenWidth = max(fwidth(screenDistance), 0.0001);
    highp float screenMask = 1.0 - smoothstep(-screenWidth, screenWidth, screenDistance);

    // Barrel distortion of the picture inside the tube.
    highp vec2 world = centred * (1.0 + 0.12 * g_Curvature * strength * radiusSquared);
    highp vec2 warped = world * 0.5 + 0.5;

    // Time base error of a worn tape: a slow horizontal wobble that grows towards
    // the top of the picture where the video head switches.
    highp float wobble = sin(warped.y * 27.0 + g_Time * 2.1) * 0.0006
                       + sin(warped.y * 113.0 + g_Time * 5.3) * 0.00025;
    warped.x += wobble * (0.35 + 0.65 * smoothstep(0.85, 1.0, warped.y)) * strength;

    // A tracking band drifts up the picture, tearing the rows it passes over.
    highp float bandCentre = fract(g_Time * 0.055);
    highp float bandDistance = abs(warped.y - bandCentre);
    bandDistance = min(bandDistance, 1.0 - bandDistance);
    highp float band = 1.0 - smoothstep(0.0, 0.035, bandDistance);
    highp float tear = hash21(vec2(floor(warped.y * g_TexSize.y * 0.25), floor(g_Time * 24.0))) - 0.5;
    warped.x += band * tear * 0.0035 * strength;

    highp vec2 sampleUv = clamp(warped, vec2(0.0), vec2(1.0));
    highp vec2 texel = 1.0 / g_TexSize;

    // VHS keeps luma sharp but smears the colour information horizontally.
    highp float chromaOffset = (1.0 + 5.0 * strength) * texel.x;
    highp vec4 centreSample = sampleBilinear(sampleUv);
    highp vec4 leftSample = sampleTexture(sampleUv - vec2(chromaOffset, 0.0));
    highp vec4 rightSample = sampleTexture(sampleUv + vec2(chromaOffset, 0.0));

    highp vec3 chroma = (centreSample.rgb - dot(centreSample.rgb, luma_weights)) * 0.5;
    chroma += (leftSample.rgb - dot(leftSample.rgb, luma_weights)) * 0.25;
    chroma += (rightSample.rgb - dot(rightSample.rgb, luma_weights)) * 0.25;
    highp float luma = dot(centreSample.rgb, luma_weights);

    highp vec3 colour = mix(centreSample.rgb, vec3(luma) + chroma, 0.65 * strength);

    // Divergence of the three beams: red and blue drift away from green towards
    // the edges of the tube.
    highp float convergence = (1.0 + 2.5 * radiusSquared) * texel.x;
    highp float red = sampleTexture(sampleUv + vec2(convergence, 0.0)).r;
    highp float blue = sampleTexture(sampleUv - vec2(convergence, 0.0)).b;
    colour.r = mix(colour.r, red, 0.85 * strength);
    colour.b = mix(colour.b, blue, 0.85 * strength);

    // Scanlines and the RGB phosphor grille.
    highp float scanlinePhase = uv.y * g_TexSize.y * PI;
    highp float scanlineWidth = max(fwidth(scanlinePhase), 0.0001) * 0.5;
    highp float scanlineFilter = scanlineWidth > 0.001 ? sin(scanlineWidth) / scanlineWidth : 1.0;
    highp float scanline = 0.90 + 0.10 * cos(scanlinePhase) * scanlineFilter;

    highp float grillePhase = uv.x * g_TexSize.x * PI * 2.0 / 3.0;
    highp float grilleWidth = max(fwidth(grillePhase), 0.0001) * 0.5;
    highp float grilleFilter = grilleWidth > 0.001 ? sin(grilleWidth) / grilleWidth : 1.0;
    highp float grille = 0.94 + 0.06 * cos(grillePhase) * grilleFilter;

    // Interlaced fields alternate every frame.
    highp float field = mod(floor(uv.y * g_TexSize.y) + floor(g_Time * 30.0), 2.0);
    highp float flicker = 1.0 + 0.035 * (field * 2.0 - 1.0) * strength;

    colour *= mix(1.0, scanline * grille * flicker, strength);

    // Tape grain, much heavier inside the tracking band.
    highp vec2 noisePosition = gl_FragCoord.xy + floor(g_Time * 60.0) * vec2(17.0, 31.0);
    highp float grain = hash21(noisePosition) - 0.5;
    colour += grain * (0.045 + 0.16 * band) * g_Noise * strength;
    colour += band * 0.02 * strength;

    // A warm, slightly washed-out 80s tube: lifted blacks and reduced contrast.
    colour *= vec3(1.04, 1.0, 0.94);
    colour = mix(vec3(dot(colour, luma_weights)), colour, 1.0 - 0.12 * strength);
    colour = colour * 0.96 + 0.045 * strength;

    // Vignette and the dark corners outside the curved picture.
    highp float vignette = 1.0 - 0.30 * smoothstep(0.15, 1.5, radiusSquared) * strength;
    colour *= vignette;

    highp float contentEdge = min(min(warped.x, 1.0 - warped.x), min(warped.y, 1.0 - warped.y));
    highp float contentWidth = max(fwidth(contentEdge), 0.0001) * 2.0;
    highp float contentMask = smoothstep(-contentWidth, contentWidth, contentEdge);
    highp float mask = screenMask * contentMask;

    o_Colour = vec4(colour * mask, source.a);
}