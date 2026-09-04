using System.Collections.Generic;
using UnityEngine;

namespace Statecraft
{
    /// <summary>
    /// The prototype builds its whole world from primitives at runtime, so it needs
    /// materials that exist regardless of which render pipeline the project ends up on.
    /// One place to resolve a shader, one cache so we do not leak a material per cube.
    /// </summary>
    public static class MaterialFactory
    {
        static Shader _lit;
        static Shader _unlit;
        static readonly Dictionary<int, Material> _litCache   = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> _unlitCache = new Dictionary<int, Material>();

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Diffuse");
                return _lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null)
                    _unlit = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color")
                        ?? LitShader;
                return _unlit;
            }
        }

        public static Material Lit(Color c)
        {
            int key = ColorKey(c);
            if (_litCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(LitShader);
            SetColour(m, c);
            _litCache[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            int key = ColorKey(c);
            if (_unlitCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(UnlitShader);
            SetColour(m, c);
            _unlitCache[key] = m;
            return m;
        }

        static void SetColour(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        static int ColorKey(Color c)
            => (Mathf.RoundToInt(c.r * 255) << 24)
             | (Mathf.RoundToInt(c.g * 255) << 16)
             | (Mathf.RoundToInt(c.b * 255) << 8)
             |  Mathf.RoundToInt(c.a * 255);

        /// <summary>Paint a primitive without allocating a fresh material per object.</summary>
        public static GameObject Paint(GameObject go, Color c, bool unlit = false)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = unlit ? Unlit(c) : Lit(c);
            return go;
        }
    }
}
