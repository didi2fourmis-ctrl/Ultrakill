using System;
using System.Collections.Generic;

namespace Ouroboros
{
    // Ce que le mod sait de lui-même, affiché dans le panneau (F9).
    // Permet de comprendre pourquoi aucun Écho n'apparaît sans ouvrir la console.
    internal static class Diag
    {
        public static string Status = "en attente d'un niveau";
        public static bool Player, Camera, DamageHooked;
        public static int RankRaw = -1;
        public static int Cap;
        public static float History;
        public static int WeaponsHooked;
        public static int ShotsRecorded;
        public static int EchoesSpawned;
        public static string Shader = "pas encore chargé";
        public static string LastError;

        private static readonly HashSet<string> Logged = new HashSet<string>();

        public static void Error(Exception e)
        {
            LastError = e.GetType().Name + " : " + e.Message;
            if (Logged.Add(LastError)) Plugin.Log.LogError(e);
        }
    }
}
