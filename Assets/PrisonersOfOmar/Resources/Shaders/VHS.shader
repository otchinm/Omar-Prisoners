// =====================================================================================================
//  VHS / CRT post-process (single full-screen pass). Input: the low-res frame (PsxCameraRig.LowRes, point
//  filtered), output: the full-resolution screen. Parameters are uploaded every frame by VhsEffect (C#).
//  Every "line" below is a LOW-RES line, so the look is identical at 720p, 1080p and 4K.
//
//  Order: CRT geometry -> vertical roll -> horizontal tape displacement (wobble, jitter, tracking band,
//  head switching, Omar tearing, glitch blocks) -> 15-bit + 4x4 Bayer quantization of every source tap ->
//  YIQ: soft luma + wide shifted chroma (bleed) + ghost + channel split -> grade -> grain / snow / dropouts ->
//  static override -> scanlines + interlace -> damage / hiding vignettes -> CRT vignette + rounded corners -> blackout.
//
//  _MainTex_TexelSize  low-res size (zw = width, height)
//  _VhsScreen  x = screen w, y = screen h, z = screen aspect, w = scanline strength
//  _VhsTime    x = time (s, wrapped), y = field counter (30 Hz, wrapped), z = vertical roll offset 0..1, w = interlace parity
//  _VhsTape    x = wobble (px), y = line jitter (px), z = tracking band, w = noise
//  _VhsTape2   x = chroma shift (px), y = chroma blur (px), z = ghost, w = head switching
//  _VhsGrade   x = contrast, y = saturation, z = black crush, w = 15-bit dither amount
//  _VhsTint    rgb = color multiply, w = shadow tint amount
//  _VhsShadow  rgb = shadow tint color (luma normalized), w = black lift
//  _VhsFx      x = interference, y = damage, z = static override, w = blackout
//  _VhsFx2     x = hiding, y = glitch burst, z = damage pulse 0..1, w = unused
//  _VhsLens    x = barrel, y = vignette, z = corner radius, w = corner softness
// =====================================================================================================
Shader "PrisonersOfOmar/VHS"
{
    Properties
    {
        _MainTex ("Low-res frame", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define LUMA float3(0.299, 0.587, 0.114)
            #define TAU 6.2831853

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _VhsScreen;
            float4 _VhsTime;
            float4 _VhsTape;
            float4 _VhsTape2;
            float4 _VhsGrade;
            float4 _VhsTint;
            float4 _VhsShadow;
            float4 _VhsFx;
            float4 _VhsFx2;
            float4 _VhsLens;

            struct appdata_vhs
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f_vhs
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;   // full float: half precision bands at 4K
            };

            v2f_vhs vert(appdata_vhs v)
            {
                v2f_vhs o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Ordered dithering matrices (values 0..1). a = integer pixel coordinates.
            float Bayer2(float2 a)
            {
                a = floor(a);
                return frac(dot(a, float2(0.5, a.y * 0.75)));
            }

            float Bayer4(float2 a)
            {
                a = a - 4.0 * floor(a * 0.25);   // positive modulo 4 (keeps values small)
                return Bayer2(0.5 * a) * 0.25 + Bayer2(a);
            }

            // One low-res tap, quantized to 15-bit color (5 bits / channel) with a 4x4 Bayer pattern locked to
            // low-res pixels: this is the PS1 frame buffer that the "tape" then records.
            float3 SampleSrc(float2 uv)
            {
                float3 c = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).rgb;
                float d = Bayer4(floor(uv * _MainTex_TexelSize.zw));
                float3 q = floor(c * 31.0 + d) / 31.0;
                return lerp(c, saturate(q), _VhsGrade.w);
            }

            float3 RgbToYiq(float3 c)
            {
                return float3(dot(c, float3(0.299, 0.587, 0.114)),
                              dot(c, float3(0.596, -0.274, -0.322)),
                              dot(c, float3(0.211, -0.523, 0.312)));
            }

            float3 YiqToRgb(float3 c)
            {
                return float3(dot(c, float3(1.0, 0.956, 0.621)),
                              dot(c, float3(1.0, -0.272, -0.647)),
                              dot(c, float3(1.0, -1.106, 1.703)));
            }

            fixed4 frag(v2f_vhs i) : SV_Target
            {
                float t = _VhsTime.x;
                float field = _VhsTime.y;
                float roll = _VhsTime.z;
                float lowW = _MainTex_TexelSize.z;
                float lowH = _MainTex_TexelSize.w;
                float px = _MainTex_TexelSize.x;
                float aspect = _VhsScreen.z;

                float interference = _VhsFx.x;
                float damage = _VhsFx.y;
                float staticAmt = _VhsFx.z;
                float blackout = _VhsFx.w;
                float hiding = _VhsFx2.x;
                float glitch = _VhsFx2.y;

                // ---------------------------------------------------------------- CRT geometry
                // slight barrel: edge midpoints stay on the edge, corners bend outwards (hidden by the rounded mask)
                float2 cc = i.uv - 0.5;
                float r2 = dot(cc, cc);
                float k = _VhsLens.x;
                float2 d = cc * (1.0 + r2 * k) / (1.0 + 0.25 * k);
                float2 crt = d + 0.5;

                // ---------------------------------------------------------------- vertical hold loss (rolling)
                float rolledY = crt.y + roll;
                float2 suv = float2(crt.x, (roll > 0.0) ? frac(rolledY) : crt.y);
                float vbi = (roll > 0.0) ? smoothstep(0.93, 0.95, frac(rolledY)) : 0.0;

                // ---------------------------------------------------------------- glitch burst: repeated lines
                float gTick = floor(t * 18.0);
                float gBlock = floor(suv.y * 7.0 + Hash11(gTick) * 3.0);
                float gHash = Hash12(float2(gBlock, gTick));
                float gOn = (gHash < glitch * 0.65) ? 1.0 : 0.0;
                float repeatY = floor(suv.y * lowH / 6.0) * 6.0 / lowH;
                suv.y = lerp(suv.y, repeatY, gOn * step(0.5, frac(gHash * 13.7)));

                float lineIdx = floor(suv.y * lowH);

                // ---------------------------------------------------------------- horizontal displacement (low-res px)
                float dx = 0.0;
                // tape wobble: slow sine drift + per-line jitter
                dx += (sin(suv.y * 3.1 + t * 1.13) * 0.55 + sin(suv.y * 13.0 - t * 2.7) * 0.2 + sin(t * 0.31) * 0.6) * _VhsTape.x;
                dx += (Hash12(float2(lineIdx, field)) - 0.5) * _VhsTape.y;

                // slow rolling tracking-noise band (top -> bottom)
                float bandY = 1.25 - frac(t * 0.047) * 1.5;
                float bandD = (suv.y - bandY) / 0.045;
                float band = exp(-bandD * bandD) * _VhsTape.z;
                dx += band * ((Hash12(float2(lineIdx, field + 13.0)) - 0.5) * 7.0 + 2.5);

                // head-switching noise: bottom ~3.5% of the frame skews to the right
                float hs = saturate(1.0 - suv.y / 0.035);
                hs = hs * hs * _VhsTape2.w;
                dx += hs * (6.0 + Hash12(float2(lineIdx, field + 5.0)) * 10.0);

                // Omar interference: the picture only DEGRADES (it must stay readable): a little line jitter and
                // now and then a thin band that slips a few pixels sideways
                float tearTick = floor(t * 9.0);
                float tearBand = floor(suv.y * 22.0 + Hash11(tearTick + 3.0) * 22.0);
                float tearOn = (Hash12(float2(tearBand, tearTick)) < interference * 0.14) ? 1.0 : 0.0;
                dx += tearOn * (Hash12(float2(tearBand + 7.0, tearTick)) - 0.5) * 7.0 * interference;
                dx += (Hash12(float2(lineIdx, field + 21.0)) - 0.5) * 1.4 * interference;

                // glitch burst: big block displacement
                dx += gOn * (Hash12(float2(gBlock + 3.0, gTick)) - 0.5) * 90.0 * glitch;

                float2 uvS = float2(suv.x + dx * px, suv.y);

                // ---------------------------------------------------------------- tape signal (YIQ)
                float blur = 1.0 + damage * 1.6 + interference * 0.6;
                float lumaR = 0.5 * blur * px;
                float3 sC = SampleSrc(uvS);
                float3 sL = SampleSrc(uvS - float2(lumaR, 0.0));
                float3 sR = SampleSrc(uvS + float2(lumaR, 0.0));
                float Y = dot(sC, LUMA) * 0.5 + (dot(sL, LUMA) + dot(sR, LUMA)) * 0.25;

                // chroma: low bandwidth (wide blur) and delayed (shifted right) = color bleed
                float cb = _VhsTape2.y * blur * px;
                float2 cuv = uvS - float2(_VhsTape2.x * px, 0.0);
                float2 IQ = RgbToYiq(SampleSrc(cuv - float2(1.5 * cb, 0.0))).yz;
                IQ += RgbToYiq(SampleSrc(cuv - float2(0.5 * cb, 0.0))).yz;
                IQ += RgbToYiq(SampleSrc(cuv + float2(0.5 * cb, 0.0))).yz;
                IQ += RgbToYiq(SampleSrc(cuv + float2(1.5 * cb, 0.0))).yz;
                IQ *= 0.25;

                // faint ghost / echo of the luma a few pixels to the right
                float ghostY = dot(SampleSrc(uvS - float2(5.0 * px, 0.0)), LUMA);
                Y = lerp(Y, ghostY, _VhsTape2.z);

                float3 col = YiqToRgb(float3(Y, IQ));

                // color channel split (glitch bursts, interference)
                float split = (glitch * 4.0 + interference * 1.2) * px;
                float3 sSplitR = SampleSrc(uvS + float2(split, 0.0));
                float3 sSplitB = SampleSrc(uvS - float2(split, 0.0));
                float splitAmt = saturate(glitch * 1.5 + interference * 0.4) * 0.6;
                col.r = lerp(col.r, sSplitR.r, splitAmt);
                col.b = lerp(col.b, sSplitB.b, splitAmt);

                // ---------------------------------------------------------------- grade
                col = max(col, 0.0);
                col = saturate((col - _VhsGrade.z) / max(1.0 - _VhsGrade.z, 0.01));   // crush blacks
                col = saturate((col - 0.3) * _VhsGrade.x + 0.3);                      // contrast (dark pivot)
                float lum = dot(col, LUMA);
                col = lerp(float3(lum, lum, lum), col, _VhsGrade.y);                  // desaturate
                col *= _VhsTint.rgb;
                float shadow = 1.0 - smoothstep(0.0, 0.5, lum);
                col = lerp(col, lum * _VhsShadow.rgb, shadow * _VhsTint.w);           // teal / green shadows
                col += _VhsShadow.rgb * (_VhsShadow.w * (1.0 - lum));                 // lifted blacks

                // ---------------------------------------------------------------- noise
                float2 nCell = float2(floor(suv.x * lowW * 1.5), lineIdx);
                float grain = Hash12(nCell + float2(field * 7.0, field * 3.0)) - 0.5;
                float grainAmt = 0.04 * _VhsTape.w + band * 0.15 + hs * 0.35 + interference * 0.1;
                col += grain * grainAmt;

                // snow: short bright horizontal dashes (sparse when the tape is clean)
                float sCell = floor(suv.x * lowW * 0.3 + Hash11(lineIdx + field) * 8.0);
                float sn = Hash12(float2(sCell, lineIdx) + float2(field * 1.7, field * 5.3));
                float snowDensity = 0.0005 * _VhsTape.w + band * 0.05 + hs * 0.12 + interference * 0.008;
                float snowLevel = 0.3 + 0.3 * saturate(band * 2.0 + hs + interference * 0.3);
                col += step(1.0 - snowDensity, sn) * snowLevel;

                // interference: a few thin bright tracking lines + a faint slow hum bar (never hides the picture)
                float tlH = Hash12(float2(lineIdx, floor(t * 15.0) + 31.0));
                float tl = (tlH < interference * 0.012) ? 1.0 : 0.0;
                float tlStart = Hash12(float2(lineIdx + 17.0, floor(t * 15.0)));
                col += tl * step(tlStart, suv.x) * step(suv.x, tlStart + 0.35) * 0.22;
                float bars = smoothstep(0.6, 1.0, sin(suv.y * 3.0 - t * 1.6) * 0.5 + 0.5);
                col *= 1.0 - bars * interference * 0.08;

                // ---------------------------------------------------------------- full static override
                float sNoise = Hash12(float2(floor(crt.x * lowW * 1.5), floor(crt.y * lowH)) + float2(field * 3.1, field * 1.3));
                float sBars = 0.75 + 0.25 * sin(crt.y * 5.0 + t * 7.0);
                col = lerp(col, float3(sNoise, sNoise, sNoise) * sBars, saturate(staticAmt));

                // vertical blanking bar while rolling
                col *= 1.0 - vbi;

                // ---------------------------------------------------------------- scanlines + interlace flicker
                float scanPos = crt.y * lowH;
                float scan = 0.5 - 0.5 * cos(frac(scanPos) * TAU);           // 0 at line edges, 1 at line centers
                float sStrength = _VhsScreen.w;
                float scanMul = (1.0 - sStrength * 0.4 * (1.0 - scan)) * (1.0 + sStrength * 0.12);
                float parity = fmod(floor(scanPos) + _VhsTime.w, 2.0);
                scanMul *= 1.0 + (parity - 0.5) * 0.05 * sStrength;
                col *= scanMul;

                // ---------------------------------------------------------------- damage: red pulsing edges, darkening
                float edgeDist = length(cc * 2.0);
                float edge = smoothstep(0.5, 1.35, edgeDist);
                float pulse = 0.55 + 0.45 * _VhsFx2.z;
                col = lerp(col, col * 0.4 + float3(0.24, 0.0, 0.01), saturate(edge * damage * pulse * 1.2));
                col *= 1.0 - damage * 0.25;

                // ---------------------------------------------------------------- hiding: soft dark vignette only
                // (the view through the wardrobe slats comes from the geometry; the HUD must stay readable)
                float hv = 1.0 - smoothstep(0.35, 0.9, length(cc * float2(1.0, 1.35)));
                col *= lerp(1.0, lerp(0.55, 1.0, hv), saturate(hiding));

                // ---------------------------------------------------------------- CRT vignette + rounded dark corners
                float vig = 1.0 - _VhsLens.y * pow(saturate(r2 * 2.0), 1.5);
                float2 halfSize = float2(aspect * 0.5, 0.5) - 0.006;
                float rad = _VhsLens.z;
                float2 q = abs(d * float2(aspect, 1.0)) - (halfSize - rad);
                float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - rad;
                float corner = 1.0 - smoothstep(-_VhsLens.w, 0.0, sd);
                col *= vig * corner;

                col *= 1.0 - saturate(blackout);
                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
