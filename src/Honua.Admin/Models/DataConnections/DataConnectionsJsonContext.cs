using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Source-generated serializer context for every data-connection DTO. Required
/// for AOT / trim-friendly WebAssembly publishing — reflection-based JSON
/// would break trimming. Both raw DTOs (for request bodies) and
/// <see cref="ApiResponse{T}"/>-wrapped responses are registered, since
/// honua-server admin endpoints always wrap successful payloads in the
/// envelope.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DataConnectionSummary))]
[JsonSerializable(typeof(DataConnectionDetail))]
[JsonSerializable(typeof(CreateConnectionRequest))]
[JsonSerializable(typeof(UpdateConnectionRequest))]
[JsonSerializable(typeof(ConnectionTestOutcome))]
[JsonSerializable(typeof(ProblemDetailsPayload))]
[JsonSerializable(typeof(ApiResponse<DataConnectionSummary>))]
[JsonSerializable(typeof(ApiResponse<DataConnectionDetail>))]
[JsonSerializable(typeof(ApiResponse<IReadOnlyList<DataConnectionSummary>>))]
[JsonSerializable(typeof(ApiResponse<ConnectionTestOutcome>))]
[JsonSerializable(typeof(ApiResponse<object>))]
public sealed partial class DataConnectionsJsonContext : JsonSerializerContext
{
}
