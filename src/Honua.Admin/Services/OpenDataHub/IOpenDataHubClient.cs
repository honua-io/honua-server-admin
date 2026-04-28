using Honua.Admin.Models.OpenDataHub;

namespace Honua.Admin.Services.OpenDataHub;

public interface IOpenDataHubClient
{
    Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken);
}
