using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Identity.Components;

public sealed class RegistryAccess : ComponentResource
{
    private const string AcrPull = "/providers/Microsoft.Authorization/roleDefinitions/7f951dda-4ed3-4680-a7ca-43fe172d538d";
    private const string AcrPush = "/providers/Microsoft.Authorization/roleDefinitions/8311e382-0749-4cb8-b61a-304f252e45ec";

    public RegistryAccess(string name, Input<string> acrId, Input<string> identityPrincipalId, Input<string> deployerPrincipalId, ComponentResourceOptions? options = null)
        : base("fmis:identity:RegistryAccess", name, options)
    {
        Assign($"{name}-pull", acrId, AcrPull, identityPrincipalId);
        Assign($"{name}-push", acrId, AcrPush, deployerPrincipalId);
        RegisterOutputs();
    }

    private void Assign(string name, Input<string> scope, string roleDefinitionId, Input<string> principalId)
    {
        var assignmentName = Output.Tuple(scope.ToOutput(), principalId.ToOutput())
            .Apply(t => DeterministicName(t.Item1, t.Item2, roleDefinitionId));
        _ = new AzureNative.Authorization.RoleAssignment(name, new AzureNative.Authorization.RoleAssignmentArgs
        {
            RoleAssignmentName = assignmentName,
            RoleDefinitionId = roleDefinitionId,
            PrincipalId = principalId,
            PrincipalType = AzureNative.Authorization.PrincipalType.ServicePrincipal,
            Scope = scope,
        }, new CustomResourceOptions { Parent = this });
    }

    private static string DeterministicName(string scope, string principalId, string role)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{scope}|{principalId}|{role}"));
        return new Guid(hash[..16]).ToString();
    }
}
