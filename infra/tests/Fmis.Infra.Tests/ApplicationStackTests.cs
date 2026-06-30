using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class ApplicationStackTests
{
    [Fact]
    public async Task Creates_a_basic_container_registry()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var registry = resources.OfType<AzureNative.ContainerRegistry.Registry>().Single();
        Assert.Equal("Basic", await InfraTesting.GetAsync(registry.Sku.Apply(s => s.Name)));
    }
}
