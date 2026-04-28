// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Admin.Models.Admin;

namespace Honua.Admin.Services.Admin;

public static class AdminRealtimeReducers
{
    private const int DefaultRecentErrorCapacity = 20;

    public static RecentErrorsResponse AddRecentError(RecentErrorsResponse? current, RecentErrorEntry entry)
    {
        var capacity = current?.Capacity > 0 ? current.Capacity : DefaultRecentErrorCapacity;
        var instanceId = string.IsNullOrWhiteSpace(current?.InstanceId)
            ? "realtime"
            : current!.InstanceId;

        var errors = new List<RecentErrorEntry>(capacity) { entry };
        if (current is not null)
        {
            errors.AddRange(current.Errors.Where(existing => !SameError(existing, entry)));
        }

        return new RecentErrorsResponse
        {
            Capacity = capacity,
            InstanceId = instanceId,
            Errors = errors
                .OrderByDescending(error => error.Timestamp)
                .Take(capacity)
                .ToArray()
        };
    }

    private static bool SameError(RecentErrorEntry left, RecentErrorEntry right)
    {
        if (!string.IsNullOrWhiteSpace(left.CorrelationId) ||
            !string.IsNullOrWhiteSpace(right.CorrelationId))
        {
            return string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.OrdinalIgnoreCase);
        }

        return left.Timestamp == right.Timestamp &&
            left.StatusCode == right.StatusCode &&
            string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Message, right.Message, StringComparison.Ordinal);
    }
}
