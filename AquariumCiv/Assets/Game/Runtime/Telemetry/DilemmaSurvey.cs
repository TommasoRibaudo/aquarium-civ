namespace AquariumCiv.Game
{
    /// <summary>
    /// P7 (#225): the words of the dilemma test, in one place for the survey and the recall sheet. Source: docs/design/results/P7.md
    /// "Measures". The slice has no survey form of its own: the prototype <c>SurveyPanel</c> reads Q4/Q6 from <c>PrototypeConfig</c>
    /// (<c>surveyQ4</c>, <c>surveyExtra</c>); whoever builds the P7 survey passes these strings.
    /// </summary>
    public static class DilemmaSurvey
    {
        public const string VariantOff = "A"; // dilemmas off
        public const string VariantOn = "B";  // dilemmas on

        /// <summary>Likert 1-5 (disagree ... agree).</summary>
        public const string Q4 = "What happened in the box felt like my doing";
        /// <summary>Free text (the survey's "extra" question).</summary>
        public const string Q6 = "Did any question feel like it had a wrong answer, or like homework?";
        /// <summary>The day-2 recall prompt. The answer is free text, so it goes to the tester's survey, never to the telemetry log.</summary>
        public const string RecallQuestion = "Name one choice you made for the people, and what came of it.";

        public static string VariantOf(bool dilemmasOn) => dilemmasOn ? VariantOn : VariantOff;
    }
}
