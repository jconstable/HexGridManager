Shader "Custom/HexShader" {
	Properties {
		_Color ("Color", Color) = (1.0,1.0,1.0,1.0)
		_Dropoff ("Dropoff", Float) = 0.0
		_Gain ("Gain", Float) = 1.0
	}
	SubShader {
		Pass {
			Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
			Blend SrcAlpha OneMinusSrcAlpha
			
			CGPROGRAM
			#pragma vertex vert alpha
			#pragma fragment frag alpha
			
			uniform float4 _Color;
			uniform float _Dropoff;
			uniform float _Gain;
			
			uniform float4 _LightColor0;
			
			//float4x4 _Object2World;
			//float4x4 _World2Object;
			//float4 _WorldSpaceLightPos0;
			
			struct vertexInput {
				half4 vertex : POSITION;
				half3 normal : NORMAL;
				half4 col : COLOR;
			};
			struct vertexOutput {
				half4 pos : SV_POSITION;
				half4 col : COLOR;
			};
			
			vertexOutput vert(vertexInput v)
			{
				vertexOutput o; 
				
				o.col = half4( v.col.rgb,1.0); 
				o.pos = mul( UNITY_MATRIX_MVP, v.vertex); 
				return o;
			}
			
			float4 frag(vertexOutput i) : COLOR
			{
			    half a = i.col.r;
				a = saturate( max(a - _Dropoff, 0.0) * _Gain);
				return half4( _Color.rgb, a );
			}
			
			
			ENDCG
		}
	} 
	FallBack "Diffuse"
}
