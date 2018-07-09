// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Chroma" {
	Properties
	{
		_Color ("Color", Color) = (0, 0, 0, 0)
	}
    SubShader {
		Cull Front
        Pass {
            CGPROGRAM
			fixed4 _Color;
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 v:POSITION) : SV_POSITION {
                return UnityObjectToClipPos (v);
            }
            fixed4 frag() : COLOR {
                return _Color;
            }
            ENDCG
        }
    }
}