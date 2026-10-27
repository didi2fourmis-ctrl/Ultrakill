using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros
{
    // Le cœur du mod : enregistre, invoque les Échos selon ton rang de style,
    // gère le PARADOXE (frapper un Écho) et la DETTE TEMPORELLE (retomber au rang D).
    internal class EchoDirector : MonoBehaviour
    {
        // Nombre max d'Échos simultanés par rang : D, C, B, A, S, SS, SSS, ULTRAKILL.
        private static readonly int[] EchoCap = { 0, 1, 1, 2, 2, 3, 3, 4 };
        private static readonly string[] RankNames = { "D", "C", "B", "A", "S", "SS", "SSS", "ULTRAKILL" };

        // Rang utilisé si le jeu ne permet pas de lire le rang de style (2 Échos).
        private const int FallbackRank = 3;

        private static readonly List<Echo> Echoes = new List<Echo>();
        private static float lastSpawn;
        private static int lastRank;
        private static bool wasDead;

        private string banner;
        private float bannerUntil;
        private bool panelOpen;
        private GUIStyle style, panelStyle;

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
            if (Input.GetKeyDown(Plugin.PanelKey.Value)) panelOpen = !panelOpen;

            if (Input.GetKeyDown(Plugin.ToggleKey.Value))
            {
                Plugin.Enabled.Value = !Plugin.Enabled.Value;
                if (!Plugin.Enabled.Value) ResetAll();
                Banner(Plugin.Enabled.Value ? "OUROBOROS : LE TEMPS SE SOUVIENT" : "OUROBOROS : SILENCE");
            }
            if (!Plugin.Enabled.Value)
            {
                Diag.Status = "désactivé (appuie sur " + Plugin.ToggleKey.Value + ")";
                return;
            }

            // Une erreur ne doit pas tuer le mod en silence : elle s'affiche dans le panneau.
            try { Tick(); }
            catch (Exception e) { Diag.Error(e); }
        }

        private void Tick()
        {
            var nm = MonoSingleton<NewMovement>.Instance;
            var cam = CameraTransform;
            Diag.Player = nm != null;
            Diag.Camera = cam != null;
            if (nm == null) { Diag.Status = "pas de joueur (menu ?)"; return; }
            if (cam == null) { Diag.Status = "caméra introuvable"; return; }
            if (!nm.isActiveAndEnabled) { Diag.Status = "joueur inactif"; return; }

            // Mort ou respawn : le ruban est coupé.
            if (nm.dead)
            {
                if (!wasDead) ResetAll();
                wasDead = true;
                Diag.Status = "mort : le passé est effacé";
                return;
            }
            wasDead = false;

            float duration = Plugin.EchoDuration.Value;
            ChronoRecorder.Tick(nm, cam, duration * 2f + 1f);
            Diag.History = Mathf.Max(0f, Time.time - ChronoRecorder.Oldest);
            Echoes.RemoveAll(e => e == null);

            int raw = StyleBridge.Rank;
            Diag.RankRaw = raw;
            bool readable = raw >= 0;
            int rank = Plugin.IgnoreRank.Value ? EchoCap.Length - 1
                : readable ? Mathf.Clamp(raw, 0, EchoCap.Length - 1)
                : FallbackRank;
            int cap = EchoCap[rank];
            Diag.Cap = cap;

            if (readable && !Plugin.IgnoreRank.Value) HandleDebt(nm, rank);
            lastRank = rank;

            if (Input.GetKeyDown(Plugin.ForceKey.Value)) ForceEcho(duration);

            float wait = Plugin.SpawnInterval.Value - (Time.time - lastSpawn);
            if (cap == 0) Diag.Status = "rang D : monte au rang C pour invoquer des Échos";
            else if (Diag.History < duration) Diag.Status = "enregistrement du passé… " + Diag.History.ToString("0.0") + " / " + duration.ToString("0") + " s";
            else if (Echoes.Count >= cap) Diag.Status = "maximum d'Échos pour ce rang";
            else if (wait > 0f) Diag.Status = "prochain Écho dans " + wait.ToString("0.0") + " s";
            else
            {
                lastSpawn = Time.time;
                Spawn(duration);
                Diag.Status = "Écho invoqué";
            }
            if (!readable && !Plugin.IgnoreRank.Value) Diag.Status += "  [rang illisible : mode secours]";

            if (PunchPressed()) TryParadox(nm, cam);
        }

        private void Spawn(float duration)
        {
            Echoes.Add(Echo.Create(duration, Fx.Cyan));
            Diag.EchoesSpawned++;
        }

        // Test : invoque un Écho immédiatement, avec tout le passé disponible.
        private void ForceEcho(float duration)
        {
            float available = Mathf.Min(duration, Diag.History - 0.05f);
            if (available < 0.5f)
            {
                Banner("PAS ENCORE DE PASSÉ À REJOUER");
                return;
            }
            lastSpawn = Time.time;
            Spawn(available);
            Banner("ÉCHO FORCÉ");
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
            var performed = Reflect.Get(Reflect.Get(Reflect.Get(im, "InputSource"), "Punch"), "WasPerformedThisFrame");
            if (performed is bool b) return b;
            return Input.GetKeyDown(KeyCode.F);
        }

        // ── HUD ──────────────────────────────────────────────────────────────────
        private void Banner(string text)
        {
            banner = text;
            bannerUntil = Time.unscaledTime + 1.5f;
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                panelStyle = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
                panelStyle.normal.textColor = Color.white;
                panelStyle.padding = new RectOffset(14, 14, 12, 12);
            }

            if (panelOpen) DrawPanel();

            if (Plugin.ShowHud.Value && Plugin.Enabled.Value && MonoSingleton<NewMovement>.Instance != null)
            {
                string rings = "";
                for (int i = 0; i < EchoCap[EchoCap.Length - 1]; i++)
                    rings += i < Echoes.Count ? "●" : (i < Diag.Cap ? "○" : "·");
                style.normal.textColor = new Color(0.49f, 0.98f, 1f);
                GUI.Label(new Rect(0, 12, Screen.width, 30), "∞ " + rings + " ∞", style);
            }

            if (banner != null && Time.unscaledTime < bannerUntil)
            {
                style.normal.textColor = new Color(1f, 0.3f, 1f);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 40), banner, style);
            }
        }

        // Panneau d'état (F9) : tout ce qu'il faut pour savoir pourquoi rien ne se passe.
        private void DrawPanel()
        {
            string Ok(bool v) => v ? "<color=#7dff7d>OK</color>" : "<color=#ff5050>NON</color>";
            string rank = Diag.RankRaw >= 0 && Diag.RankRaw < RankNames.Length ? RankNames[Diag.RankRaw]
                : Diag.RankRaw < 0 ? "<color=#ff5050>illisible</color>" : Diag.RankRaw.ToString();

            string text =
                "<b><color=#7df9ff>∞ OUROBOROS " + Plugin.Version + " ∞</color></b>\n\n" +
                "<b>État :</b> " + Diag.Status + "\n\n" +
                "Mod actif : " + Ok(Plugin.Enabled.Value) + "   (" + Plugin.ToggleKey.Value + ")\n" +
                "Joueur trouvé : " + Ok(Diag.Player) + "   Caméra : " + Ok(Diag.Camera) + "\n" +
                "Rang de style : " + rank + (Plugin.IgnoreRank.Value ? "  (ignoré : IgnoreRank)" : "") + "\n" +
                "Échos : " + Echoes.Count + " / " + Diag.Cap + "   (invoqués en tout : " + Diag.EchoesSpawned + ")\n" +
                "Passé enregistré : " + Diag.History.ToString("0.0") + " s / " + Plugin.EchoDuration.Value.ToString("0") + " s\n" +
                "Armes suivies : " + Diag.WeaponsHooked + " / 5   Tirs enregistrés : " + Diag.ShotsRecorded + "\n" +
                "Boucle causale : " + Ok(Diag.DamageHooked) + "   Shader : " + Diag.Shader + "\n" +
                (Diag.LastError != null ? "\n<color=#ff5050>Erreur : " + Diag.LastError + "</color>\n" : "") +
                "\n" + Plugin.ForceKey.Value + " : forcer un Écho     " + Plugin.PanelKey.Value + " : fermer";

            GUI.Box(new Rect(20, 60, 560, Diag.LastError != null ? 420 : 360), text, panelStyle);
        }
    }
}
