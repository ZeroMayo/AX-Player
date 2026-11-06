#ifndef SCROLL_EDGE_BLUR_FS
#define SCROLL_EDGE_BLUR_FS

#include "sh_Utils.h"

#undef INV_SQRT_2PI
#define INV_SQRT_2PI 0.39894

layout(location = 2) in mediump vec2 v_TexCoord;

layout(std140, set = 0, binding = 0) uniform m_ScrollEdgeBlurParameters
{
	mediump vec2 g_TexSize;
	int g_Radius;
	mediump float g_Sigma;
	highp vec2 g_BlurDirection;
	mediump float g_StartEdgeSize;
	mediump float g_EndEdgeSize;
	int g_EdgeAxis;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

mediump float computeGauss(in mediump float x, in mediump float sigma)
{
	return INV_SQRT_2PI * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
}

lowp vec4 blur(int radius, highp vec2 direction, mediump vec2 texCoord, mediump vec2 texSize, mediump float sigma)
{
	mediump float factor = computeGauss(0.0, sigma);
	mediump vec4 sum = texture(sampler2D(m_Texture, m_Sampler), texCoord) * factor;
	mediump float totalFactor = factor;

	for (int i = 2; i <= 200; i += 2)
	{
		mediump float x = float(i) - 0.5;
		factor = computeGauss(x, sigma) * 2.0;
		totalFactor += 2.0 * factor;
		sum += texture(sampler2D(m_Texture, m_Sampler), texCoord + direction * x / texSize) * factor;
		sum += texture(sampler2D(m_Texture, m_Sampler), texCoord - direction * x / texSize) * factor;

		if (i >= radius)
			break;
	}

	float one = g_BackbufferDraw ? 0 : 1;
	return sum / totalFactor * one;
}

void main(void)
{
	mediump float edgeCoordinate = g_EdgeAxis == 1 ? v_TexCoord.y : v_TexCoord.x;
	mediump float edgeSize = g_EdgeAxis == 1 ? g_TexSize.y : g_TexSize.x;
	mediump float startEdgeStrength = g_StartEdgeSize > 0.0
		? 1.0 - smoothstep(0.0, g_StartEdgeSize, edgeCoordinate * edgeSize)
		: 0.0;
	mediump float endEdgeStrength = g_EndEdgeSize > 0.0
		? 1.0 - smoothstep(0.0, g_EndEdgeSize, (1.0 - edgeCoordinate) * edgeSize)
		: 0.0;
	mediump float edgeStrength = max(startEdgeStrength, endEdgeStrength);
	mediump vec4 original = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord);

	if (edgeStrength <= 0.0)
	{
		o_Colour = original;
		return;
	}

	mediump float sigma = max(g_Sigma * edgeStrength, 0.01);
	o_Colour = blur(g_Radius, g_BlurDirection, v_TexCoord, g_TexSize, sigma);
}

#endif
