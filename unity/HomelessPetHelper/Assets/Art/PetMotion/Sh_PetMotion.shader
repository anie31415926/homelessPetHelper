Shader "HomelessPetHelper/Preview/PetMotion"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 crop : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 crop : TEXCOORD1; };
            sampler2D _MainTex;
            v2f vert (appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.crop = v.crop; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // Border triangles retain the old weights; discard UVs outside their padded region.
                clip(min(min(i.crop.x, i.crop.y), min(1-i.crop.x, 1-i.crop.y)));
                fixed4 c = tex2D(_MainTex, i.uv); clip(c.a - 0.01); return c;
            }
            ENDCG
        }
    }
}
