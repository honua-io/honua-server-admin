// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Common honua-server admin response envelope.
/// </summary>
public sealed record AdminApiResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("data")] public T? Data { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; init; }

    public static AdminApiResponse<T> CreateSuccess(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message,
        Timestamp = DateTimeOffset.UtcNow,
    };
}
