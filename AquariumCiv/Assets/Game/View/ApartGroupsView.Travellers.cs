using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #578 (the travellers), the camp's two seen moments. The wood: the cutter chops at the camp's cutting spot, lifts a log, walks it to the
    /// woodpile and sets it down (<see cref="ApartLife.CarryAt"/>; the sim counts the load, <see cref="SimCore.ApartBringsWood"/>); the pile of logs
    /// beside the woodpile has a log for every load, and the cycle runs a third quicker while the wood lean lasts (<see cref="SimCore.ApartWoodRate"/>).
    /// The ending: when the group's join starts (<see cref="ApartGroup.joinState"/> 2) the camp shrinks away (tents, woodpile, fire out), the
    /// people take up their bundles and walk to the green, and on arriving <see cref="SimCore.FinishApartJoin"/> makes them the settlement's: the
    /// group is gone from the sim, so <c>LateUpdate</c> destroys the camp (<c>KillCamp</c>: its root, its meshes, its dolls and their meshes).
    /// What the view owns for a camp: the "ApartCamp{id}" root (fire, lean-to, woodpile, ChopLogs, PileLogs, Glow, boat, tower), the dolls
    /// ("Apart{id}_{name}") under the "ApartGroups" root, and the meshes listed in the camp (all named "Apart..."). Nothing else.
    /// </summary>
    public partial class ApartGroupsView
    {
        /// <summary>Real seconds the camp takes to pack up before they walk.</summary>
        const float PackSeconds = 2.5f;
        /// <summary>The walk to the green, as a multiple of the camp walk.</summary>
        const float JoinSpeed = 1.8f;
        /// <summary>Box units: they have arrived this close to their place on the green.</summary>
        const float JoinArrive = 0.12f;

        static Vector2 Stash(Camp c) => c.work + c.side * 0.14f;

        Vector2 CarryGoal(Camp c, Member m) => m.carry == CarryPhase.Chop || m.carry == CarryPhase.Lift ? c.chopAt : Stash(c);

        /// <summary>Advances the cutter's carry and swaps what they hold; sets the log down (the sim counts it) once they stand at the pile.</summary>
        CarryPhase Carry(Camp c, Member m, float dt)
        {
            float period = ApartLife.CarryPeriod(c.rate);
            m.carryClock += dt;
            var ph = ApartLife.CarryAt(Mathf.Repeat(m.carryClock / period, 1f), out _);
            if (ph == CarryPhase.Chop) m.dropped = false;
            var want = ph == CarryPhase.Lift || ph == CarryPhase.Walk ? PropKind.Log : ph == CarryPhase.Drop ? PropKind.None : PropKind.Axe;
            if (want != m.prop) { m.prop = want; m.view.SetProp(want, want == PropKind.Log ? PropSlot.Shoulder : PropSlot.Hand); }
            if (ph == CarryPhase.Drop && !m.dropped && (m.pos - Stash(c)).sqrMagnitude < 0.12f * 0.12f)
            {
                m.dropped = true;
                c.carryNow = false;
                Sim?.ApartBringsWood(c.groupId);
            }
            m.carry = ph;
            return ph;
        }

        /// <summary>The pile beside the woodpile shows a log for every load the sim has counted (redrawn only when the count changes).</summary>
        void UpdatePile(Camp c, ApartGroup g)
        {
            if (c.root == null || c.kind == ApartKind.ShoreHut) return;
            int want = ApartLife.PileLogs(g.woodLoads);
            if (want == c.pileShown) return;
            if (c.pile != null)
            {
                var old = c.pile.GetComponent<MeshFilter>()?.sharedMesh;
                c.meshes.Remove(old);
                Kill(old);
                Kill(c.pile);
                c.pile = null;
            }
            c.pileShown = want;
            if (want <= 0) return;
            float yawBack = Mathf.Atan2(c.back.x, c.back.y) * Mathf.Rad2Deg;
            c.pile = Part(c, "PileLogs", ApartProps.Logs(want, want, 0.34f), c.work, yawBack + 90f);
        }

        /// <summary>The camp packs up (its pieces shrink away, the fire's glow goes out), then its people, bundles in hand, walk to the green; when
        /// all have arrived the sim finishes the join and the group, and with it the camp, is gone.</summary>
        void DriveJoin(SimCore sim, Camp c, ApartGroup g, float k, float dt)
        {
            c.joinClock += dt;
            c.carryNow = false;
            float pack = Mathf.Clamp01(c.joinClock / PackSeconds);
            float shrink = Mathf.Max(0.001f, 1f - Mathf.SmoothStep(0f, 1f, pack));
            foreach (Transform t in c.root)
            {
                if (t.name == "Glow") { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                t.localScale = Vector3.one * shrink;
            }
            var map = sim.Settlement?.Map;
            var green = map != null ? new Vector2(map.greenX, map.greenZ) : Vector2.zero;
            int alive = 0, arrived = 0, rank = 0;
            foreach (var n in g.members) if (n.alive) alive++;
            for (int i = 0; i < g.members.Count; i++)
            {
                var person = g.members[i];
                c.members.TryGetValue(person.id, out var m);
                if (!person.alive)
                {
                    if (m != null) { KillDoll(m.view); c.members.Remove(person.id); }
                    continue;
                }
                if (m == null) c.members[person.id] = m = MakeMember(sim, c, g, person, i);
                var s = new Script { prop = PropKind.Bundle, speed = JoinSpeed };
                if (pack < 1f) { s.at = m.pos; s.face = green - m.pos; s.style = DollStyle.Stand; s.walk = false; }
                else
                {
                    float a = (rank - (alive - 1) * 0.5f) * 0.9f;
                    s.at = green + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.16f;
                    s.face = s.at - m.pos; s.style = DollStyle.Walk; s.walk = true;
                    if ((m.pos - s.at).sqrMagnitude < JoinArrive * JoinArrive) arrived++;
                }
                StepScripted(c, m, s, k, dt);
                rank++;
            }
            if (alive == 0 || (pack >= 1f && arrived == alive)) sim.FinishApartJoin(g.id);
        }

        // ================================================================== debug

        /// <summary>Debug (#578, the panel's "Travellers bring wood now"): the travellers' camp stands (made if none) and its cutter carries a load to the pile.
        /// Just made, the camp is drawn a frame later: press again.</summary>
        public static string DebugWoodNow()
        {
            var sim = Sim;
            if (sim == null) return "no sim";
            var g = sim.DebugTravellersBringWood();
            var v = Instance;
            if (v == null || !v.camps.TryGetValue(g.id, out var c) || c.root == null) return $"the {g.family} family's camp is being drawn: press again";
            return DebugCarryNow();
        }

        /// <summary>Debug (#578, the panel's "Travellers join now"): the travellers (made if none) pack up and walk in to the green.</summary>
        public static string DebugJoinNow()
        {
            var sim = Sim;
            if (sim == null) return "no sim";
            var g = sim.DebugTravellersJoin();
            return $"the {g.family} family packs up and walks in";
        }

        /// <summary>Debug (#578): the cutter of every woods-kind camp goes out to cut now (whatever the hour) and carries a load to the pile in a few seconds.</summary>
        public static string DebugCarryNow()
        {
            var v = Instance;
            if (v == null) return "no ApartGroupsView";
            int n = 0;
            foreach (var c in v.camps.Values)
            {
                if (c.kind == ApartKind.ShoreHut || c.root == null) continue;
                c.carryNow = true;
                foreach (var m in c.members.Values.Where(x => x.index == 0)) { m.carryClock = ApartLife.CarryPeriod(c.rate) * (ApartLife.LiftAt - 0.05f); m.dropped = false; }
                n++;
            }
            return n == 0 ? "no camp drawn" : $"{n} cutter(s) out with a load";
        }
    }
}
