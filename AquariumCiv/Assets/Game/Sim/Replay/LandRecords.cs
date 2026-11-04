using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    // #287 phase 1: the history keeps the living land. Each year start the cover grid (EcoState.cover, one byte a cell), the pond
    // level, the Keeper's tree stage and the rabbit count are recorded, so the time-lapse can show the box barren, then green.
    // Observes only (like every history record): no dice, nothing in the economy reads it. Off with the ecology: no records.

    /// <summary>One year's land as saved. The cover is run-length coded and base64 (a JSON byte array costs 4 chars a byte):
    /// pairs of (value, run 1..255). A keyframe holds the cover itself; a delta holds (cover - previous year's cover) mod 256, which
    /// is mostly zeros, so it is a handful of runs. A keyframe comes every <see cref="SimConfig.historyKeyframeYears"/> years.</summary>
    [Serializable]
    public class LandStep
    {
        public int year;
        public bool keyframe;
        public string cover = "";
        public float pondLevel = -1000f;   // EcoState.pondLevel (-1000 = no pond yet)
        public float table;                // the water table
        public bool treePlanted;
        public int treeStage;              // 0 seed .. 4 grown, meaningful when treePlanted
        public int rabbits;
    }

    /// <summary>The land at a year as the view asks for it (<see cref="BoxAtYear.land"/>).</summary>
    public class LandAt
    {
        public const int NoPond = -1000;
        public byte[] cover;               // EcoCover per site-grid cell
        public float pondLevel = NoPond, table;
        public int treeStage, rabbits;
        public bool treePlanted;

        public bool HasPond => pondLevel > NoPond + 1f;

        /// <summary>Share of cells with growth on them (grass up to reeds), 0..1.</summary>
        public float GreenShare
        {
            get
            {
                if (cover == null || cover.Length == 0) return 0f;
                int n = 0;
                foreach (var c in cover) if (c >= (byte)EcoCover.Grass && c <= (byte)EcoCover.Reeds) n++;
                return n / (float)cover.Length;
            }
        }

        public int Count(EcoCover c) { int n = 0; foreach (var x in cover) if (x == (byte)c) n++; return n; }
    }

    /// <summary>The land between two years, for the scrub: the view draws the earlier year and the later one and fades by
    /// <see cref="t"/>, or reads per-cell values. Pure.</summary>
    public class LandBlend
    {
        public LandAt A, B;
        public float t;                     // 0 = A, 1 = B

        /// <summary>The cover to draw at this cell: the earlier year's until half way, then the later year's.</summary>
        public EcoCover CoverAt(int cell) => (EcoCover)(t < 0.5f ? A : B).cover[cell];
        /// <summary>True when the cell's cover differs between the two years (the view fades just these).</summary>
        public bool Changes(int cell) => A.cover[cell] != B.cover[cell];
        /// <summary>The cell's cover in the earlier and the later year and how far along: lerp a tint or a growth scale from these.</summary>
        public void Cell(int cell, out EcoCover from, out EcoCover to, out float blend) { from = (EcoCover)A.cover[cell]; to = (EcoCover)B.cover[cell]; blend = t; }
        public float GreenShare => A.GreenShare + (B.GreenShare - A.GreenShare) * t;
        /// <summary>Pond level, eased between the years; a pond that exists in only one of them appears or goes at half way.</summary>
        public float PondLevel
        {
            get
            {
                if (A.HasPond && B.HasPond) return A.pondLevel + (B.pondLevel - A.pondLevel) * t;
                if (B.HasPond) return t < 0.5f ? LandAt.NoPond : B.pondLevel;
                if (A.HasPond) return t < 0.5f ? A.pondLevel : LandAt.NoPond;
                return LandAt.NoPond;
            }
        }
        public float Rabbits => A.rabbits + (B.rabbits - A.rabbits) * t;
        public int TreeStage => (t < 0.5f ? A : B).treeStage;
        public bool TreePlanted => (t < 0.5f ? A : B).treePlanted;
        public int CellCount => A.cover.Length;
    }

    public partial class SimCore
    {
        byte[] lastLandCover;   // the cover as of the last recorded land year (rebuilt from the records)

        // ---------- recording ----------
        /// <summary>Once a year, at the year's first month, with the ecology on: records the land. Called from HistoryStep.</summary>
        void HistoryLand(HistoryState h)
        {
            var e = State.eco;
            if (e == null || !e.on || e.cover == null) return;
            int year = (int)(State.month / SimConfig.MonthsPerYear);
            if (h.lastLandYear >= year) return;
            var last = lastLandCover ?? (h.land.Count == 0 ? null : ReplayLand(h, h.lastLandYear, e.cover.Length));
            bool key = last == null || last.Length != e.cover.Length || year % Math.Max(1, Config.historyKeyframeYears) == 0;
            var full = Rle(e.cover);
            var data = full;
            if (!key)
            {
                var diff = new byte[e.cover.Length];
                for (int i = 0; i < diff.Length; i++) diff[i] = (byte)(e.cover[i] - last[i]);
                var d = Rle(diff);
                if (d.Length < full.Length) data = d; else key = true; // whichever is smaller; a keyframe also restarts the chain
            }
            h.land.Add(new LandStep
            {
                year = year, keyframe = key, cover = Convert.ToBase64String(data),
                pondLevel = e.pondLevel, table = e.table, treePlanted = e.treeCell >= 0, treeStage = e.treeStage, rabbits = (int)Math.Round(e.rabbits),
            });
            h.lastLandYear = year;
            lastLandCover = (byte[])e.cover.Clone();
        }

        static byte[] Rle(byte[] a)
        {
            var o = new List<byte>(64);
            int i = 0;
            while (i < a.Length)
            {
                byte v = a[i]; int n = 1;
                while (i + n < a.Length && a[i + n] == v && n < 255) n++;
                o.Add(v); o.Add((byte)n);
                i += n;
            }
            return o.ToArray();
        }

        static void Unrle(byte[] rle, byte[] into, bool add)
        {
            int p = 0;
            for (int i = 0; i + 1 < rle.Length; i += 2)
                for (int n = 0; n < rle[i + 1] && p < into.Length; n++, p++)
                    into[p] = add ? (byte)(into[p] + rle[i]) : rle[i];
        }

        /// <summary>The cover at the start of <paramref name="year"/>: the keyframe at or before it, plus the deltas up to it.</summary>
        static byte[] ReplayLand(HistoryState h, int year, int cells)
        {
            var a = new byte[cells];
            foreach (var s in h.land)
            {
                if (s.year > year) break;
                Unrle(Convert.FromBase64String(s.cover), a, !s.keyframe);
            }
            return a;
        }

        // ---------- reading ----------
        /// <summary>The land at the start of a year, null when none was recorded (ecology off, or a box saved before land records).
        /// A year between two records takes the earlier one.</summary>
        LandAt LandAtYear(HistoryState h, int year)
        {
            if (h.land.Count == 0 || h.land[0].year > year) return null;
            int cells = State.eco?.cover?.Length ?? 0;
            if (cells == 0) return null;
            LandStep rec = null;
            foreach (var s in h.land) { if (s.year > year) break; rec = s; }
            return new LandAt
            {
                cover = ReplayLand(h, rec.year, cells), pondLevel = rec.pondLevel, table = rec.table, treeStage = rec.treeStage,
                treePlanted = rec.treePlanted, rabbits = rec.rabbits,
            };
        }

        /// <summary>The land as it stands now (for <see cref="BoxNow"/>): the live state. Null with the ecology off or no records.</summary>
        LandAt LandNow()
        {
            var e = State.eco;
            if (e == null || !e.on || e.cover == null || Hist.land.Count == 0) return null;
            return new LandAt
            {
                cover = (byte[])e.cover.Clone(), pondLevel = e.pondLevel, table = e.table, treeStage = e.treeStage,
                treePlanted = e.treeCell >= 0, rabbits = (int)Math.Round(e.rabbits),
            };
        }

        /// <summary>Characters the land records take in the save (the JSON payload, roughly): for sizing.</summary>
        public int LandRecordBytes()
        {
            int n = 0;
            foreach (var s in Hist.land) n += s.cover.Length + 100; // the cover plus the other fields' JSON
            return n;
        }
    }
}
