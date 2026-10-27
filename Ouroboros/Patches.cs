using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ouroboros
{
    internal static class Patches
    {
        public static void Apply(Harmony harmony)
        {
            // Chaque arme est patchée séparément : si une mise à jour renomme une méthode,
            // seule cette arme cesse d'être enregistrée, le reste du mod continue.
            Shot(harmony, "Revolver", "Shoot", nameof(RevolverShot));
            Shot(harmony, "Shotgun", "Shoot", nameof(ShotgunShot));
            Shot(harmony, "Nailgun", "Shoot", nameof(NailShot));
            Shot(harmony, "Railcannon", "Shoot", nameof(RailShot));
            Shot(harmony, "RocketLauncher", "Shoot", nameof(RocketShot));

            try
            {
                var target = AccessTools.Method(typeof(EnemyIdentifier), "DeliverDamage");
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patches), nameof(DeliverDamagePrefix)));
                Diag.DamageHooked = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("BOUCLE CAUSALE désactivée (DeliverDamage introuvable) : " + e.Message);
            }
        }

        private static void Shot(Harmony harmony, string typeName, string method, string postfix)
        {
            try
            {
                var type = AccessTools.TypeByName(typeName);
                var target = type == null ? null : AccessTools.Method(type, method);
                if (target == null)
                {
                    Plugin.Log.LogWarning(typeName + "." + method + " introuvable, les Échos n'imiteront pas cette arme.");
                    return;
                }
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(Patches), postfix));
                Diag.WeaponsHooked++;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Patch " + typeName + " échoué : " + e.Message);
            }
        }

        private static void RevolverShot(object[] __args)
        {
            // Revolver.Shoot(int shotType) : 2 = tir chargé (piercer).
            bool charged = __args != null && __args.Length > 0 && __args[0] is int type && type == 2;
            ChronoRecorder.RecordShot(charged ? ShotKind.RevolverCharged : ShotKind.Revolver);
        }

        private static void ShotgunShot() => ChronoRecorder.RecordShot(ShotKind.Shotgun);
        private static void NailShot() => ChronoRecorder.RecordShot(ShotKind.Nail);
        private static void RailShot() => ChronoRecorder.RecordShot(ShotKind.Rail);
        private static void RocketShot() => ChronoRecorder.RecordShot(ShotKind.Rocket);

        private static void DeliverDamagePrefix(EnemyIdentifier __instance, GameObject target, ref float multiplier)
        {
            if (!Plugin.Enabled.Value || __instance == null || __instance.dead) return;
            Causality.OnAnyDamage(__instance, target, ref multiplier);
        }
    }
}
