// Shared lighting model for Last Light. The beam is a soft wedge on the sea plane cast from the
// lantern; this file mirrors SimBeam.Wedge / SimWorld.BeamAt in C#, so what looks lit is lit.
// All values are pushed as globals by BeamView / AtmosphereView every frame.
#ifndef LL_COMMON_INCLUDED
#define LL_COMMON_INCLUDED

float4 _LLBeamOrigin;      // xyz lantern position, w lamp power
float4 _LLBeamDir;         // xy direction on the sea plane (x, z), z cos(outer), w cos(inner)
float4 _LLBeamParams;      // x range, y strength, z focus 0..1, w fog extinction
float4 _LLBeamColor;       // rgb HDR colour of the beam
float4 _LLOccluders[8];    // xy centre (x, z), z radius
float _LLOccluderCount;
float4 _LLFogBanks[8];     // xy centre, z radius, w density
float _LLFogCount;
float4 _LLFalsePos[4];     // xyz lantern, w intensity (0 when dark)
float4 _LLFalseDir[4];     // xy direction, z cos(outer), w cos(inner)
float _LLFalseCount;
float4 _LLFalseColor;
float _LLFlash;            // lightning, 0..1
float4 _LLAmbient;         // rgb ambient (night sky), w moon brightness
float _LLHaze;             // ambient mist multiplier
float _LLDawn;             // 0 night .. 1 sunrise (ending)

float LLSmooth(float e0, float e1, float x)
{
    float t = saturate((x - e0) / (e1 - e0));
    return t * t * (3.0 - 2.0 * t);
}

float LLWedge(float2 origin, float2 dir, float cosOuter, float cosInner, float range, float2 p)
{
    float2 d = p - origin;
    float dist = length(d);
    float cosA = dot(d / max(dist, 1e-3), dir);
    float wedge = LLSmooth(cosOuter, cosInner, cosA);
    float reach = 1.0 - LLSmooth(range * 0.72, range, dist);
    float atten = lerp(1.0, 0.55, saturate(dist / range));
    return dist < 2.0 ? 1.0 : wedge * reach * atten;
}

float LLOcclusion(float2 origin, float2 p)
{
    float vis = 1.0;
    int count = (int)_LLOccluderCount;
    [loop] for (int i = 0; i < count; i++)
    {
        float3 o = _LLOccluders[i].xyz;
        float2 rel = p - o.xy;
        if (dot(rel, rel) < (o.z + 0.6) * (o.z + 0.6)) continue;
        float2 ab = p - origin;
        float t = saturate(dot(o.xy - origin, ab) / max(dot(ab, ab), 1e-4));
        if (t <= 0.0 || t >= 1.0) continue;
        float d = length(origin + ab * t - o.xy);
        vis *= LLSmooth(o.z * 0.75, o.z * 1.05, d);
    }
    return vis;
}

float LLChord(float2 a, float2 b, float2 c, float r)
{
    float2 d = b - a;
    float len = length(d);
    float2 dir = d / max(len, 1e-4);
    float2 f = a - c;
    float bq = dot(f, dir);
    float cq = dot(f, f) - r * r;
    float disc = bq * bq - cq;
    if (disc <= 0.0) return 0.0;
    float s = sqrt(disc);
    return max(0.0, min(-bq + s, len) - max(-bq - s, 0.0));
}

// True-beam intensity on the sea plane at p (x, z). Matches SimWorld.BeamAt.
float LLBeam2D(float2 p)
{
    float i = LLWedge(_LLBeamOrigin.xz, _LLBeamDir.xy, _LLBeamDir.z, _LLBeamDir.w, _LLBeamParams.x, p);
    if (i <= 0.0) return 0.0;
    i *= _LLBeamParams.y;
    i *= LLOcclusion(_LLBeamOrigin.xz, p);
    int fogCount = (int)_LLFogCount;
    if (fogCount > 0)
    {
        float chord = 0.0;
        [loop] for (int k = 0; k < fogCount; k++)
            chord += LLChord(_LLBeamOrigin.xz, p, _LLFogBanks[k].xy, _LLFogBanks[k].z) * _LLFogBanks[k].w;
        i *= exp(-_LLBeamParams.w * chord);
    }
    return i;
}

// Height of the beam's "fan" above the sea: it pours out of the lantern and settles on the water.
float LLBeamAxisY(float dist)
{
    return _LLBeamOrigin.y * exp(-dist / (_LLBeamParams.x * 0.26));
}

// Beam intensity in 3D: full below the fan's axis, fading above it.
float LLBeam3D(float3 posWS)
{
    float b = LLBeam2D(posWS.xz);
    if (b <= 0.0) return 0.0;
    float dist = length(posWS.xz - _LLBeamOrigin.xz);
    float axis = LLBeamAxisY(dist);
    float sigma = 1.5 + dist * 0.07;
    float above = max(0.0, posWS.y - axis);
    return b * exp(-above * above / (2.0 * sigma * sigma));
}

// Sum of the wreckers' false beams at posWS (no occlusion).
float LLFalseBeams(float3 posWS)
{
    float sum = 0.0;
    int count = (int)_LLFalseCount;
    [loop] for (int k = 0; k < count; k++)
    {
        float4 fp = _LLFalsePos[k];
        if (fp.w <= 0.0) continue;
        float4 fd = _LLFalseDir[k];
        float w = LLWedge(fp.xz, fd.xy, fd.z, fd.w, 78.0, posWS.xz);
        float dist = length(posWS.xz - fp.xz);
        float axis = fp.y * exp(-dist / 24.0);
        float sigma = 1.5 + dist * 0.07;
        float above = max(0.0, posWS.y - axis);
        sum += w * fp.w * exp(-above * above / (2.0 * sigma * sigma));
    }
    return sum;
}

// Fog-bank density at a world position (height-limited, soft edges). Noise is added by callers.
float LLFogBankDensity(float3 posWS)
{
    float d = 0.0;
    int count = (int)_LLFogCount;
    [loop] for (int k = 0; k < count; k++)
    {
        float4 f = _LLFogBanks[k];
        float dist = length(posWS.xz - f.xy);
        d = max(d, f.w * (1.0 - LLSmooth(f.z * 0.45, f.z * 1.05, dist)));
    }
    return d * (1.0 - LLSmooth(3.0, 16.0, posWS.y));
}

// Light reaching a surface from the lantern and false lights: rgb, plus a scalar for rim terms.
float3 LLBeamLighting(float3 posWS, float3 normalWS, float3 viewWS, out float beamAmount)
{
    float b = LLBeam3D(posWS);
    float3 toLamp = normalize(_LLBeamOrigin.xyz - posWS);
    float ndl = saturate(dot(normalWS, toLamp) * 0.6 + 0.4);
    float rim = pow(1.0 - saturate(dot(normalWS, viewWS)), 3.0);
    beamAmount = b;
    float3 light = _LLBeamColor.rgb * b * (ndl + rim * 1.4);
    float f = LLFalseBeams(posWS);
    light += _LLFalseColor.rgb * f * (ndl + rim);
    // Lightning: a cold flash from above.
    light += float3(0.75, 0.82, 1.0) * _LLFlash * (saturate(normalWS.y) * 0.8 + 0.4) * 2.2;
    return light;
}

// Interleaved gradient noise for dithering ray-march starts.
float LLIGN(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

float LLHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float LLNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = LLHash31(i);
    float n100 = LLHash31(i + float3(1, 0, 0));
    float n010 = LLHash31(i + float3(0, 1, 0));
    float n110 = LLHash31(i + float3(1, 1, 0));
    float n001 = LLHash31(i + float3(0, 0, 1));
    float n101 = LLHash31(i + float3(1, 0, 1));
    float n011 = LLHash31(i + float3(0, 1, 1));
    float n111 = LLHash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

float LLHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float LLNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(LLHash21(i), LLHash21(i + float2(1, 0)), u.x),
                lerp(LLHash21(i + float2(0, 1)), LLHash21(i + float2(1, 1)), u.x), u.y);
}

float LLFbm2(float2 p)
{
    float s = 0.0, a = 0.5;
    [unroll] for (int k = 0; k < 4; k++) { s += LLNoise2(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
    return s;
}

#endif
