// User 2026-11-06 "Kid tumble sometimes" (B2.c): the cheat for the rabbit chase's tumble (View/Beats/KidPlay.cs RabbitChaseGame.Tumble).
using AquariumCiv.Life;

namespace AquariumCiv.View
{
    public sealed partial class KidPlayDirector
    {
        /// <summary>"Kids chase rabbits: a kid tumbles": the chase cheat, with a tumble in its first chase (needs rabbits on view).</summary>
        public string ForceTumble()
        {
            string said = Force(PlayGame.ChaseRabbits);
            for (int i = groups.Count - 1; i >= 0; i--)
                if (groups[i].forced && groups[i].game is RabbitChaseGame chase) { chase.TumbleFirst = true; return said + "; a kid trips in the first chase"; }
            return said;
        }

        public static string DebugTumble()
        {
            var pv = PeopleView.Find();
            return pv == null ? "no people view" : pv.KidPlay.ForceTumble();
        }
    }
}
