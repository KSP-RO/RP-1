// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// render an antialiased line
// - txcoord: [normalized distance from line center, ignored]


Shader "Custom/AntiAliasedLine"
{
  Properties
  {
  }
  SubShader
  {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Blend SrcAlpha One
    ColorMask RGB
    Cull Off Lighting Off ZWrite Off

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 2.5

      struct v2f
      {
        half4 color : COLOR;
        float txcoord : TEXCOORD0;
        float pos_z : NORMAL;
      };

      v2f vert(float4 in_pos : POSITION, half4 color : COLOR, float2 txcoord : TEXCOORD0, out float4 pos : SV_POSITION)
      {
        pos = UnityObjectToClipPos(in_pos);

        v2f o;
        o.color = color;
        o.pos_z = pos.z;
        o.txcoord = txcoord.x * o.pos_z; // undo perspective correction
        return o;
      }

      half4 frag(v2f i) : COLOR
      {
        // used to antialias the line border
        float k = 1.0 - abs(i.txcoord / i.pos_z);

        // calculate output color
        half4 output = i.color;
        output.w *= k;
        return output;
      }
      ENDCG
    }
  }
}