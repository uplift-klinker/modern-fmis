namespace Fmis.Infra.Common;

public static class ResourceNames
{
    public static string For(string environment, string layer, string resource)
        => $"fmis-{environment}-{layer}-{resource}";

    public static string Audience(string environment)
        => $"https://{environment}.api.modern-fmis";
}
