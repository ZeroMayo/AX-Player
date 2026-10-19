// see the demo: https://developer.amazon.com/es/blogs/appstore/post/acefafad-29ba-4f31-8dae-00805fda3f58/intro-to-shaders-and-surfaces-with-gamemaker-studio-2
#include "sh_CustomizedShaderGlobalUniforms.h"
#include "sh_Utils.h"

layout(location = 0) in highp vec2 v_TexCoord;

layout(std140, set = 2, binding = 0) uniform m_RainbowParameters
{
	highp vec2 g_Uv;
	mediump float g_Speed;
	mediump float g_Saturation;
	mediump float g_Brightness;
	mediump float g_Section;
	mediump float g_Mix;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(location = 0) out vec4 o_Colour;

void main(void)
{
	float pos = (v_TexCoord.y - g_Uv[0]) / (g_Uv[1] - g_Uv[0]);
	vec4 texColor = texture(sampler2D(m_Texture, m_Sampler), v_TexCoord);

	vec4 col = vec4(g_Section * ((g_Time * g_Speed) + pos), g_Saturation, g_Brightness, 1);
	vec4 finalCol = mix(texColor, vec4(hsv2rgb(col).xyz, texColor.a), g_Mix);

	o_Colour = finalCol;
}