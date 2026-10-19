// see : https://github.com/kiwipxl/GLSL-shaders/blob/master/repeat.glsl
#include "sh_CustomizedShaderGlobalUniforms.h"
#include "sh_Utils.h"

layout(location = 0) in highp vec2 v_TexCoord;

// repeat texture
layout(set = 2, binding = 0) uniform lowp texture2D m_Texture1;
layout(set = 2, binding = 1) uniform lowp sampler m_Sampler1;

layout(std140, set = 3, binding = 0) uniform m_RepeatMovingBackgroundParameters
{
    mediump vec2 g_RepeatSampleCoord;
    mediump vec2 g_RepeatSampleSize;
    mediump vec2 g_DisplaySize;
    mediump vec2 g_DisplayBorder;
    mediump vec2 g_Speed;
    mediump float g_Mix;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

void main(void) {
    // calculate how many times texture should be repeated.
    vec2 repeat = g_TexSize / (g_DisplaySize + g_DisplayBorder);

    // get the repeat texture coordinate.
    float repeatTexCoordX = mod(v_TexCoord.x * repeat.x + g_Speed.x * g_Time, 1.0);
    float repeatTexCoordY = mod(v_TexCoord.y * repeat.y + g_Speed.y * g_Time, 1.0);
    vec2 repeatTexCoord = vec2(repeatTexCoordX, repeatTexCoordY) / g_DisplaySize * (g_DisplaySize + g_DisplayBorder);

    // because repeat texture will be the size of 1024*1024, so should make a conversion to get the target area of the texture.
    vec2 fixedTexCoord = repeatTexCoord * g_RepeatSampleSize + g_RepeatSampleCoord;

    // get point colour from sample.
    vec4 texColor = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord);
    vec4 repeatSampleColor = vec4(texture(sampler2D(m_Texture1, m_Sampler1), fixedTexCoord).xyz, texColor.a);
    o_Colour = mix(texColor, repeatSampleColor, g_Mix);
}
