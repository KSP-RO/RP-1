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
            Debug.Log($"{indent}+{go.name} {go.transform.position}/{go.transform.localPosition} {go.transform.localScale}");
            foreach (Component c in go.GetComponents<Component>())
            {
                string logStr = $"{indent}{c.ToString().Replace(go.name,"")}";
                if (c is TextMeshProUGUI t)
                    logStr += ": text=" + t.text;
                else if (c is RectTransform rt)
                    logStr += $": rectTrf anchor3d={rt.anchoredPosition3D}, anchorN/X={rt.anchorMin}/{rt.anchorMax} size={rt.sizeDelta} pivot={rt.pivot} offsetN/X={rt.offsetMin}/{rt.offsetMax}";

                Debug.Log(logStr);
            }

            foreach (Transform c in go.transform)
            {
                c.gameObject.Dump(indent + "    ");
            }
        }
    }
}
