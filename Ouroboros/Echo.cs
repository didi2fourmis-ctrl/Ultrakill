using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros
{
    // Un Écho : le fantôme de toi-même, N secondes dans le passé, qui rejoue chacun de tes
    // mouvements et chacun de tes tirs — avec de vrais dégâts.
    internal class Echo : MonoBehaviour
    {
        private static readonly List<ShotEvent> ShotBuffer = new List<ShotEvent>(16);

        private float spawnTime, windowStart, duration, cursor;
        private Transform body, head, visor;
        private Renderer[] renderers;
        private bool visible = true;

        public Vector3 Center => body.position;
        public Vector3 Feet { get; private set; }
        public bool Visible => visible;

        public static Echo Create(float duration, Color tint)
        {
            var root = new GameObject("OuroborosEcho");
            var echo = root.AddComponent<Echo>();
            echo.spawnTime = Time.time;
            echo.duration = duration;
            echo.windowStart = Time.time - duration;
            echo.cursor = echo.windowStart;

            echo.body = Fx.Primitive(PrimitiveType.Capsule, tint, root.transform).transform;
            echo.body.localScale = new Vector3(0.9f, 1.3f, 0.9f);
            echo.head = Fx.Primitive(PrimitiveType.Sphere, tint, root.transform).transform;
            echo.head.localScale = Vector3.one * 0.7f;
            echo.visor = Fx.Primitive(PrimitiveType.Cube, Fx.Blood, echo.head).transform;
            echo.visor.localScale = new Vector3(0.9f, 0.18f, 0.3f);
            echo.visor.localPosition = new Vector3(0f, 0.05f, 0.4f);

            var trail = echo.body.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Fx.Mat(tint);
            trail.time = 0.35f;
            trail.startWidth = 0.8f;
            trail.endWidth = 0f;

            echo.renderers = root.GetComponentsInChildren<Renderer>();
            echo.Step();
            return echo;
        }

        private void Update()
        {
            if (Time.time - spawnTime >= duration)
            {
                Fx.Ring(Center, Fx.Cyan, 2.5f);
                Destroy(gameObject);
                return;
            }
            Step();
        }

        private void Step()
        {
            float replay = windowStart + (Time.time - spawnTime);

            if (ChronoRecorder.TrySample(replay, out var f))
            {
                SetVisible(true);
                Feet = f.Body;
                body.position = f.Body;
                head.position = f.Eye;
                head.rotation = f.Look;
            }
            else
            {
                SetVisible(false);
            }

            // Scintillement : l'Écho n'appartient pas tout à fait à ce moment.
            float life = (Time.time - spawnTime) / duration;
            float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * 40f + GetInstanceID());
            float alpha = Fx.Cyan.a * flicker * (1f - life * life);
            foreach (var r in renderers)
            {
                if (r == null || r.transform == visor) continue;
                var c = r.sharedMaterial.color;
                c.a = alpha;
                r.sharedMaterial.color = c;
            }

            ChronoRecorder.ShotsBetween(cursor, replay, ShotBuffer);
            cursor = replay;
            if (visible)
                foreach (var shot in ShotBuffer) EchoGunfire.Fire(shot);
        }

        private void SetVisible(bool on)
        {
            if (visible == on) return;
            visible = on;
            foreach (var r in renderers) if (r != null) r.enabled = on;
        }

        public void Shatter(Color c)
        {
            Fx.Ring(Center, c, 3f);
            Destroy(gameObject);
        }
    }
}
