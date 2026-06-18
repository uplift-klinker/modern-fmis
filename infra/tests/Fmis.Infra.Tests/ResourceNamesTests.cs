using Fmis.Infra.Common;

namespace Fmis.Infra.Tests;

public class ResourceNamesTests
{
    [Fact]
    public void For_builds_a_prefixed_env_layer_resource_name()
    {
        var name = ResourceNames.For("dev", "auth", "spa");

        Assert.Equal("fmis-dev-auth-spa", name);
    }

    [Fact]
    public void Audience_is_environment_first()
    {
        var audience = ResourceNames.Audience("dev");

        Assert.Equal("https://dev.api.modern-fmis", audience);
    }
}
