using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Thread-safe LRU cache sitting in front of <see cref="ISpecWorkspaceClient.ResolveCatalogAsync"/>.
/// RBAC principal keyed so switching operators wipes stale candidates. Sized for
/// the S1 AC ("IntelliSense completion latency under 100 ms for cached grammar + catalog").
/// </summary>
public sealed class CatalogCache
{
    private readonly int _capacity;
    private readonly LinkedList<Entry> _lru = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _index = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private string _principalId = string.Empty;

    public CatalogCache(int capacity = 64)
    {
        _capacity = capacity;
    }

    public void SetPrincipal(string principalId)
    {
        lock (_sync)
        {
            if (!string.Equals(_principalId, principalId, StringComparison.Ordinal))
            {
                _lru.Clear();
                _index.Clear();
                _principalId = principalId;
            }
        }
    }

    public async Task<CatalogResolution> GetOrResolveAsync(
        ISpecWorkspaceClient client,
        ResolveQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(query);
        var key = BuildKey(query);
        lock (_sync)
        {
            if (_index.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return new CatalogResolution(node.Value.Candidates, Cached: true, ElapsedMillis: 0);
            }
        }

        var watch = Stopwatch.StartNew();
        var candidates = await client.ResolveCatalogAsync(query, cancellationToken).ConfigureAwait(false);
        watch.Stop();
        Put(key, candidates);
        return new CatalogResolution(candidates, Cached: false, ElapsedMillis: watch.ElapsedMilliseconds);
    }

    private void Put(string key, IReadOnlyList<CatalogCandidate> candidates)
    {
        lock (_sync)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _lru.Remove(existing);
                _index.Remove(key);
            }

            var node = new LinkedListNode<Entry>(new Entry(key, candidates));
            _lru.AddFirst(node);
            _index[key] = node;

            while (_lru.Count > _capacity && _lru.Last is not null)
            {
                _index.Remove(_lru.Last.Value.Key);
                _lru.RemoveLast();
            }
        }
    }

    internal int Count
    {
        get
        {
            lock (_sync)
            {
                return _lru.Count;
            }
        }
    }

    private static string BuildKey(ResolveQuery query) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{query.PrincipalId}|{query.Trigger}|{query.Parent ?? string.Empty}|{query.Prefix}");

    private sealed record Entry(string Key, IReadOnlyList<CatalogCandidate> Candidates);
}

public sealed record CatalogResolution(
    IReadOnlyList<CatalogCandidate> Candidates,
    bool Cached,
    long ElapsedMillis);
