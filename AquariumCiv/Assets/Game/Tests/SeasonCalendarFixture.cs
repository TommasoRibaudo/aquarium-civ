using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#603: the sim tests were measured on the old calendar (a new box's month 0 is winter), so every box they build keeps it.
    /// SpringStartTests (AquariumCiv.Life.Tests) covers the new calendar by setting the default back for itself.</summary>
    [SetUpFixture]
    public class SeasonCalendarFixture
    {
        int was;
        [OneTimeSetUp] public void Pin() { was = SeasonCalendar.NewBoxShift; SeasonCalendar.NewBoxShift = 0; }
        [OneTimeTearDown] public void Restore() { SeasonCalendar.NewBoxShift = was; SeasonCalendar.Shift = 0; }
    }
}
