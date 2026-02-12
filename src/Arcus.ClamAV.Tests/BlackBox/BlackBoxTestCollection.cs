namespace Arcus.ClamAV.Tests.BlackBox;

/// <summary>
/// xUnit collection definition for BlackBox tests.
/// All tests in this collection will share the same DockerComposeFixture instance,
/// meaning containers are started once before any tests run and stopped after all tests complete.
/// </summary>
[CollectionDefinition("BlackBox Tests")]
public class BlackBoxTestCollection : ICollectionFixture<DockerComposeFixture>
{
    // This class has no code - it exists only to define the collection
    // and apply the ICollectionFixture<> interface to it.
}
