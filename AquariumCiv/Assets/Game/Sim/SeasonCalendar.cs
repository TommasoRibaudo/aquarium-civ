namespace AquariumCiv.Sim
{
    /// <summary>
    /// #603 (answers #576): where the seasons fall on the month count. The season of a month is (month + shift) % 12 with winter 11, 0, 1,
    /// spring 2-4, summer 5-7, autumn 8-10. A new box has shift 2, so its month 0 is the first month of spring; a box saved before has
    /// shift 0 (the field is missing in its save) and keeps the seasons it had. Years, history and the sim clock count raw months,
    /// untouched. The shift is the box's own (<see cref="SimState.seasonShift"/>); <see cref="Shift"/> mirrors the box in play for the
    /// static readers (the view, the planner, the land), and <see cref="SimCore"/> sets it whenever it takes a box.
    /// </summary>
    public static class SeasonCalendar
    {
        public const int DefaultNewBoxShift = 2;

        /// <summary>The shift a new box gets. The game keeps the default; the sim test assemblies set 0 for the whole run (their timelines
        /// and pinned numbers were measured on the old calendar), and SpringStartTests sets the default back for its own tests.</summary>
        public static int NewBoxShift = DefaultNewBoxShift;

        /// <summary>The box in play's shift (set by SimCore; one box plays at a time).</summary>
        public static int Shift;

        /// <summary>The month of the calendar year, 0..11 (2 = first of spring), for a sim month or a view day.</summary>
        public static int OfMonth(long month, int shift) => (int)((((month + shift) % 12) + 12) % 12);
        public static int OfMonth(long month) => OfMonth(month, Shift);
    }
}
