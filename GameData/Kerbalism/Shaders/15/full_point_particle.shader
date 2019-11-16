// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// render a point sprite, with the point color and size specified per-particle


Shader "Custom/FullPointParticle"
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
        float size : PSIZE;
        float inv_hsize : TEXCOORD0;
        float2 screen_coords : TEXCOORD1;
        float4 color : TEXCOORD2;
      };

      v2f vert(float4 in_pos : POSITION, fixed4 in_color : COLOR, float2 in_psize : TEXCOORD, out float4 pos : SV_POSITION)
      {
        // output clip-space vertex position
        pos = UnityObjectToClipPos(in_pos);

        // pass down point size and inverse of point half-size
        v2f o;
        o.size = in_psize.x;
        o.inv_hsize = 2.0 / o.size;

        // pass down point color
        o.color = in_color;

        // calculate and pass down screen-space position of point center
        o.screen_coords = (pos.xy / pos.w) * 0.5 + 0.5;
        o.screen_coords *= _ScreenParams.xy;
        o.screen_coords.y = _ScreenParams.y - o.screen_coords.y;
        return o;
      }

      half4 frag(v2f i, UNITY_VPOS_TYPE screen_pos : VPOS) : COLOR
      {
        // calculate normalized distance between fragment screen-space
        // position, and point center in screen-space
        float k = 1.0 - distance(screen_pos.xy, i.screen_coords.xy) * i.inv_hsize;

        // calculate output color
        half4 output = i.color;
        output.w *= k;
        return output;
      }
      ENDCG
    }
  }
}