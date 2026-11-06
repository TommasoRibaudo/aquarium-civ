// User 2026-11-06 "Hugs are fine. Add them." (animation-specs B7.d, C3.a; decisions.md). Where a hug means something, and how few: the
// rules and the book the view's HugDirector keeps. Pure (System + Life + MomentRules): Tools/SimTests compiles it.
using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>Why two people hug. The wedding's hug is the staged moment's own (MomentBeats), never booked here.</summary>
    public enum HugReason { Wedding, Reunion, Reconcile, ParentKid, Cheat }

    /// <summary>Every number of the hugs in one place [est] until the user signs the feel off.</summary>
    public static class HugRules
    {
        /// <summary>Hugs a day in the whole box, by population row (tribe 6, camp 10, village 22, town 30; <see cref="MomentRules.ByPopulation"/>):
        /// few, so they stay special, and a bigger camp has a few more [est].</summary>
        static readonly double[] PerDayAt = { 2, 3, 4, 6 };

        public static int PerDay(int population) => (int)Math.Round(MomentRules.ByPopulation(population, PerDayAt));

        /// <summary>No two hugs in the box within this many real seconds (two at once read as a routine, not a moment) [est].</summary>
        public const float GapSeconds = 25f;

        /// <summary>How near the two must already be (centre to centre, box units): the one at home walks over to the hunter; a reconcile and
        /// a parent with a kid are already side by side [est]. <see cref="Reach"/> by reason.</summary>
        public const float ReunionReach = 1.4f, ReconcileReach = 1.0f, ParentKidReach = 0.9f;

        public static float Reach(HugReason r) => r switch
        {
            HugReason.Reunion => ReunionReach,
            HugReason.Reconcile => ReconcileReach,
            HugReason.ParentKid => ParentKidReach,
            _ => 3f,
        };

        /// <summary>A parent and their kid hug at one of the day's two soft edges (dawn as the kid wakes, the evening on the green) on about
        /// this share of days, each part on its own hash [est].</summary>
        public const float ParentKidShare = 0.35f;
        /// <summary>The windows (view hours): the morning's first hour and the evening on the green.</summary>
        public const float DawnFrom = 6.6f, DawnUntil = 8f, EveningFrom = 18.2f, EveningUntil = 19.5f;

        /// <summary>Is <paramref name="hour"/> in a parent-and-kid window, and which (0 dawn, 1 evening).</summary>
        public static bool ParentKidHours(float hour, out int part)
        {
            part = hour >= DawnFrom && hour < DawnUntil ? 0 : hour >= EveningFrom && hour < EveningUntil ? 1 : -1;
            return part >= 0;
        }

        /// <summary>Does this parent hug this kid in that part of that day, and from which hour on (a hash: not all at the window's start)?</summary>
        public static bool ParentKidToday(int parentKey, int kidKey, long day, int part, out float fromHour)
        {
            uint h = PlayGroups.Hash(parentKey * 7919 + kidKey, day, 3630 + part);
            float from = part == 0 ? DawnFrom : EveningFrom, until = part == 0 ? DawnUntil : EveningUntil;
            fromHour = from + (until - from) * 0.6f * ((h >> 8 & 0xffff) / 65536f);
            return (h >> 24) / 256f < ParentKidShare;
        }

        public static long PairKey(int a, int b) => a < b ? ((long)a << 32) ^ (uint)b : ((long)b << 32) ^ (uint)a;
    }

    /// <summary>
    /// The day's hugs: each pair at most once a view day, at most <see cref="HugRules.PerDay"/> in the box, none within
    /// <see cref="HugRules.GapSeconds"/> of the last. The cheat skips the book; the wedding never asks it.
    /// </summary>
    public sealed class HugBook
    {
        long day = long.MinValue;
        int count;
        float last = float.NegativeInfinity;
        readonly HashSet<long> pairs = new HashSet<long>();

        public int Today => count;
        public float LastAt => last;

        void Turn(long d)
        {
            if (d == day) return;
            day = d; count = 0; pairs.Clear();
        }

        /// <summary>May <paramref name="a"/> and <paramref name="b"/> hug now (view day <paramref name="d"/>, real clock <paramref name="now"/>)?
        /// <paramref name="why"/>: the reason it can't ("" when it can).</summary>
        public bool Can(long d, int a, int b, float now, int population, HugReason reason, out string why)
        {
            why = "";
            if (reason is HugReason.Cheat or HugReason.Wedding) return true;
            Turn(d);
            if (a == b) { why = "one person"; return false; }
            if (pairs.Contains(HugRules.PairKey(a, b))) { why = "they hugged today"; return false; }
            if (count >= HugRules.PerDay(population)) { why = $"{count} hugs today (enough)"; return false; }
            if (now - last < HugRules.GapSeconds) { why = "a hug just now"; return false; }
            return true;
        }

        public void Took(long d, int a, int b, float now, HugReason reason)
        {
            if (reason is HugReason.Cheat or HugReason.Wedding) return;
            Turn(d);
            pairs.Add(HugRules.PairKey(a, b));
            count++;
            last = now;
        }

        public void Reset() { day = long.MinValue; count = 0; pairs.Clear(); last = float.NegativeInfinity; }
    }
}
