using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Source-generated serializer context for every data-connection DTO. Required
/// for AOT / trim-friendly WebAssembly publishing — reflection-based JSON
/// would break trimming.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DataConnectionSummary))]
[JsonSerializable(typeof(DataConnectionSummary[]))]
[JsonSerializable(typeof(IReadOnlyList<DataConnectionSummary>))]
[JsonSerializable(typeof(DataConnectionDetail))]
[JsonSerializable(typeof(CreateConnectionRequest))]
[JsonSerializable(typeof(UpdateConnectionRequest))]
[JsonSerializable(typeof(ConnectionTestOutcome))]
[JsonSerializable(typeof(ProblemDetailsPayload))]
public sealed partial class DataConnectionsJsonContext : JsonSerializerContext
{
}
