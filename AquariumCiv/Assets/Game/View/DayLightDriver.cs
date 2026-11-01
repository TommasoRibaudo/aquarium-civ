using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.View
{
    /// <summary>
    /// The day's light in the box (#113, #175; research 13 section 7). Each frame it asks <see cref="DayLight"/> what the view hour
    /// looks like and writes it out: the sun's colour, angle and shadow, the ambient, the post overrides, the lamps. Sits beside
    /// <see cref="NightLamps"/>, which it drives. <see cref="CareView.UpdateSun"/> keeps the sun's intensity, so the mess still
    /// dims it; the driver only writes that while the care view is off (the time-lapse). Look Today writes nothing and puts back
    /// what the scene had. Play mode only: in edit mode <see cref="NightLampDriver"/>'s inspector value still works.
    /// <para>The post goes on a copy of the volume's profile: <c>Post_Slice.asset</c> itself must stay as it is. The night ambient
    /// strength is a global added to <c>_Ambient</c> for the same reason: the solid material is an asset too.</para>
    /// </summary>
    [DefaultExecutionOrder(-100)] // before the care view reads the day's sun
    public class DayLightDriver : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static DayLightDriver Instance { get; private set; }
        /// <summary>True while a look writes the light (so the inspector value and the old Day / Night button stand back).</summary>
        public static bool Driving => Instance != null && Instance.isActiveAndEnabled && Instance.Logic.Drives;

        /// <summary>A share still (research 13 section 7.7): the golden hour.</summary>
        public const float StillHour = 17.5f;
        /// <summary>How far a lamp reaches on what stands near it, and how strong (research: 1.6 to 2.2 was tuned down by eye: at 6 x 4.4 it lit the whole box). The pool's size multiplies its own radius.</summary>
        public const float LampReach = 1.25f, LampStrength = 0.9f;

        static readonly int LampPosId = Shader.PropertyToID("_AC_LampPos"), LampParamsId = Shader.PropertyToID("_AC_LampParams"),
            AmbientBoostId = Shader.PropertyToID("_AC_AmbientBoost"), GlassNightId = Shader.PropertyToID("_AC_GlassNight");

        public readonly DayLight Logic = new();
        public LightValues Current => Logic.Current;
        public bool Drives => Logic.Drives;
        /// <summary>Too dark for the HUD's dark ink to stand bare on the room: it takes the backing it has when the camera is off home (research 13 section 6).</summary>
        public static bool HudNeedsBacking => Driving && Instance.Logic.Current.Daylight < 0.75f;
        /// <summary><see cref="DayLight.MessAmbientScale"/> of the mess dimming; <see cref="CareView"/> sets it.</summary>
        public float MessAmbient { get; set; } = 1f;
        /// <summary>The time-lapse holds the lamps steady whatever the clock does.</summary>
        public bool HoldFlicker { get; set; }
        /// <summary>The lamps' flicker is held now (time-lapse, or the clock running away from the light).</summary>
        public bool FlickerHeld => HoldFlicker || Logic.FlickerHeld;
        /// <summary>The view clock stopped from the debug panel.</summary>
        public bool ClockPaused { get; private set; }

        PeopleView people;
        BoxView box;
        CareView care;
        Light sun;
        Volume volume;
        VolumeProfile sharedProfile, ownProfile;
        ColorAdjustments adjust;
        Vignette vignette;
        Bloom bloom;
        bool wrote, sceneRead;
        float pauseWas = 1f;
        readonly LampPick pick = new();
        readonly Vector4[] lampData = new Vector4[Lamps.MaxLamps];

        // What the scene has, for look Today.
        Color sceneSun, sceneSky, sceneEq, sceneGround, sceneClear;
        Camera camera;
        static readonly Rgb DaySky = DayLight.Table(12f).AmbientSky;
        Quaternion sceneRot;
        float sceneShadow, sceneIntensity, sceneExposure, sceneSaturation, sceneVignette, sceneBloom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<DayLightDriver>() == null) b.gameObject.AddComponent<DayLightDriver>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            ReleaseToScene();
            if (volume != null) volume.profile = sharedProfile;
            if (ownProfile != null)
            {
                foreach (var c in ownProfile.components) if (c != null) Destroy(c);
                Destroy(ownProfile);
            }
            ownProfile = null; sceneRead = false;
        }

        /// <summary>Finds the scene's light and post once, and remembers them for look Today.</summary>
        bool Bind()
        {
            if (sceneRead) return sun != null;
            people = GetComponent<PeopleView>();
            if (people == null) people = PeopleView.Find();
            box = GetComponent<BoxView>();
            sun = RenderSettings.sun;
            if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) return false;
            sceneRead = true;
            sceneSun = sun.color; sceneRot = sun.transform.rotation; sceneShadow = sun.shadowStrength; sceneIntensity = sun.intensity;
            sceneSky = RenderSettings.ambientSkyColor; sceneEq = RenderSettings.ambientEquatorColor; sceneGround = RenderSettings.ambientGroundColor;
            camera = Camera.main;
            if (camera != null) sceneClear = camera.backgroundColor;
            volume = FindAnyObjectByType<Volume>();
            if (volume != null && volume.sharedProfile != null)
            {
                sharedProfile = volume.sharedProfile;
                ownProfile = volume.profile; // a copy: never write the asset
                if (ownProfile.TryGet(out adjust)) { sceneExposure = adjust.postExposure.value; sceneSaturation = adjust.saturation.value; adjust.postExposure.overrideState = adjust.saturation.overrideState = true; }
                if (ownProfile.TryGet(out vignette)) { sceneVignette = vignette.intensity.value; vignette.intensity.overrideState = true; }
                if (ownProfile.TryGet(out bloom)) { sceneBloom = bloom.intensity.value; bloom.intensity.overrideState = true; }
            }
            return true;
        }

        float ViewHour => people != null ? people.ViewHour : 12f;

        void Update()
        {
            if (!Bind()) return;
            if (!Logic.Drives) { ReleaseToScene(); return; }
            Logic.Update(Mathf.Min(Time.unscaledDeltaTime, 0.1f), ViewHour);
            Apply();
        }

        /// <summary>Writes the current light out.</summary>
        void Apply()
        {
            var v = Logic.Current;
            wrote = true;
            sun.color = C(v.SunColor);
            sun.transform.rotation = Quaternion.Euler(v.SunPitch, v.SunYaw, 0f);
            sun.shadowStrength = v.ShadowStrength;
            if (care == null) care = CareView.Instance;
            if (care == null || !care.isActiveAndEnabled) sun.intensity = v.SunIntensity; // the care view is off (time-lapse): nothing to compose with
            RenderSettings.ambientSkyColor = C(v.AmbientSky) * MessAmbient;
            RenderSettings.ambientEquatorColor = C(v.AmbientEquator) * MessAmbient;
            RenderSettings.ambientGroundColor = C(v.AmbientGround) * MessAmbient;
            RenderSettings.fogColor = C(v.FogColor);
            if (camera != null)
            {
                // The room beyond the shelf is the camera's clear colour: it follows the sky twice over, or it stays a lit wall under a dark night.
                float r = v.AmbientSky.R / DaySky.R, g = v.AmbientSky.G / DaySky.G, b = v.AmbientSky.B / DaySky.B;
                camera.backgroundColor = new Color(sceneClear.r * r * r, sceneClear.g * g * g, sceneClear.b * b * b, sceneClear.a);
            }
            Shader.SetGlobalFloat(AmbientBoostId, v.ShaderAmbient - DayLight.DayShaderAmbient);
            Shader.SetGlobalFloat(GlassNightId, 1f - GlassNight.TintFactor(Logic.ShownHour));
            if (adjust != null) { adjust.postExposure.value = v.PostExposure; adjust.saturation.value = v.PostSaturation; }
            if (vignette != null) vignette.intensity.value = v.Vignette;
            if (bloom != null) bloom.intensity.value = v.Bloom;
            NightLamps.SetLevel(v.Lamp);
            PushLamps(v.Lamp);
            FrostSwitch.TintAlpha = GlassNight.FrostOpacity(FrostSwitch.DayTintAlpha, Logic.ShownHour);
        }

        /// <summary>The lamps nearest the view go to the shaders with how lit each is now. The pools need nothing per lamp: their mesh knows its lamps' thresholds.</summary>
        void PushLamps(float level)
        {
            var tune = LookTuning.For(Logic.Look);
            bool steady = FlickerHeld;
            Shader.SetGlobalVector(LampParamsId, new Vector4(1f / (LampReach * LampReach), level > 0f ? LampStrength * tune.LampStrength : 0f, steady ? 1f : 0f, tune.PoolRadius));
            if (box == null || level <= 0f || box.LampCount == 0)
            {
                System.Array.Clear(lampData, 0, lampData.Length);
                Shader.SetGlobalVectorArray(LampPosId, lampData);
                return;
            }
            var rig = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            var focus = rig != null ? rig.target : Vector3.zero;
            Lamps.PickNearest(box.LampX, box.LampZ, box.LampCount, focus.x, focus.z, pick);
            float t = Time.time;
            for (int i = 0; i < Lamps.MaxLamps; i++)
            {
                if (i >= pick.Count) { lampData[i] = default; continue; }
                int k = pick.Indices[i];
                float w = Lamps.BrightnessAt(level, box.LampThreshold[k], box.LampSeed, k, t, steady);
                lampData[i] = new Vector4(box.LampX[k], box.LampY[k], box.LampZ[k], w);
            }
            Shader.SetGlobalVectorArray(LampPosId, lampData);
        }

        /// <summary>Look Today, or the driver going away: everything back as the scene had it.</summary>
        void ReleaseToScene()
        {
            if (!wrote) return;
            wrote = false;
            if (sun != null)
            {
                sun.color = sceneSun; sun.transform.rotation = sceneRot; sun.shadowStrength = sceneShadow;
                if (care == null || !care.isActiveAndEnabled) sun.intensity = sceneIntensity;
            }
            RenderSettings.ambientSkyColor = sceneSky; RenderSettings.ambientEquatorColor = sceneEq; RenderSettings.ambientGroundColor = sceneGround;
            if (camera != null) camera.backgroundColor = sceneClear;
            Shader.SetGlobalFloat(AmbientBoostId, 0f);
            Shader.SetGlobalFloat(GlassNightId, 0f);
            Shader.SetGlobalVector(LampParamsId, Vector4.zero);
            if (adjust != null) { adjust.postExposure.value = sceneExposure; adjust.saturation.value = sceneSaturation; }
            if (vignette != null) vignette.intensity.value = sceneVignette;
            if (bloom != null) bloom.intensity.value = sceneBloom;
            NightLamps.SetLevel(0f);
            FrostSwitch.TintAlpha = FrostSwitch.DayTintAlpha;
        }

        static Color C(in Rgb c) => new(c.R, c.G, c.B, 1f);

        // ---------------- jumps ----------------
        /// <summary>The light at this hour now: no smoothing, no rate clamp (a scrubber, a jump).</summary>
        public void Snap(float hour)
        {
            if (!Bind()) return;
            Logic.Snap(hour);
            if (Logic.Drives) Apply();
        }

        LightValues stillWas;
        float stillShown;
        bool inStill;

        /// <summary>Era stills are all taken at <paramref name="hour"/>; pair with <see cref="EndStill"/> around the render.</summary>
        public void BeginStill(float hour)
        {
            if (!Bind() || !Logic.Drives || inStill) return;
            inStill = true;
            stillWas = Logic.Current; stillShown = Logic.ShownHour;
            Logic.Snap(hour);
            bool held = HoldFlicker;
            HoldFlicker = true; // a picture of a steady lamp, not one caught mid-sputter
            Apply();
            HoldFlicker = held;
        }

        public void EndStill()
        {
            if (!inStill) return;
            inStill = false;
            Logic.Restore(stillWas, stillShown);
            Apply();
        }

        // ---------------- debug ----------------
        /// <summary>Debug: the look named in the panel.</summary>
        public static string DebugLook(LightLook look)
        {
            if (Instance == null) return "no DayLightDriver";
            Instance.Logic.Look = look;
            Instance.Snap(Instance.ViewHour);
            return "look " + look;
        }

        /// <summary>Debug: the night floor (0.45 to 0.9), at once.</summary>
        public static string DebugNightFloor(float floor)
        {
            if (Instance == null) return "no DayLightDriver";
            Instance.Logic.NightFloor = Mathf.Clamp(floor, DayLight.MinNightFloor, DayLight.MaxNightFloor);
            Instance.Snap(Instance.ViewHour);
            return $"night floor {Instance.Logic.NightFloor:0.00}";
        }

        /// <summary>Debug: stop the view clock so a look can be judged.</summary>
        public static string DebugPauseClock(bool pause)
        {
            var p = PeopleView.Find();
            if (p == null || Instance == null) return "no PeopleView";
            if (pause == Instance.ClockPaused) return "clock " + (pause ? "paused" : "running");
            if (pause) Instance.pauseWas = p.timeScale;
            p.timeScale = pause ? 0f : Instance.pauseWas;
            Instance.ClockPaused = pause;
            return "clock " + (pause ? "paused" : "running");
        }

        public static string Status()
        {
            if (Instance == null) return "no DayLightDriver";
            var v = Instance.Logic.Current;
            return Instance.Logic.Drives
                ? $"light {Instance.Logic.Look}: shown {Instance.Logic.ShownHour:0.0} h, sun {v.SunIntensity:0.00}, lamps {v.Lamp:0.00} ({Instance.box?.LampCount} spots{(Instance.FlickerHeld ? ", held" : "")}), floor {Instance.Logic.NightFloor:0.00}"
                : "light Today: the scene's own";
        }
    }
}
