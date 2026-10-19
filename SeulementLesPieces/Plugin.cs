using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SeulementLesPieces
{
    // Mod "Seulement les pièces" :
    //  1. Le joueur ne peut tenir que le Marksman (le revolver vert, qui lance des pièces).
    //  2. Les ennemis ne prennent des dégâts QUE des pièces : ricoshot (tirer sur une pièce)
    //     et coin punch (frapper une pièce). Tout le reste ne fait aucun dégât.
    [BepInPlugin("fr.samandine.seulementlespieces", "Seulement les pieces", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> BloquerArmes;
        internal static ConfigEntry<bool> SeulementDegatsDePieces;
        internal static ConfigEntry<bool> AfficherLogs;

        private void Awake()
        {
            Log = Logger;

            BloquerArmes = Config.Bind("General", "BloquerArmes", true,
                "Force le joueur à garder le Marksman en main");
            SeulementDegatsDePieces = Config.Bind("General", "SeulementDegatsDePieces", true,
                "Les ennemis ne prennent des dégâts que des pièces (ricoshot et coin punch)");
            AfficherLogs = Config.Bind("General", "AfficherLogs", true,
                "Affiche dans la console chaque dégât autorisé ou bloqué (utile pour vérifier que le mod marche)");

            // On applique chaque patch séparément : si l'un d'eux échoue (par exemple après une
            // mise à jour du jeu), les autres fonctionnent quand même et la console dit lequel a raté.
            var harmony = new Harmony("fr.samandine.seulementlespieces");
            foreach (Type type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    Log.LogInfo($"Patch OK : {type.Name}");
                }
                catch (Exception e)
                {
                    Log.LogError($"Patch RATÉ : {type.Name} -> {e.Message}");
                }
            }

            Log.LogInfo("Seulement les pieces est chargé !");
        }
    }

    // ------------------------------------------------------------------
    // Partie 1 : bloquer toutes les armes sauf le Marksman
    // ------------------------------------------------------------------

    // GunControl est la classe du jeu qui gère les armes du joueur.
    // Après chacune de ses mises à jour (à chaque image), on vérifie l'arme en main.
    [HarmonyPatch(typeof(GunControl), "Update")]
    public static class PatchArmes
    {
        private static bool dejaPrevenu;

        static void Postfix(GunControl __instance)
        {
            if (!Plugin.BloquerArmes.Value)
                return;

            // "Traverse" permet de lire un champ du jeu par son nom, même s'il est privé.
            var gc = Traverse.Create(__instance);
            GameObject armeEnMain = gc.Field("currentWeapon").GetValue<GameObject>();

            if (armeEnMain != null && EstLeMarksman(armeEnMain))
                return; // tout va bien

            GameObject marksman = TrouverLeMarksman(gc.Field("slots").GetValue<List<List<GameObject>>>());
            if (marksman == null)
            {
                // Le joueur n'a pas (encore) débloqué le Marksman : on cache l'arme en main.
                if (armeEnMain != null)
                    armeEnMain.SetActive(false);
                if (!dejaPrevenu)
                {
                    Plugin.Log.LogWarning("Marksman introuvable : as-tu débloqué le revolver vert ?");
                    dejaPrevenu = true;
                }
                return;
            }

            MettreEnMain(__instance, armeEnMain, marksman);
        }

        // Le Marksman est le Revolver dont la "variation" vaut 1 (0 = Piercer, 1 = Marksman, 2 = Sharpshooter).
        static bool EstLeMarksman(GameObject arme)
        {
            Revolver revolver = arme.GetComponent<Revolver>();
            return revolver != null && revolver.gunVariation == 1;
        }

        static GameObject TrouverLeMarksman(List<List<GameObject>> emplacements)
        {
            if (emplacements == null)
                return null;
            foreach (List<GameObject> emplacement in emplacements)
                foreach (GameObject arme in emplacement)
                    if (arme != null && EstLeMarksman(arme))
                        return arme;
            return null;
        }

        static void MettreEnMain(GunControl gunControl, GameObject armeEnMain, GameObject marksman)
        {
            // On utilise la fonction du jeu "ForceWeapon" si elle existe...
            MethodInfo forceWeapon = AccessTools.Method(typeof(GunControl), "ForceWeapon");
            if (forceWeapon != null)
            {
                forceWeapon.Invoke(gunControl, Arguments(forceWeapon, marksman));
                return;
            }

            // ...sinon on échange les armes nous-mêmes.
            if (armeEnMain != null)
                armeEnMain.SetActive(false);
            marksman.SetActive(true);
            Traverse.Create(gunControl).Field("currentWeapon").SetValue(marksman);
        }

        // Prépare la liste des paramètres : l'arme en premier, la valeur par défaut pour les autres.
        static object[] Arguments(MethodInfo methode, GameObject arme)
        {
            ParameterInfo[] parametres = methode.GetParameters();
            object[] valeurs = new object[parametres.Length];
            valeurs[0] = arme;
            for (int i = 1; i < parametres.Length; i++)
                valeurs[i] = parametres[i].HasDefaultValue ? parametres[i].DefaultValue : null;
            return valeurs;
        }
    }

    // ------------------------------------------------------------------
    // Partie 2 : seules les pièces font des dégâts
    // ------------------------------------------------------------------
    //
    // Idée : on retient "d'où vient le code qui est en train de s'exécuter".
    //  - Si c'est du code de la classe Coin (une pièce), les dégâts sont autorisés.
    //  - Si c'est un tir (RevolverBeam) créé par une pièce, les dégâts sont autorisés.
    //  - Sinon, les dégâts sont bloqués.

    static class Contexte
    {
        public static int DansUnePiece;          // > 0 pendant que du code de Coin s'exécute
        public static bool DansUnTirDePiece;     // vrai pendant que du code d'un tir de pièce s'exécute
    }

    // Petite "étiquette" qu'on colle sur les tirs créés par une pièce.
    public class MarqueTirDePiece : MonoBehaviour { }

    static class Outils
    {
        // Toutes les fonctions "normales" d'une classe du jeu (celles qu'Harmony peut patcher).
        public static IEnumerable<MethodBase> FonctionsDe(Type type, bool seulementNonStatiques = false)
        {
            return AccessTools.GetDeclaredMethods(type)
                .Where(m => !m.IsAbstract && !m.ContainsGenericParameters && m.GetMethodBody() != null)
                .Where(m => !seulementNonStatiques || !m.IsStatic)
                .Cast<MethodBase>();
        }
    }

    // Patch sur TOUTES les fonctions de Coin : on compte quand on "entre" et "sort" du code d'une pièce.
    [HarmonyPatch]
    public static class PatchContextePiece
    {
        static IEnumerable<MethodBase> TargetMethods() => Outils.FonctionsDe(typeof(Coin));

        static void Prefix() => Contexte.DansUnePiece++;

        // "Finalizer" s'exécute toujours à la fin, même en cas d'erreur.
        static void Finalizer() => Contexte.DansUnePiece--;
    }

    // Quand une pièce renvoie un tir (ricoshot) ou se fait frapper (coin punch), elle crée un
    // nouveau tir. On regarde quels tirs existaient avant et après, et on étiquette les nouveaux.
    [HarmonyPatch]
    public static class PatchTirsCreesParLesPieces
    {
        static IEnumerable<MethodBase> TargetMethods() =>
            Outils.FonctionsDe(typeof(Coin)).Where(m => m.Name.Contains("Reflect") || m.Name.Contains("Punch"));

        static void Prefix(out HashSet<int> __state)
        {
            __state = new HashSet<int>(UnityEngine.Object.FindObjectsOfType<RevolverBeam>().Select(t => t.GetInstanceID()));
        }

        static void Finalizer(HashSet<int> __state)
        {
            if (__state == null)
                return;
            foreach (RevolverBeam tir in UnityEngine.Object.FindObjectsOfType<RevolverBeam>())
                if (!__state.Contains(tir.GetInstanceID()) && tir.GetComponent<MarqueTirDePiece>() == null)
                    tir.gameObject.AddComponent<MarqueTirDePiece>();
        }
    }

    // Patch sur toutes les fonctions de RevolverBeam (un tir) : on note si le tir a l'étiquette.
    [HarmonyPatch]
    public static class PatchContexteTir
    {
        static IEnumerable<MethodBase> TargetMethods() => Outils.FonctionsDe(typeof(RevolverBeam), seulementNonStatiques: true);

        // "__state" garde l'ancienne valeur pour la remettre à la fin.
        static void Prefix(RevolverBeam __instance, out bool __state)
        {
            __state = Contexte.DansUnTirDePiece;
            Contexte.DansUnTirDePiece = __instance != null && __instance.GetComponent<MarqueTirDePiece>() != null;
        }

        static void Finalizer(bool __state) => Contexte.DansUnTirDePiece = __state;
    }

    // Le filtre : DeliverDamage est la fonction du jeu qui inflige des dégâts à un ennemi.
    // Si notre Prefix renvoie "false", la fonction originale n'est PAS exécutée -> aucun dégât.
    [HarmonyPatch]
    public static class PatchFiltreDegats
    {
        static IEnumerable<MethodBase> TargetMethods() =>
            Outils.FonctionsDe(typeof(EnemyIdentifier)).Where(m => m.Name == "DeliverDamage");

        static bool Prefix(EnemyIdentifier __instance)
        {
            if (!Plugin.SeulementDegatsDePieces.Value)
                return true;

            bool autorise = Contexte.DansUnePiece > 0 || Contexte.DansUnTirDePiece;

            if (Plugin.AfficherLogs.Value)
            {
                // "hitter" est le nom que le jeu donne à la source des dégâts (revolver, punch, coin...).
                string source = Traverse.Create(__instance).Field("hitter").GetValue<string>();
                Plugin.Log.LogInfo($"{(autorise ? "AUTORISÉ" : "bloqué  ")} | ennemi : {__instance.name} | source : {source}");
            }

            return autorise;
        }
    }
}
