// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.UsageAnalytics;

namespace Honua.Admin.Services.UsageAnalytics;

/// <summary>
/// Product-analytics read model for the experimental reporting dashboard.
/// S1 ships with <see cref="StubUsageAnalyticsClient"/> so the admin workflow is
/// usable while the durable server-side aggregation API is being defined.
/// </summary>
public interface IUsageAnalyticsClient
{
    Task<UsageAnalyticsReport> GetReportAsync(UsageAnalyticsQuery query, CancellationToken cancellationToken);
}
