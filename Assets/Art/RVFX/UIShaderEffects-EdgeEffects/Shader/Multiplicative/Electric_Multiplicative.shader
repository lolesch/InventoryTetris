Shader "RVFX/UI/EdgeEffects/Electric_Multiplicative"
{
    Properties
    { 
        [HideInInspector] _MainTex("MainTex", 2D) = "white"{}
        _Color("Color", color) = (1,1,1,1)


        [NoScaleOffset] _NoiseTex("NoiseTex", 2D) = "gray"{}
        _NoiseTex_ScaleScrollSpeed("NoiseTex_ScaleScrollSpeed", vector) = (1,1,0,0)
        _Offset("Offset", float) = 0.0

        _DistortionIntensity("DistortionIntensity", float) = 1.0
        _Thickness("Thickness", range(0.0, 0.05)) = 0.01
        _EdgeBlur("EdgeBlur", range(0.0, 10)) = 0.0

        [Header(Mask)]
        [Toggle(_Use_Mask)] _Use_Mask("UseMask", float) = 0
        [NoScaleOffset] _MaskTex("MaskTex", 2D) = "white"{}
        _MaskTexScaleOffset("MaskTexScaleOffset", vector) = (1,1,0,0)
        _MaskTex_ScrollSpeed_X("MaskTex_ScrollSpeed_X", float) = 0.0
        _MaskTex_ScrollSpeed_Y("MaskTex_ScrollSpeed_Y", float) = 0.0
        _Mask_Power("Mask_Power", float) = 1.0
        _Mask_Intensity("Mask_Intensity", float) = 1.0


        [Header(Mask2)]
        [Toggle(_Use_Mask2)] _Use_Mask2("UseMask2", float) = 0
        [NoScaleOffset] _MaskTex2("MaskTex2", 2D) = "white"{}
        _MaskTex2ScaleOffset("MaskTex2ScaleOffset", vector) = (1,1,0,0)
        _MaskTex2_ScrollSpeed_X("MaskTex2_ScrollSpeed_X", float) = 0.0
        _MaskTex2_ScrollSpeed_Y("MaskTex2_ScrollSpeed_Y", float) = 0.0
        _Mask2_Power("Mask2_Power", float) = 1.0
        _Mask2_Intensity("Mask2_Intensity", float) = 1.0



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
        Blend DstColor Zero
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
            #pragma shader_feature_local _Use_Mask
            #pragma shader_feature_local _Use_Mask2

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



            float _Thickness;
            float _EdgeBlur;
            float _DistortionIntensity;
            float _Offset;

            float4 _Color;

            sampler2D _NoiseTex;
            float4 _NoiseTex_ScaleScrollSpeed;
        
            #ifdef UNITY_UI_CLIP_RECT
                float4 _ClipRect;
                float _UIMaskSoftnessX, _UIMaskSoftnessY;
            #endif


            #ifdef _Use_Mask
                sampler2D _MaskTex;
                float4 _MaskTexScaleOffset;
                float _MaskTex_ScrollSpeed_X;
                float _MaskTex_ScrollSpeed_Y;
                float _Mask_Power;
                float _Mask_Intensity;
            #endif


            #ifdef _Use_Mask2
                sampler2D _MaskTex2;
                float4 _MaskTex2ScaleOffset;
                float _MaskTex2_ScrollSpeed_X;
                float _MaskTex2_ScrollSpeed_Y;
                float _Mask2_Power;
                float _Mask2_Intensity;
            #endif


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv + float2(_Offset,0.0);
                o.color = v.color;

                #ifdef UNITY_UI_CLIP_RECT
                    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                    o.rectMask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * float2((_UIMaskSoftnessX + 1), (_UIMaskSoftnessY + 1))));
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {

      

     



                float noise = tex2D(_NoiseTex, i.uv * _NoiseTex_ScaleScrollSpeed.xy + _NoiseTex_ScaleScrollSpeed.zw * _Time.y).r;
                float noise_2 = tex2D(_NoiseTex, 3.0 * i.uv * _NoiseTex_ScaleScrollSpeed.xy + _NoiseTex_ScaleScrollSpeed.zw * _Time.y).r;
                
                

                noise = (noise - 0.5) * 2 * 0.03 * _DistortionIntensity;
                noise_2 = (noise_2 - 0.5) * 2 * 0.03 * _DistortionIntensity;


               
                float2 centered_uv = (i.uv + float2(noise * noise_2, noise * noise_2 * 1.19382) - 0.5) * 2.0;

   
                float thickness = _Thickness;
                float outerline_mask = 1.0 - smoothstep(thickness, thickness + _EdgeBlur, abs(centered_uv.x));
                float core_mask = pow(outerline_mask, 100.0);

                float4 col = _Color * i.color * outerline_mask;
                col.rgb += _Color.rgb * i.color.rgb * core_mask * 20.0;
                
                
                float alpha = i.color.a * col.a;




                #ifdef _Use_Mask
                    float mask = tex2D(_MaskTex, i.uv * _MaskTexScaleOffset.xy + _MaskTexScaleOffset.zw +
                    float2(_MaskTex_ScrollSpeed_X, _MaskTex_ScrollSpeed_Y) * _Time.y).r;
                    mask = saturate(pow(mask, _Mask_Power) * _Mask_Intensity);
                    alpha *= mask;
                #endif

                #ifdef _Use_Mask2
                    float mask2 = tex2D(_MaskTex2, i.uv * _MaskTex2ScaleOffset.xy + _MaskTex2ScaleOffset.zw +
                    float2(_MaskTex2_ScrollSpeed_X, _MaskTex2_ScrollSpeed_Y) * _Time.y).r;
                    mask2 = saturate(pow(mask2, _Mask2_Power) * _Mask2_Intensity);
                    alpha *= mask2;
                #endif

                #if UNITY_UI_CLIP_RECT	
                    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.rectMask.xy)) * i.rectMask.zw);
                    alpha *= m.x * m.y;
                #endif


                col.rgb = lerp(float3(1, 1, 1), col.rgb, alpha);
                return col;
            }
            ENDCG
        }
    }
}
