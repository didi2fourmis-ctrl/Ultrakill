// "using" = on importe des bibliothèques de code déjà écrites par d'autres.
using BepInEx;                // le chargeur de mods
using BepInEx.Configuration;  // pour les options modifiables par le joueur
using BepInEx.Logging;        // pour écrire des messages dans la console
using HarmonyLib;             // pour modifier les fonctions du jeu

namespace MonPremierMod
{
    // Cette ligne dit à BepInEx : "voici un mod, avec un identifiant unique, un nom et une version".
    [BepInPlugin("fr.samandine.monpremiermod", "Mon Premier Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        // "static" = accessible de partout, sans avoir besoin d'un objet Plugin.
        internal static ManualLogSource Log;
        internal static ConfigEntry<float> MultiplicateurDegats;

        // Awake() est appelée automatiquement UNE fois, quand le jeu charge le mod.
        private void Awake()
        {
            Log = Logger;

            // Crée une option dans BepInEx/config/fr.samandine.monpremiermod.cfg
            // Le joueur peut la modifier avec un simple éditeur de texte.
            MultiplicateurDegats = Config.Bind(
                "General",                         // section
                "MultiplicateurDegats",            // nom de l'option
                0.5f,                              // valeur par défaut
                "Multiplie les dégâts reçus par le joueur (0.5 = moitié, 2 = double)");

            // Harmony cherche toutes les classes marquées [HarmonyPatch] dans ce mod et les applique.
            new Harmony("fr.samandine.monpremiermod").PatchAll();

            Log.LogInfo("Mon Premier Mod est chargé !");
        }
    }

    // Ce "patch" s'accroche à la fonction GetHurt de la classe NewMovement (= le joueur dans ULTRAKILL).
    // GetHurt est appelée par le jeu à chaque fois que le joueur prend des dégâts.
    [HarmonyPatch(typeof(NewMovement), nameof(NewMovement.GetHurt))]
    public static class PatchDegatsJoueur
    {
        // "Prefix" = notre code s'exécute AVANT la fonction originale.
        // "ref int damage" = on reçoit le paramètre "damage" de GetHurt et on peut le modifier.
        static void Prefix(ref int damage)
        {
            int avant = damage;
            damage = UnityEngine.Mathf.RoundToInt(damage * Plugin.MultiplicateurDegats.Value);
            Plugin.Log.LogInfo($"Dégâts reçus : {avant} -> {damage}");
        }
    }
}
