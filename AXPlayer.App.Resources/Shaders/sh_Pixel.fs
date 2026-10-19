// see the demo: https://www.geeks3d.com/20101029/shader-library-pixelation-post-processing-effect-glsl/
#include "sh_CustomizedShaderGlobalUniforms.h"
#include "sh_Utils.h"

layout(location = 0) in highp vec2 v_TexCoord;

layout(std140, set = 2, binding = 0) uniform m_PixelParameters
{
	mediump vec2 g_Size;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

void main(void) 
{ 
	vec2 separaorParts = g_TexSize / (g_Size * g_InflationPercentage);
	vec2 uv = v_TexCoord;
	uv = uv * separaorParts;
    uv = floor(uv);
    uv = uv / separaorParts;
    o_Colour = texture(sampler2D(m_Texture, m_Sampler), uv);
}