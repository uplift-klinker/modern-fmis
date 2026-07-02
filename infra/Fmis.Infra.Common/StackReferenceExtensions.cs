using Pulumi;

namespace Fmis.Infra.Common;

public static class StackReferenceExtensions
{
    public static Output<string> RequireString(this StackReference reference, string outputName, string previewFallback)
    {
        if (Deployment.Instance.IsDryRun)
            return Output.Create(previewFallback);
        return reference.RequireString(outputName);
    }

    public static Output<string> RequireString(this StackReference reference, string outputName)
    {
        return reference.GetOutput(outputName).Apply(value =>
        {
            if (value is not null)
                return value.ToString()!;
            if (Deployment.Instance.IsDryRun)
                return string.Empty;
            throw new InvalidOperationException(
                $"The referenced stack has not published output '{outputName}'; deploy the upstream stack first.");
        });
    }
}
