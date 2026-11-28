layout(std140, set = 0, binding = 0) uniform m_MotionBlurParameters
{
    highp vec2 g_TexSize;
    highp float g_Strength;
    highp float g_HistoryValid;
    highp float g_Radius;
};

layout(set = 1, binding = 0) uniform highp texture2D m_Texture;
layout(set = 1, binding = 1) uniform highp sampler m_Sampler;

layout(set = 2, binding = 0) uniform highp texture2D m_Texture1;
layout(set = 2, binding = 1) uniform highp sampler m_Sampler1;

layout(location = 0) out vec4 o_Colour;

const vec3 luma_weights = vec3(0.2126, 0.7152, 0.0722);

// Samples taken across the exposure window. Seven is enough to hide the individual
// taps at typical video resolutions while staying cheap on integrated GPUs.
const int smear_taps = 7;

// The effect buffers are rendered with nearest-neighbour filtering (pixel snapping is
// enabled on the shared buffered draw data), so fractional positions in the current
// frame have to be interpolated manually.
highp vec4 sampleCurrentBilinear(highp vec2 uv)
{
    highp vec2 size = g_TexSize;
    highp vec2 position = uv * size - 0.5;
    highp vec2 base = floor(position);
    highp vec2 fraction = position - base;
    highp vec2 max_base = size - 1.0;

    highp vec2 p00 = clamp(base, vec2(0.0), max_base);
    highp vec2 p10 = clamp(base + vec2(1.0, 0.0), vec2(0.0), max_base);
    highp vec2 p01 = clamp(base + vec2(0.0, 1.0), vec2(0.0), max_base);
    highp vec2 p11 = clamp(base + vec2(1.0, 1.0), vec2(0.0), max_base);

    highp vec4 c00 = texture(sampler2D(m_Texture, m_Sampler), (p00 + 0.5) / size);
    highp vec4 c10 = texture(sampler2D(m_Texture, m_Sampler), (p10 + 0.5) / size);
    highp vec4 c01 = texture(sampler2D(m_Texture, m_Sampler), (p01 + 0.5) / size);
    highp vec4 c11 = texture(sampler2D(m_Texture, m_Sampler), (p11 + 0.5) / size);

    highp vec4 top = mix(c00, c10, fraction.x);
    highp vec4 bottom = mix(c01, c11, fraction.x);
    return mix(top, bottom, fraction.y);
}

highp vec4 sampleHistory(highp vec2 uv)
{
    return texture(sampler2D(m_Texture1, m_Sampler1), clamp(uv, vec2(0.0), vec2(1.0)));
}

void main(void)
{
    highp vec2 uv = gl_FragCoord.xy / g_TexSize;
    highp vec4 current = texture(sampler2D(m_Texture, m_Sampler), uv);

    // The first frame after (re)enabling the effect, a resize, or a pause has no usable
    // history yet. Pass the frame through so the history starts from a known state.
    if (g_Strength <= 0.0 || g_HistoryValid < 0.5)
    {
        o_Colour = current;
        return;
    }

    highp vec2 texel = 1.0 / g_TexSize;
    highp float radius = max(g_Radius, 1.0);

    // Difference between the frame and its history without any movement. Every search
    // result is measured against this, so a static frame can never latch onto a random
    // offset that happens to score marginally better due to noise.
    highp float static_difference = dot(abs(sampleHistory(uv).rgb - current.rgb), luma_weights);

    // Practically static pixels cannot carry any motion, so they skip the whole search.
    // This keeps the cost of the effect proportional to the amount of movement on screen
    // instead of paying for every tap on every frame of a mostly still video.
    if (static_difference < 0.02)
    {
        o_Colour = current;
        return;
    }

    highp float best_difference = static_difference;
    highp vec2 best_offset = vec2(0.0);

    // Coarse search on a radius-sized grid. This covers fast motion without the cost of a
    // full optical-flow pass; movement outside the searched range is left unblurred rather
    // than guessed at, which keeps fast cuts and very quick motion artefact free.
    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            if (x == 0 && y == 0)
                continue;

            highp vec2 offset = vec2(float(x), float(y)) * texel * radius;
            highp float difference = dot(abs(sampleHistory(uv + offset).rgb - current.rgb), luma_weights);

            if (difference < best_difference)
            {
                best_difference = difference;
                best_offset = offset;
            }
        }
    }

    // The coarse grid quantises the estimate, which would make the blur direction visibly
    // stair-step. The true minimum is guaranteed to lie within half a coarse step, so a
    // single half-step refinement lowers the error to a quarter of a texel grid cell.
    // Static regions short-circuit this, so the extra taps are only paid where it matters.
    if (best_offset.x != 0.0 || best_offset.y != 0.0)
    {
        highp vec2 coarse_offset = best_offset;
        highp vec2 refine_step = texel * max(radius * 0.5, 1.0);

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                highp vec2 offset = coarse_offset + vec2(float(x), float(y)) * refine_step;
                highp float difference = dot(abs(sampleHistory(uv + offset).rgb - current.rgb), luma_weights);

                if (difference < best_difference)
                {
                    best_difference = difference;
                    best_offset = offset;
                }
            }
        }
    }

    // Only trust the recorded motion when it explains the frame clearly better than
    // assuming no movement at all.
    highp float motion_gain = clamp((static_difference - best_difference) / max(static_difference, 0.001), 0.0, 1.0);
    highp float match_quality = 1.0 - smoothstep(0.08, 0.28, best_difference);
    highp float confidence = match_quality * smoothstep(0.15, 0.45, motion_gain);

    // A hard cut must never be smeared, otherwise the previous scene would bleed over
    // the new one for several frames.
    highp float continuity = 1.0 - smoothstep(0.30, 0.50, best_difference);

    highp float exposure = clamp(g_Strength, 0.0, 1.0);
    highp vec2 motion = best_offset * confidence;

    // Smear the current frame along the motion path. Full strength opens the shutter for
    // one whole source frame, which is the longest exposure that still uses only real data.
    highp vec2 smear = motion * (0.5 * exposure);
    highp vec4 smeared = vec4(0.0);
    highp float total_weight = 0.0;

    for (int i = 0; i < smear_taps; i++)
    {
        highp float t = (float(i) / float(smear_taps - 1)) * 2.0 - 1.0;
        highp float weight = 1.0 - abs(t) * 0.65;

        smeared += sampleCurrentBilinear(clamp(uv + smear * t, vec2(0.0), vec2(1.0))) * weight;
        total_weight += weight;
    }

    smeared /= total_weight;

    // Blend in the previous frame, reprojected along the motion, to round off the edges of
    // the exposure window. Because the sample is pulled from the matched position the
    // content stays aligned, so this softens the result instead of ghosting it.
    highp vec4 history_sample = sampleHistory(uv + motion);

    // Retaining history without a confident match is ghosting, not blur, so the temporal
    // term fades out whenever the motion estimate could not explain the frame.
    highp float retention = 0.35 * exposure * continuity * confidence;

    o_Colour = mix(smeared, history_sample, retention);
}
