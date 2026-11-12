using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Spikes.Growth.Tests
{
    /// <summary>#603: the growth lab's numbers were measured on the old calendar (a new box's month 0 is winter), like the sim and
    /// life tests (SeasonCalendarFixture, LifeSeasonCalendarFixture), so every box it builds keeps it. Without this pin a spring
    /// start moved Criterion5's largest emergent path from >= 5 to 2 (PR #611 CI). The live check on the new calendar is
    /// SettleTests.PathsEmergeThatNoConnectionLaid, which passes on both.</summary>
    [SetUpFixture]
    public class GrowthSeasonCalendarFixture
    {
        int was;
        [OneTimeSetUp] public void Pin() { was = SeasonCalendar.NewBoxShift; SeasonCalendar.NewBoxShift = 0; }
        [OneTimeTearDown] public void Restore() { SeasonCalendar.NewBoxShift = was; SeasonCalendar.Shift = 0; }
    }
}
