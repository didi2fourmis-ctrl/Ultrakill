using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Ouroboros
{
    // Le cœur du mod : enregistre, invoque les Échos selon ton rang de style,
    // gère le PARADOXE (frapper un Écho) et la DETTE TEMPORELLE (retomber au rang D).
    internal class EchoDirector : MonoBehaviour
    {
        // Nombre max d'Échos simultanés par rang : D, C, B, A, S, SS, SSS, ULTRAKILL.
        private static readonly int[] EchoCap = { 0, 1, 1, 2, 2, 3, 3, 4 };

        private static readonly List<Echo> Echoes = new List<Echo>();
        private static float lastSpawn;
        private static int lastRank;
        private static bool wasDead;

        private string banner;
        private float bannerUntil;
        private GUIStyle style;

        public static Transform CameraTransform
        {
            get
            {
                var cc = MonoSingleton<CameraController>.Instance;
                return cc != null ? cc.transform : null;
            }
        }

        public static void ResetAll()
        {
            foreach (var e in Echoes) if (e != null) Destroy(e.gameObject);
            Echoes.Clear();
            ChronoRecorder.Clear();
            Causality.Clear();
            lastRank = 0;
            lastSpawn = Time.time;
        }

        private void Update()
        {
            if (Input.GetKeyDown(Plugin.ToggleKey.Value))
            {
                Plugin.Enabled.Value = !Plugin.Enabled.Value;
                if (!Plugin.Enabled.Value) ResetAll();
                Banner(Plugin.Enabled.Value ? "OUROBOROS : LE TEMPS SE SOUVIENT" : "OUROBOROS : SILENCE");
            }
            if (!Plugin.Enabled.Value) return;

            var nm = MonoSingleton<NewMovement>.Instance;
            var cam = CameraTransform;
            if (nm == null || cam == null || !nm.isActiveAndEnabled) return;

            // Mort ou respawn : le ruban est coupé.
            if (nm.dead)
            {
                if (!wasDead) ResetAll();
                wasDead = true;
                return;
            }
            wasDead = false;

            float duration = Plugin.EchoDuration.Value;
            ChronoRecorder.Tick(nm, cam, duration * 2f + 1f);
            Echoes.RemoveAll(e => e == null);

            int rank = Mathf.Clamp(StyleBridge.Rank, 0, EchoCap.Length - 1);
            HandleDebt(nm, rank);
            lastRank = rank;

            if (Echoes.Count < EchoCap[rank]
                && Time.time - lastSpawn >= Plugin.SpawnInterval.Value
                && ChronoRecorder.Oldest <= Time.time - duration)
            {
                lastSpawn = Time.time;
                Echoes.Add(Echo.Create(duration, Fx.Cyan));
            }

            if (PunchPressed()) TryParadox(nm, cam);
        }

        // ── PARADOXE ─────────────────────────────────────────────────────────────
        // Frappe ton propre Écho : tu fusionnes avec ton passé. Tu es projeté à sa
        // position, une onde chronique pulvérise tout autour, et tu récupères de la vie.
        private void TryParadox(NewMovement nm, Transform cam)
        {
            Echo best = null;
            float bestDist = Plugin.ParadoxRange.Value;
            foreach (var e in Echoes)
            {
                if (e == null || !e.Visible) continue;
                var to = e.Center - cam.position;
                float d = to.magnitude;
                if (d > bestDist || Vector3.Dot(cam.forward, to / Mathf.Max(d, 0.01f)) < 0.55f) continue;
                best = e;
                bestDist = d;
            }
            if (best == null) return;

            var from = nm.transform.position;
            var dest = best.Feet;
            Echoes.Remove(best);
            best.Shatter(Fx.Magenta);

            nm.transform.position = dest;
            var rb = nm.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = new Vector3(rb.velocity.x, Mathf.Max(rb.velocity.y, 18f), rb.velocity.z);

            Fx.Beam(from + Vector3.up, dest + Vector3.up, Fx.Magenta, 0.5f);
            EchoGunfire.Blast(dest, Plugin.ParadoxBlastRadius.Value, 2.5f, Fx.Magenta);
            Reflect.Call(nm, "GetHealth", Plugin.ParadoxHeal.Value, false);
            Reflect.Call(MonoSingleton<CameraController>.Instance, "CameraShake", 1.5f);
            StyleBridge.Add(300, StyleBridge.Paradox);
            Banner("PARADOXE");
        }

        // ── DETTE TEMPORELLE ─────────────────────────────────────────────────────
        // Le passé ne se maintient que par le style. Retombe au rang D avec des Échos
        // actifs, et chacun d'eux s'effondre sur toi.
        private void HandleDebt(NewMovement nm, int rank)
        {
            if (!Plugin.TemporalDebt.Value || rank != 0 || lastRank == 0 || Echoes.Count == 0) return;

            int count = Echoes.Count;
            foreach (var e in Echoes)
            {
                if (e == null) continue;
                Fx.Beam(e.Center, nm.transform.position + Vector3.up, Fx.Blood, 0.4f);
                e.Shatter(Fx.Blood);
            }
            Echoes.Clear();

            Reflect.Call(nm, "GetHurt", Plugin.DebtDamage.Value * count, true);
            StyleBridge.Add(0, StyleBridge.Debt);
            Banner("DETTE TEMPORELLE x" + count);
        }

        // ── Entrée "coup de poing" ───────────────────────────────────────────────
        // InputManager.InputSource.Punch.WasPerformedThisFrame, lu par réflexion pour
        // survivre aux refontes du système d'input. Repli : touche F (défaut du jeu).
        private static bool PunchPressed()
        {
            var im = MonoSingleton<InputManager>.Instance;
            var performed = Member(Member(Member(im, "InputSource"), "Punch"), "WasPerformedThisFrame");
            if (performed is bool b) return b;
            return Input.GetKeyDown(KeyCode.F);
        }

        private static object Member(object o, string name)
        {
            if (o == null) return null;
            var t = Traverse.Create(o);
            var p = t.Property(name);
            if (p.PropertyExists()) return p.GetValue();
            var f = t.Field(name);
            return f.FieldExists() ? f.GetValue() : null;
        }

        // ── HUD ──────────────────────────────────────────────────────────────────
        private void Banner(string text)
        {
            banner = text;
            bannerUntil = Time.unscaledTime + 1.5f;
        }

        private void OnGUI()
        {
            if (!Plugin.ShowHud.Value) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };

            if (Plugin.Enabled.Value && MonoSingleton<NewMovement>.Instance != null)
            {
                int cap = EchoCap[Mathf.Clamp(lastRank, 0, EchoCap.Length - 1)];
                string rings = "";
                for (int i = 0; i < EchoCap[EchoCap.Length - 1]; i++)
                    rings += i < Echoes.Count ? "●" : (i < cap ? "○" : "·");
                style.normal.textColor = new Color(0.49f, 0.98f, 1f);
                GUI.Label(new Rect(0, 12, Screen.width, 30), "∞ " + rings + " ∞", style);
            }

            if (banner != null && Time.unscaledTime < bannerUntil)
            {
                style.normal.textColor = new Color(1f, 0.3f, 1f);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 40), banner, style);
            }
        }
    }
}
