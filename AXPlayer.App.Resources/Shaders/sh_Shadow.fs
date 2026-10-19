#include "sh_CustomizedShaderGlobalUniforms.h"
#include "sh_Utils.h"

layout(location = 0) in highp vec2 v_TexCoord;

layout(std140, set = 2, binding = 0) uniform m_ShadowParameters
{
	mediump vec4 g_Colour;
	mediump vec2 g_Offset;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

lowp vec4 shadow(texture2D tex, mediump vec2 texCoord, mediump vec2 texSize, mediump vec4 colour, mediump vec2 offset)
{
	return texture(sampler2D(tex, m_Sampler), texCoord + offset / texSize).a * colour;
}

void main(void)
{
	lowp vec4 texture = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord);
	lowp vec4 shadow = shadow(m_Texture, v_TexCoord, g_TexSize, g_Colour, g_Offset * g_InflationPercentage);
	o_Colour = mix(shadow, texture, texture.a);
}