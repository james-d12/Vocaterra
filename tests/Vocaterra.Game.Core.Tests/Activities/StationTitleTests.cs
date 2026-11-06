using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>The station panel's title is the station's name (W07b, design 10.16): its locale label, else its noun with the article, else its id.</summary>
public sealed class StationTitleTests
{
    [Fact]
    public void ACatalogueNounStationIsTitledWithItsNounAndArticle()
    {
        var rig = new StationRig();
        Assert.Equal("la madera", StationScreen.TitleOf(rig.Session, "madera")); // the catalogue's noun with its article
    }

    [Fact]
    public void TheOpenPanelIsTitledWithTheStationNotWithTheWordStation()
    {
        var rig = new StationRig();
        Assert.True(rig.Panel.Open("horno"));
        Assert.Equal("horno", rig.View.Title); // the id: this test catalogue has no noun for the oven
        Assert.NotEqual("Station", rig.View.Title);
    }

    [Fact]
    public void ALocaleLabelWinsOverTheNoun()
    {
        var locale = TestGame.Locale.With(new Dictionary<string, string>(StringComparer.Ordinal) { ["ui.station.horno"] = "The bread oven" });
        var rig = new StationRig(locale: locale);
        Assert.True(rig.Panel.Open("horno"));
        Assert.Equal("The bread oven", rig.View.Title);
    }

    [Fact]
    public void AStationWhoseIdIsNoNounIsTitledWithItsId()
    {
        var rig = new StationRig();
        Assert.Equal("fogon", StationScreen.TitleOf(rig.Session, "fogon"));
    }
}
