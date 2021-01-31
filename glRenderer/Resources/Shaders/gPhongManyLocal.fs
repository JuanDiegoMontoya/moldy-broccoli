#version 460 core
#include "common.h"

#define SPECULAR_STRENGTH 1

struct PointLight
{
  vec4 diffuse;
  vec4 specular;

  vec4 position;
  float linear;
  float quadratic;
  float radiusSquared;
  float _padding;
};

layout (std430, binding = 0) readonly buffer lit
{
  PointLight lights[];
};

layout (location = 0) in vec3 vLightPos;
layout (location = 1) in flat int vInstanceID;

layout (location = 2) uniform sampler2D gNormal;
layout (location = 3) uniform sampler2D gAlbedoSpec;
layout (location = 4) uniform sampler2D gShininess;
layout (location = 5) uniform sampler2D gDepth;
layout (location = 6) uniform vec3 u_viewPos;
layout (location = 7) uniform mat4 u_invProj;
layout (location = 8) uniform mat4 u_invView;

layout (location = 0) out vec4 fragColor;

vec3 CalcLocalColor(PointLight light);
float CalcAttenuation(PointLight light);
vec3 CalcPointLight(PointLight light, vec3 normal, vec3 viewDir);

vec3 albedo;
float specular;
vec3 vPos;
float shininess;
void main()
{
  vec2 texSize = textureSize(gNormal, 0);
  vec2 texCoord = gl_FragCoord.xy / texSize;
  albedo = texture(gAlbedoSpec, texCoord).rgb;
  specular = texture(gAlbedoSpec, texCoord).a;
  //vPos = texture(gPosition, texCoord).xyz;
  vPos = WorldPosFromDepth(texture(gDepth, texCoord).r, texSize, u_invProj, u_invView);
  shininess = texture(gShininess, texCoord).r;
  vec3 vNormal = oct_to_float32x3(texture(gNormal, texCoord).xy);
  //vec3 vNormal = texture(gNormal, texCoord).xyz;

  float distanceToLightSquared = dot(vPos - lights[vInstanceID].position.xyz, vPos - lights[vInstanceID].position.xyz);
  if (vNormal == vec3(0) || distanceToLightSquared > lights[vInstanceID].radiusSquared)
  {
    fragColor = vec4(0);
    return;
  }

  vec3 pixelPos = WorldPosFromDepth(0.0, texSize, u_invProj, u_invView);
  float pixelDistanceToLightSquared = dot(pixelPos - lights[vInstanceID].position.xyz, pixelPos - lights[vInstanceID].position.xyz);
  if ((gl_FrontFacing == true && pixelDistanceToLightSquared < lights[vInstanceID].radiusSquared) || 
    gl_FrontFacing == false && pixelDistanceToLightSquared >= lights[vInstanceID].radiusSquared)
  {
    fragColor = vec4(0);
    return;
  }

  vec3 normal = normalize(vNormal);
  vec3 viewDir = normalize(u_viewPos - vPos);
  vec3 local = CalcPointLight(lights[vInstanceID], normal, viewDir);

  fragColor = vec4(local, 0.0) * smoothstep((lights[vInstanceID].radiusSquared), .4 * (lights[vInstanceID].radiusSquared), (distanceToLightSquared));
  //fragColor = vec4(clamp(mix(local, vec3(0.0), distanceToLightSquared / lights[vInstanceID].radiusSquared), 0.0, 1.0), 0.0);
  //fragColor = vec4(local, 0.0);
}

vec3 CalcLocalColor(PointLight light, vec3 lightDir, vec3 normal, vec3 viewDir)
{
  float diff = max(dot(normal, lightDir), 0.0);
  
  float spec = 0;
  vec3 reflectDir = reflect(-lightDir, normal);
  spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess) * SPECULAR_STRENGTH;
  
  vec3 diffuse  = (light.diffuse.xyz)  * diff * albedo;
  vec3 specu = light.specular.xyz * spec * specular;
  return (diffuse + specu);
}

float CalcAttenuation(PointLight light)
{
  float distance = length(light.position.xyz - vPos);
  return 1.0 / (light.linear * distance + light.quadratic * (distance * distance));
}

vec3 CalcPointLight(PointLight light, vec3 normal, vec3 viewDir)
{
  vec3 lightDir = normalize(light.position.xyz - vPos);
  vec3 local = CalcLocalColor(light, lightDir, normal, viewDir);
  local *= CalcAttenuation(light);
  return local;
}