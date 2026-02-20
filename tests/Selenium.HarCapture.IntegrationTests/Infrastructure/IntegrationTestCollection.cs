namespace Selenium.HarCapture.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<TestWebServer>
{
    public const string Name = "Integration";
}
