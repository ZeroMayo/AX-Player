layout(std140, set = 0, binding = 0) uniform m_InterpolationParameters
{
    highp vec2 g_TexSize;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;
layout(set = 2, binding = 0) uniform lowp texture2D m_PreviousTexture;
layout(set = 2, binding = 1) uniform lowp sampler m_PreviousSampler;

layout(location = 0) out vec4 o_Colour;

highp float luminance(highp vec3 colour)
{
    return dot(colour, vec3(0.299, 0.587, 0.114));
}

highp vec2 estimateMotion(highp vec2 uv, highp vec2 texel)
{
    highp float xx = 0.0;
    highp float xy = 0.0;
    highp float yy = 0.0;
    highp float xt = 0.0;
    highp float yt = 0.0;

    for (int y = -1; y <= 1; ++y)
    {
        for (int x = -1; x <= 1; ++x)
        {
            highp vec2 offset = vec2(float(x), float(y)) * texel;
            highp float previous = luminance(texture(sampler2D(m_PreviousTexture, m_PreviousSampler), uv + offset).rgb);
            highp float current = luminance(texture(sampler2D(m_Texture, m_Sampler), uv + offset).rgb);
            highp float currentRight = luminance(texture(sampler2D(m_Texture, m_Sampler), uv + offset + vec2(texel.x, 0.0)).rgb);
            highp float currentLeft = luminance(texture(sampler2D(m_Texture, m_Sampler), uv + offset - vec2(texel.x, 0.0)).rgb);
            highp float currentUp = luminance(texture(sampler2D(m_Texture, m_Sampler), uv + offset + vec2(0.0, texel.y)).rgb);
            highp float currentDown = luminance(texture(sampler2D(m_Texture, m_Sampler), uv + offset - vec2(0.0, texel.y)).rgb);

            highp float gradientX = (currentRight - currentLeft) * 0.5;
            highp float gradientY = (currentUp - currentDown) * 0.5;
            highp float temporal = current - previous;

            xx += gradientX * gradientX;
            xy += gradientX * gradientY;
            yy += gradientY * gradientY;
            xt += gradientX * temporal;
            yt += gradientY * temporal;
        }
    }

    highp float determinant = xx * yy - xy * xy;
    if (determinant < 0.00001)
        return vec2(0.0);

    highp vec2 motion = vec2(
        (xy * yt - yy * xt) / determinant,
        (xy * xt - xx * yt) / determinant);

    return clamp(motion, vec2(-12.0), vec2(12.0));
}

void main(void)
{
    highp vec2 texel = 1.0 / g_TexSize;
    highp vec2 uv = gl_FragCoord.xy * texel;
    highp vec2 motion = estimateMotion(uv, texel);

    highp vec2 previousUv = clamp(uv - motion * 0.5 * texel, texel * 0.5, vec2(1.0) - texel * 0.5);
    highp vec2 currentUv = clamp(uv + motion * 0.5 * texel, texel * 0.5, vec2(1.0) - texel * 0.5);
    lowp vec4 previousColour = texture(sampler2D(m_PreviousTexture, m_PreviousSampler), previousUv);
    lowp vec4 currentColour = texture(sampler2D(m_Texture, m_Sampler), currentUv);

    o_Colour = mix(previousColour, currentColour, 0.5);
}
