using Honua.Admin.Models.AppBuilder;

namespace Honua.Admin.Services.AppBuilder;

public interface IAppBuilderClient
{
    Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken);
}
