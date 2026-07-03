using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Application.Components;

public sealed class FrontendSite : ComponentResource
{
    private readonly Input<string> resourceGroupName;
    private readonly AzureNative.Storage.StorageAccount account;

    public Output<string> Url { get; }
    public Output<string> ConfigJson { get; private set; }

    public FrontendSite(
        string name,
        Input<string> resourceGroupName,
        string location,
        ComponentResourceOptions? options = null)
        : base("fmis:application:FrontendSite", name, options)
    {
        this.resourceGroupName = resourceGroupName;

        var childOptions = new CustomResourceOptions { Parent = this };

        account = new AzureNative.Storage.StorageAccount(name, new AzureNative.Storage.StorageAccountArgs
        {
            ResourceGroupName = resourceGroupName,
            AccountName = name,
            Location = location,
            Sku = new AzureNative.Storage.Inputs.SkuArgs { Name = AzureNative.Storage.SkuName.Standard_LRS },
            Kind = AzureNative.Storage.Kind.StorageV2,
            AllowSharedKeyAccess = true,
        }, childOptions);

        new AzureNative.Storage.StorageAccountStaticWebsite($"{name}-static", new AzureNative.Storage.StorageAccountStaticWebsiteArgs
        {
            ResourceGroupName = resourceGroupName,
            AccountName = account.Name,
            IndexDocument = "index.html",
            Error404Document = "index.html",
        }, childOptions);

        if (!Deployment.Instance.IsDryRun)
        {
            new Pulumi.SyncedFolder.AzureBlobFolder($"{name}-dist", new Pulumi.SyncedFolder.AzureBlobFolderArgs
            {
                Path = "../../frontend/dist",
                ResourceGroupName = resourceGroupName,
                StorageAccountName = account.Name,
                ContainerName = "$web",
            }, new ComponentResourceOptions { Parent = this });
        }

        Url = account.PrimaryEndpoints.Apply(e => e?.Web ?? "");
        ConfigJson = Output.Create("");

        RegisterOutputs();
    }

    public void WriteConfig(
        Input<string> backendUrl,
        Input<string> authDomain,
        Input<string> spaClientId,
        Input<string> audience)
    {
        var json = Output.Format($"{{\"apiBaseUrl\":\"{backendUrl}\",\"auth\":{{\"domain\":\"{authDomain}\",\"clientId\":\"{spaClientId}\",\"audience\":\"{audience}\"}}}}");
        ConfigJson = json;
        if (!Deployment.Instance.IsDryRun)
        {
            new AzureNative.Storage.Blob("config.json", new AzureNative.Storage.BlobArgs
            {
                ResourceGroupName = resourceGroupName,
                AccountName = account.Name,
                ContainerName = "$web",
                BlobName = "config.json",
                ContentType = "application/json",
                Source = json.Apply(c => (AssetOrArchive)new StringAsset(c)),
            }, new CustomResourceOptions { Parent = this });
        }
    }
}
