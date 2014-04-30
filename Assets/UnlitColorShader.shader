Shader "Unlit Color Only CG" {

    Properties {

        _Color ("Custom Color", Color) = (1,1,1,1)

    }

    SubShader {

        Tags { "RenderType"="Opaque" }

        LOD 200

        

        Pass {

            Tags { "LightMode" = "Always" }

            

            Fog { Mode Off }

            ZWrite On

            ZTest LEqual

            Cull Back

            Lighting Off

    

            CGPROGRAM

                #pragma vertex vert

                #pragma fragment frag

                #pragma fragmentoption ARB_precision_hint_fastest

                

                fixed4 _Color;

                

                struct appdata {

                    float4 vertex : POSITION;

                };

                

                struct v2f {

                    float4 vertex : POSITION;

                };

                

                v2f vert (appdata v) {

                    v2f o;

                    o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);

                    return o;

                }

                

                fixed4 frag (v2f i) : COLOR {

                    return _Color;

                }

            ENDCG

    

        }

    } 

}