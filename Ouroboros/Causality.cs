using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros
{
    // BOUCLE CAUSALE : quand toi et ton Écho touchez le même ennemi dans une courte fenêtre,
    // le passé et le présent se superposent et le coup est amplifié.
    internal static class Causality
    {
        private static readonly Dictionary<int, float> PlayerHits = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> EchoHits = new Dictionary<int, float>();
        private static bool echoStriking;

        public static void Clear()
        {
            PlayerHits.Clear();
            EchoHits.Clear();
        }

        // Appelé pour tout dégât reçu par un ennemi (via Harmony).
        public static void OnAnyDamage(EnemyIdentifier eid, GameObject target, ref float multiplier)
        {
            int id = eid.GetInstanceID();
            float now = Time.time;
            var own = echoStriking ? EchoHits : PlayerHits;
            var other = echoStriking ? PlayerHits : EchoHits;

            if (other.TryGetValue(id, out float t) && now - t <= Plugin.CausalityWindow.Value)
            {
                multiplier *= 1f + Plugin.CausalityBonus.Value;
                other.Remove(id);
                own.Remove(id);
                StyleBridge.Add(150, StyleBridge.Loop);
                Fx.Ring(target != null ? target.transform.position : eid.transform.position, Fx.Gold, 4f);
                return;
            }
            own[id] = now;
        }

        public static void EchoStrike(EnemyIdentifier eid, GameObject target, Vector3 force, Vector3 point, float multiplier)
        {
            echoStriking = true;
            try
            {
                Reflect.Call(eid, "DeliverDamage", target, force, point, multiplier, false);
            }
            finally
            {
                echoStriking = false;
            }
            if (eid != null && eid.dead) StyleBridge.Add(80, StyleBridge.EchoKill);
            else StyleBridge.Add(10, StyleBridge.EchoHit);
        }
    }
}
