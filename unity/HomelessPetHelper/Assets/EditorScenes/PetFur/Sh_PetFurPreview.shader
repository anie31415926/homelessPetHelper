Shader "HomelessPetHelper/Preview/PetFur"
{
    Properties
    {
        _MainTex ("Portrait", 2D) = "white" {}
        _EdgeTex ("Contours", 2D) = "white" {}
        _Light ("Shadow direction, strength, rows", Vector) = (.6,.8,.76,4)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex, _EdgeTex;
            float4 _MainTex_TexelSize, _Light;

            struct Attributes
            {
                float4 position : POSITION;
                float4 local : TEXCOORD0;
                float4 basis : TEXCOORD1;
                float4 patch : TEXCOORD2;
                float3 style : TEXCOORD3;
                float2 warmth : TEXCOORD4;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 local : TEXCOORD0;
                float4 basis : TEXCOORD1;
                float4 patch : TEXCOORD2;
                float3 style : TEXCOORD3;
                float2 warmth : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.local = input.local;
                output.basis = input.basis;
                output.patch = input.patch;
                output.style = input.style;
                output.warmth = input.warmth;
                return output;
            }

            float2 EdgeInk(float3 style, float s, float t)
            {
                float span = lerp(.46, 1.0, saturate((style.x - 2.0) / 14.0));
                float phase = frac(style.z * _Light.w);
                float row = (min(_Light.w - 1, floor(style.z * _Light.w)) + .5) / _Light.w;
                float tail = 1.0 - saturate(t);
                float u = saturate((s + .32 * tail * tail) * span + (1.0 - span) * phase);
                return tex2D(_EdgeTex, float2(u, row)).ra;
            }

            float TipHeight(float s, float ink, float depth)
            {
                float height = .69 + (ink - .44) * depth * 1.6;
                return lerp(.18, height, smoothstep(0, .24, s) * (1 - smoothstep(.77, 1, s)));
            }

            float Side(float s) { return smoothstep(0, .19, s) * (1 - smoothstep(.81, 1, s)); }

            float Fold(float s, float edge, float bend) { return edge * (1 - bend * (.61 + .51 * sin(s * UNITY_PI))); }

            float Shadow(Varyings input, float lip, float bend)
            {
                float2 local = input.local.xy;
                float2 away = float2(dot(_Light.xy, input.basis.xy), dot(_Light.xy, input.basis.zw));
                float height = input.local.z * (.20 + .08 * input.local.w) * input.patch.w;
                float2 shift = away * height / input.patch.zw;
                float s = saturate(local.x - shift.x * smoothstep(.06, .58, local.y));
                float2 ink = EdgeInk(input.style, s, local.y - shift.y);
                float tip = Fold(s, TipHeight(s, ink.x, input.style.y), bend) + shift.y;
                float softness = (2 + .23 * height) / input.patch.w;
                float projected = 1 - smoothstep(tip - softness, tip + softness, local.y);
                float contact = 1 - smoothstep(0, 4.5 / input.patch.w, local.y - lip);
                return (.07 * contact * Side(local.x) + .23 * projected * Side(s)) *
                    smoothstep(0, .58, input.local.z) * smoothstep(0, .30, local.y) * _Light.z;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float s = input.local.x, t = input.local.y, lift = input.local.z, curl = input.local.w;
                float2 ink = EdgeInk(input.style, s, t);
                float bend = lift * (.38 + .23 * curl);
                float lip = Fold(s, TipHeight(s, ink.x, input.style.y), bend);
                float aa = 1.05 / input.patch.w;
                float cover = 1 - smoothstep(lip - aa, lip + aa, t);
                float mask = Side(s) * smoothstep(0, .24, t) * smoothstep(0, .21, lift);
                float shade = Shadow(input, lip, bend);
                clip(mask * cover + shade - .002);

                float2 pixel = input.patch.xy + input.basis.xy * (s - .5) * input.patch.z + input.basis.zw * t * input.patch.w;
                float2 uv = pixel * _MainTex_TexelSize.xy;
                float4 fur = tex2D(_MainTex, float2(uv.x, 1 - uv.y));
                #ifndef UNITY_COLORSPACE_GAMMA
                fur.rgb = LinearToGammaSpace(fur.rgb);
                #endif
                float warmth = dot(fur.rgb, float3(1, .6, -1.6));
                float coverage = fur.a * smoothstep(input.warmth.x - .035, input.warmth.x, warmth) *
                    (1 - smoothstep(input.warmth.y, input.warmth.y + .035, warmth));
                clip(coverage - .002);
                float reach = saturate(t / lip);
                float roll = smoothstep(.28, .65, reach) * (1 - smoothstep(.69, .97, reach));
                float tuck = smoothstep(.73, .98, reach) * curl;
                float facing = .5 - .5 * dot(_Light.xy, input.basis.zw);
                float highlight = ((.018 + .040 * curl) * roll + .025 * smoothstep(.91, 1, reach)) * facing;
                float3 surface = fur.rgb * (1 + lift * (highlight - .08 * tuck * (1 - facing)));
                surface *= 1 - ink.y * smoothstep(.60, .94, reach) * lift * float3(.11, .15, .18);
                float a = mask * cover * coverage;
                float b = shade * (1 - cover) * min(Side(s), 1 - smoothstep(.93, 1, t)) * coverage * (1 - a);
                float3 shadow = fur.rgb * float3(.09, .065, .07);
                #ifndef UNITY_COLORSPACE_GAMMA
                surface = GammaToLinearSpace(surface);
                shadow = GammaToLinearSpace(shadow);
                #endif
                return float4(surface * a + shadow * b, a + b);
            }
            ENDCG
        }
    }
}
