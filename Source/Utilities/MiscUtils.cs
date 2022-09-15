using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public static class MiscUtils
    {
        public static TValue ValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            dict.TryGetValue(key, out TValue value);
            return value;
        }

        public static T Pop<T>(this List<T> list)
        {
            T val = list[0];
            list.RemoveAt(0);
            return val;
        }

        public static Texture2D GetReadableCopy(this Texture2D texture)
        {
            Texture2D readable = new Texture2D(texture.width, texture.height);

            RenderTexture tmp = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            
            readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }

        public static Sprite ReplaceSprite(Sprite sprite, string path, SpriteMeshType type)
        {
            return Sprite.Create(GameDatabase.Instance.GetTexture(path, false),
                sprite.rect,
                sprite.pivot,
                sprite.pixelsPerUnit,
                0,
                type,
                sprite.border);
        }
    }
}
