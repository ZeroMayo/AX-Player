// Should include this file if is customized shader.

layout(std140, set = 0, binding = 0) uniform m_SharedParameters
{
    mediump vec2 g_TexSize;
    float g_InflationPercentage;
    mediump float g_Time;
};