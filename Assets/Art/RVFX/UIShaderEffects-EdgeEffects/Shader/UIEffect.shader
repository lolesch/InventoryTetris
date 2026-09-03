Shader "RVFX/UI/EdgeEffects/UIEffect"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Saturation("Saturation", Range(0, 2)) = 1.0
        _HueShift("HueShift", range(-180, 180)) = 0.0


        [Header(ChromaticAberration)]
        [Toggle(_Use_Chromatic_Aberration)] _Use_Chromatic_Aberration("Use_Chromatic_Aberration", float) = 0
        [Space(10)]
        _ChromaticAberration_Opacity("ChromaticAberration_Opacity", range(0,1)) = 0.0
        _ChromaticAberration_Offset_R("ChromaticAberration_Offset_R", vector) = (0,0,0,0)
        _ChromaticAberration_Offset_G("ChromaticAberration_Offset_G", vector) = (0,0,0,0)
        _ChromaticAberration_Offset_B("ChromaticAberration_Offset_B", vector) = (0,0,0,0)


        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}
        ZTest[unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
        }


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma shader_feature_local _Use_Chromatic_Aberration
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                #if UNITY_UI_CLIP_RECT
                    float4	rectMask    : TEXCOORD1;
                #endif
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _HueShift;
            float _Saturation;



            #ifdef _Use_Chromatic_Aberration
            float _ChromaticAberration_Opacity;
                float2 _ChromaticAberration_Offset_R;
                float2 _ChromaticAberration_Offset_G;
                float2 _ChromaticAberration_Offset_B;
            #endif

            #ifdef UNITY_UI_CLIP_RECT
                        float4 _ClipRect;
                        float _UIMaskSoftnessX, _UIMaskSoftnessY;
            #endif
            float3 ApplyHueShift(float3 aColor, float hue)
            {
                float angle = radians(hue);
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float cosAngle = cos(angle);
                return aColor * cosAngle + cross(k, aColor) * sin(angle) + k * dot(k, aColor) * (1 - cosAngle);
            }
            float3 AdjustSaturation(float3 color, float saturation)
            {
                float gray = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(float3(gray.rrr), color, saturation);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                #ifdef UNITY_UI_CLIP_RECT
                    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                    o.rectMask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * float2((_UIMaskSoftnessX + 1), (_UIMaskSoftnessY + 1))));
                #endif
      
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture



                float4 col = tex2D(_MainTex, i.uv);
                #ifdef _Use_Chromatic_Aberration

                    float col_r = tex2D(_MainTex, i.uv + _ChromaticAberration_Offset_R).r;
                    float col_g = tex2D(_MainTex, i.uv + _ChromaticAberration_Offset_G).g;
                    float col_b = tex2D(_MainTex, i.uv + _ChromaticAberration_Offset_B).b;

                    col.rgb = lerp(col.rgb, float3(col_r, col_g, col_b), _ChromaticAberration_Opacity);
                #else
                 
                #endif


                col.rgb = ApplyHueShift(col.rgb, _HueShift);
                col *= i.color;
                col.rgb = AdjustSaturation(col.rgb, _Saturation);
     
                #if UNITY_UI_CLIP_RECT	
                            half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.rectMask.xy)) * i.rectMask.zw);
                            col.a *= m.x * m.y;
                #endif
                return col;
            }
            ENDCG
        }
    }
}
