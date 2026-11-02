using AquariumCiv.GameUI;
using AquariumCiv.View.Moments;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// What a moment says on screen, for the era moment and the last one alike: the hint, one bubble and one caption, on the Keeper's
    /// Glass components, named <c>{prefix}-hint</c>, <c>-bubble</c> and <c>-caption</c>. The bubble asks <see cref="GameUiPanel.Bubbles"/>
    /// for the word: a moment's own lines take it, a line said outside one waits for it. A line too long for a bubble takes the caption
    /// (spec §3). Positions that depend on the safe area are set here; the look is in Glass.uss.
    /// </summary>
    public sealed class MomentOverlay
    {
        /// <summary>The board's hurry hint, up from the bottom edge, panel px (46 pt).</summary>
        public const float HintBottom = 126.5f;
        const float CaptionTop = 143f, CaptionUnderHud = 275f, CaptionUnderNotch = 16f;

        readonly BubbleHost.Voice momentVoice, lineVoice;
        BubbleHost.Voice speaking;
        bool bubbleUp, hintUp, captionUp, captionIsLine;
        float lineUntil = -1f, waitUntil = -1f, waitSeconds;
        string waitWho, waitLine;
        bool waitUnderHud;

        public GlassHint Hint { get; }
        public GlassBubble Bubble { get; }
        public GlassCaption Caption { get; }

        /// <summary>A said line or a moment's line is up, in the bubble or the caption.</summary>
        public bool LineUp => bubbleUp || captionIsLine;

        /// <param name="root">The whole screen: the caption keeps its own lane under the notch.</param>
        /// <param name="safe">Inside the safe area: the hint and the bubble.</param>
        public MomentOverlay(VisualElement root, VisualElement safe, string prefix)
        {
            momentVoice = GameUiPanel.Bubbles.Join(BubbleHost.Rank.Moment, Yielded);
            lineVoice = GameUiPanel.Bubbles.Join(BubbleHost.Rank.Line, Yielded);
            Caption = new GlassCaption("") { name = prefix + "-caption" };
            Caption.style.display = DisplayStyle.None;
            root.Add(Caption);
            Bubble = new GlassBubble("", "") { name = prefix + "-bubble" };
            Bubble.AddToClassList("kg-bubble--world");
            Bubble.AddToClassList("moment-bubble");
            Bubble.AddToClassList(GlassMotion.FadeClass);
            Bubble.style.display = DisplayStyle.None;
            safe.Add(Bubble);
            Hint = new GlassHint(MomentDirector.HurryHint) { name = prefix + "-hint" };
            Hint.AddToClassList("moment-hint");
            Hint.style.display = DisplayStyle.None;
            safe.Add(Hint);
        }

        public void ApplySafe(Vector4 insets) => Hint.style.bottom = GameUiPanel.Lane(HintBottom, insets.w);

        void Yielded()
        {
            speaking = null;
            lineUntil = -1f;
            if (!bubbleUp) return;
            bubbleUp = false;
            GlassMotion.Hide(Bubble, GlassMotion.FadeOff, GlassMotion.FadeMs);
        }

        bool Show(BubbleHost.Voice voice, string who, string line, bool underHud)
        {
            if (GlassBubble.TooLong(line))
            {
                HideBubble();
                SetCaption(line, who, underHud);
                captionIsLine = true;
                return true;
            }
            if (!GameUiPanel.Bubbles.Ask(voice)) return false;
            if (captionIsLine) HideCaption();
            speaking = voice;
            Bubble.Set(who, line);
            if (!bubbleUp) { bubbleUp = true; GlassMotion.Show(Bubble, GlassMotion.FadeOff); }
            return true;
        }

        /// <summary>A moment's own line: it takes the word. The owner times it on the moment's clock and calls <see cref="HideBubble"/>.</summary>
        public void SayMoment(string who, string line)
        {
            lineUntil = waitUntil = -1f;
            Show(momentVoice, who, line, false);
        }

        /// <summary>A line outside a moment, for <paramref name="seconds"/> real seconds. If someone has the word it waits, that long at most.</summary>
        public void SayLine(string who, string line, float seconds, bool underHud)
        {
            if (Show(lineVoice, who, line, underHud)) { lineUntil = Time.unscaledTime + seconds; return; }
            waitWho = who; waitLine = line; waitSeconds = seconds; waitUnderHud = underHud;
            waitUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Every frame: a said line's time, and a waiting line's turn.</summary>
        public void Tick()
        {
            float now = Time.unscaledTime;
            if (lineUntil > 0f && now >= lineUntil) HideBubble();
            if (waitUntil < 0f) return;
            if (now >= waitUntil) { waitUntil = -1f; return; }
            if (!LineUp && Show(lineVoice, waitWho, waitLine, waitUnderHud))
            {
                lineUntil = now + waitSeconds;
                waitUntil = -1f;
            }
        }

        public void HideBubble()
        {
            lineUntil = waitUntil = -1f;
            if (speaking != null) GameUiPanel.Bubbles.Done(speaking);
            speaking = null;
            if (captionIsLine) HideCaption();
            if (!bubbleUp) return;
            bubbleUp = false;
            GlassMotion.Hide(Bubble, GlassMotion.FadeOff, GlassMotion.FadeMs);
        }

        /// <summary>A line on the caption plate, with the HUD down or under its top row.</summary>
        public void SetCaption(string text, string who = null, bool underHud = false)
        {
            captionIsLine = false;
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            Caption.UnderHud = underHud;
            Caption.style.top = Mathf.Max(underHud ? CaptionUnderHud : CaptionTop, i.y + CaptionUnderNotch);
            Caption.Set(text, who);
            if (captionUp) return;
            captionUp = true;
            GlassMotion.Show(Caption, GlassMotion.CaptionOff, null, GlassMotion.CaptionInMs);
        }

        public void HideCaption()
        {
            captionIsLine = false;
            if (!captionUp) return;
            captionUp = false;
            GlassMotion.Hide(Caption, GlassMotion.CaptionOff, GlassMotion.CaptionOutMs);
        }

        /// <summary>The hint on or off, with these words if given.</summary>
        public void SetHint(bool on, string word = null)
        {
            if (word != null && Hint.Word != word) Hint.Word = word;
            if (on == hintUp) return;
            hintUp = on;
            if (on) GlassMotion.Show(Hint, GlassMotion.FadeOff);
            else GlassMotion.Hide(Hint, GlassMotion.FadeOff, GlassMotion.FadeMs);
        }

        /// <summary>Everything down: the moment is over.</summary>
        public void Clear()
        {
            HideBubble();
            HideCaption();
            SetHint(false);
        }

        /// <summary>The view is going: it leaves the word free.</summary>
        public void Release()
        {
            GameUiPanel.Bubbles.Done(momentVoice);
            GameUiPanel.Bubbles.Done(lineVoice);
        }
    }
}
