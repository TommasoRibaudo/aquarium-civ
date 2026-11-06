using System.Linq;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #371 (visit-loop.md 2.5): a choice's callback as a live moment. Registered with <see cref="HeldMomentStage"/> for
    /// <see cref="MomentKind.Callback"/>: the person the line is about walks to the thing the choice left in the box (the feather, the
    /// roof, the stone), points at it, holds a hand up to the glass; neighbours watch and clap; "Because the Keeper chose ..." is said as
    /// the chatter (the echo's line). The staging itself is the box's echo machinery (<c>EchoScenes</c> callback case, <c>PlaceKind.Prop</c>);
    /// this adds the thing's bob when the moment starts. A callback whose people are gone, or the view can't stage, is a card as before.
    /// </summary>
    public static class CallbackMoment
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => HeldMomentStage.Register(MomentKind.Callback, Stage);

        /// <summary>Stages <paramref name="m"/> now, or null (it becomes a card).</summary>
        public static MomentStaging Stage(HeldMoment m, SimCore sim)
        {
            var st = HeldMomentStage.Placeholder(m, sim);
            if (st == null) return null;
            var entry = sim.State.chronicle.FirstOrDefault(e => e.id == m.entryId);
            var cp = entry == null ? null : sim.ConsequencePropOfEntry(entry);
            if (cp != null) ConsequencePropsView.Pulse(cp.id);
            return st;
        }
    }
}
