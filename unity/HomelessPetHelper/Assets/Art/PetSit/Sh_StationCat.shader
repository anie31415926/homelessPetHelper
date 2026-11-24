Shader "HomelessPetHelper/Station/Cat"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _SeatContact ("Seat contact", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 contact : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _SeatContact;
            v2f vert (appdata v)
            {
                // SkinnedMeshRenderer supplies deformed vertices in the rig's local coordinates.
                float x = v.vertex.x, y = v.vertex.y;
                float width = smoothstep(-1.58, -1.39, x) * (1 - smoothstep(-.49, -.30, x));
                float depth = 1 - smoothstep(.065, .14, y);
                float seat = .006 + clamp(y, 0, .024) * .68 + max(0, y - .024) * .15;
                float root = smoothstep(-1.94, -1.66, x) * (1 - smoothstep(-1.14, -.86, x))
                    * (1 - smoothstep(.275, .43, y));
                float rootY = .006 + max(0, y) * .04;
                v.vertex.y = lerp(y, lerp(lerp(y, seat, width * depth), rootY, root),
                    _SeatContact * v.contact.x);
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - .015);
                return c;
            }
            ENDCG
        }
    }
}
