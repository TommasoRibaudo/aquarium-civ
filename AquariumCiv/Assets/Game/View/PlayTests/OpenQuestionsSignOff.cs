using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The sign-off walk for everything in docs/open-questions.md: the USER runs it in the Editor and gives a verdict on a panel in the Game view,
    /// stop by stop. The stop list is read from the file when the test starts (every ## section, every bullet, in file order), so a new item
    /// joins by itself. Three kinds: LOOK (the "Sign-offs on the phone" bullets; staged in the world with the existing cheats and framed, or, with
    /// no staging, shown as text with "not staged: why"), DECISION (the "Decisions needed", P7, "Still open" and tester-run bullets; the ★
    /// recommendation and an Alternative become buttons, plus Other with a note), and CALLS (one stop per "Calls I made" / "Polish noted" section,
    /// a tick box per bullet; an unticked bullet means "I want a change"). A "Go to..." list jumps between sections and a run offers to continue
    /// where the last one stopped. Every verdict is written to docs/overnight/open-questions-signoff.md at once. Start it from the menu
    /// "Aquarium Civ/Open questions sign-off" or in the Test Runner (PlayMode, AquariumCiv.View.PlayTests, OpenQuestionsSignOff). [Explicit].
    /// Real input: this class is NOT an InputTestFixture (that isolates the input system from the real devices), so the mouse, the keyboard and the
    /// Kit work as in the game. Auto mode (a file Temp/agent/oqsignoff/AUTO, or OQSIGNOFF_AUTO=1) answers every stop, shoots the look stops and
    /// three sample text stops to Temp/agent/oqsignoff/NN_name.png and writes auto-results.md there instead.
    /// </summary>
    [Explicit, Category("OpenQuestionsSignOff")]
    public partial class OpenQuestionsSignOff
    {
        public static bool AutoAnswer;
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        static readonly string ShotDir = "F:/GameDev/AquariumCiv/Temp/agent/oqsignoff/";
        const string LastKey = "OpenQuestionsSignOff.Last";

        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();
        static void Log(string s) => Debug.Log("[oqso] " + s);

        SignOffPanel panel;
        BoxView box;
        PeopleView pv;
        GameHud hud;
        bool auto;
        float repinHour = -1f, repinAt;

        // ---------------------------------------------------------------- opening the box (PlayTestBase.OpenSlice without its input fixture)
        static void SetSize(int w, int h)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("AquariumCiv.EditorTools.AgentGameView");
                if (t == null) continue;
                t.GetMethod("SetSize").Invoke(null, new object[] { w, h });
                return;
            }
        }

        IEnumerator Open(int w, int h, int seed, bool skipFirstRun = true)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            GameUiPanel.Toasts.ClearHolds();
            GameClock.DebugNextSeed = seed;
            CampFrameView.Auto = false;
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
            string savePath = GameClock.DefaultSavePath;
            foreach (var ext in new[] { "", ".bak" })
                if (File.Exists(savePath + ext)) File.Delete(savePath + ext);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            for (int i = 0; i < 90; i++) yield return null;
            if (GameClock.Instance != null) Bootstraps.All();
            EveningTalkView.AutoStart = false;
            yield return null; yield return null;
            if (skipFirstRun && FirstRunView.Instance != null && FirstRunView.Instance.Active) FirstRunView.DebugSkipAll(); // #647's stop keeps a box in its first run
            yield return null; yield return null;
        }

        // ---------------------------------------------------------------- small helpers
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        /// <summary>Auto runs: what a stop keeps on screen until its shot (a camera pose, a follow), run every frame of <see cref="HoldWait"/>.</summary>
        Action hold;

        IEnumerator HoldWait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; try { hold?.Invoke(); } catch (Exception e) { Log("hold: " + e.Message); hold = null; } yield return null; }
        }

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(ShotDir);
            File.WriteAllBytes(ShotDir + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        void Say(string s) { panel.status = s; Log(s); }

        /// <summary>The game's default view; <paramref name="shift"/> looks a little past the box so it sits below the sign-off panel.</summary>
        static void DefaultView(float shift = 1.3f)
        {
            var b = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Rig.CancelFlight(); Rig.target = b.transform.position; Rig.zoom = 1f; Rig.Apply();
            var f = Camera.main.transform.forward; f.y = 0f;
            if (shift > 0f && f.sqrMagnitude > 1e-4f) { Rig.target = b.transform.position + f.normalized * shift; Rig.Apply(); }
        }

        static void Look(Vector3 world, float zoom, float pitch = 52f)
        {
            Rig.CancelFlight(); Rig.pitch = pitch; Rig.zoom = zoom; Rig.target = world; Rig.Apply();
            // the panel covers the top of the screen: look a little past the subject so it sits in the lower middle
            var f = Camera.main.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) { Rig.target = world + f.normalized * (ShiftPerZoom * zoom); Rig.Apply(); }
        }

        const float ShiftPerZoom = 0.9f;

        Vector3 World(float x, float z) => box.transform.TransformPoint(box.land.OnGround(x, z));

        void Hour(float h, bool repin = false)
        {
            // a debug duty (fill basket, fish on the rack) must not outlive an hour jump: its walk phase asks the planner for the same hour again (stack overflow seen in the first auto run)
            if (pv != null && pv.Planner != null) pv.Planner.ClearLive();
            PeopleView.DebugSetHour(h);
            repinHour = repin ? h : -1f; repinAt = Time.realtimeSinceStartup + 5f;
        }

        Transform FirstFeather()
        {
            var props = ConsequencePropsView.Instance;
            if (props == null) return null;
            foreach (var kv in props.Standing)
                if (kv.Value != null && kv.Value.name.StartsWith("Consequence_feather")) return kv.Value;
            return null;
        }

        bool FireAndRack(out Vector3 fire, out Vector3 rack)
        {
            fire = rack = default;
            if (!pv.Planner.FirePoint(out float fx, out float fz)) return false;
            fire = World(fx, fz);
            if (pv.Planner.TryGetFishRack(pv.Now, out var fr)) rack = World(fr.x, fr.z) + Vector3.up * 0.06f;
            else { pv.Planner.HideFrame(out float rx, out float rz, out _, out _); rack = World(rx, rz); }
            return true;
        }

        bool FisherAt(out Vector3 at)
        {
            at = default;
            foreach (var a in pv.Activities)
                if (a.kind == ActivityKind.Fish && !a.moving && pv.TryGetDollPosition(a.who, out var p)) { at = World(p.x, p.y); return true; }
            return false;
        }

        // ---------------------------------------------------------------- the world each stop needs
        IEnumerator EnsurePond()
        {
            if (Sim.EcoPondCells > 0) yield break;
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            Log("pond: " + EcoDebug.Table(0.3f));
            Sim.DebugGrowFarWood();
            yield return Frames(20);
        }

        IEnumerator EnsureFish()
        {
            yield return EnsurePond();
            foreach (var s in new[] { LifeSpecies.Ducks, LifeSpecies.Fish })
                if (!Sim.LifeIsPlaced(s))
                {
                    if (!Sim.JarReady(s)) Sim.DebugJarsReady();
                    int c = Sim.DebugPlaceNearCamp(s);
                    Log($"{s} not placed yet: placed near the camp ({c})");
                }
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            if (!Sim.FishingWater) Say("note: no fishing water yet (pond " + Sim.EcoPondCells + " cells)");
        }

        IEnumerator EnsureSmallPond()
        {
            Log(EcoDebug.PondDries() + " / " + EcoDebug.WaterBreaksNow());
            yield return Frames(20);
        }


        // ---------------------------------------------------------------- the stops
        enum Kind { Look, Decision, Calls }

        sealed class Stop
        {
            public string Section = "", Sub = "", Issue = "", Name = "", Slug = "", Judge = "", NotStaged;
            /// <summary>The Game view size for this stop (playtest 1: most at 1179x2556, a few at 750x1334 or 1206x2622).</summary>
            public int W = 1179, H = 2556;
            /// <summary>"" = the played box; else the key of a box the stop's setup opens itself (it keeps that box while the next stop has the same key).</summary>
            public string Box = "";
            public Kind Kind;
            public Func<IEnumerator> Setup;
            public (string label, Func<IEnumerator> run)[] Extras = new (string, Func<IEnumerator>)[0];
            public string[] Options = new string[0];
            public string[] Bullets = new string[0];
            public string[] BulletTitles = new string[0];
        }

        static Stop S(string issue, string name, string slug, string judge, Func<IEnumerator> setup, params (string, Func<IEnumerator>)[] extras)
            => new Stop { Issue = issue, Name = name, Slug = slug, Judge = judge, Setup = setup, Extras = extras, Kind = Kind.Look };

        /// <summary>A look stop with a light staging; its judge text is the item's own text from open-questions.md.</summary>
        Stop P(string issue, string name, string slug, string say, Func<string> cheat = null, bool pond = false, bool sound = false, bool camp = false)
            => new Stop { Issue = issue, Name = name, Slug = slug, Kind = Kind.Look, Setup = () => PlainSetup(say, cheat, pond, sound, camp) };

        IEnumerator PlainSetup(string say, Func<string> cheat, bool pond, bool sound, bool camp)
        {
            Hour(10f);
            if (sound) GameSettings.Sound = true;
            if (pond) yield return EnsurePond();
            if (cheat != null) Log(cheat());
            if (camp)
            {
                Log("camp props: " + ConsequenceCheats.ShowCampProps());
                yield return Wait(1.5f);
                if (FireAndRack(out var fire, out var rack)) { Look((fire + rack) * 0.5f, 0.35f); yield return Wait(0.6f); }
                else DefaultView();
            }
            else DefaultView();
            Say(say);
            yield return Wait(1.5f);
        }

        /// <summary>What a look item can be staged as, by issue number (the first item to name a number gets it). An empty list: covered by another item's stops.</summary>
        Dictionary<string, Stop[]> Staged() => new Dictionary<string, Stop[]>
        {
            ["#538"] = new[] { S("#538", "camera pan to the walls", "camera",
                "Pan with one finger (the left mouse button) to the left and right wall at close, mid and far zoom (scroll or pinch). The camera should reach both side walls at every zoom, with a soft stop at the wall. You drive: nothing is framed.", SetupCamera) },
            ["#526"] = new[] {
                S("#526", "feather stake, day", "stake_day", "A pale stake in a stone ring with three fanned feathers. Does it read as a feather stake, and does it stand clear of the tents, rack, pot and stash?", () => SetupStake(11.5f)),
                S("#526", "feather stake, night", "stake_night", "The same stake at night. Is it still readable in the dark (and near the fire's light), not a tall dark pole?", () => SetupStake(22.5f)) },
            ["#543"] = new[] {
                S("#542", "placing ducks and fish", "placing", "Jars for ducks and fish are ready, nothing framed. Open the Kit, take the ducks, tap the water (a ghost shows), tap again to place; then do the same with the fish. Does the camera frame the whole pond? Is the ghost easy to see? Does any tap on the water confirm?", SetupPlacing),
                S("#543", "a fisher with the creel", "fisher_creel", "A fisher at the shore with a reed creel on the hip. Is the creel readable, with fish showing in it once something is caught?", SetupFisher),
                S("#543", "rack with fresh fish", "rack_fresh", "Six fresh fish hang on the rack by the fire. Do they read as silver fresh fish, hanging neatly?", SetupRackFresh),
                S("#543", "rack with dry fish", "rack_dry", "The same rack with dried fish. Are they clearly brown against the fresh silver ones?", SetupRackDry),
                S("#543", "stash pile with fish", "pile_fish", "The stash pile with dried fish lying on a dark mat in front of the logs. Do the fish read as fish, and does the pile still read as a stash?", SetupPile),
                S("#543", "the chain, live", "chain", "A full basket: someone walks it to the rack and hangs the fish one by one. Watch the whole walk. Does it read, and is the timing right? Use Set up again to watch it twice.", SetupChain) },
            ["#542"] = new Stop[0],
            ["#539"] = new[] {
                S("#539", "settlers by the woods, pond full", "settlers_woods", "Settlers arrive by the woods with the pond at full size. Their camp should keep clear of where the water is. If the water reaches it, the camp moves within 5 s. Is the camp in a good place?", () => SetupSettlers(false)),
                S("#539", "settlers by the shore, pond full", "settlers_shore", "Settlers arrive by the shore with the pond full. Does their camp keep dry feet, clear of the water?", () => SetupSettlers(true)) },
            ["#540"] = new[] { S("#540", "camp props apart", "camp_props", "Look at the fire with the drying rack, the stew pot, the stash and the tents. Do the props stand apart, none on top of another, and does the camp still read as one place?", SetupCampProps) },
            ["#541"] = new[] { S("#541", "two kids playing together", "kids", "Two kids play as one group, framed close. Do they read as playing together (one game, near each other)? Note any wait with no cue when a kid joins.", SetupKids) },
            ["#495"] = new[] { S("#495", "fisher on the shore", "fisher_shore", "A small pond and a fisher on the shore. Press Pond +1 step to grow the pond and watch: does the fisher keep dry feet and move with the shore?", SetupShoreFisher, ("Pond +1 step", PondStepExtra)) },
            ["#518"] = new[] { S("#518", "seed ghost", "seed_ghost", "A seed is in hand. The first tap on the ground shows a ghost, the second plants. Is the ghost big enough to see? (The seed is a choice in the life jar now: Kit, Life, the tree.) Two-finger pans must never plant.", SetupSeed) },
            ["#516"] = new[] { S("#516", "ghost silhouettes", "ghost_silhouette", "Open the Kit, take the thicket (bushes) and tap the ground. The ghost should be a pale silhouette in the species' colours, with no area ring, and the hint names the group. The Kit button reads Put away while a tool is in hand.", SetupGhosts) },
            ["#524"] = new[] { S("#524", "wet glass edge", "wet_glass", "Mist the box next to the glass (the Kit's mister). The edge darkens and a strip down the soil face. Subtle at the default zoom: accept, or stronger?", SetupWetGlass) },
            ["#525"] = new[] { S("#525", "tree's warm pool at night", "tree_night", "A grown tree at night. There should be a faint warm pool under it, far weaker than the fire's. Accept, or stronger?", SetupTreeNight) },
            // sounds: sound on, default view
            ["#520"] = new[] { P("#520", "barren wind", "wind", "sound is on: listen (the wind loop plays on a mostly bare box)", sound: true) },
            ["#331"] = new[] { P("#331", "sand gusts", "gusts", "sound is on: listen for gusts every 8-25 s (silent below 40 % bare)", sound: true) },
            ["#280"] = new[] { P("#280", "mister and bird sounds", "sound280", "sound is on: mist, then listen to the spray and the birds", sound: true) },
            // batch 2 and the P8 run: light stagings
            ["#332"] = new[] { P("#332", "camp rack", "rack332", "the camp with its rack at a close frame", camp: true) },
            ["#266"] = new[] { P("#266", "misting and puddles", "mist266", "default view: take the mister from the Kit and mist the land") },
            ["#269"] = new[] { P("#269", "pond meets the glass", "pond269", "a pond (cheat) at the default view", pond: true) },
            ["#279"] = new[] { P("#279", "the pond's first water, frogs and ducks", "pond279", "a pond (cheat) at the default view: look for frogs and ducks", pond: true) },
            ["#282"] = new[] { P("#282", "rabbits", "rabbits282", "five rabbits added (cheat)", () => EcoDebug.AddRabbits(5)) },
            ["#283"] = new[] { P("#283", "the hunt", "hunt283", "a hunt started (cheat)", () => EcoDebug.StartHunt()) },
            ["#284"] = new[] { P("#284", "hides and the first tent", "tent284", "the camp with its props", camp: true) },
            ["#288"] = new[] { P("#288", "berries", "berries288", "three bushes added (cheat)", () => EcoDebug.AddBushes(3)) },
            ["#273"] = new[] { P("#273", "camera reach at the walls", "camera273", "default view: pan to the walls at close and far zoom") },
            ["#249"] = new[] { P("#249", "the Kit button", "kit249", "default view: open the Kit") },
            ["#281"] = new[] { P("#281", "fresh green on return", "green281", "default view: the fresh green shows after you come back") },
            // feedback batch 5 (#569-#578): the stops are in OpenQuestionsSignOff.Fb5.cs
            // playtest 1 (#643-#680) and the new start (#656, #677): the stops are in OpenQuestionsSignOff.Pt1.cs
            ["#643"] = Pt1("#643"), ["#644"] = Pt1("#644"), ["#645"] = Pt1("#645"), ["#647"] = Pt1("#647"), ["#648"] = Pt1("#648"),
            ["#649"] = Pt1("#649"), ["#650"] = Pt1("#650"), ["#651"] = Pt1("#651"), ["#652"] = Pt1("#652"), ["#654"] = Pt1("#654"),
            ["#657"] = Pt1("#657"), ["#659"] = Pt1("#659"), ["#669"] = Pt1("#669"), ["#670"] = Pt1("#670"), ["#672"] = Pt1("#672"),
            ["#673"] = Pt1("#673"), ["#674"] = Pt1("#674"), ["#680"] = Pt1("#680"), ["#675"] = Pt1("#675"), ["#676"] = Pt1("#676"),
            ["#665"] = Pt1("#665"), ["#671"] = Pt1("#671"), ["#656"] = Pt1("#656"), ["#677"] = Pt1("#677"), ["#658"] = Pt1("#658"),
            ["#569"] = Fb5("#569"), ["#570"] = Fb5("#570"), ["#571"] = Fb5("#571"), ["#572"] = Fb5("#572"), ["#573"] = Fb5("#573"),
            ["#574"] = Fb5("#574"), ["#575"] = Fb5("#575"), ["#576"] = Fb5("#576"), ["#577"] = Fb5("#577"), ["#578"] = Fb5("#578"),
        };

        // ---------------------------------------------------------------- reading docs/open-questions.md
        sealed class Item { public string Title, Text; }
        sealed class Sub { public string Title = ""; public List<Item> Items = new List<Item>(); }
        sealed class Sec { public string Title = ""; public List<Sub> Subs = new List<Sub>(); }

        static string Clean(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");
            s = s.Replace("`", "");
            return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        }

        static string TitleOf(string raw)
        {
            if (raw.StartsWith("**")) { int e = raw.IndexOf("**", 2); if (e > 2) return Clean(raw.Substring(0, e + 2)).TrimEnd('.', ':', ' '); }
            string c = Clean(raw);
            int colon = c.IndexOf(':');
            if (colon > 0 && colon < 70) return c.Substring(0, colon);
            int dot = c.IndexOf(". ");
            if (dot > 0 && dot < 70) return c.Substring(0, dot);
            return c.Length <= 60 ? c : c.Substring(0, 57) + "...";
        }

        static List<Sec> ParseOpenQuestions(string path)
        {
            var secs = new List<Sec>();
            Sec sec = null; Sub sub = null; Item item = null;
            string pending = null;
            void Flush() { if (item != null) { item.Text = Clean(pending); item.Title = TitleOf(pending); } item = null; pending = null; }
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("## ")) { Flush(); sec = new Sec { Title = Clean(line.Substring(3)) }; secs.Add(sec); sub = null; continue; }
                if (sec == null) continue;
                if (line.StartsWith("### ")) { Flush(); sub = new Sub { Title = Clean(line.Substring(4)) }; sec.Subs.Add(sub); continue; }
                if (line.StartsWith("- ")) { Flush(); if (sub == null) { sub = new Sub(); sec.Subs.Add(sub); } item = new Item(); pending = line.Substring(2); sub.Items.Add(item); continue; }
                if (item != null && line.StartsWith("  ") && line.Trim().Length > 0) { pending += " " + line.Trim(); continue; }
                Flush();
            }
            Flush();
            return secs;
        }

        static string Cut(string o) => o.Length <= 90 ? o : o.Substring(0, 87) + "...";

        static string[] OptionsOf(string text)
        {
            var opts = new List<string>();
            int st = text.IndexOf('★');
            if (st >= 0)
            {
                string rest = text.Substring(st + 1).Trim();
                int end = rest.IndexOf(". ");
                if (end < 0) end = rest.Length;
                int alt = rest.IndexOf("Alternative:");
                if (alt >= 0 && alt < end) end = alt;
                string o = rest.Substring(0, end).Trim().TrimEnd('.');
                if (o.Length > 0) opts.Add("★ " + Cut(o));
            }
            int a = text.IndexOf("Alternative:");
            if (a >= 0)
            {
                string o = text.Substring(a + 12).Trim();
                int end = o.IndexOf(". "); if (end > 0) o = o.Substring(0, end);
                opts.Add(Cut(o));
            }
            return opts.ToArray();
        }

        static string ShortSection(string title)
        {
            int p = title.IndexOf(" (");
            return (p > 0 ? title.Substring(0, p) : title).Replace("Playtest-feedback ", "");
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant()) { if (char.IsLetterOrDigit(c)) sb.Append(c); else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_'); }
            string r = sb.ToString().Trim('_');
            return r.Length > 28 ? r.Substring(0, 28) : r;
        }

        static IEnumerable<string> KeysOf(string title)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(title, @"#\d+")) yield return m.Value;
        }

        List<Stop> BuildStops(string mdPath)
        {
            var secs = ParseOpenQuestions(mdPath);
            var staged = Staged();
            var used = new HashSet<string>();
            var list = new List<Stop>();
            void Stage(Item it, string section, string subTitle, bool addTextIfNone)
            {
                bool any = false;
                foreach (var key in KeysOf(it.Title))
                    if (staged.TryGetValue(key, out var stops) && used.Add(key))
                    {
                        any = true;
                        foreach (var s in stops)
                        {
                            s.Section = section; s.Sub = subTitle;
                            if (string.IsNullOrEmpty(s.Judge)) s.Judge = it.Text;
                            list.Add(s);
                        }
                    }
                if (any || !addTextIfNone) return;
                list.Add(new Stop
                {
                    Section = section, Sub = subTitle, Kind = Kind.Look, Name = it.Title, Slug = Slug(it.Title), Judge = it.Text,
                    Setup = () => PlainSetup("", null, false, false, false),
                    NotStaged = "not staged: no cheat stages this one; judge it in play, or skip",
                });
            }
            foreach (var sec in secs)
            {
                string secShort = ShortSection(sec.Title);
                foreach (var sub in sec.Subs)
                {
                    bool look = sub.Title.Contains("Sign-offs"), calls = sub.Title.Contains("Calls I made") || sub.Title.Contains("Polish noted");
                    string subShort = sub.Title.Length == 0 ? "" : (look ? "Look" : calls ? sub.Title.Replace(" without you", "").Replace(", not fixed", "") : sub.Title);
                    if (look) foreach (var it in sub.Items) Stage(it, secShort, subShort, true);
                    else if (calls)
                    {
                        foreach (var it in sub.Items) Stage(it, secShort, subShort, false); // a call that names a staged issue gets its look first
                        list.Add(new Stop
                        {
                            Section = secShort, Sub = subShort, Kind = Kind.Calls, Name = sub.Title, Slug = Slug(secShort + "_" + subShort),
                            Judge = "Tap a line to untick it if you want it changed (it turns orange, CHANGE).", Bullets = sub.Items.Select(i => i.Text).ToArray(), BulletTitles = sub.Items.Select(i => i.Title).ToArray(),
                            Setup = NoSetup,
                        });
                    }
                    else
                        foreach (var it in sub.Items)
                            list.Add(new Stop
                            {
                                Section = secShort, Sub = subShort, Kind = Kind.Decision, Name = it.Title, Slug = Slug(it.Title), Judge = it.Text,
                                Options = OptionsOf(it.Text), Setup = NoSetup,
                            });
                }
            }
            return list;
        }

        IEnumerator NoSetup() { yield return null; }

        IEnumerator SetupCamera()
        {
            Hour(10f);
            DefaultView(0f);
            Say("Mouse: left drag pans, right or middle drag turns, wheel zooms.");
            yield return Frames(2);
        }

        IEnumerator SetupStake(float hour)
        {
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            yield return Wait(2f);
            Hour(hour, true);
            yield return Wait(2.5f);
            var f = FirstFeather();
            if (f == null) { Say("setup failed: no feather stake stands"); DefaultView(); yield break; }
            Look(f.position, 0.2f);
            yield return Wait(0.8f);
        }

        IEnumerator SetupCampProps()
        {
            Log("camp props: " + ConsequenceCheats.ShowCampProps());
            yield return Wait(2f);
            Hour(11.5f, true);
            yield return Wait(2f);
            if (!FireAndRack(out var fire, out var rack)) { Say("setup failed: no camp fire"); yield break; }
            Look((fire * 0.6f + rack * 0.4f), 0.34f);
            yield return Wait(0.8f);
        }

        IEnumerator SetupPlacing()
        {
            yield return EnsurePond();
            Log("jars ready: " + Sim.DebugJarsReady());
            Hour(10f);
            yield return Wait(3f);
            DefaultView();
            Say($"jars ready: ducks {Sim.JarReady(LifeSpecies.Ducks)}, fish {Sim.JarReady(LifeSpecies.Fish)}. Open Kit, take ducks, tap the water, tap again; then fish.");
            yield return Frames(5);
        }

        IEnumerator SetupFisher()
        {
            Hour(10f);
            yield return EnsureFish();
            pv.Planner.ClearLive();
            var fv = FishingView.Ensure();
            int kept0 = fv.FishKept;
            Log("fishers sent: " + FishingView.DebugBiteNow());
            yield return Until(() => FisherAt(out _), 40f);
            yield return Until(() => fv.FishKept > kept0, 45f);
            yield return Wait(1f);
            if (FisherAt(out var at)) Look(at, 0.18f, 45f); else Say("setup failed: nobody is fishing");
            yield return Wait(0.5f);
        }

        IEnumerator LookAtRack(float zoom = 0.2f)
        {
            if (!pv.Planner.TryGetFishRack(pv.Now, out var rack)) { Say("setup failed: no fish rack"); yield break; }
            Look(World(rack.x, rack.z) + Vector3.up * 0.06f, zoom, 40f);
            yield return Wait(0.8f);
        }

        IEnumerator SetupRackFresh()
        {
            Hour(10f, true);
            pv.Planner.ClearLive();
            var e = Sim.State.eco;
            e.fishBasket = 0; e.fishRackDry = 0; e.fishRackFresh = 6; e.fishRackDay = pv.Now.day;
            Log("fresh fish hung on the rack (state cheat)");
            yield return Wait(1.5f);
            yield return LookAtRack();
        }

        IEnumerator SetupRackDry()
        {
            Hour(10f, true);
            pv.Planner.ClearLive();
            var e = Sim.State.eco;
            e.fishBasket = 0; e.fishRackFresh = 0; e.fishRackDry = 6; e.fishRackDay = pv.Now.day - 1;
            Log("dry fish hung on the rack (state cheat)");
            yield return Wait(1.5f);
            yield return LookAtRack();
        }

        IEnumerator SetupPile()
        {
            Hour(10f, true);
            pv.Planner.ClearLive();
            var e = Sim.State.eco;
            e.fishBasket = 0; e.fishRackFresh = 0; e.fishRackDry = 0;
            Log(Sim.DebugFishToPile(9) + " dried fish on the pile; " + StashProps.DebugStock(Sim, true));
            yield return Wait(1.5f);
            if (pv.Planner.TryGetStash(out var pile)) Look(World(pile.x, pile.z) + Vector3.up * 0.03f, 0.2f, 45f);
            else Say("setup failed: no stash pile");
            yield return Wait(0.8f);
        }

        IEnumerator SetupChain()
        {
            Hour(10f);
            pv.Planner.ClearLive();
            var e = Sim.State.eco;
            e.fishBasket = 0; e.fishRackFresh = 0; e.fishRackDry = 0;
            yield return Wait(1f);
            if (FireAndRack(out var fire, out var rack)) Look((fire + rack) * 0.5f, 0.32f);
            yield return Wait(1f);
            Log("fill fish basket: " + FishRackView.DebugFillBasket());
            Say("watch: the basket goes to the rack and hangs. Set up again repeats it.");
        }

        IEnumerator SetupSettlers(bool shore)
        {
            Hour(19.2f);
            yield return EnsurePond();
            Log("pond to full: " + EcoDebug.PondToFull());
            yield return Frames(10);
            string kind = shore ? ApartKind.ShoreHut : ApartKind.WoodsCamp;
            var g = Sim.ApartGroups.LastOrDefault(x => x.kind == kind);
            if (g == null) { Log("settlers: " + EcoDebug.SettlersArrive(shore)); g = Sim.ApartGroups[Sim.ApartGroups.Count - 1]; }
            Rig.GoHome(0.01f, true);
            yield return Wait(6f); // the view re-asks the spot every 5 s
            ApartGroupsView.DebugFly(g.id, 0.4f);
            yield return Wait(1.2f);
        }

        IEnumerator SetupKids()
        {
            var sim = Sim;
            Hour(10f);
            var kids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).ToList();
            if (kids.Count < 2)
            {
                var born = new[] { sim.DebugBirth(), sim.DebugBirth() };
                SimLayoutSource.DebugSkipYears(0.1f);
                yield return null;
                foreach (var n in born) n.age = 7f;
                pv.Planner.Invalidate();
                sim.CleanAll();
                Hour(10f);
                yield return Frames(4);
                for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
                Hour(10f);
                yield return Frames(4);
            }
            Log("two kids play: " + KidPlayDirector.DebugTwo());
            yield return Wait(2f);
            var ids = pv.Planner.People.Where(w => pv.Planner.StageOf(w) == LifeStage.Kid).ToList();
            var sum = Vector2.zero; int n2 = 0;
            foreach (var k in ids) if (pv.TryGetDollPosition(k, out var p)) { sum += p; n2++; }
            if (n2 == 0) { Say("setup failed: no kids to frame"); yield break; }
            var mid = sum / n2;
            Look(World(mid.x, mid.y), 0.16f, 45f);
            yield return Wait(0.8f);
        }

        IEnumerator SetupShoreFisher()
        {
            Hour(10f);
            yield return EnsureFish();
            yield return EnsureSmallPond();
            yield return FishOnTheShore();
        }

        IEnumerator FishOnTheShore()
        {
            pv.Planner.ClearLive();
            Log("fishers sent: " + FishingView.DebugBiteNow());
            yield return Until(() => FisherAt(out _), 40f);
            yield return Wait(1f);
            if (FisherAt(out var at)) Look(at, 0.35f, 50f); else Say("setup failed: nobody is fishing");
            yield return Wait(0.5f);
        }

        IEnumerator PondStepExtra()
        {
            Log("pond +1 step: " + EcoDebug.PondStep());
            yield return Frames(20);
            yield return FishOnTheShore();
            Say("pond " + Sim.EcoPondCells + " cells");
        }

        IEnumerator SetupSeed()
        {
            Hour(10f);
            DefaultView();
            Say(EcoDebug.SeedInHand());
            yield return Wait(1f);
        }

        IEnumerator SetupGhosts()
        {
            Hour(10f);
            Log("jars ready: " + Sim.DebugJarsReady());
            DefaultView();
            Say($"thicket jar ready: {Sim.JarReady(LifeSpecies.Thicket)}");
            yield return Wait(1f);
        }

        IEnumerator SetupWetGlass()
        {
            Hour(10f);
            Log(EcoDebug.Humidity(0.85f));
            DefaultView();
            yield return Wait(1f);
        }

        IEnumerator SetupTreeNight()
        {
            if (Sim.State.eco.treeCell < 0) Log("plant: " + EcoDebug.PlantHere());
            Log(EcoDebug.TreeStage(4));
            yield return Frames(10);
            Hour(22.5f, true);
            yield return Wait(2.5f);
            int tc = Sim.State.eco.treeCell;
            if (tc < 0) { Say("setup failed: no tree stands"); DefaultView(); yield break; }
            Look(World(SiteMap.CXc(tc), SiteMap.CZc(tc)), 0.3f, 45f);
            yield return Wait(0.8f);
        }

        // ---------------------------------------------------------------- running a setup (abortable by any verdict)
        IEnumerator Steps(IEnumerator body, string what)
        {
            panel.busy = true;
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception e) { Say($"setup failed: {what}: {e.GetType().Name}: {e.Message}"); Debug.LogWarning("[oqso] " + e); break; }
                if (panel.verdict != SignOffPanel.V.None) break;
                yield return cur;
            }
            panel.busy = false;
        }


        // ---------------------------------------------------------------- results
        sealed class Row { public int Index; public string Stop, Section, Verdict, Note, Time; }

        sealed class Results
        {
            readonly string path, prior, head;
            readonly SortedDictionary<int, Row> rows = new SortedDictionary<int, Row>();

            public Results(string path, string title)
            {
                this.path = path;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                prior = File.Exists(path) ? File.ReadAllText(path).TrimEnd() + "\n\n" : "# Open questions sign-off\n\nVerdicts from the `OpenQuestionsSignOff` play test, one run per section.\n\n";
                head = $"## {title}\n\n";
            }

            static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();

            public void Set(int index, string stop, string section, string verdict, string note)
            {
                rows[index] = new Row { Index = index, Stop = stop, Section = section, Verdict = verdict, Note = note, Time = DateTime.Now.ToString("HH:mm:ss") };
                Save();
            }

            void Save()
            {
                var sb = new StringBuilder(prior).Append(head);
                sb.Append("| Stop | Section | Verdict | Note | Time |\n|---|---|---|---|---|\n");
                foreach (var r in rows.Values) sb.Append($"| {r.Index + 1}. {Cell(r.Stop)} | {Cell(r.Section)} | {Cell(r.Verdict)} | {Cell(r.Note)} | {r.Time} |\n");
                File.WriteAllText(path, sb.ToString());
            }
        }

        // ---------------------------------------------------------------- the run
        static int ModeOf(Kind k) => k == Kind.Look ? SignOffPanel.ModeLook : k == Kind.Decision ? SignOffPanel.ModeDecision : SignOffPanel.ModeCalls;

        [UnityTest, Timeout(14400000)]
        public IEnumerator OpenQuestions_SignOff()
        {
            auto = AutoAnswer || Environment.GetEnvironmentVariable("OQSIGNOFF_AUTO") == "1" || File.Exists(ShotDir + "AUTO");
            GameClock.EcologyPref = true;
            ShowMeDirector.BubblesOn = false;
            GameObject panelGo = null;
            bool soundWas = GameSettings.Sound;
            try
            {
                yield return Open(1179, 2556, 4242);
                GameUiPanel.AcceptInputUnfocused(true);
                Assert.IsTrue(Sim.EcologyOn, "a living-land box");
                hud = GameHud.Instance;
                box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                pv = PeopleView.Find();
                panelGo = new GameObject("OpenQuestionsSignOffPanel");
                UnityEngine.Object.DontDestroyOnLoad(panelGo); // #569 reloads a young box for its stop and the played box afterwards
                panel = panelGo.AddComponent<SignOffPanel>();
                panel.Note("Getting the box ready...");
                Log("skipped seasons: " + EcoDebug.SkipSeasons(50));
                if (Sim.State.era != Era.Camp) Log("camp gate: " + EcoDebug.CampGate());
                yield return StagePlayedBox(); // the new start greens nothing by itself: a lived-in camp for the look stops
                Hour(10f);
                yield return Wait(2f);
                DefaultView();
                yield return Frames(10);

                string mdPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "open-questions.md"));
                string resultsPath = auto
                    ? ShotDir + "auto-results.md"
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "overnight", "open-questions-signoff.md"));
                if (auto && File.Exists(resultsPath)) File.Delete(resultsPath);
                var stops = BuildStops(mdPath);
                Log($"{stops.Count} stops from {mdPath}"); for (int k = 0; k < stops.Count; k++) Log($"stop {k + 1}: {stops[k].Kind} [{stops[k].Section}] {stops[k].Issue} {stops[k].Name}");
                var names = new List<string>(); var first = new List<int>(); var count = new List<int>();
                for (int k = 0; k < stops.Count; k++)
                {
                    if (names.Count == 0 || names[names.Count - 1] != stops[k].Section) { names.Add(stops[k].Section); first.Add(k); count.Add(0); }
                    count[count.Count - 1]++;
                }
                panel.SetSections(names.ToArray(), first.ToArray(), count.ToArray());
                var results = new Results(resultsPath, "Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + (auto ? " (auto)" : ""));

                int i = 0, autoEnd = int.MaxValue;
                if (auto && File.Exists(ShotDir + "AUTO")) { var rg = File.ReadAllText(ShotDir + "AUTO").Trim().Split(','); if (rg.Length == 2 && int.TryParse(rg[0], out int a0) && int.TryParse(rg[1], out int a1)) { i = a0 - 1; autoEnd = a1; } } // "from,to" (1-based, inclusive) limits an auto run
                if (!auto)
                {
                    int last = UnityEditor.EditorPrefs.GetInt(LastKey, 0);
                    if (last > 0 && last < stops.Count)
                    {
                        panel.ShowStart($"Your last run stopped before stop {last + 1} of {stops.Count} ({stops[last].Section}: {stops[last].Name}). Continue there, or start at the beginning. \"Go to...\" jumps to any section.");
                        yield return Until(() => panel.verdict != SignOffPanel.V.None, 4 * 3600f);
                        var sv = panel.Take(out _);
                        if (sv == SignOffPanel.V.Signed) i = last;
                        else if (sv == SignOffPanel.V.Jump) i = Mathf.Clamp(panel.jumpTo, 0, stops.Count - 1);
                    }
                }
                int textShots = 0;
                while (true)
                {
                    if (i >= stops.Count || i >= autoEnd)
                    {
                        panel.ShowDone(resultsPath);
                        UnityEditor.EditorPrefs.DeleteKey(LastKey);
                        if (auto) break;
                        yield return Until(() => panel.verdict != SignOffPanel.V.None, 4 * 3600f);
                        var done = panel.Take(out _);
                        if (done == SignOffPanel.V.Back) { i = stops.Count - 1; continue; }
                        if (done == SignOffPanel.V.Jump) { i = Mathf.Clamp(panel.jumpTo, 0, stops.Count - 1); continue; }
                        break;
                    }
                    var st = stops[i];
                    string crumb = st.Section + (st.Sub.Length > 0 && st.Kind != Kind.Look ? " · " + st.Sub : "") + (st.Kind == Kind.Look ? "" : " · " + st.Kind.ToString().ToLowerInvariant());
                    panel.Show(i + 1, stops.Count, ModeOf(st.Kind), crumb, (st.Issue.Length > 0 ? st.Issue + " " : "") + st.Name, st.Judge, st.Extras.Select(x => x.label).ToArray(), st.Options, st.Bullets, st.BulletTitles, st.NotStaged);
                    if (pv != null) pv.timeScale = 1f; // a live stop may have sped the view clock up
                    panel.collapsed = false; // a stop before may have folded it (the HUD icons, the caption, the framings)
                    if (st.Box.Length == 0 && ((youngBox && !st.Slug.StartsWith("pacing_")) || ownBox.Length > 0)) { yield return RebuildPlayedBox(); }
                    if (Screen.width != st.W || Screen.height != st.H) { SetSize(st.W, st.H); yield return Until(() => Screen.width == st.W && Screen.height == st.H, 4f); yield return Frames(4); }
                    yield return CloseLooseUi();
                    if (!st.Slug.StartsWith("season_") && !st.Slug.StartsWith("pacing_")) yield return ResetSeason();
                    hold = null; // a stop's camera hold (auto runs) never outlives it
                    yield return Steps(st.Setup(), st.Name);
                    if (auto && panel.verdict == SignOffPanel.V.None)
                    {
                        if (st.Kind == Kind.Calls && panel.ticks.Length > 1) panel.ticks[1] = false; // shows the unticked look in the shot
                        bool shoot = st.Kind == Kind.Look || textShots < 3;
                        yield return HoldWait(st.Kind == Kind.Look ? 4f : 1f);
                        if (shoot)
                        {
                            if (st.Kind != Kind.Look) textShots++;
                            if (st.Slug == "title_line") { panel.collapsed = true; yield return Frames(3); } // the title sits behind the panel: fold it for the shot
                            if (st.Slug == "pt1_caption") { panel.hidden = true; yield return Frames(3); } // the caption plate: nothing over it
                            yield return Shot((i + 1).ToString("00") + "_" + st.Slug);
                            panel.collapsed = false; panel.hidden = false;
                        }
                        for (int e = 0; e < st.Extras.Length; e++)
                        {
                            yield return Steps(st.Extras[e].run(), st.Extras[e].label);
                            yield return HoldWait(3f);
                            yield return Shot((i + 1).ToString("00") + "_" + st.Slug + "_x" + (e + 1) + "_" + Slug(st.Extras[e].label)); // one shot per extra
                        }
                        if (st.Kind == Kind.Decision) { if (st.Options.Length > 0) { panel.verdict = SignOffPanel.V.Option; panel.optionIndex = 0; } else panel.verdict = SignOffPanel.V.Other; }
                        else panel.verdict = SignOffPanel.V.Signed;
                    }
                    while (panel.verdict == SignOffPanel.V.None)
                    {
                        if (panel.extraClicked >= 0)
                        {
                            int e = panel.extraClicked; panel.extraClicked = -1;
                            yield return Steps(st.Extras[e].run(), st.Extras[e].label);
                            continue;
                        }
                        if (repinHour >= 0f && Time.realtimeSinceStartup > repinAt)
                        {
                            repinAt = Time.realtimeSinceStartup + 5f;
                            float d = Mathf.Abs(Mathf.DeltaAngle(pv.Now.hour * 15f, repinHour * 15f)) / 15f;
                            if (d > 1.5f) PeopleView.DebugSetHour(repinHour);
                        }
                        yield return null;
                    }
                    int opt = panel.optionIndex, jump = panel.jumpTo;
                    bool[] ticks = (bool[])panel.ticks.Clone();
                    var v = panel.Take(out string note);
                    string label = (st.Issue.Length > 0 ? st.Issue + " " : "") + st.Name;
                    string where = st.Section + (st.Sub.Length > 0 ? " / " + st.Sub : "");
                    switch (v)
                    {
                        case SignOffPanel.V.Signed:
                            if (st.Kind == Kind.Calls)
                            {
                                var wants = st.BulletTitles.Where((t, k) => k < ticks.Length && !ticks[k]).ToList();
                                results.Set(i, label, where, wants.Count == 0 ? "accepted all" : $"accepted, {wants.Count} want a change", (wants.Count > 0 ? "change: " + string.Join("; ", wants) + (note.Length > 0 ? " | " : "") : "") + note);
                            }
                            else results.Set(i, label, where, "signed off", note);
                            i++; break;
                        case SignOffPanel.V.Needs: results.Set(i, label, where, "needs change", note); i++; break;
                        case SignOffPanel.V.Option: results.Set(i, label, where, opt >= 0 && opt < st.Options.Length ? st.Options[opt] : "option", note); i++; break;
                        case SignOffPanel.V.Other: results.Set(i, label, where, "other", note); i++; break;
                        case SignOffPanel.V.Skip: results.Set(i, label, where, st.Kind == Kind.Look ? "skipped" : "decide later", note); i++; break;
                        case SignOffPanel.V.Back: i = Mathf.Max(0, i - 1); break;
                        case SignOffPanel.V.Jump: i = Mathf.Clamp(jump, 0, stops.Count - 1); break;
                        case SignOffPanel.V.Again: break;
                    }
                    if (!auto) UnityEditor.EditorPrefs.SetInt(LastKey, Mathf.Clamp(i, 0, stops.Count));
                }
                Log("sign-off walk finished: " + resultsPath);
            }
            finally
            {
                if (panelGo != null) UnityEngine.Object.Destroy(panelGo);
                GameSettings.Sound = soundWas;
                RestorePt1();
                ShowMeDirector.BubblesOn = true;
                var p = PeopleView.Find();
                if (p != null) p.timeScale = 1f;
                GameClock.EcologyPref = false;
                GameClock.DebugNextSeed = 0;
                CampFrameView.Auto = true;
            }
        }
    }

    /// <summary>
    /// The sign-off panel: IMGUI on a plain scene object (so it needs no UI Toolkit), scaled so it reads on a 1179x2556 Game view, at the
    /// top under the HUD's title row. Its rect is registered with <see cref="UiInput"/> so a click on it never reaches the camera or the HUD.
    /// Modes: a look stop (Signed off / Needs change), a decision (one button per option, Other with the note), a calls stop (a tick box per
    /// bullet), a start screen (continue where the last run stopped) and the done screen. Keys: Enter signed off (or the first option),
    /// N needs change, R set up again, B back (not while the note field has focus).
    /// </summary>
    public sealed class SignOffPanel : MonoBehaviour
    {
        public enum V { None, Signed, Needs, Again, Back, Skip, Option, Other, Jump }
        public const int ModeLook = 0, ModeDecision = 1, ModeCalls = 2, ModeStart = 3, ModeDone = 4;

        public V verdict;
        public string status = "";
        public bool busy;
        public int extraClicked = -1, optionIndex = -1, jumpTo = -1;
        public bool[] ticks = new bool[0];
        string note = "", head = "", crumb = "", judge = "", notStaged = "";
        string[] extras = new string[0], options = new string[0], bulletTitles = new string[0], bullets = new string[0];
        string[] secNames = new string[0]; int[] secFirst = new int[0], secCount = new int[0];
        int mode;
        internal bool collapsed;
        /// <summary>Auto runs: no panel at all for a shot whose subject sits where even the folded bar would be.</summary>
        internal bool hidden;
        bool typing, jumpOpen;
        Rect screenRect, pub, noteRect;
        float measuredH = 200f;
        bool pubSet;
        Vector2 scroll;
        GUIStyle box, title, body, small, button, field, toggle, tickOff, warn;
        Texture2D bg;

        const float UnitsWide = 540f, TopPx = 235f, MaxContent = 300f;

        void Awake() { UiInput.Add(Over); }

        void OnDestroy()
        {
            UiInput.Remove(Over);
            if (pubSet) UiInput.DebugBlocks.Remove(pub);
            if (bg != null) Destroy(bg);
        }

        bool Over(Vector2 p) => screenRect.width > 0f && screenRect.Contains(new Vector2(p.x, Screen.height - p.y));

        public void SetSections(string[] names, int[] first, int[] count) { secNames = names; secFirst = first; secCount = count; }

        public void Note(string s) { mode = ModeLook; head = "Open questions sign-off"; crumb = ""; judge = s; extras = options = bullets = bulletTitles = new string[0]; notStaged = ""; ticks = new bool[0]; }

        public void Show(int n, int total, int mode, string crumbText, string what, string toJudge, string[] extraButtons, string[] optionTexts, string[] bulletTexts, string[] bulletHeads, string notStagedText)
        {
            head = $"{n}/{total} · {what}"; crumb = crumbText; judge = toJudge; extras = extraButtons; options = optionTexts; bullets = bulletTexts; bulletTitles = bulletHeads;
            this.mode = mode; notStaged = notStagedText ?? ""; status = ""; busy = false; jumpOpen = false; scroll = Vector2.zero;
            ticks = new bool[bulletTexts.Length]; for (int i = 0; i < ticks.Length; i++) ticks[i] = true;
            verdict = V.None; note = ""; extraClicked = -1; optionIndex = -1; jumpTo = -1;
        }

        public void ShowStart(string info)
        {
            mode = ModeStart; head = "Open questions sign-off"; crumb = ""; judge = info; extras = options = bullets = bulletTitles = new string[0]; ticks = new bool[0];
            notStaged = ""; status = ""; busy = false; jumpOpen = false; verdict = V.None; note = ""; jumpTo = -1;
        }

        public void ShowDone(string path)
        {
            mode = ModeDone; head = "All stops done"; crumb = ""; judge = "Verdicts are saved in " + path + ". Press Finish, or Back to return to the last stop.";
            extras = options = bullets = bulletTitles = new string[0]; ticks = new bool[0]; notStaged = ""; status = ""; busy = false; verdict = V.None; jumpOpen = false;
        }

        public V Take(out string noteOut)
        {
            var v = verdict; noteOut = note; verdict = V.None; note = ""; return v;
        }

        void Set(V v, int option = -1) { if (verdict == V.None) { verdict = v; optionIndex = option; } }

        void Update()
        {
            Publish();
            if (typing) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (mode == ModeDecision) { if (options.Length > 0) Set(V.Option, 0); else Set(V.Other); }
                else Set(V.Signed);
            }
            else if (kb.nKey.wasPressedThisFrame && mode == ModeLook) Set(V.Needs);
            else if (kb.rKey.wasPressedThisFrame && mode <= ModeCalls) Set(V.Again);
            else if (kb.bKey.wasPressedThisFrame) Set(V.Back);
        }

        void LateUpdate() => Publish();

        void Publish()
        {
            if (screenRect.width <= 0f) return;
            var list = UiInput.DebugBlocks;
            if (pubSet) list.Remove(pub);
            pub = screenRect; pubSet = true;
            list.Add(pub);
        }

        void Init()
        {
            bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            bg.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.09f, 0.92f)); bg.Apply();
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
            box.normal.background = bg;
            title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            title.normal.textColor = new Color(1f, 0.93f, 0.7f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            body.normal.textColor = Color.white;
            small = new GUIStyle(body) { fontSize = 11, fontStyle = FontStyle.Italic };
            small.normal.textColor = new Color(0.75f, 0.85f, 1f);
            warn = new GUIStyle(small);
            warn.normal.textColor = new Color(1f, 0.75f, 0.45f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 13 };
            toggle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(2, 2, 3, 3) };
            toggle.normal.textColor = Color.white;
            tickOff = new GUIStyle(toggle);
            tickOff.normal.textColor = new Color(1f, 0.7f, 0.4f);
        }

        void OnGUI()
        {
            if (box == null) Init();
            if (hidden) { screenRect = Rect.zero; return; }
            float sc = Screen.width / UnitsWide;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            GUI.depth = -100;
            const float X = 6f, W = UnitsWide - 12f, IW = W - 16f;
            float y = TopPx / sc;
            var e = Event.current;
            if (collapsed) y = (Screen.height - 470f) / sc; // folded, the bar sits low, above the bottom tools: the title, the top icons and the caption stay clear
            if (collapsed)
            {
                var r = new Rect(X, y, W, 40f);
                screenRect = new Rect(r.x * sc, r.y * sc, r.width * sc, r.height * sc);
                GUI.Box(r, GUIContent.none, box);
                GUI.Label(new Rect(X + 8f, y + 6f, W - 60f, 30f), head.Length > 0 ? head : "sign-off", title);
                if (GUI.Button(new Rect(X + W - 46f, y + 4f, 40f, 32f), "▼", button)) collapsed = false;
                GUI.matrix = old;
                return;
            }
            GUILayout.BeginArea(new Rect(X, y, W, 1100f));
            GUILayout.BeginVertical(box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(head, title, GUILayout.Width(IW - 50f));
            if (GUILayout.Button("▲", button, GUILayout.Width(40f), GUILayout.Height(30f))) collapsed = true;
            GUILayout.EndHorizontal();
            if (crumb.Length > 0) GUILayout.Label(crumb, small);
            // the content: the item's text, or the tick list; scrolls when long
            if (mode == ModeCalls)
            {
                float natural = 0f;
                for (int i = 0; i < bullets.Length; i++) natural += toggle.CalcHeight(new GUIContent("[x] " + bullets[i]), IW - 30f) + 4f;
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(natural + 6f + 24f, MaxContent)));
                for (int i = 0; i < bullets.Length; i++) if (GUILayout.Button((ticks[i] ? "[x]  " : "[  ]  CHANGE:  ") + bullets[i], ticks[i] ? toggle : tickOff)) ticks[i] = !ticks[i];
                GUILayout.EndScrollView();
            }
            else
            {
                float natural = body.CalcHeight(new GUIContent(judge), IW - 20f);
                if (natural > MaxContent)
                {
                    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(MaxContent));
                    GUILayout.Label(judge, body);
                    GUILayout.EndScrollView();
                }
                else GUILayout.Label(judge, body);
            }
            if (notStaged.Length > 0) GUILayout.Label(notStaged, warn);
            string st = busy ? "setting up... " + status : status;
            if (st.Length > 0) GUILayout.Label(st, small);
            if (mode <= ModeCalls)
            {
                if (e.type == EventType.MouseDown && !noteRect.Contains(e.mousePosition)) GUI.FocusControl(null);
                GUI.SetNextControlName("note");
                note = GUILayout.TextField(note, 300, field, GUILayout.Height(30f));
                if (e.type == EventType.Repaint) noteRect = GUILayoutUtility.GetLastRect();
                typing = GUI.GetNameOfFocusedControl() == "note";
                GUILayout.Label(mode == ModeLook ? "note (saved with Needs change)" : mode == ModeDecision ? "note (saved with Other)" : "note (saved with the verdict)", small);
            }
            else typing = false;
            var prev = GUI.backgroundColor;
            if (mode == ModeLook)
            {
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);
                if (GUILayout.Button("Signed off", button, GUILayout.Height(42f))) { Set(V.Signed); GUI.FocusControl(null); }
                GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
                if (GUILayout.Button("Needs change", button, GUILayout.Height(42f))) { Set(V.Needs); GUI.FocusControl(null); }
                GUI.backgroundColor = prev;
                GUILayout.EndHorizontal();
            }
            else if (mode == ModeDecision)
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);
                for (int i = 0; i < options.Length; i++)
                {
                    if (GUILayout.Button(options[i], button, GUILayout.MinHeight(42f))) { Set(V.Option, i); GUI.FocusControl(null); }
                    GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
                }
                GUI.backgroundColor = new Color(1f, 0.7f, 0.5f);
                if (GUILayout.Button("Other (type what you want in the note)", button, GUILayout.Height(40f))) { Set(V.Other); GUI.FocusControl(null); }
                GUI.backgroundColor = prev;
            }
            else if (mode == ModeCalls)
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);
                if (GUILayout.Button("Accept all ticked", button, GUILayout.Height(42f))) { Set(V.Signed); GUI.FocusControl(null); }
                GUI.backgroundColor = prev;
            }
            else if (mode == ModeStart)
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);
                if (GUILayout.Button("Continue", button, GUILayout.Height(42f))) Set(V.Signed);
                GUI.backgroundColor = prev;
                if (GUILayout.Button("Start at the beginning", button, GUILayout.Height(36f))) Set(V.Back);
            }
            else
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);
                if (GUILayout.Button("Finish", button, GUILayout.Height(42f))) Set(V.Signed);
                GUI.backgroundColor = prev;
                if (GUILayout.Button("Back", button, GUILayout.Height(34f))) Set(V.Back);
            }
            if (mode <= ModeCalls)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Set up again", button, GUILayout.Height(34f))) { Set(V.Again); GUI.FocusControl(null); }
                if (GUILayout.Button("Back", button, GUILayout.Height(34f))) { Set(V.Back); GUI.FocusControl(null); }
                if (GUILayout.Button(mode == ModeLook ? "Skip" : "Decide later", button, GUILayout.Height(34f))) { Set(V.Skip); GUI.FocusControl(null); }
                GUILayout.EndHorizontal();
            }
            if (extras.Length > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < extras.Length; i++) if (GUILayout.Button(extras[i], button, GUILayout.Height(34f))) { extraClicked = i; GUI.FocusControl(null); }
                GUILayout.EndHorizontal();
            }
            if (secNames.Length > 0)
            {
                if (GUILayout.Button(jumpOpen ? "Go to... (close)" : "Go to...", button, GUILayout.Height(30f))) jumpOpen = !jumpOpen;
                if (jumpOpen)
                    for (int i = 0; i < secNames.Length; i++)
                        if (GUILayout.Button($"{secFirst[i] + 1}. {secNames[i]} ({secCount[i]})", button, GUILayout.Height(28f))) { jumpTo = secFirst[i]; Set(V.Jump); jumpOpen = false; }
            }
            GUILayout.Label("Enter signed off / first option · N needs change · R set up again · B back", small);
            GUILayout.EndVertical();
            if (e.type == EventType.Repaint) { var lr = GUILayoutUtility.GetLastRect(); if (lr.height > 10f) measuredH = lr.height; }
            GUILayout.EndArea();
            screenRect = new Rect(X * sc, y * sc, W * sc, measuredH * sc);
            GUI.matrix = old;
        }
    }
}
