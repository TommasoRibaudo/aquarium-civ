using AquariumCiv.Life;
using NUnit.Framework;

namespace AquariumCiv.View.Tests
{
    /// <summary>#644 (playtest, the user 2026-11-13): a sown field follows the season; winter fields are bare.</summary>
    public class FieldSeasonTests
    {
        [Test]
        public void Winter_SownFieldsAreBare()
        {
            Assert.AreEqual(CropKind.Fallow, FieldCrop.InSeason(CropKind.Wheat, Season.Winter));
            Assert.AreEqual(CropKind.Fallow, FieldCrop.InSeason(CropKind.Sprout, Season.Winter));
        }

        [Test]
        public void Spring_Shoots_SummerAndAutumn_Wheat()
        {
            Assert.AreEqual(CropKind.Sprout, FieldCrop.InSeason(CropKind.Wheat, Season.Spring));
            Assert.AreEqual(CropKind.Wheat, FieldCrop.InSeason(CropKind.Sprout, Season.Summer));
            Assert.AreEqual(CropKind.Wheat, FieldCrop.InSeason(CropKind.Wheat, Season.Autumn));
        }

        [Test]
        public void RestingAndOtherFields_KeepTheirLook()
        {
            foreach (Season s in System.Enum.GetValues(typeof(Season)))
            {
                Assert.AreEqual(CropKind.Fallow, FieldCrop.InSeason(CropKind.Fallow, s));
                Assert.AreEqual(CropKind.Pasture, FieldCrop.InSeason(CropKind.Pasture, s));
                Assert.AreEqual(CropKind.Gathering, FieldCrop.InSeason(CropKind.Gathering, s));
            }
        }
    }
}
