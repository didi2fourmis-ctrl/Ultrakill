using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros
{
    // Les armes de l'Écho : des tirs instantanés rejoués exactement là où tu as visé.
    internal static class EchoGunfire
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[48];
        private static readonly HashSet<EnemyIdentifier> Struck = new HashSet<EnemyIdentifier>();
        private static readonly Comparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        public static void Fire(ShotEvent shot)
        {
            float dmg = Plugin.EchoDamage.Value;
            switch (shot.Kind)
            {
                case ShotKind.Revolver:
                    Hitscan(shot.Origin, shot.Dir, 1f * dmg, false, Fx.Cyan, 0.12f);
                    break;
                case ShotKind.RevolverCharged:
                    Hitscan(shot.Origin, shot.Dir, 2f * dmg, true, Fx.Cyan, 0.35f);
                    break;
                case ShotKind.Shotgun:
                    for (int i = 0; i < 8; i++)
                    {
                        var spread = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-6f, 6f), 0f);
                        Hitscan(shot.Origin, spread * shot.Dir, 0.35f * dmg, false, Fx.Cyan, 0.05f);
                    }
                    break;
                case ShotKind.Nail:
                    Hitscan(shot.Origin, shot.Dir, 0.3f * dmg, false, Fx.Cyan, 0.04f);
                    break;
                case ShotKind.Rail:
                    Hitscan(shot.Origin, shot.Dir, 6f * dmg, true, Fx.Magenta, 0.6f);
                    break;
                case ShotKind.Rocket:
                    var end = Hitscan(shot.Origin, shot.Dir, 1f * dmg, false, Fx.Gold, 0.2f);
                    Blast(end, 4f, 2f * dmg, Fx.Gold);
                    break;
            }
        }

        // Retourne le point d'impact final.
        public static Vector3 Hitscan(Vector3 origin, Vector3 dir, float damage, bool pierce, Color color, float width)
        {
            const float range = 400f;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, range, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(Hits, 0, n, ByDistance);

            Vector3 end = origin + dir * range;
            Struck.Clear();
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col == null || IsPlayer(col)) continue;

                var eid = FindEnemy(col);
                if (eid != null)
                {
                    if (eid.dead || !Struck.Add(eid)) continue;
                    Causality.EchoStrike(eid, col.gameObject, dir * 2500f, Hits[i].point, damage);
                    if (!pierce) { end = Hits[i].point; break; }
                    continue;
                }

                if (col.isTrigger) continue;
                end = Hits[i].point;
                break;
            }

            Fx.Beam(origin + dir * 0.8f, end, color, width);
            return end;
        }

        public static void Blast(Vector3 at, float radius, float damage, Color color)
        {
            Fx.Ring(at, color, radius * 2f);
            Struck.Clear();
            foreach (var col in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var eid = FindEnemy(col);
                if (eid == null || eid.dead || !Struck.Add(eid)) continue;
                var push = (col.transform.position - at).normalized * 5000f + Vector3.up * 2000f;
                Causality.EchoStrike(eid, col.gameObject, push, col.ClosestPoint(at), damage);
            }
        }

        private static EnemyIdentifier FindEnemy(Collider col)
        {
            var eidid = col.GetComponent<EnemyIdentifierIdentifier>();
            if (eidid != null && eidid.eid != null) return eidid.eid;
            return col.GetComponentInParent<EnemyIdentifier>();
        }

        private static bool IsPlayer(Collider col)
        {
            var nm = MonoSingleton<NewMovement>.Instance;
            return nm != null && col.transform.IsChildOf(nm.transform);
        }
    }
}
