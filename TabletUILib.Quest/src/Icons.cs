using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace TabletUILib
{
    internal static class Icons
    {
        internal const float Size = 0.12f;

        static Mesh quad;
        static Shader shader;
        static Dictionary<Texture2D, Material> materials = new Dictionary<Texture2D, Material>();

        internal static Texture2D Load(Assembly asm, string name)
        {
            var names = asm.GetManifestResourceNames();
            var found = names.FirstOrDefault(n => n == name) ?? names.FirstOrDefault(n => n.EndsWith("." + name));
            if (found == null)
            {
                MelonLogger.Warning("no " + name + " embedded in " + asm.GetName().Name + ", using the text label");
                return null;
            }

            byte[] bytes;
            using (var s = asm.GetManifestResourceStream(found))
            using (var mem = new MemoryStream())
            {
                s.CopyTo(mem);
                bytes = mem.ToArray();
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!Il2CppImageConversionManager.LoadImage(tex, bytes))
            {
                MelonLogger.Warning(found + " in " + asm.GetName().Name + " isnt a png or jpg, using the text label");
                Object.Destroy(tex);
                return null;
            }
            tex.name = found;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Object.DontDestroyOnLoad(tex);
            return tex;
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "TabletUILib quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 3, 1, 3, 0, 2 };
            quad.RecalculateNormals();
            quad.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Object.DontDestroyOnLoad(quad);
            return quad;
        }

        internal static bool Add(Transform parent, Texture2D tex)
        {
            if (shader == null) shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader == null) return false;

            if (!materials.TryGetValue(tex, out var mat))
            {
                mat = new Material(shader) { mainTexture = tex, name = tex.name };
                mat.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Object.DontDestroyOnLoad(mat);
                materials[tex] = mat;
            }

            var go = new GameObject("TabletUILib icon");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one * Size;
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return true;
        }
    }
}
