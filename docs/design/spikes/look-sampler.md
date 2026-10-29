# Spike: what is the player looking at? (2026-10-29, branch vs/prep)

**Problem.** `EventLog` samples look-time with one ray from the exact screen centre every 0.5 s. That misses anything
off-centre: P5's look-time on a nudge only counted after the player tapped it (the tap re-centres the camera), and P6's
wide shelf view logged "empty" because the centre falls between the two boxes.

**What was built** (`_Shared/Runtime/Telemetry/LookSampler.cs`, tests `_Shared/Tests/LookSamplerTests.cs`, 5 tests):
- Each sample is shared across every tagged (`LogCategory`) thing with a visible renderer, weighted by a Gaussian around
  the look focus (σ = 0.22 of the screen) times its on-screen size (clamped 0.005–0.25). Shares sum to the sample.
- The focus is the screen centre, pulled to the last touch and drifting back over 3 s (players look where they touch).
- Behind a switch: `EventLog.weightedLook` (default **off**). P2–P6 log look-time exactly as they shipped.

**Checked:** EditMode 47/47 (shares sum to 1, an off-centre nudge gets look-time, two boxes seen from afar are not
"empty", off-screen/behind-camera ignored, bigger things weigh more, focus follows a touch). Live in P6 lab play mode:
16 targets found (notable 5, building 7, citizen 4), shares building 0.87 / notable 0.08 / citizen 0.05 (the centre ray
said "building"), **0.05 ms per sample** in the Editor (FindObjectsByType + one projection per target).

**Known gaps**
- Ground and glass don't appear: their `LogCategory` sits on collider objects with no renderer in view, so weighted mode
  under-reports "ground"/"glass" compared with the centre ray. Fix in the slice: tag the renderers, or add a low-weight
  background term from the centre ray.
- No occlusion: something hidden behind a building still counts. Fine at diorama scale; a ray per target would fix it.
- `FindObjectsByType` every 0.5 s is fine at prototype scale; the slice should keep a registry (LogCategory
  OnEnable/OnDisable).

**Recommendation for the slice:** use the weighted sampler (with tagged renderers and a registry) as the look metric,
and keep the centre ray as a second "camera centre" number for comparability with the prototype data. Don't switch it on
for 0.3.x: it would change a metric mid-test.
