// B76 (animation-specs "The teller's manner at the fire", W717): the Keeper's pick tells tonight's story at the fire, slow or short.
// Where everyone sits (the teller behind the fire, facing the camera and lit by it; the kids in front at 0.4 u, the grown-ups behind at
// 0.55 u, a half ring on the camera's side), the teller's DollStyle.Tell and the listeners' beats (View/Beats/TellerBeats.cs), and the
// teller's lines, each once at its beat. The fire keeper keeps poking the fire (B9.c): not moved. The unpicked evening teller (the
// eldest's winter story) keeps DollStyle.Speech (approvals row 229).
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using UnityEngine;

namespace AquariumCiv.View
{
    public sealed partial class CampLife
    {
        /// <summary>B76: the teller's distance behind the fire (the festival lead's spot), the kids' and the grown-ups' rings (u).</summary>
        public const float TellerBehind = 0.36f, KidRing = 0.4f, GrownRing = 0.55f, KidsNearest = 0.33f; // [est] the spec's 0.22 stood the teller in the flames (the fire is drawn ~0.3 u across): clip 717-teller-slow try 1

        int tellKey = -1, tellManner;
        float tellS = -1f, tellLastS = -1f, tellScale = 1f;
        long tellDay = long.MinValue;
        Vector2 tellFire, tellToCam;
        float tellPileYaw = float.NaN;
        readonly Dictionary<int, (Vector2 at, Vector2 face, bool kid, int order, bool first, bool hugs)> tellSeats = new();
        readonly HashSet<int> tellSaid = new();

        /// <summary>Tests and the panel: the teller now (-1 none), the seconds into the telling, the seats.</summary>
        public int TellerNow => tellKey;
        public float TellerSeconds => tellS;
        public int TellerListeners => tellSeats.Count;

        void StepTeller(IReadOnlyList<PersonActivity> acts)
        {
            TellerBeats.SpeaksItsOwnLines = false;
            tellKey = -1;
            if (sim == null || planner == null || meals == null || !meals.HasFire || !sim.StoryTellerOn(now.day, out int id, out int manner)) return;
            bool slow = manner != SmallChoices.MannerShort;
            float end = slow ? LifePlanner.TellSlowUntil : LifePlanner.TellShortUntil + LifePlanner.LaughHours;
            if (now.hour < LifePlanner.TellFrom || now.hour >= end) return;
            var tellerAct = default(PersonActivity); bool found = false;
            foreach (var a in acts) if (a.who.Key == id) { tellerAct = a; found = true; break; }
            if (!found || tellerAct.kind != ActivityKind.StoryTell) return;
            tellKey = id; tellManner = manner;
            // the beats' clock: real seconds since the telling began, scaled to the spec's 23 s (slow) / 9 s + 2.5 s (short)
            // from when the teller really starts (a chat before it can hold them past TellFrom: the short telling's clip, try 2)
            // (in the camp the supper's sit can hold the teller till ~18.6: the whole telling, the laugh too, is fitted into what is left)
            float until = slow ? LifePlanner.TellSlowUntil : LifePlanner.TellShortUntil + LifePlanner.LaughHours;
            float from = Mathf.Clamp(tellerAct.start, LifePlanner.TellFrom, until - 0.2f);
            float real = LifeClock.RealSecondsBetween(from, now.hour);
            float total = LifeClock.RealSecondsBetween(from, until);
            tellScale = (slow ? TellerBeats.SlowLength : TellerBeats.ShortLength + TellerBeats.LaughLength) / Mathf.Max(1f, total);
            tellS = real * tellScale;
            TellerBeats.SpeaksItsOwnLines = true;
            if (tellDay != now.day) { tellDay = now.day; tellSaid.Clear(); tellLastS = -1f; }
            tellFire = meals.Fire; tellToCam = ToCam(tellFire);
            // the woodpile's bearing from the way the teller faces (the camera)
            tellPileYaw = float.NaN;
            if (!slow && sim.WoodpileStands && PropOf(ConsequenceKind.WoodpileRoof, out var pile))
            {
                var tp = tellFire - tellToCam * TellerBehind;
                tellPileYaw = Vector2.SignedAngle(pile.at - tp, tellToCam);
            }
            // the seats: listeners at the fire, kids in front (0.4 u), grown-ups behind (0.55 u), a half ring on the camera's side
            tellSeats.Clear();
            var kids = new List<int>(); var grown = new List<int>();
            foreach (var a in acts)
            {
                if (a.who.Key == id || a.kind is not (ActivityKind.ListenStory or ActivityKind.LaughPoint) || a.place.kind != PlaceKind.Fire && a.place.kind != PlaceKind.Green) continue;
                if (byKey.ContainsKey(a.who.Key)) continue; // a camp use has them
                (planner.StageOf(a.who) == LifeStage.Kid ? kids : grown).Add(a.who.Key);
            }
            kids.Sort(); grown.Sort();
            var tellerAt = tellFire - tellToCam * TellerBehind;
            void Ring(List<int> keys, float r, bool kid)
            {
                int n = keys.Count;
                for (int i = 0; i < n; i++)
                {
                    float deg = n == 1 ? 0f : -70f + 140f * i / (n - 1);
                    var at = tellFire + CampScenes5.Rot(tellToCam, deg) * r;
                    int order = Mathf.Abs(i - (n - 1) / 2);
                    tellSeats[keys[i]] = (at, tellerAt, kid, order + (kid ? 0 : 10), kid && i == n / 2, !kid && i == n / 2);
                }
            }
            Ring(kids, KidRing, true); Ring(grown, GrownRing, false);
            // the lines, each once, at its beat
            var lines = PeopleVoice.TellerLines(manner, !float.IsNaN(tellPileYaw));
            int li = TellerBeats.LineBetween(slow, !float.IsNaN(tellPileYaw), tellLastS, tellS);
            if (li >= 0 && li < lines.Count && tellSaid.Add(li) && PeopleVoice.Allowed(lines[li], sim))
                ShowMeDirector.Instance?.Say(lines[li], PersonRef.Notable(id), StagedMoments.BeatLineSeconds + 0.6f, LineKind.Echo);
            tellLastS = tellS;
        }

        /// <summary>B76: the teller's spot behind the fire, a listener's in the half ring (a kid scooted in as the beats say).</summary>
        public bool TellerSpot(in PersonActivity a, out float x, out float z, out Vector2? face)
        {
            x = z = 0f; face = null;
            if (tellKey < 0 || a.moving || a.enRoute) return false;
            if (a.who.Key == tellKey)
            {
                var at = tellFire - tellToCam * TellerBehind;
                x = at.x; z = at.y; face = at + tellToCam;
                return true;
            }
            if (!tellSeats.TryGetValue(a.who.Key, out var seat)) return false;
            var p = seat.at;
            if (seat.kid && tellManner != SmallChoices.MannerShort)
            {
                var pose = DollPose.Rest;
                float scoot = TellerBeats.SlowListener(ref pose, tellS, true, seat.first, false);
                var toFire = (tellFire - p).normalized;
                float most = Vector2.Distance(p, tellFire) - KidsNearest; // kids safe: never nearer the fire than 0.33 u
                p += toFire * Mathf.Clamp(scoot, -0.05f, most);
            }
            x = p.x; z = p.y; face = seat.face;
            return true;
        }

        public bool TellerLook(in PersonActivity a, bool walking, ref DollStyle style)
        {
            if (tellKey < 0 || walking) return false;
            if (a.who.Key == tellKey) { style = DollStyle.Tell; return true; }
            return false;
        }

        public void TellerPose(in PersonActivity a, bool walking, ref DollPose pose)
        {
            if (tellKey < 0 || walking) return;
            bool slow = tellManner != SmallChoices.MannerShort;
            if (a.who.Key == tellKey) { if (slow) TellerBeats.Slow(ref pose, tellS); else TellerBeats.Short(ref pose, tellS, tellPileYaw); return; }
            if (!tellSeats.TryGetValue(a.who.Key, out var seat)) return;
            if (slow) TellerBeats.SlowListener(ref pose, tellS, seat.kid, seat.first, seat.hugs);
            else TellerBeats.ShortListener(ref pose, tellS, seat.kid, seat.order, tellPileYaw);
        }

        /// <summary>The panel: the telling now.</summary>
        public string TellerStatus() => tellKey < 0 ? "no picked telling now" : $"{sim?.FindNotable(tellKey)?.name} tells ({(tellManner == SmallChoices.MannerShort ? "short" : "slow")}), {tellS:0.0} s in, {tellSeats.Count} listening";
    }
}
