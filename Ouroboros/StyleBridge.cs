using System.Collections.Generic;
using HarmonyLib;

namespace Ouroboros
{
    internal static class StyleBridge
    {
        public const string EchoHit = "ouroboros.echo";
        public const string EchoKill = "ouroboros.echokill";
        public const string Loop = "ouroboros.loop";
        public const string Paradox = "ouroboros.paradox";
        public const string Debt = "ouroboros.debt";

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { EchoHit, "<color=#7df9ff>ÉCHO</color>" },
            { EchoKill, "<color=#7df9ff>TUÉ PAR LE PASSÉ</color>" },
            { Loop, "<color=#ffd34d>BOUCLE CAUSALE</color>" },
            { Paradox, "<color=#ff4dff>PARADOXE</color>" },
            { Debt, "<color=#ff2a2a>DETTE TEMPORELLE</color>" },
        };

        private static int registeredFor;

        private static StyleHUD Hud
        {
            get
            {
                var hud = MonoSingleton<StyleHUD>.Instance;
                if (hud == null) return null;
                // Le StyleHUD est recréé à chaque niveau : on réenregistre nos noms.
                if (registeredFor != hud.GetInstanceID())
                {
                    registeredFor = hud.GetInstanceID();
                    foreach (var kv in Names) Register(hud, kv.Key, kv.Value);
                }
                return hud;
            }
        }

        private static void Register(StyleHUD hud, string id, string name)
        {
            if (AccessTools.Method(typeof(StyleHUD), "RegisterStyleItem") != null)
            {
                Reflect.Call(hud, "RegisterStyleItem", id, name);
                return;
            }
            var dict = Traverse.Create(hud).Field("idNameDict").GetValue<Dictionary<string, string>>();
            if (dict != null) dict[id] = name;
        }

        public static void Add(int points, string id)
        {
            var hud = Hud;
            if (hud != null) Reflect.Call(hud, "AddPoints", points, id);
        }

        // 0 = D ... 7 = ULTRAKILL, -1 si le rang est illisible.
        public static int Rank
        {
            get
            {
                var hud = Hud;
                return Reflect.Get(hud, "rankIndex") is int rank ? rank : -1;
            }
        }
    }
}
