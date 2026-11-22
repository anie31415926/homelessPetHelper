Shader "HomelessPetHelper/UI/AnimalCoat"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoatTex ("Dirt and fur", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UvRect ("Sprite UV bounds", Vector) = (0,0,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
            "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR;
                float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex, _CoatTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _UvRect, _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.local = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                float2 span = _UvRect.zw - _UvRect.xy;
                float2 uv = (i.uv - _UvRect.xy) / span;
                fixed2 marks = tex2D(_CoatTex, uv).rg;
                c.rgb = lerp(c.rgb, fixed3(100.0/255,82.0/255,59.0/255), marks.r);
                c.rgb = lerp(c.rgb, fixed3(104.0/255,117.0/255,94.0/255), marks.g);
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
