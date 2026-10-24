using UnrealFrontiersLauncher;

public class SiteLinksTests
{
    [Theory]
    [InlineData("https://unrealfrontiers.com", "https://unrealfrontiers.com")]
    [InlineData("https://dev.unrealfrontiers.com/", "https://dev.unrealfrontiers.com")]
    [InlineData("http://localhost:3200", "http://localhost:8180")]
    [InlineData("http://26.86.1.245:3200", "http://26.86.1.245:8180")]
    public void SiteUrl_MapsTheDevApiPortToTheSite(string server, string expected) => Assert.Equal(expected, SiteLinks.SiteUrl(server));

    [Theory]
    [InlineData("https://unrealfrontiers.com", "https://unrealfrontiers.com/support")]
    [InlineData("https://dev.unrealfrontiers.com/", "https://dev.unrealfrontiers.com/support")]
    [InlineData("http://localhost:3200", "http://localhost:8180/support")]
    public void SupportUrl_IsThePlayersTicketPageOnTheSite(string server, string expected) => Assert.Equal(expected, SiteLinks.SupportUrl(server));
}
