using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The choice scene in the box (#224; <see cref="ChoiceScenes"/>): the people the dilemma is about walk to their spots at the walking
    /// pace and wait there looking up at the glass, then react to the Keeper's touch. Staging only, on the relic moments' machinery
    /// (<see cref="Staging"/>): no sim dice, no sim writes. One at a time; a new one ends the last.
    /// </summary>
    public sealed partial class LifePlanner
    {
        ChoiceStage choice;

        /// <summary>The choice scene staged now (waiting or reacting), or null.</summary>
        public ChoiceStage Choice => choice;

        /// <summary>
        /// Stage <paramref name="v"/>'s choice scene now for <paramref name="seconds"/> real seconds: the people walk over at the walking
        /// pace and wait looking up at the glass. (<paramref name="rx"/>, <paramref name="rz"/>) is the screen's right on the ground and
        /// (<paramref name="tx"/>, <paramref name="tz"/>) the way to the viewer, so option 0 stands on the Keeper's left where it can;
        /// <paramref name="hx"/>, <paramref name="hz"/> are the box's half extents. Null when it can't be staged (no positions, a
        /// candidate gone, nowhere to stand): the card's buttons still answer.
        /// </summary>
        public ChoiceStage StageChoice(DilemmaView v, ViewTime at, float rx, float rz, float tx, float tz, float seconds = ChoiceScenes.WaitSeconds,
            float hx = 3f, float hz = 2.2f)
        {
            if (v?.dilemma == null || v.options == null || v.options.Length < 2) return null;
            Sync();
            EnsureDay(at.day);
            CutChoice(at); // a scene staged again goes on from where the last left them
            choice = null;
            var r = ChoiceScenes.Unit(rx, rz);
            var t = ChoiceScenes.Unit(tx, tz);
            var (ra, rb) = ChoiceScenes.Rules(v.kind);
            var st = new ChoiceStage { dilemmaId = v.dilemma.id, kind = v.kind, defId = v.dilemma.defId ?? "", day = at.day, start = at.hour, end = EndAfter(at, seconds), rx = r.x, rz = r.z, tx = t.x, tz = t.z };
            st.sides[0].rule = ra; st.sides[1].rule = rb;
            var used = new HashSet<int>();

            // The asker: the one who put the question, else the leader, else the eldest grown-up (never one of the two).
            var asker = Alive(v.dilemma.askerId);
            if (asker == null || asker.key == v.options[0].personId || asker.key == v.options[1].personId)
                asker = leader != null && leader.key != v.options[0].personId && leader.key != v.options[1].personId ? leader : null;
            if (asker == null && v.kind != DilemmaKind.Who)
            {
                var e = EldestGrown();
                if (e != null && e.key != v.options[0].personId && e.key != v.options[1].personId) asker = e;
            }
            if (asker != null && asker.stage == LifeStage.Baby) asker = null;

            switch (v.kind)
            {
                case DilemmaKind.Who:
                {
                    var anchor = era == Era.Camp && IsValidPlace(fire) ? fire : SeatOrGather();
                    if (!Pos(anchor, out var c)) return null;
                    var (left, right) = ChoiceScenes.Flank(c, r, t, ChoiceScenes.WhoSpread, ChoiceScenes.WhoForward);
                    for (int i = 0; i < 2; i++)
                    {
                        var p = Alive(v.options[i].personId);
                        if (p == null || p.stage == LifeStage.Baby) return null;
                        var pt = i == 0 ? left : right;
                        Side(st, i, anchor, pt);
                        st.sides[i].people.Add((p.who, pt.x, pt.z));
                        used.Add(p.key);
                    }
                    if (asker != null && !used.Contains(asker.key)) Behind(st, asker, c, t, ChoiceScenes.AskerBack, used);
                    break;
                }
                case DilemmaKind.WhichWay:
                {
                    var pa = BuildingPlace(v.options[0].buildingId);
                    var pb = BuildingPlace(v.options[1].buildingId);
                    (float x, float z) ca = default, cb = default;
                    bool shared = pa.IsNone || pb.IsNone || pa == pb || !Pos(pa, out ca) || !Pos(pb, out cb);
                    if (shared)
                    {
                        // A site is missing (or both are one): two stakes either side of whichever stands, else the gathering place.
                        var anchor = !pa.IsNone ? pa : !pb.IsNone ? pb : gather;
                        if (!Pos(anchor, out var c)) return null;
                        var (left, right) = ChoiceScenes.Flank(c, r, t, ChoiceScenes.SharedStakeSpread, ChoiceScenes.StakeForward);
                        Side(st, 0, anchor, left);
                        Side(st, 1, anchor, right);
                    }
                    else
                    {
                        Side(st, 0, pa, ChoiceScenes.Along(ca, t, ChoiceScenes.StakeForward));
                        Side(st, 1, pb, ChoiceScenes.Along(cb, t, ChoiceScenes.StakeForward));
                    }
                    // A worker beside each stake, on its outer side (the two never stand between the stakes).
                    float side0 = (st.sides[0].x - st.sides[1].x) * r.x + (st.sides[0].z - st.sides[1].z) * r.z;
                    for (int i = 0; i < 2; i++)
                    {
                        float outward = (i == 0 ? 1f : -1f) * (side0 >= 0f ? 1f : -1f);
                        var spot = ChoiceScenes.Along((st.sides[i].x, st.sides[i].z), r, outward * ChoiceScenes.StakeBeside);
                        var w = NearestWorker(st.sides[i].place, at, used);
                        if (w == null) continue;
                        st.sides[i].people.Add((w.who, spot.x, spot.z));
                        used.Add(w.key);
                    }
                    break;
                }
                case DilemmaKind.Tradition:
                {
                    var anchor = churchB != null && IsValidPlace(PlaceOf(churchB)) ? PlaceOf(churchB) : gather;
                    if (!Pos(anchor, out var c)) return null;
                    var (left, right) = ChoiceScenes.Flank(c, r, t, ChoiceScenes.OfferingSpread, ChoiceScenes.OfferingForward);
                    Side(st, 0, anchor, left);
                    Side(st, 1, anchor, right);
                    if (asker != null) Behind(st, asker, c, t, -ChoiceScenes.AskerOffering, used);
                    break;
                }
                default: // strangers: the chief at the fire, the travellers at the box's edge
                {
                    var anchor = IsValidPlace(fire) ? fire : gather;
                    if (!Pos(anchor, out var c)) return null;
                    var edge = ChoiceScenes.EdgePoint(c, r, hx, hz, out _);
                    // #231: when one option keeps them apart in the box, they wait towards the home they would make (far woods, far shore).
                    if (v.def?.options != null)
                        for (int i = 0; i < v.def.options.Length; i++)
                        {
                            var o = v.def.options[i];
                            if (o == null || o.strangers <= 0 || string.IsNullOrEmpty(o.apart)) continue;
                            var spot = sim.ApartSpotPreview(o.apart);
                            var home = ApartLife.FreeSpot(sim.Settlement?.Map, spot.x, spot.z, out _); // where ApartGroupsView will draw their camp
                            var toward = ChoiceScenes.HomeWard(c, home, hx, hz, out bool homeOk);
                            if (!homeOk) break;
                            edge = toward;
                            st.hasHome = true; st.homeX = home.x; st.homeZ = home.z; st.homeOption = i;
                            break;
                        }
                    var chiefAt = ChoiceScenes.Along(c, t, ChoiceScenes.ChiefForward);
                    Side(st, 0, anchor, chiefAt);
                    if (asker != null) { st.sides[0].people.Add((asker.who, chiefAt.x, chiefAt.z)); used.Add(asker.key); }
                    Side(st, 1, Place.None, edge);
                    int n = 2;
                    if (v.def != null && v.def.options != null)
                        foreach (var o in v.def.options) if (o != null && o.strangers > 0) n = o.strangers;
                    st.sides[1].travellers.AddRange(ChoiceScenes.TravellerSpots(edge, r, t, n));
                    break;
                }
            }

            // The people's waits: each walks to their side's place at the walking pace, then looks up and fidgets.
            var stg = new Staging { def = null, glass = GlassPlace, spot = st.sides[0].place.IsNone ? st.sides[1].place : st.sides[0].place, start = at.hour, end = st.end };
            int salt = 0;
            foreach (var s in st.sides)
                foreach (var (who, _, _) in s.people)
                {
                    if (!byKey.TryGetValue(who.Key, out var p)) continue;
                    var segs = WaitSegs(p, s.place, at, seconds, ChoiceScenes.Fidget(s.rule.stand, false), salt++);
                    Assign(stg, p, RelicCast.Owner, segs, new HashSet<int>());
                }
            if (!st.asker.IsNone && byKey.TryGetValue(st.asker.Key, out var ap))
                Assign(stg, ap, RelicCast.Second, WaitSegs(ap, AskerPlace(st), at, seconds, ChoiceScenes.Fidget(ChoiceStand.Asker, true), salt++), new HashSet<int>());
            if (stg.segs.Count == 0 && st.sides[0].Prop == ChoiceProp.None && st.sides[1].travellers.Count == 0) return null;
            if (stg.segs.Count > 0) AddLive(SceneKind.Choice, at, st.end, stg);
            choice = st;
            return st;
        }

        /// <summary>
        /// The Keeper touched option <paramref name="chosen"/> (the sim has the answer already): the chosen side cheers, the other side
        /// shrugs or sulks by trait, the asker claps, and grown-ups and kids near enough come over and cheer (for strangers sent on, those
        /// by the fire wave them off). Lasts <see cref="ChoiceScenes.ReactSeconds"/>; then everyone walks back into their day. False when no
        /// choice scene is staged.
        /// </summary>
        public bool ReactChoice(int chosen, ViewTime at)
        {
            var st = choice;
            if (st == null || chosen < 0 || chosen > 1) return false;
            Sync();
            EnsureDay(at.day);
            CutChoice(at); // the wait ends here: the reaction goes on from where it left them, nobody walks off and back
            st.chosen = chosen;
            float end = EndAfter(at, ChoiceScenes.ReactSeconds);
            st.day = at.day; st.start = at.hour; st.end = end;
            var win = st.sides[chosen];
            var spot = !win.place.IsNone ? win.place : st.sides[1 - chosen].place;
            var stg = new Staging { def = ChoiceScenes.GatherDef(), glass = GlassPlace, spot = spot, start = at.hour, end = end };
            var used = new HashSet<int>();
            foreach (var s in st.sides)
                foreach (var (who, _, _) in s.people)
                {
                    if (!byKey.TryGetValue(who.Key, out var p) || used.Contains(p.key)) continue;
                    var beats = ChoiceScenes.ReactBeats(s.option == chosen, false, p.n?.trait);
                    Assign(stg, p, RelicCast.Owner, BeatSegs(stg, beats, s.place.IsNone ? StopPlace(p, at.hour) : s.place, false), used);
                }
            if (!st.asker.IsNone && byKey.TryGetValue(st.asker.Key, out var ap) && !used.Contains(ap.key))
                Assign(stg, ap, RelicCast.Second, BeatSegs(stg, ChoiceScenes.ReactBeats(false, true, ap.n?.trait), AskerPlace(st), false), used);
            if (!win.place.IsNone)
            {
                CastCrowd(stg, RelicCast.Grown, at, used, stg.def.maxGrown, q => q.Grown && FreeToReact(q, at));
                CastCrowd(stg, RelicCast.Kids, at, used, stg.def.maxKids, q => q.stage == LifeStage.Kid && FreeToReact(q, at));
            }
            else CastCrowd(stg, RelicCast.Onlookers, at, used, stg.def.maxQueue, q => q.stage != LifeStage.Baby && FreeToReact(q, at)); // strangers sent on: waved off
            if (stg.segs.Count > 0) AddLive(SceneKind.Choice, at, end, stg);
            return true;
        }

        /// <summary>The choice scene ends now (reacted, or left alone for its drift): it is cut at <paramref name="at"/>, so everyone
        /// walks back into their day from where they stand, at the walking pace (the way back of any interruption).</summary>
        public void EndChoice(ViewTime at)
        {
            Sync();
            CutChoice(at);
            choice = null;
        }

        /// <summary>Drop every choice scene at once (the box changed, a test): the people's plans go back to their day as it is.</summary>
        public void EndChoice()
        {
            choice = null;
            if (live.RemoveAll(sc => sc.kind == SceneKind.Choice) > 0) LiveChanged();
        }

        /// <summary>Cut today's choice scenes at <paramref name="at"/>: nothing in them after it.</summary>
        void CutChoice(ViewTime at)
        {
            bool changed = false;
            float h = at.hour;
            for (int i = 0; i < live.Count; i++)
            {
                var sc = live[i];
                if (sc.kind != SceneKind.Choice || sc.staging == null || sc.day != at.day || sc.end <= h) continue;
                foreach (var list in sc.staging.segs.Values)
                    for (int j = list.Count - 1; j >= 0; j--)
                    {
                        var s = list[j];
                        if (s.start >= h) { list.RemoveAt(j); continue; }
                        if (s.end > h) { s.end = h; list[j] = s; }
                    }
                sc.end = h;
                sc.staging.end = h;
                live[i] = sc;
                changed = true;
            }
            if (changed) LiveChanged();
        }

        /// <summary>Is the staged choice scene still running at <paramref name="now"/>?</summary>
        public bool ChoiceRunning(ViewTime now) => choice != null && now.day == choice.day && now.hour < choice.end;

        // ---------------------------------------------------------------- helpers

        bool Pos(Place p, out (float x, float z) at)
        {
            at = default;
            if (p.IsNone || !IsValidPlace(p) || !positions.TryGetPosition(p, out float x, out float z)) return false;
            at = (x, z);
            return true;
        }

        Place SeatOrGather()
        {
            if (leader != null) { var s = SeatOfHolder(leader.key); if (!s.IsNone && IsValidPlace(s)) return s; }
            return gather;
        }

        Place BuildingPlace(int id)
        {
            if (id < 0 || !buildingIds.Contains(id)) return Place.None;
            var p = PlaceOf(FindBuilding(id));
            return IsValidPlace(p) ? p : Place.None;
        }

        static void Side(ChoiceStage st, int i, Place place, (float x, float z) at)
        {
            st.sides[i].place = place;
            st.sides[i].x = at.x; st.sides[i].z = at.z;
        }

        void Behind(ChoiceStage st, Person asker, (float x, float z) c, (float x, float z) t, float back, HashSet<int> used)
        {
            var at = ChoiceScenes.Along(c, t, -back);
            st.asker = asker.who; st.askerX = at.x; st.askerZ = at.z;
            used.Add(asker.key);
        }

        Place AskerPlace(ChoiceStage st)
        {
            foreach (var s in st.sides) if (!s.place.IsNone) return s.place;
            return gather;
        }

        /// <summary>The nearest grown-up who is up and not in a moment, at most <see cref="ChoiceScenes.MaxFetchSeconds"/>' walk away.</summary>
        Person NearestWorker(Place to, ViewTime at, HashSet<int> used)
        {
            Person best = null;
            float bl = float.MaxValue;
            foreach (var q in people)
            {
                if (!q.Grown || used.Contains(q.key) || !FreeToReact(q, at)) continue;
                float l = WalkLen(StopPlace(q, at.hour), to);
                if (WalkSecs(l) > ChoiceScenes.MaxFetchSeconds) continue;
                if (l < bl || (l == bl && best != null && q.key < best.key)) { bl = l; best = q; }
            }
            return best;
        }

        /// <summary>One person's wait as segments: come over (twice the walk, so the walk always fits), then the look-up loop.</summary>
        List<Seg> WaitSegs(Person p, Place place, ViewTime at, float seconds, ActivityKind fidget, int salt)
        {
            float walk = WalkSecs(WalkLen(StopPlace(p, at.hour), place));
            float come = walk <= 0.01f ? 0f : Math.Min(seconds * 0.6f, walk * 2f + 1f);
            var list = new List<Seg>();
            foreach (var (from, to, kind) in ChoiceScenes.WaitBeats(seconds, come, fidget, salt + p.key))
            {
                float h0 = EndAfter(at, from), h1 = EndAfter(at, to);
                if (h1 - h0 <= 1e-5f) continue;
                list.Add(StagedSeg(kind, h0, h1, place, Prop.None, true, false));
            }
            return list;
        }

        /// <summary>Beats (shares of the staging) as segments at one place.</summary>
        List<Seg> BeatSegs(Staging st, List<(float from, float to, ActivityKind kind)> beats, Place place, bool faces)
        {
            var list = new List<Seg>();
            foreach (var (from, to, kind) in beats)
            {
                float h0 = st.T(from), h1 = st.T(to);
                if (h1 - h0 <= 1e-5f) continue;
                list.Add(StagedSeg(kind, h0, h1, place, Prop.None, faces || kind == ActivityKind.Cheer, false));
            }
            return list;
        }
    }
}
