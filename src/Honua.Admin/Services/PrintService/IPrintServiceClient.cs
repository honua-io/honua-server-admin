// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.PrintService;

namespace Honua.Admin.Services.PrintService;

public interface IPrintServiceClient
{
    Task<PrintServiceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<PrintPreviewDocument> PreviewAsync(PrintJobRequest request, CancellationToken cancellationToken);

    Task<PrintJobSummary> QueueExportAsync(PrintJobRequest request, CancellationToken cancellationToken);
}
