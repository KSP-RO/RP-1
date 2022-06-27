using UnityEngine;
using TMPro;

namespace RP0
{
    public static class TransformUtils
    {
        public static Transform FindDeepChild(this Transform parent, string name)
        {
            var result = parent.Find(name);
            if (result != null)
                return result;
            foreach (Transform child in parent)
            {
                result = child.FindDeepChild(name);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static void Dump(this GameObject go, string indent = "")
        {
            Debug.Log(indent + "Object: " + go.name);
            foreach (Component c in go.GetComponents<Component>())
            {
                string logStr = indent + c;
                if (c is TextMeshProUGUI t)
                    logStr += ": text=" + t.text;
                else if (c is RectTransform rt)
                    logStr += $": rect: {rt.rect}";

                Debug.Log(logStr);
            }

            foreach (Transform c in go.transform)
            {
                c.gameObject.Dump(indent + "    ");
            }
        }
    }
}
