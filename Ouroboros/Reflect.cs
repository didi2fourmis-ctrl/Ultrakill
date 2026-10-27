using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ouroboros
{
    // Les signatures d'ULTRAKILL changent d'une mise à jour à l'autre. On appelle donc les
    // méthodes "fragiles" par nom, en remplissant les paramètres optionnels avec leur
    // valeur par défaut : le mod survit aux ajouts de paramètres.
    internal static class Reflect
    {
        private static readonly Dictionary<string, MethodInfo> Cache = new Dictionary<string, MethodInfo>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        public static object Call(object target, string method, params object[] leading)
        {
            if (target == null) return null;
            var type = target.GetType();
            string key = type.FullName + "::" + method + "/" + leading.Length;

            if (!Cache.TryGetValue(key, out var mi))
            {
                mi = Find(type, method, leading);
                Cache[key] = mi;
            }
            if (mi == null)
            {
                if (Warned.Add(key)) Plugin.Log.LogWarning("Méthode introuvable : " + key);
                return null;
            }

            var ps = mi.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < leading.Length) { args[i] = leading[i]; continue; }
                object def = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                if (def == null || def is DBNull || def == Type.Missing)
                    def = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
                args[i] = def;
            }

            try { return mi.Invoke(target, args); }
            catch (Exception e)
            {
                if (Warned.Add(key + "!")) Plugin.Log.LogWarning("Échec de " + key + " : " + e.InnerException?.Message);
                return null;
            }
        }

        private static MethodInfo Find(Type type, string method, object[] leading)
        {
            MethodInfo best = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != method) continue;
                    var ps = m.GetParameters();
                    if (ps.Length < leading.Length || !Matches(ps, leading)) continue;
                    if (best == null || ps.Length < best.GetParameters().Length) best = m;
                }
                if (best != null) return best;
            }
            return null;
        }

        private static bool Matches(ParameterInfo[] ps, object[] leading)
        {
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < leading.Length)
                {
                    if (leading[i] != null && !ps[i].ParameterType.IsInstanceOfType(leading[i])) return false;
                }
                else if (!ps[i].IsOptional) return false;
            }
            return true;
        }
    }
}
