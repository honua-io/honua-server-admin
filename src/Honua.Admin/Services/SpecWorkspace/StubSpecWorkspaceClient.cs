using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Deterministic in-memory grounding + planning + execution simulator. S1 uses this to
/// drive every AC through the admin UI so the walking skeleton is demonstrable before
/// the real server/SDK surfaces land. A deliberately small set of regex rules covers
/// the scripted demo flow: pick a dataset, pick an aggregate op, plan, apply, cancel.
/// </summary>
public sealed partial class StubSpecWorkspaceClient : ISpecWorkspaceClient
{
    private static readonly Regex AtMentionRegex = AtRegex();
    private static readonly Regex AggregateRegex = AggRegex();
    private static readonly Regex FilterRegex = FilterRgx();
    private static readonly Regex MapRegex = MapRgx();
    private static readonly Regex BufferRegex = BufRegex();
    private static readonly Regex ClarifyPickOpRegex = OpRgx();

    private readonly Dictionary<string, CatalogDataset> _datasets;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _cancelledJobs = new(StringComparer.Ordinal);
    private SpecGrammar? _grammar;

    public StubSpecWorkspaceClient()
    {
        _datasets = BuildSeedCatalog();
    }

    public async Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = request.Prompt?.Trim() ?? string.Empty;
        if (prompt.Length == 0)
        {
            return new IntentOutcome
            {
                Kind = IntentResponseKind.Unsupported,
                Message = "Enter an intent referencing a dataset with @name and a compute verb."
            };
        }

        if (!string.IsNullOrEmpty(request.ClarificationId) && !string.IsNullOrEmpty(request.ClarificationValue))
        {
            return ApplyClarification(request);
        }

        if (prompt.Contains("crs", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("epsg:", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("wgs84", StringComparison.OrdinalIgnoreCase))
        {
            return new IntentOutcome
            {
                Kind = IntentResponseKind.Clarification,
                Message = "Scope needs an explicit CRS.",
                Clarification = new ClarificationRequest
                {
                    Id = "specify-crs:scope",
                    Kind = ClarificationKind.SpecifyCrs,
                    Question = "Which CRS should scope use?",
                    Options = new[]
                    {
                        new ClarificationOption("EPSG:4326", "EPSG:4326"),
                        new ClarificationOption("EPSG:3857", "EPSG:3857"),
                        new ClarificationOption("WGS84", "WGS84")
                    }
                }
            };
        }

        var mentions = AtMentionRegex.Matches(prompt);
        if (mentions.Count == 0)
        {
            if (prompt.Contains("dataset", StringComparison.OrdinalIgnoreCase))
            {
                return new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Pick a dataset to ground the intent.",
                    Clarification = new ClarificationRequest
                    {
                        Id = "pick-dataset:source",
                        Kind = ClarificationKind.PickDataset,
                        Question = "Which dataset did you mean?",
                        Options = _datasets.Values.Select(d => new ClarificationOption(d.Id, d.Title, d.Description)).ToArray()
                    }
                };
            }

            return new IntentOutcome
            {
                Kind = IntentResponseKind.Unsupported,
                Message = "No @dataset reference found. Try `aggregate count of @parcels by county`."
            };
        }

        var datasetId = mentions[0].Groups[1].Value.ToLowerInvariant();
        if (!_datasets.TryGetValue(datasetId, out var dataset))
        {
            return new IntentOutcome
            {
                Kind = IntentResponseKind.Clarification,
                Message = $"Dataset '{datasetId}' is not in the catalog. Pick one you can see.",
                Clarification = new ClarificationRequest
                {
                    Id = "pick-dataset:source",
                    Kind = ClarificationKind.PickDataset,
                    Question = "Which dataset did you mean?",
                    Options = _datasets.Values.Select(d => new ClarificationOption(d.Id, d.Title, d.Description)).ToArray()
                }
            };
        }

        if (AggregateRegex.IsMatch(prompt))
        {
            if (!prompt.Contains("by ", StringComparison.OrdinalIgnoreCase))
            {
                return new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Aggregate needs a grouping column.",
                    Clarification = new ClarificationRequest
                    {
                        Id = $"pick-column:aggregate:{dataset.Id}",
                        Kind = ClarificationKind.PickColumn,
                        Question = "Which column should aggregate group by?",
                        Options = dataset.Columns.Select(c => new ClarificationOption(c.Name, c.Name, c.Documentation)).ToArray()
                    }
                };
            }

            return ApplyAggregateMutation(request.CurrentSpec, dataset, prompt);
        }

        if (FilterRegex.IsMatch(prompt))
        {
            var columnMatch = Regex.Match(prompt, @"@[\w]+\.(?<column>[\w]+)");
            if (!columnMatch.Success)
            {
                return new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Filter needs a column.",
                    Clarification = new ClarificationRequest
                    {
                        Id = $"pick-column:filter:{dataset.Id}",
                        Kind = ClarificationKind.PickColumn,
                        Question = "Which column should filter use?",
                        Options = dataset.Columns.Select(c => new ClarificationOption(c.Name, c.Name, c.Documentation)).ToArray()
                    }
                };
            }

            var column = columnMatch.Groups["column"].Value;
            if (!prompt.Contains('='))
            {
                return new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Filter needs a comparison value.",
                    Clarification = new ClarificationRequest
                    {
                        Id = $"pick-value:filter:{dataset.Id}:{column}",
                        Kind = ClarificationKind.PickValue,
                        Question = $"Which value should @{dataset.Id}.{column} equal?"
                    }
                };
            }

            var value = prompt[(prompt.LastIndexOf('=') + 1)..].Trim();
            return ApplyFilterMutation(request.CurrentSpec, dataset, column, value);
        }

        if (BufferRegex.IsMatch(prompt))
        {
            var bufferMatch = BufferRegex.Match(prompt);
            var distance = bufferMatch.Groups[1].Value;
            var unit = bufferMatch.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(unit))
            {
                return new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Buffer requires a unit.",
                    Clarification = new ClarificationRequest
                    {
                        Id = $"specify-unit:buffer:{dataset.Id}:{distance}",
                        Kind = ClarificationKind.SpecifyUnit,
                        Question = "Which unit for the buffer distance?",
                        Field = distance,
                        Options = new[]
                        {
                            new ClarificationOption("m", "meters"),
                            new ClarificationOption("km", "kilometers"),
                            new ClarificationOption("mi", "miles")
                        }
                    }
                };
            }

            return ApplyBufferMutation(request.CurrentSpec, dataset, distance, unit);
        }

        if (MapRegex.IsMatch(prompt))
        {
            return ApplyMapMutation(request.CurrentSpec, dataset);
        }

        if (ClarifyPickOpRegex.IsMatch(prompt))
        {
            return new IntentOutcome
            {
                Kind = IntentResponseKind.Clarification,
                Message = "Which compute op should run?",
                Clarification = new ClarificationRequest
                {
                    Id = $"choose-op:{dataset.Id}",
                    Kind = ClarificationKind.ChooseOp,
                    Question = "Pick a compute op",
                    Options = new[]
                    {
                        new ClarificationOption("aggregate", "Aggregate (count/sum/avg)"),
                        new ClarificationOption("filter", "Filter (where predicate)"),
                        new ClarificationOption("buffer", "Buffer (geometry)"),
                        new ClarificationOption("join", "Join (on predicate)")
                    }
                }
            };
        }

        return ApplySourceOnlyMutation(request.CurrentSpec, dataset);
    }

    public Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var prefix = query.Prefix ?? string.Empty;
        IReadOnlyList<CatalogCandidate> candidates = query.Trigger switch
        {
            CatalogTrigger.AtMention => _datasets.Values
                .Where(d => d.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(d => new CatalogCandidate
                {
                    Id = d.Id,
                    Kind = CatalogCandidateKind.Dataset,
                    Label = d.Title,
                    Type = d.Kind,
                    SampleValues = d.SampleRows,
                    RbacScope = d.RbacScope,
                    CostHint = d.CostHint,
                    Documentation = d.Description
                })
                .ToArray(),
            CatalogTrigger.DotMember => query.Parent is { Length: > 0 } parent && _datasets.TryGetValue(parent.ToLowerInvariant(), out var ds)
                ? ds.Columns
                    .Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new CatalogCandidate
                    {
                        Id = $"{ds.Id}.{c.Name}",
                        Kind = CatalogCandidateKind.Column,
                        Label = c.Name,
                        Parent = ds.Id,
                        Type = c.Type,
                        SampleValues = c.SampleValues,
                        Documentation = c.Documentation
                    })
                    .ToArray()
                : System.Array.Empty<CatalogCandidate>(),
            CatalogTrigger.ParamList => ResolveParamCandidates(prefix),
            CatalogTrigger.SymbologyRamp => ResolveSymbologyCandidates(prefix),
            _ => System.Array.Empty<CatalogCandidate>()
        };

        return Task.FromResult(candidates);
    }

    public Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.Sources.Count == 0)
        {
            return Task.FromResult(new PlanResult
            {
                JobId = Guid.NewGuid().ToString("n"),
                Failed = true,
                FailureMessage = "Add at least one @dataset to the sources section before planning.",
                Warnings = new[] { new PlanWarning(null, PlanWarningSeverity.Red, "spec has no sources") }
            });
        }

        var nodes = new List<PlanNode>();
        var warnings = new List<PlanWarning>();
        var depth = 0;
        foreach (var source in document.Sources)
        {
            if (!_datasets.TryGetValue(source.Dataset.ToLowerInvariant(), out var dataset))
            {
                warnings.Add(new PlanWarning(source.Id, PlanWarningSeverity.Red, $"unknown dataset '{source.Dataset}'"));
                continue;
            }

            var nodeWarnings = new List<string>();
            if (source.Pin is null)
            {
                nodeWarnings.Add("mutable-source-no-pin");
                warnings.Add(new PlanWarning(source.Id, PlanWarningSeverity.Yellow, $"source '{source.Id}' is not pinned; results may drift"));
            }

            nodes.Add(new PlanNode
            {
                Id = source.Id,
                Op = "source",
                Depth = depth,
                EstimatedRows = dataset.EstimatedRows,
                EstimatedBytes = dataset.EstimatedRows * 128,
                EstimatedMillis = 50,
                Warnings = nodeWarnings
            });
        }

        depth++;
        foreach (var step in document.Compute)
        {
            var inputRows = step.Inputs
                .Select(i => nodes.FirstOrDefault(n => n.Id == i)?.EstimatedRows ?? 0)
                .DefaultIfEmpty(0L)
                .Max();
            var outRows = step.Op switch
            {
                "aggregate" => Math.Max(inputRows / 100, 1),
                "filter" => Math.Max(inputRows / 4, 1),
                "buffer" => inputRows,
                "join" => inputRows * 2,
                _ => inputRows
            };

            nodes.Add(new PlanNode
            {
                Id = $"{step.Op}-{depth}",
                Op = step.Op,
                Inputs = step.Inputs,
                Depth = depth,
                EstimatedRows = outRows,
                EstimatedBytes = outRows * 128,
                EstimatedMillis = step.Op == "join" ? 1200 : 300
            });
            depth++;
        }

        foreach (var layer in document.Map.Layers)
        {
            nodes.Add(new PlanNode
            {
                Id = $"map-{layer.Source}",
                Op = "map",
                Inputs = new[] { layer.Source },
                Depth = depth,
                EstimatedRows = 0,
                EstimatedBytes = 0,
                EstimatedMillis = 80
            });
        }

        return Task.FromResult(new PlanResult
        {
            JobId = Guid.NewGuid().ToString("n"),
            Nodes = nodes,
            Warnings = warnings
        });
    }

    public async IAsyncEnumerable<ApplyEvent> ApplyAsync(
        SpecDocument document,
        string jobId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _cancelledJobs.Remove(jobId);
        var plan = await PlanAsync(document, cancellationToken).ConfigureAwait(false);
        if (plan.Failed)
        {
            yield return new ApplyEvent
            {
                Kind = ApplyEventKind.Failed,
                JobId = jobId,
                Message = plan.FailureMessage
            };
            yield break;
        }

        yield return new ApplyEvent { Kind = ApplyEventKind.Started, JobId = jobId };

        var cacheIndex = 0;
        foreach (var node in plan.Nodes)
        {
            if (IsCancelled(jobId) || cancellationToken.IsCancellationRequested)
            {
                yield return new ApplyEvent
                {
                    Kind = ApplyEventKind.Cancelled,
                    JobId = jobId,
                    Message = "cancelled"
                };
                yield break;
            }

            var status = cacheIndex == 0 ? ApplyNodeStatus.Running : ApplyNodeStatus.Running;
            yield return new ApplyEvent
            {
                Kind = ApplyEventKind.NodeUpdate,
                JobId = jobId,
                NodeId = node.Id,
                NodeOp = node.Op,
                Status = status
            };

            var delayCancelled = false;
            try
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                delayCancelled = true;
            }

            if (delayCancelled || IsCancelled(jobId) || cancellationToken.IsCancellationRequested)
            {
                yield return new ApplyEvent
                {
                    Kind = ApplyEventKind.Cancelled,
                    JobId = jobId,
                    Message = "cancelled"
                };
                yield break;
            }

            var finalStatus = cacheIndex % 3 == 2 ? ApplyNodeStatus.CacheHit : ApplyNodeStatus.Completed;
            yield return new ApplyEvent
            {
                Kind = ApplyEventKind.NodeUpdate,
                JobId = jobId,
                NodeId = node.Id,
                NodeOp = node.Op,
                Status = finalStatus
            };
            cacheIndex++;
        }

        yield return new ApplyEvent
        {
            Kind = ApplyEventKind.Completed,
            JobId = jobId,
            Payload = BuildPayload(document)
        };
    }

    public Task CancelAsync(string jobId, CancellationToken cancellationToken)
    {
        _cancelledJobs.Add(jobId);
        return Task.CompletedTask;
    }

    public Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summary = section switch
        {
            SpecSectionId.Sources => document.Sources.Count switch
            {
                0 => "no sources yet",
                1 => $"{document.Sources[0].Dataset} as @{document.Sources[0].Id}",
                _ => $"{document.Sources.Count} datasets referenced"
            },
            SpecSectionId.Scope => document.Scope.Bbox is null && document.Scope.Crs is null
                ? "global, no CRS pinned"
                : $"crs={document.Scope.Crs ?? "default"}",
            SpecSectionId.Compute => document.Compute.Count == 0
                ? "no compute steps"
                : string.Join(" → ", document.Compute.Select(c => c.Op)),
            SpecSectionId.Map => document.Map.Layers.Count == 0
                ? "no map layers"
                : $"{document.Map.Layers.Count} layer(s) with {document.Map.Layers[0].Symbology}",
            SpecSectionId.Output => document.Output.Kind == SpecOutputKind.None
                ? "no output bound"
                : $"{document.Output.Kind.ToString().ToLowerInvariant()} → {document.Output.Target ?? "(preview)"}",
            _ => string.Empty
        };
        return Task.FromResult(summary);
    }

    public async Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken)
    {
        if (_grammar is not null)
        {
            return _grammar;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_grammar is not null)
            {
                return _grammar;
            }

            var asm = typeof(StubSpecWorkspaceClient).Assembly;
            using var stream = ResolveGrammarStream(asm)
                ?? throw new InvalidOperationException("spec-grammar.v1.json missing from assembly resources");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var grammar = JsonSerializer.Deserialize(json, SpecWorkspaceJsonContext.Default.SpecGrammar)
                ?? throw new InvalidOperationException("failed to deserialize spec grammar");
            _grammar = grammar;
            return grammar;
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        foreach (var source in document.Sources)
        {
            if (!_datasets.ContainsKey(source.Dataset.ToLowerInvariant()))
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Sources, ValidationSeverity.Red, "unknown-identifier", $"dataset '{source.Dataset}' is not in the catalog", source.Dataset));
            }
            if (source.Pin is null)
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Sources, ValidationSeverity.Yellow, "mutable-source-no-pin", $"source '{source.Id}' has no pin; results may drift", source.Id));
            }
        }

        foreach (var step in document.Compute)
        {
            if (step.Op is not ("filter" or "aggregate" or "join" or "buffer"))
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "unknown-op", $"op '{step.Op}' is not in the grammar", step.Op));
            }

            if (step.Op == "aggregate")
            {
                if (!step.Args.ContainsKey("by"))
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "missing-required-param", "aggregate requires `by`", "by"));
                }

                if (!step.Args.ContainsKey("metric"))
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "missing-required-param", "aggregate requires `metric`", "metric"));
                }
            }

            if (step.Op == "buffer")
            {
                if (!step.Args.TryGetValue("distance", out var distance))
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "missing-required-param", "buffer requires `distance`", "distance"));
                }
                else if (!Regex.IsMatch(distance, @"^\d+(?:\.\d+)?(m|km|mi)$", RegexOptions.IgnoreCase))
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "type-mismatch", $"distance `{distance}` must include m, km, or mi", distance));
                }
            }

            if (step.Op == "join" && !step.Args.ContainsKey("how"))
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "missing-required-param", "join requires `how`", "how"));
            }

            foreach (var input in step.Inputs)
            {
                if (!document.Sources.Any(s => s.Id == input))
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "unknown-identifier", $"input '{input}' not declared as a source", input));
                }
            }

            if (step.Op == "aggregate" && step.Args.TryGetValue("by", out var by))
            {
                var columnRef = by.TrimStart('@');
                var parts = columnRef.Split('.', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "type-mismatch", $"aggregate by `{by}` must reference @source.column", by));
                }
                else
                {
                    var source = document.Sources.FirstOrDefault(s => s.Id == parts[0]);
                    if (source is null || !_datasets.TryGetValue(source.Dataset, out var datasetForGroup))
                    {
                        diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "unknown-identifier", $"aggregate source '{parts[0]}' is unknown", parts[0]));
                    }
                    else if (!datasetForGroup.Columns.Any(c => c.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase)))
                    {
                        diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Red, "unknown-identifier", $"column '{parts[1]}' is not in dataset '{source.Dataset}'", parts[1]));
                    }
                }
            }

            if (step.Op == "join")
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Compute, ValidationSeverity.Yellow, "non-deterministic-op", "join can produce non-deterministic row ordering in the stub", "join"));
            }
        }

        if (document.Scope.Crs is { Length: > 0 } crs && !(crs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) || crs.Equals("WGS84", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Scope, ValidationSeverity.Yellow, "crs-unit-mismatch", $"crs '{crs}' is unusual", crs));
        }

        if (document.Sources.Count > 0)
        {
            var estimatedRows = document.Sources
                .Select(s => _datasets.TryGetValue(s.Dataset, out var dataset) ? dataset.EstimatedRows : 0)
                .Sum();

            if (estimatedRows > 10000)
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Sources, ValidationSeverity.Yellow, "estimated-oversize", $"estimated source volume is {estimatedRows:n0} rows", document.Sources[0].Dataset));
            }
        }

        foreach (var layer in document.Map.Layers)
        {
            if (!document.Sources.Any(s => s.Id == layer.Source))
            {
                diagnostics.Add(new ValidationDiagnostic(SpecSectionId.Map, ValidationSeverity.Red, "unknown-identifier", $"map layer references missing source '{layer.Source}'", layer.Source));
            }
        }

        return diagnostics;
    }

    private static Stream? ResolveGrammarStream(Assembly asm)
    {
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (name.EndsWith("spec-grammar.v1.json", StringComparison.Ordinal))
            {
                return asm.GetManifestResourceStream(name);
            }
        }
        return null;
    }

    private IntentOutcome ApplyClarification(IntentRequest request)
    {
        var parts = (request.ClarificationId ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new IntentOutcome
            {
                Kind = IntentResponseKind.Unsupported,
                Message = "Clarification could not be resolved."
            };
        }

        if (parts[0] == "pick-dataset" && _datasets.TryGetValue(request.ClarificationValue!.ToLowerInvariant(), out var ds))
        {
            return ApplySourceOnlyMutation(request.CurrentSpec, ds);
        }

        if (parts[0] == "choose-op")
        {
            var dataset = parts.Length > 1 && _datasets.TryGetValue(parts[1], out var clarifiedDataset)
                ? clarifiedDataset
                : _datasets.Values.First();

            return request.ClarificationValue switch
            {
                "aggregate" => ApplyAggregateMutationForColumn(request.CurrentSpec, dataset, dataset.Columns.FirstOrDefault(c => c.Name != "id")?.Name ?? "id"),
                "filter" => ApplyFilterMutation(request.CurrentSpec, dataset, dataset.Columns.FirstOrDefault(c => c.Name != "id")?.Name ?? "id", dataset.SampleRows.FirstOrDefault() ?? "Alpha"),
                "buffer" => ApplyBufferMutation(request.CurrentSpec, dataset, "100", "m"),
                _ => ApplySourceOnlyMutation(request.CurrentSpec, dataset)
            };
        }

        if (parts[0] == "pick-column" && parts.Length >= 3 && _datasets.TryGetValue(parts[2], out var datasetForColumn))
        {
            return parts[1] switch
            {
                "aggregate" => ApplyAggregateMutationForColumn(request.CurrentSpec, datasetForColumn, request.ClarificationValue!),
                "filter" => new IntentOutcome
                {
                    Kind = IntentResponseKind.Clarification,
                    Message = "Filter needs a comparison value.",
                    Clarification = new ClarificationRequest
                    {
                        Id = $"pick-value:filter:{datasetForColumn.Id}:{request.ClarificationValue}",
                        Kind = ClarificationKind.PickValue,
                        Question = $"Which value should @{datasetForColumn.Id}.{request.ClarificationValue} equal?"
                    }
                },
                _ => ApplySourceOnlyMutation(request.CurrentSpec, datasetForColumn)
            };
        }

        if (parts[0] == "pick-value" && parts.Length >= 4 && _datasets.TryGetValue(parts[2], out var datasetForValue))
        {
            return ApplyFilterMutation(request.CurrentSpec, datasetForValue, parts[3], request.ClarificationValue!);
        }

        if (parts[0] == "specify-unit" && parts.Length >= 4 && _datasets.TryGetValue(parts[2], out var datasetForBuffer))
        {
            return ApplyBufferMutation(request.CurrentSpec, datasetForBuffer, parts[3], request.ClarificationValue!);
        }

        if (parts[0] == "specify-crs")
        {
            return ApplyScopeMutation(request.CurrentSpec, request.ClarificationValue!);
        }

        return new IntentOutcome
        {
            Kind = IntentResponseKind.Unsupported,
            Message = "Clarification could not be resolved."
        };
    }

    private IntentOutcome ApplySourceOnlyMutation(SpecDocument current, CatalogDataset dataset)
    {
        var source = new SpecSourceEntry(dataset.Id, dataset.Id, Pin: null);
        var sources = current.Sources.Any(s => s.Id == source.Id)
            ? current.Sources
            : current.Sources.Append(source).ToArray();
        var next = current with { Sources = sources };
        return BuildMutationOutcome(SpecSectionId.Sources, current, next, $"add source @{dataset.Id}");
    }

    private IntentOutcome ApplyAggregateMutation(SpecDocument current, CatalogDataset dataset, string prompt)
    {
        var withSource = EnsureSource(current, dataset);
        var groupBy = ExtractGroupBy(prompt, dataset);
        return ApplyAggregateMutationForColumn(withSource, dataset, groupBy);
    }

    private IntentOutcome ApplyAggregateMutationForColumn(SpecDocument current, CatalogDataset dataset, string groupBy)
    {
        var withSource = EnsureSource(current, dataset);
        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["by"] = $"@{dataset.Id}.{groupBy.TrimStart('@')}",
            ["metric"] = "count"
        };
        var step = new SpecComputeStep("aggregate", new[] { dataset.Id }, args);
        var compute = withSource.Compute.ToList();
        compute.Add(step);
        var next = withSource with { Compute = compute };
        return BuildMutationOutcome(SpecSectionId.Compute, current, next, $"aggregate count of @{dataset.Id} by {groupBy}");
    }

    private IntentOutcome ApplyBufferMutation(SpecDocument current, CatalogDataset dataset, string distance, string unit)
    {
        var withSource = EnsureSource(current, dataset);
        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = $"{distance}{unit}"
        };
        var step = new SpecComputeStep("buffer", new[] { dataset.Id }, args);
        var compute = withSource.Compute.ToList();
        compute.Add(step);
        var next = withSource with { Compute = compute };
        return BuildMutationOutcome(SpecSectionId.Compute, current, next, $"buffer @{dataset.Id} by {distance}{unit}");
    }

    private IntentOutcome ApplyFilterMutation(SpecDocument current, CatalogDataset dataset, string column, string value)
    {
        var withSource = EnsureSource(current, dataset);
        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["where"] = $"@{dataset.Id}.{column}={value}"
        };
        var step = new SpecComputeStep("filter", new[] { dataset.Id }, args);
        var compute = withSource.Compute.ToList();
        compute.Add(step);
        var next = withSource with
        {
            Compute = compute,
            Output = new SpecOutput
            {
                Kind = SpecOutputKind.Analysis,
                Target = "preview"
            }
        };

        return BuildMutationOutcome(SpecSectionId.Compute, current, next, $"filter @{dataset.Id}.{column} = {value}");
    }

    private IntentOutcome ApplyMapMutation(SpecDocument current, CatalogDataset dataset)
    {
        var withSource = EnsureSource(current, dataset);
        var layer = new SpecMapLayer(dataset.Id, "viridis");
        var layers = withSource.Map.Layers.Any(l => l.Source == dataset.Id)
            ? withSource.Map.Layers
            : withSource.Map.Layers.Append(layer).ToArray();
        var map = withSource.Map with { Layers = layers };
        var output = new SpecOutput { Kind = SpecOutputKind.Map, Target = "preview" };
        var next = withSource with { Map = map, Output = output };
        return BuildMutationOutcome(SpecSectionId.Map, current, next, $"render @{dataset.Id} on map");
    }

    private static IntentOutcome ApplyScopeMutation(SpecDocument current, string crs)
    {
        var next = current with
        {
            Scope = current.Scope with
            {
                Crs = crs
            }
        };

        return BuildMutationOutcome(SpecSectionId.Scope, current, next, $"set scope crs to {crs}");
    }

    private static SpecDocument EnsureSource(SpecDocument current, CatalogDataset dataset)
    {
        if (current.Sources.Any(s => s.Id == dataset.Id))
        {
            return current;
        }
        var sources = current.Sources.Append(new SpecSourceEntry(dataset.Id, dataset.Id)).ToArray();
        return current with { Sources = sources };
    }

    private static IntentOutcome BuildMutationOutcome(SpecSectionId section, SpecDocument before, SpecDocument after, string summary)
    {
        var beforeJson = SerializeSection(before, section);
        var afterJson = SerializeSection(after, section);
        return new IntentOutcome
        {
            Kind = IntentResponseKind.Mutation,
            Message = summary,
            Mutation = new SpecMutation
            {
                Section = section,
                Summary = summary,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                NextDocument = after
            }
        };
    }

    private static string SerializeSection(SpecDocument doc, SpecSectionId section)
    {
        object? payload = section switch
        {
            SpecSectionId.Sources => (object)doc.Sources,
            SpecSectionId.Scope => doc.Scope,
            SpecSectionId.Compute => doc.Compute,
            SpecSectionId.Map => doc.Map,
            SpecSectionId.Output => doc.Output,
            _ => doc
        };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(payload, payload!.GetType(), options);
    }

    private static string ExtractGroupBy(string prompt, CatalogDataset dataset)
    {
        var match = Regex.Match(prompt, "by\\s+([a-zA-Z_][\\w]*)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return dataset.Columns.FirstOrDefault()?.Name ?? "id";
    }

    private IReadOnlyList<CatalogCandidate> ResolveParamCandidates(string prefix)
    {
        var names = new[] { ("by", "column[]"), ("metric", "op"), ("where", "expression"), ("distance", "quantity"), ("how", "enum") };
        return names
            .Where(n => n.Item1.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(n => new CatalogCandidate
            {
                Id = n.Item1,
                Kind = CatalogCandidateKind.Operator,
                Label = n.Item1,
                Type = n.Item2
            })
            .ToArray();
    }

    private static IReadOnlyList<CatalogCandidate> ResolveSymbologyCandidates(string prefix)
    {
        var ramps = new[] { "viridis", "plasma", "magma", "blues", "reds" };
        return ramps
            .Where(r => r.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(r => new CatalogCandidate
            {
                Id = r,
                Kind = CatalogCandidateKind.SymbologyRamp,
                Label = r
            })
            .ToArray();
    }

    private ApplyPayload BuildPayload(SpecDocument document)
    {
        if (document.Map.Layers.Count > 0)
        {
            var features = new List<MapFeature>();
            foreach (var layer in document.Map.Layers)
            {
                var datasetId = ResolveDatasetId(document, layer.Source);
                if (datasetId is null || !_datasets.TryGetValue(datasetId, out var ds))
                {
                    continue;
                }
                features.AddRange(ds.SampleFeatures.Select(f => f with { Source = layer.Source }));
            }
            return new ApplyPayload
            {
                Kind = SpecOutputKind.Map,
                MapFeatures = features
            };
        }

        if (document.Compute.Any(c => c.Op == "aggregate"))
        {
            var step = document.Compute.First(c => c.Op == "aggregate");
            var groupBy = step.Args.TryGetValue("by", out var by) ? by : "group";
            var metric = step.Args.TryGetValue("metric", out var m) ? m : "count";
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [groupBy] = "Alpha",
                    [metric] = "142"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [groupBy] = "Beta",
                    [metric] = "97"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [groupBy] = "Gamma",
                    [metric] = "58"
                }
            }.Select(d => (IReadOnlyDictionary<string, string>)d).ToArray();
            return new ApplyPayload
            {
                Kind = SpecOutputKind.Analysis,
                TableColumns = new[] { groupBy, metric },
                TableRows = rows
            };
        }

        if (document.Output.Kind == SpecOutputKind.Analysis)
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "row-1",
                    ["status"] = "retained"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "row-2",
                    ["status"] = "retained"
                }
            }.Select(d => (IReadOnlyDictionary<string, string>)d).ToArray();

            return new ApplyPayload
            {
                Kind = SpecOutputKind.Analysis,
                TableColumns = new[] { "id", "status" },
                TableRows = rows
            };
        }

        if (document.Output.Kind == SpecOutputKind.AppScaffold)
        {
            return new ApplyPayload
            {
                Kind = SpecOutputKind.AppScaffold,
                AppScaffold = new AppScaffold(
                    "Preview App",
                    new[] { new AppScaffoldParameter("date", "date", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)) },
                    "single-column")
            };
        }

        return new ApplyPayload
        {
            Kind = SpecOutputKind.None
        };
    }

    private bool IsCancelled(string jobId) => _cancelledJobs.Contains(jobId);

    private string? ResolveDatasetId(SpecDocument document, string layerSource)
    {
        var source = document.Sources.FirstOrDefault(s => string.Equals(s.Id, layerSource, StringComparison.OrdinalIgnoreCase));
        var candidate = source?.Dataset ?? layerSource;
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate.ToLowerInvariant();
    }

    private static Dictionary<string, CatalogDataset> BuildSeedCatalog()
    {
        var datasets = new[]
        {
            new CatalogDataset(
                "parcels",
                "Parcels (statewide)",
                "vector:polygon",
                "Administrative parcel polygons.",
                "operator:read",
                "~12k rows / 8MB",
                EstimatedRows: 12000,
                Columns: new[]
                {
                    new CatalogColumn("id", "string", Array.Empty<string>(), "primary key"),
                    new CatalogColumn("county", "string", new[] { "Alpha", "Beta", "Gamma" }, "county name"),
                    new CatalogColumn("acreage", "double", new[] { "1.2", "5.3", "12.0" }, "parcel area")
                },
                SampleRows: new[] { "APN-001", "APN-002" },
                SampleFeatures: new[]
                {
                    new MapFeature("APN-001", "parcels", "polygon", "Parcel APN-001", 37.77, -122.42),
                    new MapFeature("APN-002", "parcels", "polygon", "Parcel APN-002", 37.78, -122.41)
                }),
            new CatalogDataset(
                "wells",
                "Water wells",
                "vector:point",
                "Licensed water wells with annual production.",
                "operator:read",
                "~3k rows / 1.2MB",
                EstimatedRows: 3000,
                Columns: new[]
                {
                    new CatalogColumn("id", "string", Array.Empty<string>(), "well id"),
                    new CatalogColumn("depth_m", "double", new[] { "12", "30", "90" }, "depth in meters"),
                    new CatalogColumn("basin", "string", new[] { "North", "South" }, "basin name")
                },
                SampleRows: new[] { "W-12", "W-33" },
                SampleFeatures: new[]
                {
                    new MapFeature("W-12", "wells", "point", "Well W-12", 37.76, -122.40),
                    new MapFeature("W-33", "wells", "point", "Well W-33", 37.80, -122.45)
                })
        };

        return datasets.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record CatalogDataset(
        string Id,
        string Title,
        string Kind,
        string Description,
        string RbacScope,
        string CostHint,
        long EstimatedRows,
        IReadOnlyList<CatalogColumn> Columns,
        IReadOnlyList<string> SampleRows,
        IReadOnlyList<MapFeature> SampleFeatures);

    private sealed record CatalogColumn(string Name, string Type, IReadOnlyList<string> SampleValues, string Documentation);

    [GeneratedRegex("@([a-zA-Z_][\\w]*)", RegexOptions.Compiled)]
    private static partial Regex AtRegex();

    [GeneratedRegex("\\b(aggregate|count|sum|avg|group)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AggRegex();

    [GeneratedRegex("\\b(filter|where)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FilterRgx();

    [GeneratedRegex("\\b(map|render|show|plot)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MapRgx();

    [GeneratedRegex("buffer\\s+(?:@[\\w]+\\s+)?(?:by\\s+)?(\\d+(?:\\.\\d+)?)\\s*(m|km|mi)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BufRegex();

    [GeneratedRegex("\\b(compute|do something|analyse|analyze)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OpRgx();
}
