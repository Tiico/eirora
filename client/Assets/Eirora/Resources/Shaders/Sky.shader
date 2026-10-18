// The sky gradient behind the world: unlit vertex colours drawn first, without depth.
Shader "Eirora/Sky"
{
    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
