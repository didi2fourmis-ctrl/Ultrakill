using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros
{
    // OUROBOROS — « Le sang se souvient. »
    // Le temps enregistre chacun de tes gestes. Plus ton style est haut, plus ton passé
    // revient combattre à tes côtés sous forme d'Échos. Frappe un Écho pour fusionner
    // avec lui (PARADOXE). Retombe au rang D et tes Échos s'effondrent sur toi.
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "fr.samandine.ouroboros";
        public const string Name = "OUROBOROS";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<KeyCode> PanelKey;
        internal static ConfigEntry<KeyCode> ForceKey;
        internal static ConfigEntry<bool> IgnoreRank;
        internal static ConfigEntry<float> EchoDuration;
        internal static ConfigEntry<float> SpawnInterval;
        internal static ConfigEntry<float> EchoDamage;
        internal static ConfigEntry<float> ParadoxRange;
        internal static ConfigEntry<int> ParadoxHeal;
        internal static ConfigEntry<float> ParadoxBlastRadius;
        internal static ConfigEntry<float> CausalityWindow;
        internal static ConfigEntry<float> CausalityBonus;
        internal static ConfigEntry<bool> TemporalDebt;
        internal static ConfigEntry<int> DebtDamage;
        internal static ConfigEntry<bool> ShowHud;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true, "Active le mod.");
            ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.F8, "Touche pour activer/désactiver OUROBOROS en jeu.");
            ShowHud = Config.Bind("General", "ShowHud", true, "Affiche le compteur d'Échos.");
            PanelKey = Config.Bind("General", "PanelKey", KeyCode.F9, "Ouvre/ferme le panneau d'état d'OUROBOROS.");
            ForceKey = Config.Bind("General", "ForceKey", KeyCode.F10, "Invoque un Écho tout de suite (test), quel que soit le rang.");
            IgnoreRank = Config.Bind("Echoes", "IgnoreRank", false, "Si true, le nombre maximum d'Échos (4) est toujours disponible, quel que soit le rang de style.");

            EchoDuration = Config.Bind("Echoes", "EchoDuration", 5f, "Secondes de passé rejouées par chaque Écho.");
            SpawnInterval = Config.Bind("Echoes", "SpawnInterval", 2.5f, "Délai minimum entre deux Échos.");
            EchoDamage = Config.Bind("Echoes", "DamageMultiplier", 0.6f, "Multiplicateur des dégâts des tirs rejoués.");

            ParadoxRange = Config.Bind("Paradox", "Range", 5f, "Portée pour frapper un Écho (PARADOXE).");
            ParadoxHeal = Config.Bind("Paradox", "Heal", 25, "PV rendus par un PARADOXE.");
            ParadoxBlastRadius = Config.Bind("Paradox", "BlastRadius", 9f, "Rayon de l'onde chronique.");

            CausalityWindow = Config.Bind("Causality", "Window", 0.6f, "Fenêtre (s) pendant laquelle toi et ton Écho devez toucher le même ennemi.");
            CausalityBonus = Config.Bind("Causality", "Bonus", 1.0f, "Dégâts bonus d'une BOUCLE CAUSALE (1.0 = x2).");

            TemporalDebt = Config.Bind("Debt", "Enabled", true, "Retomber au rang D fait s'effondrer tes Échos sur toi.");
            DebtDamage = Config.Bind("Debt", "DamagePerEcho", 15, "Dégâts subis par Écho effondré.");

            var harmony = new Harmony(Guid);
            Patches.Apply(harmony);

            SceneManager.sceneLoaded += (scene, mode) => EchoDirector.ResetAll();

            var host = new GameObject("OUROBOROS");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<EchoDirector>();

            Log.LogInfo("OUROBOROS chargé. Le serpent se mord la queue.");
        }
    }
}
