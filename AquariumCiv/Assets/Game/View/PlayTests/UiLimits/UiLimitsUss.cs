namespace AquariumCiv.UiLimits
{
    /// <summary>The spike's style sheets and icons, written to a temporary Assets folder by the test and deleted after it.</summary>
    static class UiLimitsUss
    {
        public const string Main = @"
.fill { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }
.page { position: absolute; left: 0; top: 0; right: 0; bottom: 0; padding: 40px 28px 28px 28px; }
.tag { font-size: 26px; color: #FFFFFF; background-color: rgba(0,0,0,0.65); padding: 2px 10px; align-self: flex-start; margin-top: 16px; margin-bottom: 6px; white-space: normal; }
.card { background-color: #EFE3CC; border-radius: 32px; padding: 22px; }
.ink { color: #6A4B36; font-size: 38px; white-space: normal; }
.lrow { flex-direction: row; align-items: center; flex-wrap: wrap; }
.chip { background-color: #C66F4E; color: #FFF8EC; font-size: 34px; padding: 10px 24px; border-radius: 40px; }
.sw { width: 240px; height: 150px; margin-right: 16px; margin-bottom: 8px; }

/* shadows */
.shadow-host { width: 760px; height: 130px; margin: 10px 40px 22px 40px; }
.shadow-hard { position: absolute; left: 10px; top: 14px; right: -10px; bottom: -14px; border-radius: 32px; background-color: rgba(30,26,23,0.45); }
.shadow-blur { position: absolute; left: 0; top: 16px; right: 0; bottom: -16px; border-radius: 32px; background-color: rgba(30,26,23,0.75); filter: blur(22px); }
.shadow-9 { position: absolute; left: -60px; top: -44px; right: -60px; bottom: -76px; -unity-slice-left: 96; -unity-slice-right: 96; -unity-slice-top: 96; -unity-slice-bottom: 96; }
.shadow-host .card { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }

.sb-a { position: absolute; left: -16px; top: 6px; right: -16px; bottom: -34px; border-radius: 40px; background-color: rgba(30,26,23,0.8); filter: blur(18px); }
.sg-shadow { position: absolute; left: -60px; top: -44px; right: -60px; bottom: -76px; background-color: #FFFFFF; }

/* blur */
.blur-self { filter: blur(6px); }
.glass-flat { background-color: rgba(239,227,204,0.55); border-radius: 32px; padding: 22px; border-width: 2px; border-color: rgba(255,255,255,0.5); }
.frost { height: 300px; border-radius: 36px; overflow: hidden; margin-bottom: 4px; }
.frost-inner { position: absolute; left: -48px; top: -48px; right: -48px; bottom: -48px; overflow: hidden; }
.frost-img { position: absolute; -unity-background-scale-mode: stretch-to-fill; }
.frost-tint { position: absolute; left: 0; top: 0; right: 0; bottom: 0; background-color: rgba(239,227,204,0.42); border-radius: 36px; border-width: 2px; border-color: rgba(255,255,255,0.55); }
.frost-text { padding: 26px; color: #4A3425; font-size: 38px; -unity-font-style: bold; white-space: normal; }

/* gradient, masks, borders */
.mask-r { width: 300px; height: 190px; border-radius: 48px; overflow: hidden; margin-right: 16px; margin-bottom: 8px; }
.mask-c { width: 190px; height: 190px; border-radius: 95px; overflow: hidden; margin-right: 16px; margin-bottom: 8px; }
.mask-img { position: absolute; left: -40px; top: -40px; width: 420px; height: 300px; -unity-background-scale-mode: stretch-to-fill; }
.mask-nested { position: absolute; left: 30px; top: 30px; right: 30px; bottom: 30px; border-radius: 30px; overflow: hidden; }
.b-uneven { width: 300px; height: 150px; background-color: #EFE3CC; border-top-width: 2px; border-right-width: 6px; border-bottom-width: 14px; border-left-width: 6px; border-color: #6A4B36; border-radius: 28px; margin-right: 16px; margin-bottom: 8px; }
.b-colors { width: 300px; height: 150px; background-color: #EFE3CC; border-width: 12px; border-top-color: #E9D8A6; border-left-color: #E9D8A6; border-right-color: #6A4B36; border-bottom-color: #6A4B36; border-radius: 28px; margin-right: 16px; margin-bottom: 8px; }
.b-radii { width: 300px; height: 150px; background-color: #EFE3CC; border-width: 4px; border-color: #6A4B36; border-top-left-radius: 60px; border-top-right-radius: 8px; border-bottom-right-radius: 60px; border-bottom-left-radius: 8px; margin-right: 16px; margin-bottom: 8px; }
.b-pill-bg { width: 300px; height: 150px; border-radius: 75px; margin-right: 16px; margin-bottom: 8px; -unity-background-scale-mode: stretch-to-fill; border-width: 4px; border-color: #6A4B36; }

/* text */
.t { font-size: 44px; color: #FFF8EC; margin-bottom: 4px; white-space: normal; }
.t-sm { font-size: 34px; color: #FFF8EC; white-space: normal; }
.o1 { -unity-text-outline-width: 1px; -unity-text-outline-color: #2E2A27; }
.o2 { -unity-text-outline-width: 2px; -unity-text-outline-color: #2E2A27; }
.o4 { -unity-text-outline-width: 4px; -unity-text-outline-color: #2E2A27; }
.s0 { text-shadow: 2px 2px 0 rgba(0,0,0,0.9); }
.s4 { text-shadow: 2px 3px 4px rgba(0,0,0,0.9); }
.s12 { text-shadow: 0 2px 12px rgba(0,0,0,1); }
.ls-m { letter-spacing: -2px; }
.ls-p { letter-spacing: 6px; }
.ws { word-spacing: 24px; }
.para { -unity-paragraph-spacing: 30px; }
.nowrap { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; width: 500px; }
.pre { white-space: pre; }
.prewrap { white-space: pre-wrap; width: 500px; }
.autosize { -unity-text-auto-size: best-fit 16px 60px; width: 500px; height: 60px; white-space: nowrap; }
.bold { -unity-font-style: bold; }
.italic { -unity-font-style: italic; }
.bolditalic { -unity-font-style: bold-and-italic; }
.neg-line { margin-top: -10px; }

/* icons */
.icon { width: 121px; height: 121px; margin-right: 18px; margin-bottom: 8px; }
.icon-sm { width: 66px; height: 66px; margin-right: 18px; margin-bottom: 8px; }
.icon-xl { width: 240px; height: 240px; margin-right: 18px; margin-bottom: 8px; }
.tint-a { -unity-background-image-tint-color: #C66F4E; }
.tint-b { -unity-background-image-tint-color: #5E7F43; }
.tint-c { -unity-background-image-tint-color: #2E2A27; }

/* motion */
.sheet { position: absolute; left: 0; right: 0; bottom: 0; height: 30%; background-color: #EFE3CC; border-top-left-radius: 48px; border-top-right-radius: 48px; padding: 28px 36px; }
.scrim { position: absolute; left: 0; top: 0; right: 0; bottom: 0; background-color: rgba(30,26,23,0.45); }
.slide { transition: translate 1200ms ease-out, opacity 1200ms linear, scale 1200ms ease-out; }
.slide-out { translate: 0 100%; opacity: 0; }
.fade { transition: opacity 1200ms linear; }
.fade-out { opacity: 0; }
.tr { transition: all 400ms linear; position: absolute; left: 0; top: 0; width: 100px; height: 100px; background-color: #336699; color: #FFFFFF; opacity: 1; }

/* layout */
.gap-row { flex-direction: row; gap: 40px; }
.first-test:first-child { background-color: #5E7F43; }
.last-test:last-child { background-color: #5E7F43; }
.m-row { flex-direction: row; }
.m-row > .chip { margin-right: 40px; }
.safe-box { position: absolute; border-width: 3px; border-color: #30FF90; }
.ratio { width: 300px; aspect-ratio: 1.5; background-color: #CFA65E; }
.backing { background-color: #4E6B38; padding: 8px 18px; border-radius: 16px; }
.scroll { flex-grow: 1; margin-top: 12px; }

/* tactile */
.paper { width: 900px; height: 420px; -unity-slice-left: 70; -unity-slice-right: 70; -unity-slice-top: 70; -unity-slice-bottom: 70; padding: 56px; }
.paper-sm { width: 420px; height: 150px; -unity-slice-left: 70; -unity-slice-right: 70; -unity-slice-top: 70; -unity-slice-bottom: 70; padding: 36px; margin-right: 18px; }
.paper-tiled { -unity-slice-type: tiled; }
.brass { width: 420px; height: 170px; -unity-slice-left: 48; -unity-slice-right: 48; -unity-slice-top: 48; -unity-slice-bottom: 48; align-items: center; justify-content: center; margin-right: 18px; margin-bottom: 8px; }
.brass-wide { width: 640px; }
.brass-t { font-size: 44px; color: #3A2A14; -unity-font-style: bold; text-shadow: 0 2px 0 rgba(255,240,190,0.7); }
.portrait-ring { width: 220px; height: 220px; border-radius: 110px; border-width: 10px; border-color: #B08A3E; overflow: hidden; margin-right: 22px; }
.portrait-img { position: absolute; left: 0; top: 0; right: 0; bottom: 0; -unity-background-scale-mode: scale-and-crop; }

/* cost */
.c-sheet { position: absolute; left: 0; right: 0; bottom: 0; height: 30%; border-top-left-radius: 48px; border-top-right-radius: 48px; padding: 28px 36px; }
.c-solid { background-color: #EFE3CC; }
.c-mask { overflow: hidden; }
.c-fx { -unity-text-outline-width: 2px; -unity-text-outline-color: #2E2A27; text-shadow: 2px 3px 4px rgba(0,0,0,0.9); }
.c-shadow-blur { position: absolute; left: -16px; right: -16px; bottom: -34px; height: 31%; background-color: rgba(30,26,23,0.8); border-radius: 56px; filter: blur(18px); }
.c-shadow-9 { position: absolute; left: -60px; right: -60px; bottom: -80px; height: 36%; -unity-slice-left: 96; -unity-slice-right: 96; -unity-slice-top: 96; -unity-slice-bottom: 96; }
.c-blur { filter: blur(8px); }
.c-loop { transition: translate 300ms linear, opacity 300ms linear; }
.c-loop-out { translate: 0 60px; opacity: 0.5; }
";

        /// <summary>CSS the designers will reach for. Read the importer's warnings: anything here that USS refuses is logged.</summary>
        public const string Probe = @"
.p-gap { gap: 12px; }
.p-rowgap { row-gap: 4px; }
.p-lineheight { line-height: 1.5; }
.p-transform { text-transform: uppercase; }
.p-boxshadow { box-shadow: 0 4px 8px black; }
.p-backdrop { backdrop-filter: blur(10px); }
.p-dropshadow { filter: drop-shadow(0 4px 8px black); }
.p-lineargrad { background-image: linear-gradient(red, blue); }
.p-radialgrad { background: radial-gradient(red, blue); }
.p-decoration { text-decoration: underline; }
.p-weight { font-weight: bold; }
.p-style { font-style: italic; }
.p-blend { mix-blend-mode: multiply; }
.p-clippath { clip-path: circle(50%); }
.p-grid { display: grid; }
.p-calc { width: calc(100% - 20px); }
.p-pct-font { font-size: 120%; }
.p-rem { font-size: 2rem; }
.p-vw { width: 50vw; }
.p-keyframes { animation: pulse 1s; }
.p-ok-blur { filter: blur(4px); }
.p-ok-gray { filter: grayscale(1) sepia(0.5) contrast(1.2) hue-rotate(20deg) invert(0.1) opacity(0.5); }
.p-ok-tint { filter: tint(#FF0000); }
.p-first:first-child { color: red; }
.p-last:last-child { color: red; }
.p-child > .p-x { color: red; }
.p-hover:hover { color: red; }
.p-before::before { color: red; }
";

        public const string ProbeNth = @".a { color: red; }
.p-nth:nth-child(2) { color: red; }
.b { color: blue; }
";
        public const string ProbeNot = @".a { color: red; }
.p-not:not(.x) { color: red; }
.b { color: blue; }
";

        public const string Svg = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
<path fill='#FFFFFF' d='M12 2.2l2.9 6.3 6.9.8-5.1 4.7 1.4 6.8L12 16.6l-6.1 3.5 1.4-6.8L2.2 9.3l6.9-.8z'/></svg>";

        public const string SvgStroke = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
<path fill='none' stroke='#FFFFFF' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round' d='M4 20c0-9 6-15 16-16 0 10-5 16-13 16M4 20c3-5 6-8 10-10'/></svg>";

        public const string SvgGradient = @"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>
<defs><linearGradient id='g' x1='0' y1='0' x2='0' y2='1'><stop offset='0' stop-color='#F2D49B'/><stop offset='1' stop-color='#8A5A2B'/></linearGradient></defs>
<rect x='0' y='0' width='24' height='24' rx='5' fill='url(#g)'/></svg>";
    }
}
