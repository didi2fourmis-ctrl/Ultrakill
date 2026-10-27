using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros
{
    internal enum ShotKind { Revolver, RevolverCharged, Shotgun, Nail, Rail, Rocket }

    internal struct Frame
    {
        public float T;
        public Vector3 Body;
        public Vector3 Eye;
        public Quaternion Look;
    }

    internal struct ShotEvent
    {
        public float T;
        public Vector3 Origin;
        public Vector3 Dir;
        public ShotKind Kind;
    }

    // Le "ruban" du temps : position, regard et tirs du joueur, échantillonnés en continu.
    internal static class ChronoRecorder
    {
        private const float SampleStep = 1f / 60f;

        private static readonly List<Frame> Frames = new List<Frame>(2048);
        private static readonly List<ShotEvent> Shots = new List<ShotEvent>(512);
        private static float lastSample = float.NegativeInfinity;
        private static float lastNail = float.NegativeInfinity;

        public static float Oldest => Frames.Count > 0 ? Frames[0].T : float.PositiveInfinity;

        public static void Clear()
        {
            Frames.Clear();
            Shots.Clear();
            lastSample = float.NegativeInfinity;
        }

        public static void Tick(NewMovement nm, Transform cam, float keepSeconds)
        {
            float now = Time.time;
            if (now - lastSample >= SampleStep)
            {
                lastSample = now;
                Frames.Add(new Frame { T = now, Body = nm.transform.position, Eye = cam.position, Look = cam.rotation });
            }

            float cutoff = now - keepSeconds;
            int f = 0;
            while (f < Frames.Count && Frames[f].T < cutoff) f++;
            if (f > 0) Frames.RemoveRange(0, f);
            int s = 0;
            while (s < Shots.Count && Shots[s].T < cutoff) s++;
            if (s > 0) Shots.RemoveRange(0, s);
        }

        public static void RecordShot(ShotKind kind)
        {
            var cam = EchoDirector.CameraTransform;
            if (cam == null) return;

            // Le nailgun tire en rafale : on condense pour que l'Écho ne crache pas 20 faisceaux/s.
            if (kind == ShotKind.Nail)
            {
                if (Time.time - lastNail < 0.08f) return;
                lastNail = Time.time;
            }

            Diag.ShotsRecorded++;
            Shots.Add(new ShotEvent { T = Time.time, Origin = cam.position, Dir = cam.forward, Kind = kind });
        }

        public static bool TrySample(float t, out Frame frame)
        {
            frame = default;
            int n = Frames.Count;
            if (n == 0 || t < Frames[0].T || t > Frames[n - 1].T) return false;

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Frames[mid].T <= t) lo = mid; else hi = mid;
            }

            var a = Frames[lo];
            var b = Frames[hi];
            // Un trou dans le ruban (téléport, checkpoint) : on ne rejoue pas l'impossible.
            if (b.T - a.T > 0.25f) return false;

            float k = b.T > a.T ? (t - a.T) / (b.T - a.T) : 0f;
            frame = new Frame
            {
                T = t,
                Body = Vector3.Lerp(a.Body, b.Body, k),
                Eye = Vector3.Lerp(a.Eye, b.Eye, k),
                Look = Quaternion.Slerp(a.Look, b.Look, k)
            };
            return true;
        }

        public static void ShotsBetween(float from, float to, List<ShotEvent> into)
        {
            into.Clear();
            for (int i = 0; i < Shots.Count; i++)
            {
                float t = Shots[i].T;
                if (t > from && t <= to) into.Add(Shots[i]);
            }
        }
    }
}
