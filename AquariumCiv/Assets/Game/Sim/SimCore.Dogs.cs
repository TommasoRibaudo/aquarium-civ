using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>#438: a dog's stage (spec §5.7, B24-B25): a pup at the fire, grown (the hide collar; hunts and guards), old (a grey muzzle,
    /// it lies by the fire more and no longer hunts).</summary>
    public enum DogStage { Pup, Grown, Old }

    /// <summary>
    /// #438 the dog line (docs/design/placed-life.md §5.7; research 19 §6.2, §7). A dog only ever comes from the lost-pup choice (6.2,
    /// <see cref="LifeChoices.Pup"/>): in spring, a child finds a pup by the den (on the scraps path from the choice on; two springs after
    /// the choice on the chase path; and, if the thief never came, from the third spring after the wolves were placed, so dogs are never
    /// closed off); "leave it" asks again next spring. Brought home, the pup bonds to the child who found it (its keeper: both cards show
    /// it). At a year it is grown (a hide collar): it flushes rabbits for the hunters (<see cref="DogFlushBonus"/>), sometimes finds a
    /// missed deer (<see cref="DogFindsMiss"/>), and guards at night (the first bark: boldness down, and the meat thief stops for good).
    /// Every 2-3 game years a grown dog has a litter (2-3 pups) while there is room under the cap of 3: one pup stays, the others go
    /// back to the wild pack (a history line), so the dogs' ages stagger and the line goes on; each generation's look drifts toward "dog" (P0: +0.25 ear
    /// droop, coat patch and tail curl, -0.05 muzzle, +-0.1 by a hash). An old dog's
    /// muzzle greys, and one visit it is simply gone (only while the Keeper is away, never on screen), told like an old person's death.
    /// The book's "first dog": its name, its keeper's name, the month.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The cap on dogs (spec §4.1: only what's needed).</summary>
        public const int DogCap = 3;
        /// <summary>A pup is grown at this age (game months); a dog is old (grey) this long before its end.</summary>
        public const int DogGrownMonths = 12, DogOldMonths = 24;

        /// <summary>The pup was brought home (the dog arc starts) / a litter was born / a dog was gone between visits.</summary>
        public event Action<Dog> DogAdopted, DogBorn, DogGone;
        /// <summary>The first guard bark (B25.b): from now on the meat thief stops.</summary>
        public event Action<Dog> DogFirstGuard;

        static readonly string[] DogNames = { "Smoke", "Ash", "Pebble", "Moss", "Ember", "Burr", "Thistle", "Sorrel", "Bramble", "Fern", "Soot", "Hazel", "Flint", "Tansy" };

        // ---------- the dogs ----------
        List<Dog> DogList => State.eco?.dogs ?? (State.eco != null ? State.eco.dogs = new List<Dog>() : new List<Dog>());

        /// <summary>Every dog alive now, oldest first.</summary>
        public IReadOnlyList<Dog> Dogs => EcologyOn ? DogList.Where(d => d.Alive).ToList() : new List<Dog>();
        /// <summary>Every dog there ever was (gone ones keep their record), in the order they came.</summary>
        public IReadOnlyList<Dog> AllDogs => EcologyOn ? DogList : (IReadOnlyList<Dog>)Array.Empty<Dog>();
        public int DogCount => EcologyOn ? DogList.Count(d => d.Alive) : 0;
        public Dog FindDog(int id) => EcologyOn ? DogList.FirstOrDefault(d => d.id == id) : null;

        public int DogAgeMonths(Dog d) => (int)Math.Max(0, (d.goneMonth >= 0 ? d.goneMonth : State.month) - d.bornMonth);
        public DogStage DogStageOf(Dog d)
        {
            int age = DogAgeMonths(d);
            return age < DogGrownMonths ? DogStage.Pup : age >= d.lifeMonths - DogOldMonths ? DogStage.Old : DogStage.Grown;
        }
        /// <summary>The hide collar (from grown on, B25.a).</summary>
        public bool DogCollar(Dog d) => DogStageOf(d) != DogStage.Pup;
        /// <summary>Grey round the muzzle (old).</summary>
        public bool DogGrey(Dog d) => DogStageOf(d) == DogStage.Old;
        /// <summary>Grown dogs alive (they hunt and guard; an old one still guards by the fire).</summary>
        public int GrownDogs => EcologyOn ? DogList.Count(d => d.Alive && DogStageOf(d) != DogStage.Pup) : 0;
        /// <summary>Grown dogs that go out with the hunters (not the old ones).</summary>
        public int DogsAtHunt => EcologyOn ? DogList.Count(d => d.Alive && DogStageOf(d) == DogStage.Grown) : 0;

        /// <summary>The dog's keeper (the child it is bonded to; a notable id, -1 none). The person card shows the dog; the dog's card the keeper.</summary>
        public int DogKeeperOf(int dogId) => FindDog(dogId)?.keeperId ?? -1;
        /// <summary>The dogs bonded to this person, alive, oldest first.</summary>
        public List<Dog> DogsOfKeeper(int notableId) => EcologyOn ? DogList.Where(d => d.Alive && d.keeperId == notableId && notableId >= 0).ToList() : new List<Dog>();

        // ---------- the first dog (the book) ----------
        public int FirstDogId => EcologyOn ? State.eco.firstDogId : -1;
        public string FirstDogName => EcologyOn ? State.eco.firstDogName : "";
        public int FirstDogKeeperId => EcologyOn ? State.eco.firstDogKeeperId : -1;
        /// <summary>The first dog's keeper's full name as it was when the pup came home (kept even after they die).</summary>
        public string FirstDogKeeperName => EcologyOn ? State.eco.firstDogKeeperName : "";
        public long FirstDogMonth => EcologyOn ? State.eco.firstDogMonth : -1;

        // ---------- the hunt (B25.a; the planner wires it in #445) ----------
        /// <summary>A small catch bonus on a rabbit hunt while a grown dog runs ahead and flushes them (add it to the catch chance) [est].</summary>
        public float DogFlushBonus => DogsAtHunt > 0 ? 0.15f : 0f;
        /// <summary>After a missed deer on view day <paramref name="day"/> (hunt <paramref name="hunt"/> of the day), the dog tracks it and finds
        /// it lame, 1 time in 3 (hashed, so the view and the sim agree). Settle it with <see cref="DogTrackCatch"/>.</summary>
        public bool DogFindsMiss(long day, int hunt) => DogsAtHunt > 0
            && Terrain.Hash3(State.seed * 31 + 438, (int)(day & 0x7fffffff), hunt) < 1f / 3f;
        /// <summary>The dog found the missed deer: it counts as a hit (the floors and the spring rule hold: false, and nothing changes, when the
        /// herd has none to spare).</summary>
        public bool DogTrackCatch(out bool stag)
        {
            stag = false;
            if (DogsAtHunt == 0 || !DeerHuntCatch(out stag)) return false;
            if (State.chronicle.All(x => x.kind != "dog_track"))
                Chronicle("dog_track", "The hunters missed the deer, but the dog put its nose to the ground and led them through the wood to where it lay.", 0.6f);
            return true;
        }

        // ---------- the lost pup (choice 6.2) ----------
        /// <summary>A pup was found this spring and its choice waits for the Keeper (the finder is <see cref="LostPupFinder"/>).</summary>
        public bool LostPupWaiting => EcologyOn && State.eco.pupFoundMonth >= 0 && State.eco.firstDogId < 0;
        public int LostPupFinder => LostPupWaiting ? State.eco.pupFinder : -1;
        /// <summary>How many times the lost pup was asked about (it comes again next spring after "leave it").</summary>
        public int LostPupAsked => EcologyOn ? State.eco.pupAsked : 0;

        /// <summary>The first spring step (calendar month 3) strictly after <paramref name="m"/>, then <paramref name="n"/> - 1 more.</summary>
        long SpringAfter(long m, int n)
        {
            long first = m - SeasonCalendar.OfMonth(m, State.seasonShift) + 3;
            if (first <= m) first += 12;
            return first + 12L * (n - 1);
        }

        /// <summary>The earliest month a pup can be found (spec §5.6 choice 2): the scraps path from the choice on; the chase path from the
        /// second spring after it; no choice yet (the thief never came) from the third spring after the wolves came. -1 never (a dog came home).</summary>
        public long PupPathFrom
        {
            get
            {
                if (!EcologyOn || State.eco.firstDogId >= 0) return -1;
                var e = State.eco;
                long from = e.wolfWay switch
                {
                    (int)WolfWay.Scraps => e.wolfWayMonth,
                    (int)WolfWay.Chase => SpringAfter(e.wolfWayMonth, 2),
                    _ => EcoM.Jar(LifeSpecies.Wolves).placedMonth < 0 ? -1 : SpringAfter(EcoM.Jar(LifeSpecies.Wolves).placedMonth, 3),
                };
                return from < 0 ? -1 : Math.Max(from, e.pupNotBefore);
            }
        }

        string PupWhyNot()
        {
            var e = State.eco;
            if (e.firstDogId >= 0) return "a dog came home already";
            if (e.pupFoundMonth < 0) return "no pup found";
            return null;
        }

        /// <summary>The child who finds the pup: a living child (5 to 15), by a hash; else the youngest living person. Null: nobody.</summary>
        Notable PupFinder(long month)
        {
            var kids = State.notables.Where(n => n.alive && n.age >= 5f && n.age < AdultAge).OrderBy(n => n.id).ToList();
            if (kids.Count > 0) return kids[(int)(Terrain.Hash3(State.seed * 31 + 4380, 1, (int)(month & 0x7fffffff)) * kids.Count) % kids.Count];
            return State.notables.Where(n => n.alive).OrderBy(n => n.age).ThenBy(n => n.id).FirstOrDefault();
        }

        /// <summary>In spring, with this spring's pups at the den and the path open, a child finds a lost pup (the choice waits for the Keeper).</summary>
        void PupMonth(int season, long month)
        {
            var e = State.eco;
            if (season != 0 || e.wolfPups <= 0 || e.pupFoundMonth >= 0 || !WolvesPlaced) return;
            long from = PupPathFrom;
            if (from < 0 || month < from) return;
            var kid = PupFinder(month);
            if (kid == null) return;
            e.pupFoundMonth = month; e.pupFinder = kid.id;
        }

        void PreparePup(Dilemma d)
        {
            var e = State.eco;
            if (Alive(e.pupFinder) == null) e.pupFinder = PupFinder(State.month)?.id ?? -1;
            d.subjectId = e.pupFinder;
            e.pupAsked++;
        }

        /// <summary>Bring it home (0): the dog arc starts, bonded to the finder. Leave it (1): it runs back to the den; next spring it can be
        /// found again.</summary>
        void ApplyPup(Dilemma d, int option)
        {
            var e = State.eco;
            int finder = d.subjectId >= 0 ? d.subjectId : e.pupFinder;
            e.pupFoundMonth = -1;
            if (option == 1 || e.firstDogId >= 0) { e.pupNotBefore = SpringAfter(State.month, 1); return; }
            Adopt(Alive(finder) ?? PupFinder(State.month));
        }

        /// <summary>The pup comes home (choice 6.2's "bring it home", or the debug): generation 0 (a wolf), bonded to its finder; the first
        /// dog's record for the book.</summary>
        Dog Adopt(Notable keeper)
        {
            var e = State.eco;
            var dog = NewDog(0, keeper?.id ?? -1, -1);
            e.pupFoundMonth = -1;
            if (e.firstDogId < 0)
            {
                e.firstDogId = dog.id; e.firstDogMonth = State.month; e.firstDogName = dog.name;
                e.firstDogKeeperId = keeper?.id ?? -1; e.firstDogKeeperName = keeper?.FullName ?? "";
            }
            e.nextLitterMonth = State.month + LitterGap(dog.id);
            string who = keeper?.FullName ?? "A child";
            Chronicle("first_dog", $"{who} carried the wolf pup home in both arms. Everyone crowded round it at the fire, and the children named it {dog.name}. It was the first dog.", 0.85f, keeper?.id ?? -1);
            DogAdopted?.Invoke(dog);
            return dog;
        }

        Dog NewDog(int gen, int keeperId, int parentId)
        {
            var e = State.eco;
            int id = Math.Max(1, e.dogNextId);
            e.dogNextId = id + 1;
            var dog = new Dog { id = id, gen = gen, keeperId = keeperId, parentId = parentId, bornMonth = State.month, name = DogName(id) };
            dog.lifeMonths = 120 + (int)(DogHash(id, 1) * 37f); // 10 to 13 game years
            DogLook(dog);
            DogList.Add(dog);
            return dog;
        }

        float DogHash(int id, int salt) => Terrain.Hash3(State.seed * 31 + 4381, id, salt);

        /// <summary>P0's drift: generation g adds 0.25 to ear droop, patch and tail curl (capped at 1) and takes 0.05 off the muzzle, with
        /// +-0.1 by a hash of the dog so litter-mates differ. Generation 0 is a wolf (no drift at all).</summary>
        void DogLook(Dog d)
        {
            if (d.gen <= 0) { d.earDroop = d.patch = d.tailCurl = 0f; d.muzzle = 1f; return; }
            float b = Math.Min(1f, 0.25f * d.gen);
            float J(int salt) => (DogHash(d.id, salt) - 0.5f) * 0.2f;
            d.earDroop = Math.Clamp(b + J(11), 0f, 1f);
            d.patch = Math.Clamp(b + J(12), 0f, 1f);
            d.tailCurl = Math.Clamp(b + J(13), 0f, 1f);
            d.muzzle = Math.Clamp(1f - 0.05f * d.gen + J(14), 0.7f, 1f);
        }

        string DogName(int id)
        {
            var used = new HashSet<string>(DogList.Select(x => x.name));
            int start = (int)(DogHash(id, 2) * DogNames.Length) % DogNames.Length;
            for (int k = 0; k < DogNames.Length; k++) { string n = DogNames[(start + k) % DogNames.Length]; if (!used.Contains(n)) return n; }
            return DogNames[start] + " " + (id / DogNames.Length + 1);
        }

        int LitterGap(int salt) => 24 + (int)(Terrain.Hash3(State.seed * 31 + 4382, salt, (int)(State.month & 0x7fffffff)) * 13f); // 2 to 3 game years

        // ---------- the month and the season ----------
        /// <summary>Every month (from WolvesMonth): the lost pup in spring, the first guard bark, the grey muzzles, old age (away only), and in
        /// the spring step a litter.</summary>
        void DogsMonth(int season)
        {
            if (!EcologyOn) return;
            var e = State.eco; long month = State.month;
            PupMonth(season, month);
            var list = DogList;
            if (list.Count == 0) return;
            foreach (var d in list.ToList())
            {
                if (!d.Alive) continue;
                var stage = DogStageOf(d);
                if (stage == DogStage.Old && !d.greyTold)
                {
                    d.greyTold = true;
                    Chronicle("dog_grey", $"{d.name}'s muzzle had gone grey. It lay by the fire more now, and the children brought it the soft bits.", 0.4f, d.keeperId);
                }
                // Never on screen (spec §5.7): an old dog is gone between visits, with a line like an old person's.
                if (DogAgeMonths(d) >= d.lifeMonths && CatchingUp)
                {
                    d.goneMonth = month;
                    var k = FindNotable(d.keeperId);
                    string whose = k != null ? $", {k.name}'s dog," : "";
                    Chronicle("dog_died", $"{d.name}{whose} died of old age at {DogAgeMonths(d) / 12}. They buried it under the trees at the edge of the camp.", 0.6f, d.keeperId);
                    DogGone?.Invoke(d);
                }
            }
            if (e.dogGuardMonth < 0 && WolvesPlaced)
            {
                var guard = list.FirstOrDefault(d => d.Alive && DogStageOf(d) != DogStage.Pup);
                if (guard != null)
                {
                    e.dogGuardMonth = month;
                    e.wolfBold = Math.Clamp(e.wolfBold - Config.eco.boldGuard, 0f, 1f);
                    Chronicle("dog_guard", $"That night {guard.name} stood up by the fire and barked at the dark. The eyes at the edge of the light turned and went away.", 0.6f, guard.keeperId);
                    DogFirstGuard?.Invoke(guard);
                }
            }
            if (season == 0 && CalendarMonth % 3 == 0 && e.nextLitterMonth >= 0 && month >= e.nextLitterMonth) Litter(month, false);
        }

        /// <summary>A grown dog's litter (2-3 pups) while there is room under the cap: the youngest line's grown dog is the mother; one pup
        /// stays (the next generation, bonded to a child without a dog, else the mother's keeper), the others trot off to the far wood and
        /// the wild pack takes them in (<see cref="EcoModel.TakeInDogPups"/>), with a history line.
        /// The next litter in 2-3 game years. False when no grown dog or no room (it waits).</summary>
        bool Litter(long month, bool force)
        {
            var e = State.eco;
            var mother = DogList.Where(d => d.Alive && DogStageOf(d) == DogStage.Grown).OrderByDescending(d => d.gen).ThenBy(d => d.id).FirstOrDefault()
                         ?? (force ? DogList.Where(d => d.Alive).OrderByDescending(d => d.gen).ThenBy(d => d.id).FirstOrDefault() : null);
            int room = DogCap - DogCount;
            if (mother == null || room <= 0) return false;
            int n = 2 + (DogHash(mother.id, (int)(month & 0xffff)) < 0.5f ? 1 : 0);
            int keep = Math.Min(1, room), extra = n - keep; // one pup stays (only what's needed), so the ages stagger and the line never greys out at once
            var kids = State.notables.Where(p => p.alive && p.age >= 3f && p.age < AdultAge && DogsOfKeeper(p.id).Count == 0).OrderBy(p => p.id).ToList();
            for (int k = 0; k < keep; k++)
            {
                int keeper = kids.Count > k ? kids[k].id : mother.keeperId;
                var pup = NewDog(mother.gen + 1, keeper, mother.id);
                DogBorn?.Invoke(pup);
            }
            e.nextLitterMonth = month + LitterGap(mother.id);
            Chronicle("dog_litter", $"{mother.name} had {Words(n).ToLowerInvariant()} pups by the fire. The children argued over which one should stay.", 0.55f, mother.keeperId);
            if (extra > 0)
            {
                // User 2026-11-08: the extra pups go back to the wild pack (within its cap; past it they leave the box as young wolves do)
                EcoM.TakeInDogPups(extra, month);
                Chronicle("dog_pups_wild", extra == 1
                    ? "One of the pups trotted off toward the far wood, and the wolves took it in."
                    : $"{Words(extra)} of the pups trotted off toward the far wood, and the wolves took them in.", 0.4f);
            }
            return true;
        }

        static string Words(int n) => n switch { 2 => "Two", 3 => "Three", _ => n.ToString() };

        // ---------- debug (the panel's "Wolves and dogs" block) ----------
        /// <summary>Debug ("lost pup now"): a child finds a pup now, whatever the season or the path, and the choice is raised (when choices
        /// are on). False when a dog has already come home or there is nobody to find it.</summary>
        public bool DebugLostPupNow()
        {
            if (!EcologyOn || State.eco.firstDogId >= 0) return false;
            var kid = PupFinder(State.month);
            if (kid == null) return false;
            State.eco.pupFoundMonth = State.month; State.eco.pupFinder = kid.id;
            if (Config.dilemmas) DebugRaiseLifeChoice(LifeChoices.Pup);
            return true;
        }

        /// <summary>Debug ("adopt a pup now"): the pup comes home now without the choice (for the dog scenes).</summary>
        public Dog DebugAdoptPup()
        {
            if (!EcologyOn || State.eco.firstDogId >= 0) return null;
            return Adopt(PupFinder(State.month));
        }

        /// <summary>Debug ("next dog generation"): adopts the first pup if there is no dog, grows the youngest, makes room (the oldest dog is
        /// gone, with its line) and has the litter now. The number of dogs after.</summary>
        public int DebugNextDogGeneration()
        {
            if (!EcologyOn) return 0;
            if (DogCount == 0 && DebugAdoptPup() == null) return DogCount;
            foreach (var d in DogList.Where(x => x.Alive && DogStageOf(x) == DogStage.Pup)) d.bornMonth = State.month - DogGrownMonths;
            if (DogCount >= DogCap)
            {
                var old = DogList.Where(x => x.Alive).OrderBy(x => x.bornMonth).ThenBy(x => x.id).First();
                old.goneMonth = State.month;
                Chronicle("dog_died", $"{old.name} died of old age at {DogAgeMonths(old) / 12}. They buried it under the trees at the edge of the camp.", 0.6f, old.keeperId);
                DogGone?.Invoke(old);
            }
            Litter(State.month, true);
            return DogCount;
        }
    }
}
