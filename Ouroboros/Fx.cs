using UnityEngine;
using UnityEngine.Rendering;

namespace Ouroboros
{
    // Effets visuels sans asset bundle : primitives + Sprites/Default (toujours présent dans un build).
    internal static class Fx
    {
        public static readonly Color Cyan = new Color(0.49f, 0.98f, 1f, 0.45f);
        public static readonly Color Gold = new Color(1f, 0.83f, 0.3f, 0.7f);
        public static readonly Color Magenta = new Color(1f, 0.3f, 1f, 0.7f);
        public static readonly Color Blood = new Color(1f, 0.1f, 0.1f, 0.8f);

        private static Shader shader;

        public static Material Mat(Color c)
        {
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { color = c };
        }

        public static GameObject Primitive(PrimitiveType type, Color c, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static void Beam(Vector3 from, Vector3 to, Color c, float width)
        {
            var go = new GameObject("OuroborosBeam");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mat(c);
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width * 0.3f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<Fade>().Init(0.3f, c, lr: lr);
        }

        public static void Ring(Vector3 at, Color c, float size)
        {
            var go = Primitive(PrimitiveType.Sphere, c);
            go.name = "OuroborosRing";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.1f;
            go.AddComponent<Fade>().Init(0.35f, c, grow: size, r: go.GetComponent<Renderer>());
        }

        private class Fade : MonoBehaviour
        {
            private float life, born, grow;
            private Color color;
            private LineRenderer line;
            private Renderer rend;

            public void Init(float duration, Color c, float grow = 0f, LineRenderer lr = null, Renderer r = null)
            {
                life = duration;
                born = Time.time;
                color = c;
                this.grow = grow;
                line = lr;
                rend = r;
            }

            private void Update()
            {
                float k = (Time.time - born) / life;
                if (k >= 1f) { Destroy(gameObject); return; }
                var c = color;
                c.a *= 1f - k;
                if (line != null) line.sharedMaterial.color = c;
                if (rend != null) rend.sharedMaterial.color = c;
                if (grow > 0f) transform.localScale = Vector3.one * Mathf.Lerp(0.1f, grow, Mathf.Sqrt(k));
            }
        }
    }
}
