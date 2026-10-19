#include "sh_CustomizedShaderGlobalUniforms.h"
#include "sh_Utils.h"

layout(location = 0) in highp vec2 v_TexCoord;

layout(std140, set = 2, binding = 0) uniform m_OutlineParameters
{
	mediump vec4 g_Colour;
	mediump vec4 g_OutlineColour;
	mediump float g_Radius;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

mediump vec4 tex(in mediump vec2 texCoord)
{
	return texture(sampler2D(m_Texture, m_Sampler), texCoord);
}

lowp float outlineAlpha(in float radius, in mediump vec2 texCoord, in mediump vec2 texSize)
{
	mediump vec2 offset = vec2(radius) / texSize;
	lowp float alpha = 0.0;

	alpha = max(alpha, tex(texCoord - vec2(0.20, 0.98) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.38, 0.92) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.56, 0.83) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.71, 0.71) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.83, 0.56) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.92, 0.38) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.98, 0.20) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(1.00, -0.00) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.98, -0.20) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.92, -0.38) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.83, -0.56) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.71, -0.71) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.56, -0.83) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.38, -0.92) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.20, -0.98) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.00, -1.00) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.20, -0.98) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.38, -0.92) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.56, -0.83) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.71, -0.71) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.83, -0.56) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.92, -0.38) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.98, -0.20) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-1.00, 0.00) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.98, 0.20) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.92, 0.38) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.83, 0.56) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.71, 0.71) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.56, 0.83) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.38, 0.92) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(-0.20, 0.98) * offset).a);
	alpha = max(alpha, tex(texCoord - vec2(0.00, 1.00) * offset).a);
	return alpha;
}

lowp vec4 outline(float radius, mediump vec2 texCoord, mediump vec2 texSize, mediump vec4 colour)
{
	lowp float res = max(outlineAlpha(radius, texCoord, texSize), outlineAlpha(radius / 2.0, texCoord, texSize));
	return mix(vec4(0.0), colour, res);
}

void main(void)
{
	lowp vec4 texColour = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord);
	lowp vec4 originColour = vec4(mix(texColour.rgb, g_Colour.rgb, g_Colour.a), texColour.a);
	lowp vec4 outlineColour = outline(g_Radius * g_InflationPercentage, v_TexCoord, g_TexSize, g_OutlineColour);

	o_Colour = mix(outlineColour, originColour, originColour.a);
}
