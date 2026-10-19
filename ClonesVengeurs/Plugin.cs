using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace ClonesVengeurs
{
    // Mod "Clones vengeurs" :
    //  Chaque fois qu'un ennemi te fait perdre de la vie, il se duplique.
    //  Un clone apparaît à côté de lui (au prochain affichage d'image).
    [BepInPlugin("fr.samandine.clonesvengeurs", "Clones vengeurs", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Actif;
        internal static ConfigEntry<int> CopiesParCoup;
        internal static ConfigEntry<int> MaxClones;
        internal static ConfigEntry<float> DelaiParEnnemi;
        internal static ConfigEntry<bool> ClonesSeDupliquent;
        internal static ConfigEntry<bool> ExclureBoss;
        internal static ConfigEntry<bool> DevinerTireur;
        internal static ConfigEntry<float> RayonDevinette;
        internal static ConfigEntry<bool> SupprimerClonesALaMort;
        internal static ConfigEntry<bool> AfficherLogs;

        private void Awake()
        {
            Log = Logger;

            Actif = Config.Bind("General", "Actif", true,
                "Active la duplication des ennemis qui te blessent");
            CopiesParCoup = Config.Bind("General", "CopiesParCoup", 1,
                "Nombre de clones créés à chaque fois qu'un ennemi te blesse");
            MaxClones = Config.Bind("General", "MaxClones", 20,
                "Nombre maximum de clones vivants en même temps (sécurité anti-lag)");
            DelaiParEnnemi = Config.Bind("General", "DelaiParEnnemi", 1f,
                "Un même ennemi ne peut se dupliquer qu'une fois par ce nombre de secondes");
            ClonesSeDupliquent = Config.Bind("General", "ClonesSeDupliquent", true,
                "Les clones peuvent eux aussi se dupliquer quand ils te blessent");
            ExclureBoss = Config.Bind("General", "ExclureBoss", true,
                "Les boss (ennemis avec une grande barre de vie) ne se dupliquent pas");
            DevinerTireur = Config.Bind("Avance", "DevinerTireur", true,
                "Pour les projectiles et explosions, devine quel ennemi a tiré (l'ennemi le plus proche quand le projectile apparaît)");
            RayonDevinette = Config.Bind("Avance", "RayonDevinette", 10f,
                "Distance maximale (en mètres) entre un projectile qui apparaît et l'ennemi qui l'a tiré");
            SupprimerClonesALaMort = Config.Bind("General", "SupprimerClonesALaMort", true,
                "Supprime tous les clones quand tu meurs (sinon ils restent après le checkpoint)");
            AfficherLogs = Config.Bind("General", "AfficherLogs", false,
                "Affiche dans la console qui t'a blessé et chaque clone créé");

            // Comme dans "Seulement les pièces" : chaque patch est appliqué séparément,
            // et la console dit lequel a raté s'il y a un problème.
            var harmony = new Harmony("fr.samandine.clonesvengeurs");
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

            SourcesDeDegats.Patcher(harmony);

            Log.LogInfo("Clones vengeurs est chargé !");
        }

        // Update est appelée à chaque image : c'est ici qu'on crée les clones demandés.
        // (On ne clone pas directement pendant le coup, pour ne pas perturber le code du jeu en cours.)
        private void Update()
        {
            Duplication.Traiter();
        }
    }

    // ------------------------------------------------------------------
    // Partie 1 : savoir QUI est en train de faire des dégâts
    // ------------------------------------------------------------------
    //
    // Problème : la fonction du jeu NewMovement.GetHurt (le joueur prend des dégâts) ne dit
    // pas qui a frappé. Idée (la même que pour les pièces) : on retient "quel objet du jeu est
    // en train d'exécuter son code" au moment où GetHurt est appelée.
    //
    // Pour savoir quelles fonctions surveiller, on lit le code compilé du jeu au démarrage et
    // on garde toutes les fonctions qui appellent GetHurt (projectiles, coups au corps à corps,
    // explosions, lasers...). Comme ça, pas besoin de connaître leur nom à l'avance.

    // Petite "étiquette" collée sur un projectile/explosion pour retenir quel ennemi l'a créé.
    public class Origine : MonoBehaviour
    {
        public EnemyIdentifier ennemi;
    }

    static class SourcesDeDegats
    {
        // La "pile" des objets dont le code est en cours d'exécution (le dernier = le plus récent).
        public static readonly List<object> Pile = new List<object>();

        public static void Patcher(Harmony harmony)
        {
            // Les "numéros" internes des fonctions GetHurt dans le code compilé du jeu.
            HashSet<int> numerosGetHurt = new HashSet<int>(
                AccessTools.GetDeclaredMethods(typeof(NewMovement))
                    .Where(m => m.Name == "GetHurt")
                    .Select(m => m.MetadataToken));

            List<MethodInfo> appelants = new List<MethodInfo>();
            foreach (Type type in TypesDuJeu())
            {
                if (type.ContainsGenericParameters || EstLeJoueur(type))
                    continue;
                foreach (MethodInfo m in AccessTools.GetDeclaredMethods(type))
                    if (AppelleUneDe(m, numerosGetHurt))
                        appelants.Add(m);
            }

            int ok = 0;
            var prefix = new HarmonyMethod(typeof(SourcesDeDegats), nameof(Entrer));
            var finalizer = new HarmonyMethod(typeof(SourcesDeDegats), nameof(Sortir));
            foreach (MethodInfo m in appelants)
            {
                try
                {
                    harmony.Patch(m, prefix: prefix, finalizer: finalizer);
                    ok++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Impossible de surveiller {m.DeclaringType.Name}.{m.Name} : {e.Message}");
                }
            }
            Plugin.Log.LogInfo($"Sources de dégâts surveillées : {ok} fonctions");

            // Pour les projectiles et explosions : quand ils apparaissent, on note qui les a créés.
            var naissance = new HarmonyMethod(typeof(SourcesDeDegats), nameof(Naissance));
            int okNaissance = 0;
            foreach (Type type in appelants.Select(m => m.DeclaringType).Distinct())
            {
                if (!typeof(Component).IsAssignableFrom(type))
                    continue;
                foreach (string nom in new[] { "Awake", "Start" })
                {
                    try
                    {
                        MethodInfo m = AccessTools.DeclaredMethod(type, nom, Type.EmptyTypes);
                        if (m == null || m.IsStatic)
                            continue;
                        harmony.Patch(m, prefix: naissance);
                        okNaissance++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Impossible de surveiller {type.Name}.{nom} : {e.Message}");
                    }
                }
            }
            Plugin.Log.LogInfo($"Apparitions surveillées : {okNaissance} fonctions");
        }

        static IEnumerable<Type> TypesDuJeu()
        {
            try
            {
                return typeof(NewMovement).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        // Le joueur lui-même (et ses classes internes) ne compte pas comme une source.
        static bool EstLeJoueur(Type type)
        {
            for (Type t = type; t != null; t = t.DeclaringType)
                if (t == typeof(NewMovement))
                    return true;
            return false;
        }

        // Cherche dans le code compilé (IL) une instruction "call" (0x28) ou "callvirt" (0x6F)
        // suivie du numéro d'une des fonctions GetHurt.
        static bool AppelleUneDe(MethodInfo m, HashSet<int> numeros)
        {
            if (m.IsAbstract || m.ContainsGenericParameters)
                return false;
            byte[] il;
            try
            {
                il = m.GetMethodBody()?.GetILAsByteArray();
            }
            catch
            {
                return false;
            }
            if (il == null)
                return false;
            for (int i = 0; i + 4 < il.Length; i++)
                if ((il[i] == 0x28 || il[i] == 0x6F) && numeros.Contains(BitConverter.ToInt32(il, i + 1)))
                    return true;
            return false;
        }

        // Avant une fonction surveillée : on empile l'objet. "__state" retient la taille de la pile.
        static void Entrer(object __instance, out int __state)
        {
            __state = Pile.Count;
            Pile.Add(__instance);
        }

        // Après (toujours, même en cas d'erreur) : on remet la pile comme avant.
        static void Sortir(int __state)
        {
            if (Pile.Count > __state)
                Pile.RemoveRange(__state, Pile.Count - __state);
        }

        // Le dernier ennemi trouvé dans la pile (= celui dont le code est en train de te blesser).
        public static EnemyIdentifier EnnemiEnCours(Component saufCelui = null)
        {
            for (int i = Pile.Count - 1; i >= 0; i--)
            {
                Component c = EnComposant(Pile[i]);
                if (c == null || c == saufCelui)
                    continue;
                EnemyIdentifier ennemi = Ennemis.Proprietaire(c);
                if (ennemi != null)
                    return ennemi;
            }
            return null;
        }

        // Les coroutines et les "lambdas" du jeu sont des petits objets cachés : on retrouve
        // l'objet du jeu qui les a lancées grâce à leur champ "<>4__this".
        static Component EnComposant(object o)
        {
            if (o is Component c)
                return c;
            if (o == null)
                return null;
            FieldInfo parent = AccessTools.Field(o.GetType(), "<>4__this");
            return parent?.GetValue(o) as Component;
        }

        // Quand un objet qui peut faire des dégâts apparaît (Awake / Start), on retient son tireur.
        static void Naissance(Component __instance)
        {
            try
            {
                if (__instance == null || !Plugin.DevinerTireur.Value)
                    return;
                if (__instance.GetComponent<Origine>() != null || Ennemis.Proprietaire(__instance) != null)
                    return;

                // 1. Créé pendant que le code d'un ennemi (ou d'un de ses projectiles) tournait ?
                //    Ex. : l'explosion d'une roquette ennemie hérite de l'ennemi de la roquette.
                EnemyIdentifier tireur = EnnemiEnCours(__instance);

                // 2. Sinon : l'ennemi le plus proche, s'il s'agit bien d'un objet ennemi.
                if (tireur == null && Ennemis.SembleEnnemi(__instance))
                    tireur = Ennemis.PlusProche(__instance);

                if (tireur != null)
                    __instance.gameObject.AddComponent<Origine>().ennemi = tireur;
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"Naissance : {e.Message}");
            }
        }
    }

    static class Ennemis
    {
        static readonly Dictionary<Type, FieldInfo[]> champsEnnemi = new Dictionary<Type, FieldInfo[]>();

        // À quel ennemi appartient ce morceau de code ?
        public static EnemyIdentifier Proprietaire(Component c)
        {
            // a) Étiquette posée à l'apparition (projectiles, explosions)
            Origine origine = c.GetComponent<Origine>();
            if (origine != null && origine.ennemi != null)
                return origine.ennemi;

            // b) L'objet fait partie d'un ennemi (ex. : la main d'un Filth qui te mord)
            EnemyIdentifier parent = c.GetComponentInParent<EnemyIdentifier>();
            if (parent != null)
                return parent;

            // c) L'objet a un champ qui pointe vers un ennemi (ex. : "eid", "originEnemy"...)
            foreach (FieldInfo champ in ChampsEnnemi(c.GetType()))
                if (champ.GetValue(c) is EnemyIdentifier ennemi && ennemi != null)
                    return ennemi;

            return null;
        }

        static FieldInfo[] ChampsEnnemi(Type type)
        {
            if (!champsEnnemi.TryGetValue(type, out FieldInfo[] champs))
            {
                // On ignore les champs "target" : ce sont les CIBLES, pas les tireurs.
                champs = AccessTools.GetDeclaredFields(type)
                    .Concat(type.BaseType != null ? AccessTools.GetDeclaredFields(type.BaseType) : Enumerable.Empty<FieldInfo>())
                    .Where(f => !f.IsStatic && f.FieldType == typeof(EnemyIdentifier))
                    .Where(f => f.Name.IndexOf("target", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToArray();
                champsEnnemi[type] = champs;
            }
            return champs;
        }

        // Un projectile du joueur ne doit jamais être attribué à un ennemi.
        // On regarde les réglages que le jeu met sur l'objet ("playerBullet", "friendly", "enemy"...).
        public static bool SembleEnnemi(Component c)
        {
            var t = Traverse.Create(c);
            foreach (string nom in new[] { "playerBullet", "friendly", "parried" })
                if (t.Field(nom).FieldExists() && t.Field(nom).GetValue() is bool b && b)
                    return false;
            if (t.Field("enemy").FieldExists() && t.Field("enemy").GetValue() is bool ennemi)
                return ennemi;
            return TypeEnnemiAttendu(c) != null;
        }

        // Certains projectiles savent de quel TYPE d'ennemi ils viennent (ex. : "safeEnemyType").
        static EnemyType? TypeEnnemiAttendu(Component c)
        {
            foreach (FieldInfo champ in AccessTools.GetDeclaredFields(c.GetType()))
                if (!champ.IsStatic && champ.FieldType == typeof(EnemyType))
                    return (EnemyType)champ.GetValue(c);
            return null;
        }

        public static EnemyIdentifier PlusProche(Component c)
        {
            EnemyType? typeAttendu = TypeEnnemiAttendu(c);
            Vector3 position = c.transform.position;
            float meilleure = Plugin.RayonDevinette.Value;
            EnemyIdentifier choix = null;
            foreach (EnemyIdentifier e in UnityEngine.Object.FindObjectsOfType<EnemyIdentifier>())
            {
                if (e.dead || (typeAttendu.HasValue && e.enemyType != typeAttendu.Value))
                    continue;
                float distance = Vector3.Distance(position, e.transform.position);
                if (distance < meilleure)
                {
                    meilleure = distance;
                    choix = e;
                }
            }
            return choix;
        }
    }

    // ------------------------------------------------------------------
    // Partie 2 : quand le joueur perd de la vie, l'ennemi responsable se duplique
    // ------------------------------------------------------------------

    [HarmonyPatch]
    public static class PatchJoueurBlesse
    {
        static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(NewMovement)).Where(m => m.Name == "GetHurt");

        // Avant : on note la vie du joueur.
        static void Prefix(NewMovement __instance, out int __state) => __state = __instance.hp;

        // Après : si la vie a baissé (pas d'invincibilité, pas d'esquive), on cherche le coupable.
        static void Postfix(NewMovement __instance, int __state)
        {
            try
            {
                Duplication.Joueur = __instance;
                if (!Plugin.Actif.Value || __instance.hp >= __state || __instance.hp <= 0)
                    return;

                EnemyIdentifier coupable = SourcesDeDegats.EnnemiEnCours();
                if (Plugin.AfficherLogs.Value)
                    Plugin.Log.LogInfo($"Blessé ({__state} -> {__instance.hp}) par : {(coupable != null ? coupable.name : "inconnu")}");

                if (coupable != null)
                    Duplication.Demander(coupable);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Erreur dans GetHurt : {e}");
            }
        }
    }

    // Étiquette posée sur chaque clone, pour les compter et les reconnaître.
    public class MarqueClone : MonoBehaviour { }

    static class Duplication
    {
        public static NewMovement Joueur;

        static readonly List<EnemyIdentifier> demandes = new List<EnemyIdentifier>();
        static readonly Dictionary<int, float> dernierClonage = new Dictionary<int, float>();
        static bool joueurEtaitMort;

        public static void Demander(EnemyIdentifier ennemi)
        {
            if (!demandes.Contains(ennemi))
                demandes.Add(ennemi);
        }

        // Appelée à chaque image par Plugin.Update.
        public static void Traiter()
        {
            bool joueurMort = Joueur != null && Traverse.Create(Joueur).Field("dead").GetValue() is bool mort && mort;
            if (joueurMort && !joueurEtaitMort && Plugin.SupprimerClonesALaMort.Value)
                SupprimerTousLesClones();
            joueurEtaitMort = joueurMort;

            if (demandes.Count == 0)
                return;
            EnemyIdentifier[] aTraiter = demandes.ToArray();
            demandes.Clear();
            if (joueurMort)
                return;

            foreach (EnemyIdentifier ennemi in aTraiter)
            {
                try
                {
                    Dupliquer(ennemi);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Clonage raté : {e}");
                }
            }
        }

        static void Dupliquer(EnemyIdentifier ennemi)
        {
            if (ennemi == null || ennemi.dead)
                return;
            if (!Plugin.ClonesSeDupliquent.Value && ennemi.GetComponent<MarqueClone>() != null)
                return;
            if (Plugin.ExclureBoss.Value && EstUnBoss(ennemi))
                return;

            int id = ennemi.GetInstanceID();
            if (dernierClonage.TryGetValue(id, out float quand) && Time.time - quand < Plugin.DelaiParEnnemi.Value)
                return;
            dernierClonage[id] = Time.time;

            for (int i = 0; i < Plugin.CopiesParCoup.Value; i++)
            {
                if (ClonesVivants() >= Plugin.MaxClones.Value)
                {
                    if (Plugin.AfficherLogs.Value)
                        Plugin.Log.LogInfo($"Limite de {Plugin.MaxClones.Value} clones atteinte");
                    return;
                }
                Cloner(ennemi);
            }
        }

        static void Cloner(EnemyIdentifier ennemi)
        {
            GameObject original = ennemi.gameObject;

            // Le clone est placé à la racine de la scène (pas dans la "vague" de l'arène) :
            // le tuer ne compte donc pas pour ouvrir les portes, il faut toujours tuer les vrais.
            GameObject copie = UnityEngine.Object.Instantiate(original, PositionLibre(original), original.transform.rotation);
            copie.name = original.name;
            if (copie.GetComponent<MarqueClone>() == null)
                copie.AddComponent<MarqueClone>();

            // La mort d'un clone ne doit pas déclencher les événements du niveau liés à l'original.
            EnemyIdentifier eidCopie = copie.GetComponent<EnemyIdentifier>();
            if (eidCopie != null)
                OublierEvenementsDuNiveau(eidCopie);

            if (Plugin.AfficherLogs.Value)
                Plugin.Log.LogInfo($"Clone créé : {copie.name} ({ClonesVivants()} clones vivants)");
        }

        static void OublierEvenementsDuNiveau(EnemyIdentifier eid)
        {
            foreach (FieldInfo champ in AccessTools.GetDeclaredFields(typeof(EnemyIdentifier)))
            {
                if (champ.IsStatic)
                    continue;
                string type = champ.FieldType.Name;
                if (champ.Name == "onDeath" && champ.FieldType.GetConstructor(Type.EmptyTypes) != null)
                    champ.SetValue(eid, Activator.CreateInstance(champ.FieldType)); // événement vide
                else if (type == "ActivateNextWave" || type == "ActivateArena")
                    champ.SetValue(eid, null);
            }
        }

        // Une position à côté de l'original, sans traverser un mur ni sortir du sol.
        static Vector3 PositionLibre(GameObject original)
        {
            Vector3 depart = original.transform.position;
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.5f, 3f);
            Vector3 cible = depart + new Vector3(direction.x, 0f, direction.y);

            // Ennemis qui marchent : on cherche un point du "NavMesh" (le sol où ils peuvent marcher).
            if (original.GetComponentInChildren<NavMeshAgent>() != null)
                return NavMesh.SamplePosition(cible, out NavMeshHit point, 4f, NavMesh.AllAreas) ? point.position : depart;

            // Ennemis volants : on vérifie qu'il n'y a pas de mur entre l'original et le clone.
            int murs = LayerMask.GetMask("Environment", "Outdoors");
            return murs != 0 && Physics.Linecast(depart, cible, murs) ? depart : cible;
        }

        static bool EstUnBoss(EnemyIdentifier ennemi)
        {
            if (Traverse.Create(ennemi).Field("isBoss").GetValue() is bool boss && boss)
                return true;
            Type barreDeBoss = AccessTools.TypeByName("BossHealthBar");
            return barreDeBoss != null && ennemi.GetComponentsInChildren(barreDeBoss, true).Length > 0;
        }

        static int ClonesVivants()
        {
            int n = 0;
            foreach (MarqueClone clone in UnityEngine.Object.FindObjectsOfType<MarqueClone>())
            {
                EnemyIdentifier eid = clone.GetComponent<EnemyIdentifier>();
                if (eid != null && !eid.dead)
                    n++;
            }
            return n;
        }

        static void SupprimerTousLesClones()
        {
            foreach (MarqueClone clone in UnityEngine.Object.FindObjectsOfType<MarqueClone>())
                UnityEngine.Object.Destroy(clone.gameObject);
            dernierClonage.Clear();
        }
    }
}
