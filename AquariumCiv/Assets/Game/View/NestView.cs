using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #709 (B71, docs/design/animation-specs.md "### #709"): the nest in the thicket, drawn from the sim's nest (SimCore.Nest.cs).
    /// <list type="bullet">
    /// <item>B71.a: a bowl of twigs in the nest bush's crown, on the camera's side; its eggs (pale blue-green, a speck); the chicks with
    /// their peep bob; the empty nest after; gone with its thicket.</item>
    /// <item>B71.b: the first spring morning the sign's grey bird flies in three times with a twig (the nest grows 0.6 → 0.8 → 1), then
    /// perches; from the eggs on it sits (fluffed, head turning), gets up every 20–40 s for a small circle, and leaves at the end of spring
    /// (the chicks after it).</item>
    /// <item>B71.c/B71.d: the people's beats at the nest are laid over their poses (<see cref="PeopleView.NestPose"/>): the kids creep,
    /// tiptoe, peer and count, step back when the bird puts its wings out; the gatherer sets her basket down, reaches in, holds an egg
    /// up to the light and lays it on the berries; the bird comes back and scolds her (she bows and backs off). The egg rides on her
    /// basket to the pile.</item>
    /// </list>
    /// The visits themselves are the planner's (<see cref="LifePlanner.NestVisitsOn"/>); this view settles them every frame
    /// (<see cref="LifePlanner.ResolveNestVisits"/>, moved here from FishRackView). The bird is outside the animal budget (Q4).
    /// </summary>
    [DefaultExecutionOrder(8)]
    public sealed class NestView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static NestView Instance { get; private set; }

        // ---------------------------------------------------------------- the spec's numbers
        /// <summary>B71.a: the nest's outer radius and height (×1.5 like the props), the eggs', the chicks' sizes; how high in the crown.</summary>
        public const float NestR = 0.045f, NestH = 0.018f, EggR = 0.009f, ChickR = 0.011f, NestUp = 0.06f;
        /// <summary>B71.b: the sign bird's scale, its flights (the sign's own), the trips, the pause on the rim between them.</summary>
        public const float BirdScale = 1.8f, FlyIn = BushView.FlyIn, FlyOut = BushView.FlyOut, DropTwig = 0.7f;
        public const int Trips = 3;
        public const float SitLower = 0.01f, Fluff = 1.1f, HeadDeg = 40f, HeadEvery = 2f, CircleR = 0.3f, CircleUp = 0.15f;
        public const float CircleEveryMin = 20f, CircleEveryMax = 40f, CircleMin = 6f, CircleMax = 10f;
        /// <summary>B71.a: the chicks' peep bob (Hz) and its height; an egg popping in (s) and between eggs.</summary>
        public const float PeepHz = 3f, PeepUp = 0.006f, EggPop = 0.35f, EggGap = 1.2f;
        /// <summary>B71.d: the scold: wings out flapping at 6 Hz for 1 s, never nearer the gatherer than 0.05 u.</summary>
        public const float ScoldHz = 6f, ScoldFor = 1.6f, ScoldKeep = 0.05f;

        public enum BirdState { Away, Trip, Perch, Sit, Circle, Scold, Leave }

        BoxView box;
        PeopleView people;
        SimCore subscribed;
        Transform holder, rootSeen;
        Transform nest, bird, birdHead, twig, heldEgg;
        MeshFilter birdMf;
        readonly Transform[] eggs = new Transform[4];
        readonly Transform[] chicks = new Transform[2];
        static Mesh nestMesh, eggMesh, chickMesh, twigMesh, birdUp, birdDown, birdHeadMesh;

        readonly Queue<NestEventKind> events = new();
        int cell = -1;
        Vector3 nestAt, flyFrom, front;
        float nestGrow, nestPop = -1f;
        int eggsShown, chicksShown;
        readonly float[] eggT = new float[4];
        float nextEggAt;
        BirdState bs = BirdState.Away;
        float bt, stateFor, nextCircle;
        int tripsLeft, tripN, builtYearShown = int.MinValue;
        float chickLeaveT = -1f;
        Vector3 scoldAt;
        int scoldKey;
        readonly List<NestPlan> settled = new();
        int heldKey = int.MinValue;
        float heldT;
        bool heldOnBasket;

        /// <summary>For the tests and the panel: what is drawn now.</summary>
        public bool NestShown => nest != null && nest.gameObject.activeInHierarchy && nestGrow > 0.01f;
        public float NestGrowth => nestGrow;
        public int EggsShown => eggsShown;
        public int ChicksShown => chicksShown;
        public BirdState Bird => bs;
        public bool BirdShown => bird != null && bird.gameObject.activeInHierarchy;
        public int TripsDone { get; private set; }
        public int Scolds { get; private set; }
        public int EggsCarried { get; private set; }
        public Vector3 NestLocal => nestAt;
        public Vector3 BirdLocal => bird != null ? bird.localPosition : Vector3.zero;
        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static NestView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<NestView>();
            return v != null ? v : b.gameObject.AddComponent<NestView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            PeopleView.NestPose = PoseHook;
        }

        void OnDisable()
        {
            if (PeopleView.NestPose == (PeopleView.StagedPoseHook)PoseHook) PeopleView.NestPose = null;
            if (subscribed != null) subscribed.NestChanged -= OnNest;
            subscribed = null;
            if (Instance == this) Instance = null;
            if (holder != null) Destroy(holder.gameObject);
            holder = null; nest = null; bird = null;
            cell = -1; bs = BirdState.Away; events.Clear();
        }

        void OnNest(NestEventKind kind, int at) => events.Enqueue(kind);

        // ---------------------------------------------------------------- update
        void Update()
        {
            var sim = Sim;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (subscribed != sim)
            {
                if (subscribed != null) subscribed.NestChanged -= OnNest;
                subscribed = sim;
                if (sim != null) sim.NestChanged += OnNest;
                events.Clear(); cell = -1;
            }
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { HideAll(); return; }
            if (rootSeen != box.Root) { holder = null; nest = null; bird = null; rootSeen = box.Root; cell = -1; }
            if (people == null) people = PeopleView.Find();
            var planner = people != null ? people.Planner : null;
            settled.Clear();
            if (planner != null && !ReplayGate.Active) planner.ResolveNestVisits(people.Now, settled); // #709: the egg's take and the kids' look, once each (was in FishRackView)
            foreach (var p in settled) if (p.gather && p.taken) { heldKey = p.key; heldT = 0f; heldOnBasket = false; }

            if (!sim.NestStands || !ReplayGate.Shows(-1))
            {
                events.Clear();
                if (nest != null && nest.gameObject.activeSelf && nestGrow > 0f)
                {   // gone with its thicket (blown down, plucked): it shrinks away
                    nestGrow = Mathf.MoveTowards(nestGrow, 0f, dt / 0.3f);
                    nest.localScale = Vector3.one * nestGrow;
                    if (nestGrow <= 0f) HideAll();
                }
                else HideAll();
                cell = -1;
                return;
            }
            if (!box.BushSpots.TryGetValue(sim.NestCell, out var spot)) { HideAll(); return; }
            Ensure();
            if (cell != sim.NestCell) Place(sim.NestCell, spot.pos, spot.r);

            while (events.Count > 0) Handle(events.Dequeue(), sim);
            Reconcile(sim, dt);
            StepBird(sim, planner, dt);
            StepEggsAndChicks(sim, dt);
            StepHeldEgg(dt);
            nest.gameObject.SetActive(nestGrow > 0.001f);
            float pop = nestPop >= 0f ? Pop(nestPop / 0.25f) : 1f;
            if (nestPop >= 0f) { nestPop += dt; if (nestPop > 0.25f) nestPop = -1f; }
            nest.localScale = Vector3.one * Mathf.Max(0.001f, nestGrow * pop);
        }

        static float Pop(float u) => 1f + Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 0.2f;

        void HideAll()
        {
            if (holder != null && holder.gameObject.activeSelf) holder.gameObject.SetActive(false);
            nestGrow = 0f; eggsShown = 0; chicksShown = 0; bs = BirdState.Away;
        }

        void Place(int c, Vector3 pos, float r)
        {
            cell = c;
            // the crown's front: toward the default camera (yaw 28: it stands off -x, -z), so the 34° pitch sees in
            front = new Vector3(-0.47f, 0f, -0.88f);
            nestAt = pos + Vector3.up * Mathf.Max(NestUp, r * 0.95f) + front * r * 0.45f;
            nest.localPosition = nestAt;
            var away = new Vector3(nestAt.x >= 0f ? 1f : -1f, 0f, nestAt.z >= 0f ? 1f : -1f).normalized; // in from over the glass, as the sign
            flyFrom = nestAt + away * 0.9f + Vector3.up * 0.55f;
        }

        // ---------------------------------------------------------------- events and the sim's state
        void Handle(NestEventKind k, SimCore sim)
        {
            switch (k)
            {
                case NestEventKind.Built:
                    StartBuild(sim);
                    break;
                case NestEventKind.Moved:
                    cell = -1; // drawn at the new bush next frame
                    break;
                case NestEventKind.Laid:
                    nextEggAt = 0.4f;
                    if (bs is BirdState.Away or BirdState.Perch or BirdState.Trip && sim.NestParentPresent) { if (bs != BirdState.Trip) SetBird(BirdState.Sit); }
                    break;
                case NestEventKind.EggTaken:
                    if (eggsShown > sim.NestEggs) eggsShown = sim.NestEggs; // the egg went into the gatherer's hand (StepHeldEgg draws it)
                    if (bs != BirdState.Away && bs != BirdState.Leave) StartScold();
                    break;
                case NestEventKind.Hatched:
                    for (int i = 0; i < chicks.Length; i++) eggT[Mathf.Min(i, 3)] = 0f;
                    if (bs == BirdState.Sit) SetBird(BirdState.Perch); // up off the nest: the chicks show
                    break;
                case NestEventKind.ParentLeft:
                    if (bs != BirdState.Away) SetBird(BirdState.Leave);
                    if (chicksShown > 0) chickLeaveT = 0f;
                    break;
                case NestEventKind.Gone:
                    break;
            }
        }

        void StartBuild(SimCore sim)
        {
            int year = (int)((sim.State.month + sim.State.seasonShift) / 12);
            builtYearShown = year;
            tripsLeft = Trips; tripN = 0;
            if (nestGrow < 0.01f) nestGrow = 0f;
            eggsShown = 0; chicksShown = 0; chickLeaveT = -1f;
            SetBird(BirdState.Away); // the first trip starts once it is day
        }

        /// <summary>The drawn nest follows the sim when nothing played it in (a loaded box, a jump): the nest built, the eggs in, the bird there.</summary>
        void Reconcile(SimCore sim, float dt)
        {
            var phase = sim.NestPhaseNow;
            int year = (int)((sim.State.month + sim.State.seasonShift) / 12);
            if (tripsLeft == 0 && phase != NestPhase.None) nestGrow = Mathf.MoveTowards(nestGrow, 1f, dt / 0.3f);
            if (phase == NestPhase.Building && builtYearShown != year && sim.NestParentPresent && bs == BirdState.Away) StartBuild(sim);
            if (phase != NestPhase.Eggs && phase != NestPhase.Chicks && eggsShown > 0) eggsShown = 0;
            if (phase == NestPhase.Eggs && eggsShown > sim.NestEggs) eggsShown = sim.NestEggs;
            if (phase == NestPhase.Chicks && chicksShown == 0 && chickLeaveT < 0f) { chicksShown = sim.NestChicks; for (int i = 0; i < chicks.Length; i++) eggT[i] = 0f; eggsShown = 0; }
            if (phase == NestPhase.Empty && chickLeaveT < 0f) chicksShown = 0;
            bool parent = sim.NestParentPresent;
            if (parent && bs == BirdState.Away && tripsLeft == 0) SetBird(phase == NestPhase.Eggs ? BirdState.Sit : BirdState.Perch);
            if (!parent && bs is BirdState.Sit or BirdState.Perch or BirdState.Circle) SetBird(BirdState.Leave);
            if (phase == NestPhase.Chicks && bs == BirdState.Sit) SetBird(BirdState.Perch);
        }

        // ---------------------------------------------------------------- the bird
        void SetBird(BirdState s)
        {
            bs = s; bt = 0f;
            if (s == BirdState.Circle) stateFor = Random.Range(CircleMin, CircleMax);
            if (s == BirdState.Sit || s == BirdState.Perch) nextCircle = Random.Range(CircleEveryMin, CircleEveryMax);
            if (s == BirdState.Scold) stateFor = ScoldFor + 0.8f;
        }

        Vector3 Rim => nestAt + Vector3.up * NestH * 0.8f;
        /// <summary>The rim's far side (from the camera): the perched bird never hides the chicks or the eggs.</summary>
        Vector3 Perch => nestAt - front * NestR * 1.0f + Vector3.up * NestH;

        /// <summary>A gather is on at the nest now (the gatherer walking in or reaching): the bird keeps off the nest (B71.d: "the bird is off it").</summary>
        bool GatherOn(LifePlanner planner, out NestPlan plan)
        {
            plan = null;
            if (planner == null || people == null) return false;
            var now = people.Now;
            foreach (var p in planner.NestVisitsOn(now.day))
                if (p.gather && now.hour >= p.start && now.hour < p.resolveHour + 0.02f) { plan = p; return true; }
            return false;
        }

        void StepBird(SimCore sim, LifePlanner planner, float dt)
        {
            bt += dt;
            bool day = people == null || (people.ViewHour >= BushView.SignFromHour && people.ViewHour <= BushView.SignToHour);
            Vector3 p = bird.localPosition; bool flying = false, sitting = false; float headYaw = 0f, lift = 0f, scale = BirdScale;
            bool wingsHalf = false;
            switch (bs)
            {
                case BirdState.Away:
                    bird.gameObject.SetActive(false);
                    bool sign = BushView.Instance != null && BushView.Instance.SignPlaying; // the first bush's bird comes down first (a cheat can start both at once)
                    if (tripsLeft > 0 && day && !sign) { SetBird(BirdState.Trip); bird.localPosition = flyFrom; }
                    return;
                case BirdState.Trip:
                {
                    float inEnd = FlyIn, dropEnd = inEnd + DropTwig, outEnd = dropEnd + (tripsLeft > 1 ? FlyOut : 0f);
                    twig.gameObject.SetActive(bt < inEnd + DropTwig * 0.4f);
                    if (bt < inEnd)
                    {
                        float u = Smooth(bt / inEnd);
                        p = Vector3.Lerp(flyFrom, Rim, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.06f; flying = true;
                    }
                    else if (bt < dropEnd)
                    {
                        p = Rim;
                        if (bt - dt < inEnd + DropTwig * 0.4f && bt >= inEnd + DropTwig * 0.4f)
                        {   // the twig goes in: the nest grows a step (0.6, 0.8, 1) and pops
                            tripN++; TripsDone++;
                            nestGrow = Mathf.Max(nestGrow, 0.4f + 0.2f * tripN);
                            nestPop = 0f;
                            if (tripN == 1) AudioDirector.NestChirp(tripN);
                        }
                        lift = Mathf.Max(0f, Mathf.Sin((bt - inEnd) / DropTwig * Mathf.PI)) * 0.01f; // a dip to drop it in
                    }
                    else if (bt < outEnd)
                    {
                        float u = Smooth((bt - dropEnd) / FlyOut);
                        p = Vector3.Lerp(Rim, flyFrom, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.05f; flying = true;
                    }
                    else
                    {
                        tripsLeft--;
                        if (tripsLeft > 0) { bt = 0f; p = flyFrom; }
                        else { twig.gameObject.SetActive(false); SetBird(sim.NestPhaseNow == NestPhase.Eggs ? BirdState.Sit : BirdState.Perch); p = Perch; }
                    }
                    break;
                }
                case BirdState.Perch:
                case BirdState.Sit:
                {
                    bool sit = bs == BirdState.Sit;
                    p = sit ? Rim + Vector3.down * SitLower : Perch;
                    sitting = sit;
                    if (sit) scale *= Fluff;
                    headYaw = HeadTurn(bt);
                    p.y += !sit && Mathf.Sin(bt * 2.6f) > 0.97f ? 0.004f : 0f; // a small hop now and then
                    if (sit && eggsShown < Mathf.Min(sim.NestEggs, eggs.Length) && nextEggAt < 0.3f) lift = 0.012f; // up off the eggs as one comes
                    if (KidsLookingNow(planner, out _)) { headYaw = 0f; wingsHalf = true; lift += 0.004f; } // B71.c: Alert at the kids
                    bool gather = GatherOn(planner, out _);
                    nextCircle -= dt;
                    if (gather || nextCircle <= 0f && !KidsLookingNow(planner, out _)) { SetBird(BirdState.Circle); if (gather) stateFor = 999f; }
                    break;
                }
                case BirdState.Circle:
                {
                    // up off the nest, a small circle round it (B17.b's songbird), back down
                    float up = 0.6f;
                    bool gather = GatherOn(planner, out _);
                    if (gather) stateFor = Mathf.Max(stateFor, bt + 2f); else if (stateFor > 100f) stateFor = bt + 2f;
                    float back = Mathf.Max(up + 0.5f, stateFor);
                    float a = (bt - up) / Mathf.Max(1f, back - up - 1f) * Mathf.PI * 2f;
                    var centre = nestAt + Vector3.up * CircleUp;
                    var onCircle = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * CircleR;
                    var home = sim.NestPhaseNow == NestPhase.Eggs ? Rim : Perch;
                    if (bt < up) p = Vector3.Lerp(home, centre + new Vector3(1f, 0f, 0f) * CircleR, Smooth(bt / up));
                    else if (bt < back - 1f) p = onCircle;
                    else if (bt < back) p = Vector3.Lerp(onCircle, home, Smooth(bt - (back - 1f)));
                    else { SetBird(sim.NestPhaseNow == NestPhase.Eggs && sim.NestParentPresent ? BirdState.Sit : sim.NestParentPresent ? BirdState.Perch : BirdState.Leave); p = home; }
                    flying = bs == BirdState.Circle;
                    break;
                }
                case BirdState.Scold:
                {
                    // B71.d: lands on the bush, hops toward the gatherer, wings out flapping, never touching; back to the nest
                    Vector3 toward = scoldAt - nestAt; toward.y = 0f;
                    float reach = Mathf.Max(0f, toward.magnitude - ScoldKeep - 0.03f);
                    var land = nestAt + Vector3.up * 0.02f + (toward.sqrMagnitude > 1e-6f ? toward.normalized * Mathf.Min(reach, 0.12f) : Vector3.zero);
                    if (bt < 0.6f) { p = Vector3.Lerp(bird.localPosition, land, Smooth(bt / 0.6f)); flying = true; }
                    else if (bt < 0.6f + ScoldFor)
                    {
                        float s = bt - 0.6f;
                        p = land + Vector3.up * Mathf.Abs(Mathf.Sin(s * Mathf.PI * 2.5f)) * 0.012f; // hop, hop
                        flying = ((int)(s * ScoldHz * 2f) & 1) == 0; // wings out, flapping at 6 Hz
                        if (bt - dt < 0.65f && bt >= 0.65f) { AudioDirector.NestChirp(3, 1.35f); AudioDirector.NestChirp(4, 1.45f); Scolds++; }
                    }
                    else if (bt < stateFor) { p = Vector3.Lerp(land, Rim, Smooth((bt - 0.6f - ScoldFor) / 0.8f)); flying = true; }
                    else { SetBird(sim.NestPhaseNow == NestPhase.Eggs ? BirdState.Sit : BirdState.Perch); p = Rim; }
                    if (toward.sqrMagnitude > 1e-6f) bird.localRotation = Quaternion.LookRotation(toward.normalized);
                    break;
                }
                case BirdState.Leave:
                {
                    var to = flyFrom + (flyFrom - nestAt) * 0.6f + Vector3.up * 0.3f;
                    float u = Smooth(bt / FlyOut);
                    p = Vector3.Lerp(bt < 0.05f ? bird.localPosition : p, to, u);
                    flying = true;
                    if (bt >= FlyOut) { SetBird(BirdState.Away); bird.gameObject.SetActive(false); return; }
                    break;
                }
            }
            bird.gameObject.SetActive(true);
            var prev = bird.localPosition;
            bird.localPosition = p + Vector3.up * lift;
            var d = p - prev; d.y = 0f;
            if (bs != BirdState.Scold)
            {
                var side = new Vector3(-front.z, 0f, front.x); // side-on to the camera: a bird's profile reads, a bird facing the camera reads as a cross
                Vector3 face = flying && d.sqrMagnitude > 1e-8f ? d : (sitting || bs == BirdState.Perch ? side : bird.localRotation * Vector3.forward);
                if (face.sqrMagnitude > 1e-8f) bird.localRotation = Quaternion.Slerp(bird.localRotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-dt * 8f));
            }
            bird.localScale = Vector3.one * scale;
            birdHead.localRotation = Quaternion.Euler(wingsHalf ? -15f : 0f, headYaw, 0f);
            birdMf.sharedMesh = flying ? (((int)(bt * 9f) & 1) == 0 ? birdUp : birdDown) : wingsHalf ? birdUp : birdDown;
        }

        static float HeadTurn(float t)
        {
            // every 2 s the head turns to a new side: left, ahead, right, ahead
            int k = (int)(t / HeadEvery);
            float target = (k % 4) switch { 0 => 0f, 1 => HeadDeg, 2 => 0f, _ => -HeadDeg };
            float prevT = ((k + 3) % 4) switch { 0 => 0f, 1 => HeadDeg, 2 => 0f, _ => -HeadDeg };
            return Mathf.Lerp(prevT, target, Smooth((t - k * HeadEvery) / 0.25f));
        }

        static float Smooth(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        void StartScold()
        {
            scoldAt = nestAt + Vector3.back * 0.2f;
            if (people != null && heldKey != int.MinValue && people.TryGetDollTransform(KeyRef(heldKey), out var t))
                scoldAt = box.Root.InverseTransformPoint(t.position);
            SetBird(BirdState.Scold);
        }

        static PersonRef KeyRef(int key) => key >= 0 ? PersonRef.Notable(key) : PersonRef.Crowd(-key - 1);

        bool KidsLookingNow(LifePlanner planner, out NestPlan plan)
        {
            plan = null;
            if (planner == null || people == null) return false;
            var now = people.Now;
            foreach (var p in planner.NestVisitsOn(now.day))
                if (!p.gather && now.hour >= p.pickStart && now.hour < p.resolveHour) { plan = p; return true; }
            return false;
        }

        // ---------------------------------------------------------------- eggs and chicks
        void StepEggsAndChicks(SimCore sim, float dt)
        {
            int wantEggs = sim.NestPhaseNow == NestPhase.Eggs ? Mathf.Min(sim.NestEggs, eggs.Length) : 0;
            if (eggsShown < wantEggs)
            {   // they come one at a time while the bird lifts off them (B71.a; the sim lays them in one go)
                nextEggAt -= dt;
                if (nextEggAt <= 0f) { eggT[eggsShown] = 0f; eggsShown++; nextEggAt = EggGap; }
            }
            for (int i = 0; i < eggs.Length; i++)
            {
                bool on = i < eggsShown && chicksShown == 0;
                eggs[i].gameObject.SetActive(on);
                if (!on) continue;
                eggT[i] += dt;
                float s = eggT[i] < EggPop ? Mathf.Sin(Mathf.Clamp01(eggT[i] / EggPop) * Mathf.PI * 0.5f) * Pop(eggT[i] / EggPop) : 1f;
                float a = i * 1.7f + 0.4f;
                eggs[i].localPosition = nestAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * NestR * 0.32f * (i == 0 && eggsShown == 1 ? 0f : 1f) + Vector3.up * NestH * 0.55f;
                eggs[i].localScale = Vector3.one * Mathf.Max(0.001f, s);
            }
            // the chicks: a peep bob at 3 Hz, beaks up; at the end of spring they hop up and flutter off after the parent
            int wantChicks = chickLeaveT >= 0f ? chicksShown : (sim.NestPhaseNow == NestPhase.Chicks ? Mathf.Min(sim.NestChicks, chicks.Length) : 0);
            chicksShown = wantChicks;
            for (int i = 0; i < chicks.Length; i++)
            {
                bool on = i < chicksShown;
                chicks[i].gameObject.SetActive(on);
                if (!on) continue;
                eggT[i] += dt;
                float a = i * 2.6f + 0.9f;
                var home = nestAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * NestR * 0.3f + Vector3.up * NestH * 0.6f;
                float bob = Mathf.Max(0f, Mathf.Sin((Time.time + i * 0.17f) * PeepHz * Mathf.PI * 2f)) * PeepUp;
                float hatch = eggT[i] < 0.5f ? Mathf.Clamp01(eggT[i] / 0.5f) : 1f;
                var p = home + Vector3.up * bob;
                if (chickLeaveT >= 0f)
                {
                    float t = chickLeaveT - 0.4f - i * 0.35f; // after the parent, one by one
                    if (t > 0f)
                    {
                        var to = flyFrom + Vector3.up * 0.2f + new Vector3(i * 0.08f, 0f, 0f);
                        float u = Smooth(t / (FlyOut + 0.4f));
                        p = Vector3.Lerp(home, to, u) + Vector3.up * Mathf.Sin(Mathf.Min(1f, t * 3f) * Mathf.PI) * 0.03f; // a hop up, then away
                        if (u >= 1f) chicks[i].gameObject.SetActive(false);
                    }
                }
                chicks[i].localPosition = p;
                chicks[i].localScale = Vector3.one * Mathf.Max(0.001f, hatch * Pop(eggT[i] / 0.5f));
                chicks[i].localRotation = Quaternion.LookRotation(-front) * Quaternion.Euler(0f, (i == 0 ? -25f : 25f), 0f);
            }
            if (chickLeaveT >= 0f)
            {
                chickLeaveT += dt;
                if (chickLeaveT > FlyOut + 2f) { chickLeaveT = -1f; chicksShown = 0; }
            }
        }

        // ---------------------------------------------------------------- the egg in the gatherer's hand (B71.d)
        void StepHeldEgg(float dt)
        {
            if (heldKey == int.MinValue || people == null) { heldEgg.gameObject.SetActive(false); return; }
            heldT += dt;
            var who = KeyRef(heldKey);
            if (!people.TryGetDollTransform(who, out var doll) || !people.TryGetActivityOf(who, out var act) || heldT > 60f || act.kind == ActivityKind.TipLoad && heldT > 2f)
            {
                if (heldOnBasket) EggsCarried++;
                heldKey = int.MinValue; heldEgg.gameObject.SetActive(false); return;
            }
            var view = doll.GetComponent<PegDollView>();
            Vector3 world;
            if (heldT < 1.4f && view != null && view.armR != null) world = view.armR.TransformPoint(new Vector3(0f, -KidGame.ArmLen * 1.05f, 0f)); // up to the light
            else if (view != null && view.prop != null && view.prop.gameObject.activeInHierarchy) { world = view.prop.position + Vector3.up * 0.02f * doll.lossyScale.y; heldOnBasket = true; } // on the berries
            else world = doll.position + Vector3.up * 0.1f * doll.lossyScale.y;
            heldEgg.gameObject.SetActive(true);
            heldEgg.position = world;
        }

        // ---------------------------------------------------------------- the people's beats at the nest (PeopleView.NestPose)
        void PoseHook(in PersonActivity a, PegDollView view, ref DollPose pose)
        {
            var pv = people;
            if (pv == null || pv.Planner == null || Sim == null || !Sim.NestStands) return;
            var now = pv.Now;
            var plans = pv.Planner.NestVisitsOn(now.day);
            if (plans.Count == 0) return;
            int key = a.who.Key;
            foreach (var plan in plans)
            {
                if (plan.gather && plan.key == key) { GatherPose(plan, a, now, ref pose); return; }
                if (!plan.gather && plan.lookerKeys.Contains(key)) { LookPose(plan, a, now, ref pose, plan.lookerKeys.IndexOf(key)); return; }
                if (!plan.gather && plan.beckonKey == key) { BeckonPose(plan, now, ref pose); return; }
            }
        }

        /// <summary>B71.d over the gatherer's Gather at the nest bush: notice, set the basket down, reach in slowly, the egg Pops into the hand
        /// (the take settles at the window's end), hold it up to the light, lay it on the berries; leaving, a small apologetic bow.</summary>
        static void GatherPose(NestPlan plan, in PersonActivity a, ViewTime now, ref DollPose p)
        {
            float W = LifeClock.RealSecondsBetween(plan.pickStart, plan.resolveHour);
            if (a.kind == ActivityKind.Gather && now.hour >= plan.pickStart && now.hour < plan.resolveHour)
            {
                float s = LifeClock.RealSecondsBetween(plan.pickStart, now.hour);
                float reachAt = Mathf.Max(0.7f, W - 3.2f), takeAt = W - 2.0f;
                p.armL = new Vector3(0f, 0f, -14f); p.armR = new Vector3(0f, 0f, 14f); p.extend = false; p.hit = 0f;
                if (s < 0.3f) p.yaw += 15f * Smooth(s / 0.3f); // she notices it
                else if (s < 0.7f) { float u = Smooth((s - 0.3f) / 0.4f); p.squash = Mathf.Lerp(1f, 0.88f, u); p.lean = 18f * u; p.armL = new Vector3(20f * u, 0f, -14f); } // the basket down
                else if (s < takeAt)
                {
                    float u = Smooth((s - reachAt) / 0.6f); // careful: slow
                    p.lean = Mathf.Lerp(8f, 25f, u); p.armR = new Vector3(Mathf.Lerp(10f, 100f, u), 0f, 10f); p.squash = 0.95f;
                }
                else if (s < takeAt + 0.8f) { float u = Smooth((s - takeAt) / 0.8f); p.lean = Mathf.Lerp(25f, -4f, u); p.armR = new Vector3(Mathf.Lerp(100f, 140f, u), 0f, 8f); } // up to the light
                else { float u = Smooth((s - takeAt - 0.8f) / 0.6f); p.lean = Mathf.Lerp(-4f, 10f, u); p.armR = new Vector3(Mathf.Lerp(140f, 60f, u), 0f, 10f); } // onto the berries
                return;
            }
            if (plan.taken && now.hour >= plan.resolveHour)
            {   // the bird scolds: she backs off a step with a small bow
                float s = LifeClock.RealSecondsBetween(plan.resolveHour, Mathf.Min(now.hour, plan.carryEnd));
                if (s < 1.6f) { float w = Mathf.Sin(Mathf.Clamp01(s / 1.6f) * Mathf.PI); p.lean += 20f * w; p.offset.z += 0.02f * w; }
            }
        }

        /// <summary>B71.c over the kids at the nest: creep in, tiptoe and peer, one counts the eggs (pokes the air three times), the bird's wings
        /// come out: two steps back, hands behind the back, watching; then they tiptoe off. They never touch it.</summary>
        static void LookPose(NestPlan plan, in PersonActivity a, ViewTime now, ref DollPose p, int index)
        {
            if (now.hour < plan.pickStart)
            {   // the last part of the walk in: a crouched creep
                if (a.progress > 0.6f) { float u = Smooth((a.progress - 0.6f) / 0.4f); p.squash *= Mathf.Lerp(1f, 0.86f, u); p.lean += 14f * u; p.armL.x += 15f * u; p.armR.x += 15f * u; }
                return;
            }
            if (now.hour >= plan.resolveHour)
            {   // tiptoe off, then a run (the walk itself)
                float s = LifeClock.RealSecondsBetween(plan.resolveHour, now.hour);
                if (s < 1.2f) { p.offset.y += 0.02f; p.lean -= 4f; p.armL = new Vector3(-20f, 0f, -10f); p.armR = new Vector3(-20f, 0f, 10f); }
                return;
            }
            float t = LifeClock.RealSecondsBetween(plan.pickStart, now.hour);
            float W = LifeClock.RealSecondsBetween(plan.pickStart, plan.resolveHour);
            float alertAt = Mathf.Min(3.4f, W * 0.55f);
            p.hit = 0f;
            if (t < alertAt)
            {   // up on tiptoe, peering in; one counts
                float u = Smooth(t / 0.4f);
                p.offset.y += 0.03f * u; p.lean = 25f * u; p.squash = 1f; p.roll = 0f;
                p.armL = new Vector3(10f, 0f, -14f); p.armR = new Vector3(10f, 0f, 14f);
                if (index == 0 && t > 1.4f && t < 2.6f) p.armR = new Vector3(70f + Mathf.Max(0f, Mathf.Sin((t - 1.4f) * Mathf.PI * 2.5f)) * 12f, 0f, 10f); // the count: three pokes at the air
                return;
            }
            // the bird puts its wings out: two steps back, hands behind the back, watching
            float b = Smooth((t - alertAt) / 0.6f);
            p.offset.z += 0.07f * b; p.offset.y += 0.03f * (1f - b);
            p.lean = Mathf.Lerp(25f, 4f, b);
            p.armL = new Vector3(-20f, 0f, -10f); p.armR = new Vector3(-20f, 0f, 10f);
        }

        /// <summary>B71.c's end: a grown-up nearby beckons the kids away (arm x 60 → 100, twice) over the look's last 1.5 s.</summary>
        static void BeckonPose(NestPlan plan, ViewTime now, ref DollPose p)
        {
            if (now.hour < plan.pickStart || now.hour >= plan.resolveHour) return;
            float left = LifeClock.RealSecondsBetween(now.hour, plan.resolveHour);
            if (left > 1.5f) return;
            float s = 1.5f - left;
            p.armR = new Vector3(60f + 40f * Mathf.Abs(Mathf.Sin(s / 1.5f * Mathf.PI * 2f)), 0f, 12f);
        }

        // ---------------------------------------------------------------- the pieces
        void Ensure()
        {
            if (holder == null)
            {
                holder = new GameObject("Nest") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
                BuildMeshes();
                nest = Part(holder, "NestBowl", nestMesh);
                for (int i = 0; i < eggs.Length; i++) { eggs[i] = Part(holder, "Egg" + i, eggMesh); eggs[i].gameObject.SetActive(false); }
                for (int i = 0; i < chicks.Length; i++) { chicks[i] = Part(holder, "Chick" + i, chickMesh); chicks[i].gameObject.SetActive(false); }
                bird = Part(holder, "NestBird", birdDown);
                birdMf = bird.GetComponent<MeshFilter>();
                birdHead = Part(bird, "Head", birdHeadMesh);
                birdHead.localPosition = new Vector3(0f, 0.038f, 0.02f);
                twig = Part(bird, "Twig", twigMesh);
                twig.localPosition = new Vector3(0f, 0.037f, 0.045f);
                twig.gameObject.SetActive(false);
                bird.gameObject.SetActive(false);
                heldEgg = Part(holder, "HeldEgg", eggMesh);
                heldEgg.localScale = Vector3.one * 1.2f;
                heldEgg.gameObject.SetActive(false);
                cell = -1;
            }
            if (!holder.gameObject.activeSelf) holder.gameObject.SetActive(true);
        }

        Transform Part(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static readonly Color Bark = new(0.42f, 0.3f, 0.2f), Straw = new(0.74f, 0.62f, 0.38f), EggCol = new(0.72f, 0.88f, 0.84f), Speck = new(0.36f, 0.42f, 0.4f),
            FluffCol = new(0.62f, 0.56f, 0.5f), Beak = new(0.95f, 0.72f, 0.28f), BirdBody = new(0.46f, 0.44f, 0.42f), BirdDark = new(0.3f, 0.29f, 0.29f), Breast = new(0.78f, 0.62f, 0.5f);

        static void BuildMeshes()
        {
            if (nestMesh != null) return;
            nestMesh = NestMesh();
            var mb = new MeshBuilder(7091) { jitter = 0f };
            mb.Ico(Vector3.zero, EggR, new Vector3(0.9f, 1.15f, 0.9f), EggCol, 0.05f);
            mb.Box(new Vector3(EggR * 0.5f, EggR * 0.3f, -EggR * 0.75f), Vector3.one * EggR * 0.35f, Speck);
            eggMesh = Keep(mb.ToMesh("NestEgg"));
            mb = new MeshBuilder(7092) { jitter = 0f };
            mb.Ico(Vector3.zero, ChickR, new Vector3(1f, 0.95f, 1f), FluffCol, 0.25f);
            mb.Box(new Vector3(0f, ChickR * 0.9f, ChickR * 0.45f), new Vector3(0.004f, 0.009f, 0.004f), Beak, Quaternion.Euler(-30f, 0f, 0f), true); // the beak up, open
            mb.Box(new Vector3(-0.003f, ChickR * 0.45f, ChickR * 0.85f), Vector3.one * 0.0025f, BirdDark);
            mb.Box(new Vector3(0.003f, ChickR * 0.45f, ChickR * 0.85f), Vector3.one * 0.0025f, BirdDark);
            chickMesh = Keep(mb.ToMesh("NestChick"));
            mb = new MeshBuilder(7093) { jitter = 0f };
            mb.Box(Vector3.zero, new Vector3(0.022f, 0.002f, 0.002f), Bark, Quaternion.Euler(0f, 90f, 8f), true); // a twig in the beak, across it
            twigMesh = Keep(mb.ToMesh("NestTwig"));
            birdUp = Keep(BirdMesh(true)); birdDown = Keep(BirdMesh(false));
            mb = new MeshBuilder(7094) { jitter = 0f };
            mb.Ico(Vector3.zero, 0.0125f, Vector3.one, BirdDark, 0f);
            mb.Box(new Vector3(0f, -0.001f, 0.017f), new Vector3(0.006f, 0.005f, 0.012f), Beak);
            birdHeadMesh = Keep(mb.ToMesh("NestBirdHead"));
        }

        static Mesh Keep(Mesh m) { m.hideFlags = HideFlags.DontSave; return m; }

        /// <summary>B71.a: a shallow bowl of ten twigs in a ring (bark and straw), open on top, its floor a dark disc; origin at its foot.</summary>
        static Mesh NestMesh()
        {
            var mb = new MeshBuilder(709) { jitter = 0.04f };
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * NestR * 0.8f + Vector3.up * NestH * (0.35f + 0.25f * (i % 2));
                var rot = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, (i % 3 - 1) * 14f);
                mb.Box(at, new Vector3(0.006f, 0.006f, NestR * 0.75f), i % 3 == 0 ? Straw : Bark, rot, true);
            }
            mb.Prism(Vector3.zero, NestR * 0.55f, NestR * 0.85f, NestH * 0.6f, 8, new Color(0.3f, 0.22f, 0.15f));
            return Keep(mb.ToMesh("Nest"));
        }

        /// <summary>The sign's grey bird (BushView's shapes) without its head (the head turns on its own), facing +z, feet at the origin.</summary>
        static Mesh BirdMesh(bool wingsUp)
        {
            var mb = new MeshBuilder(55) { jitter = 0f };
            mb.Ico(new Vector3(0, 0.022f, 0), 0.02f, new Vector3(0.85f, 0.8f, 1.35f), BirdBody, 0f, Breast);
            mb.Box(new Vector3(0, 0.024f, -0.034f), new Vector3(0.016f, 0.004f, 0.034f), BirdDark, Quaternion.Euler(-15f, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                var rot = Quaternion.Euler(0f, 0f, s * (wingsUp ? 55f : -8f));
                mb.Box(new Vector3(s * (wingsUp ? 0.03f : 0.016f), wingsUp ? 0.04f : 0.024f, -0.002f), new Vector3(0.034f, 0.003f, 0.026f), BirdDark, rot, true);
            }
            mb.Box(new Vector3(-0.005f, 0.006f, 0.003f), new Vector3(0.002f, 0.012f, 0.002f), Beak);
            mb.Box(new Vector3(0.005f, 0.006f, 0.003f), new Vector3(0.002f, 0.012f, 0.002f), Beak);
            return mb.ToMesh(wingsUp ? "NestBirdUp" : "NestBirdDown");
        }

        // ---------------------------------------------------------------- debug (SimDebugPanel's "Nest (#709)" block)
        /// <summary>"Nest: skip the building": the three trips done now (the nest whole, the bird perched), for the later stages' looks.</summary>
        public static string DebugSkipBuild()
        {
            var v = Instance; var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (v == null || sim == null || !sim.NestStands) return "no nest";
            v.tripsLeft = 0; v.nestGrow = 1f; v.builtYearShown = (int)((sim.State.month + sim.State.seasonShift) / 12);
            if (v.twig != null) v.twig.gameObject.SetActive(false);
            v.SetBird(sim.NestParentPresent ? (sim.NestPhaseNow == NestPhase.Eggs ? BirdState.Sit : BirdState.Perch) : BirdState.Away);
            return "the nest is built";
        }

        string laterWhat; float laterIn = -1f;

        /// <summary>Debug (the AnimLab clips): a nest cheat ("lay", "hatch", "flyoff") <paramref name="seconds"/> of game time from now, so a recording
        /// starts before it.</summary>
        public static string DebugLater(string what, float seconds)
        {
            var v = Instance;
            if (v == null) return "no nest view";
            v.laterWhat = what; v.laterIn = seconds;
            return $"{what} in {seconds:0.0} s";
        }

        void LateUpdate()
        {
            if (laterIn < 0f) return;
            laterIn -= Time.deltaTime;
            if (laterIn > 0f) return;
            laterIn = -1f;
            var sim = Sim;
            if (sim == null) return;
            if (laterWhat == "lay") sim.DebugNestLay();
            else if (laterWhat == "hatch") sim.DebugNestHatch();
            else if (laterWhat == "flyoff") sim.DebugNestFlyOff();
        }

        /// <summary>"Nest: show me": the camera to the nest.</summary>
        public static string DebugLook()
        {
            var v = Instance;
            if (v == null || v.cell < 0 || v.box == null) return "no nest drawn";
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam == null) return "no camera";
            cam.EaseTo(v.box.Root.TransformPoint(v.nestAt), 28f, 34f, 0.22f, 0.6f);
            return "the camera at the nest";
        }
    }
}
