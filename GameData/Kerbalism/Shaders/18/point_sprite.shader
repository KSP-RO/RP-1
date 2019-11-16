// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// render a textured point sprite
// - POINT_TEXTURE: 2d texture to use
// - POINT_COLOR: multiply texture sample with this
// - POINT_SIZE: point size on screen, in pixels


Shader "Custom/PointSprite"
{
  Properties
  {
    POINT_TEXTURE ("Point texture", 2D) = "white" {}
    POINT_COLOR ("Point color", Color) = (1.0,1.0,1.0,1.0)
    POINT_SIZE ("Point size in pixels", Float) = 16.0
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

      sampler2D POINT_TEXTURE;
      fixed4 POINT_COLOR;
      float POINT_SIZE;

      struct v2f
      {
        float size : PSIZE;
        float inv_size : TEXCOORD0;
        float2 screen_coords : TEXCOORD1;
      };

      v2f vert(float4 in_pos : POSITION, out float4 pos : SV_POSITION)
      {
        // output clip-space vertex position
        pos = UnityObjectToClipPos(in_pos);

        // pass down point size and inverse of point size
        v2f o;
        o.size = POINT_SIZE;
        o.inv_size = 1.0 / o.size;

        // calculate and pass down screen-space position of point center
        o.screen_coords = (pos.xy / pos.w) * 0.5 + 0.5;
        o.screen_coords *= _ScreenParams.xy;
        o.screen_coords.y = _ScreenParams.y - o.screen_coords.y;
        return o;
      }

      half4 frag(v2f i, UNITY_VPOS_TYPE screen_pos : VPOS) : COLOR
      {
        // calculate txcoords
        float s = 0.5 + (screen_pos.x - i.screen_coords.x) * i.inv_size;
        float t = 0.5 - (screen_pos.y - i.screen_coords.y) * i.inv_size;

        // sample texture and multiply by specified color
        return tex2D(POINT_TEXTURE, float2(s, t)) * POINT_COLOR;
      }
      ENDCG
    }
  }
}