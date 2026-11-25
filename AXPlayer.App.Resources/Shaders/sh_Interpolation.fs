layout(std140, set = 0, binding = 0) uniform m_InterpolationParameters
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

// The effect buffers are rendered with nearest-neighbour filtering (pixel snapping is enabled
// on the shared buffered draw data), so the current frame has to be sampled manually. The
// history buffer, on the other hand, is created with linear filtering and can be sampled directly.
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

    // Find the offset at which the previous frame best matches the current one.
    // This is a deliberately small search: it catches slow and medium motion
    // without the cost of a full optical-flow pass. Motion outside the searched
    // range simply falls back to an uncompensated temporal blend below.
    // Start from the unmoved sample so flat regions keep a zero offset instead of
    // latching onto the first equally good offset the loop happens to visit.
    highp vec3 centre_history = texture(sampler2D(m_Texture1, m_Sampler1), uv).rgb;
    highp float best_difference = dot(abs(centre_history - current.rgb), luma_weights);
    highp vec2 best_offset = vec2(0.0);

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0)
                continue;

            highp vec2 offset = vec2(float(x), float(y)) * texel * radius;
            highp vec3 history = texture(sampler2D(m_Texture1, m_Sampler1), clamp(uv + offset, vec2(0.0), vec2(1.0))).rgb;
            highp float difference = dot(abs(history - current.rgb), luma_weights);

            if (difference < best_difference)
            {
                best_difference = difference;
                best_offset = offset;
            }
        }
    }

    // Motion compensation is only applied where the search actually found a plausible
    // match. On badly matched (fast) motion the history is sampled in place, which
    // degrades gracefully into a plain temporal blend.
    highp float compensation = 1.0 - smoothstep(0.08, 0.28, best_difference);

    // A hard cut must never be blended, otherwise the previous scene would smear over
    // the new one for several frames.
    highp float continuity = 1.0 - smoothstep(0.30, 0.50, best_difference);

    // Split the estimated motion in half: the current frame is warped backwards by
    // half a frame while the previous frame is warped forwards by half a frame. The
    // blend therefore lands between the two source frames instead of on top of the
    // current one.
    highp vec2 half_offset = best_offset * 0.5 * compensation;
    highp vec4 history_sample = texture(sampler2D(m_Texture1, m_Sampler1), clamp(uv + half_offset, vec2(0.0), vec2(1.0)));
    highp vec4 current_sample = sampleCurrentBilinear(clamp(uv - half_offset, vec2(0.0), vec2(1.0)));

    highp float blend = 0.5 * clamp(g_Strength, 0.0, 1.0) * continuity;
    o_Colour = mix(current_sample, history_sample, blend);
}
