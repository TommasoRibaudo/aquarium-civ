using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>"Show me"'s echo staged from data (#76; chronicle-and-history §2.5): <see cref="EchoScenes"/> says who, where
    /// and the beats, and this stages it with the relic moments' staging (#92: cast groups, the walking pace, the walk back
    /// into the day). Additive: the older <see cref="Echo"/> stays for the debug panel until the view moves over.</summary>
    public sealed partial class LifePlanner
    {
        /// <summary>
        /// Stage <paramref name="echo"/> now: the lead (and the other one) interrupt their day for its 3–5 s, a few people near
        /// enough join, then everyone walks back into their day. The place is the echo's (<see cref="EchoWhere"/>); when the
        /// lead can't walk there in half a beat at the walking pace, it's played where the lead stands (§2.5: "else the
        /// person's current spot"). One echo at a time: a new one ends the last. Null when it has no echo or nobody can play
        /// it (fly-to only). The returned info's <see cref="RelicMomentInfo.spot"/> is where it is played.
        /// </summary>
        public RelicMomentInfo StageEcho(EchoScene echo, ViewTime at)
        {
            if (echo == null || !echo.Plays) return null;
            Sync();
            EnsureDay(at.day);
            EndEchoes();
            var lead = Alive(echo.leadId);
            if (lead == null || lead.stage == LifeStage.Baby) return null;
            var def = echo.def;
            float end = EndAfter(at, def.seconds);
            var spot = EchoSpot(echo, lead, at);
            var st = new Staging { relicId = echo.relicId ?? "", def = def, glass = GlassPlace, spot = spot, start = at.hour, end = end };
            var used = new HashSet<int>();

            var leadBeats = new List<RelicBeat>(def.For(RelicCast.Owner));
            if (leadBeats.Count == 0) return null;
            var part = Part(st, lead, leadBeats, at, false);
            if (part == null)
            {
                // Too far to walk in time: the echo comes to the lead.
                st.spot = StopPlace(lead, at.hour);
                part = Part(st, lead, leadBeats, at, false);
            }
            if (part == null) return null;
            Assign(st, lead, RelicCast.Owner, part, used);

            var other = Alive(echo.otherId);
            if (other != null && other != lead && other.stage != LifeStage.Baby)
            {
                var beats = new List<RelicBeat>(def.For(RelicCast.Second));
                var p2 = beats.Count > 0 ? Part(st, other, beats, at, false) : null;
                if (p2 != null) Assign(st, other, RelicCast.Second, p2, used); // too far away: they don't join
            }
            CastCrowd(st, RelicCast.Kids, at, used, def.maxKids, q => q.stage == LifeStage.Kid);
            CastCrowd(st, RelicCast.Grown, at, used, def.maxGrown, q => q.Grown);

            live.Add(new Scene
            {
                kind = echo.kind == SceneKind.None ? SceneKind.Chat : echo.kind, day = at.day, start = at.hour, end = end,
                a = lead.key, b = other?.key ?? -1, chronicleId = echo.entry?.id ?? -1, echo = true, place = st.spot,
                aPartner = -1, aFriend = -1, aParent = -1, staging = st,
            });
            echoLines.Add(new EchoLineRec { day = at.day, start = st.T(Math.Max(0f, Math.Min(1f, echo.lineAt))), end = end, text = echo.line, speakerId = echo.lineSpeakerId, st = st, relicId = st.relicId });
            LiveChanged();
            return st.Info(at.day);
        }

        /// <summary>The echoes playing at <paramref name="t"/> (the view's camera follow, the debug readout).</summary>
        public int ActiveEchoes(ViewTime t, List<RelicMomentInfo> into)
        {
            into.Clear();
            foreach (var sc in live)
                if (sc.echo && sc.staging?.def != null && sc.day == t.day && t.hour >= sc.start && t.hour < sc.end)
                    into.Add(sc.staging.Info(sc.day));
            return into.Count;
        }

        /// <summary>The line said now in a playing echo (a bubble over the speaker): the echo's line, said by its speaker when
        /// they are in the cast (a crowd line: by the lead); not said when its speaker didn't come.</summary>
        public int EchoLinesAt(ViewTime t, List<RelicLine> into)
        {
            into.Clear();
            foreach (var l in echoLines)
            {
                if (string.IsNullOrEmpty(l.text) || l.day != t.day || t.hour < l.start || t.hour >= l.end) continue;
                if (!IsLive(l.st)) continue;
                if (l.speakerId >= 0 && !l.st.roles.ContainsKey(l.speakerId)) continue; // their words, not someone else's
                int key = l.speakerId >= 0 ? l.speakerId : l.st.order.Count > 0 ? l.st.order[0] : 0;
                if (key == 0) continue;
                byKey.TryGetValue(key, out var sp);
                into.Add(new RelicLine { relicId = l.relicId, lineNo = 0, text = l.text, speaker = sp?.who ?? PersonRef.None, start = l.start, end = l.end });
            }
            return into.Count;
        }

        /// <summary>End the echoes (the card stack closed, a new "Show me").</summary>
        public void EndEchoes()
        {
            int n = live.RemoveAll(sc => sc.echo && sc.staging?.def != null);
            echoLines.Clear();
            if (n > 0) LiveChanged();
        }

        struct EchoLineRec
        {
            public long day;
            public float start, end;
            public string text, relicId;
            public int speakerId;
            public Staging st;
        }

        readonly List<EchoLineRec> echoLines = new List<EchoLineRec>();

        bool IsLive(Staging st)
        {
            foreach (var sc in live) if (sc.staging == st) return true;
            return false;
        }

        Place EchoSpot(EchoScene echo, Person lead, ViewTime at)
        {
            Place p;
            switch (echo.where)
            {
                case EchoWhere.Building:
                    var b = echo.buildingId >= 0 && buildingIds.Contains(echo.buildingId) ? FindBuilding(echo.buildingId) : null;
                    p = b != null ? PlaceOf(b) : Place.None;
                    break;
                case EchoWhere.Relic: p = RelicPlace(echo.relicId); break;
                case EchoWhere.Home: p = lead.home; break;
                case EchoWhere.Seat: p = SeatOfHolder(lead.key); break;
                case EchoWhere.Bench: p = wellB != null ? PlaceOf(wellB) : gather; break;
                case EchoWhere.Fire: p = fire; break;
                case EchoWhere.Gather: p = gather; break;
                case EchoWhere.Glass: p = GlassPlace; break;
                case EchoWhere.Remembering: p = remembering; break;
                case EchoWhere.Shaping: p = Place.Land(PlaceKind.Nudge, echo.nudgeId); break;
                default: p = Place.None; break;
            }
            return !p.IsNone && IsValidPlace(p) ? p : StopPlace(lead, at.hour); // LeadHere, or a place no longer in the box
        }
    }
}
