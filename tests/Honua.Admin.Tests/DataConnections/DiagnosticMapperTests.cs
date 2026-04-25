using System;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Services.DataConnections;
using Xunit;

namespace Honua.Admin.Tests.DataConnections;

public sealed class DiagnosticMapperTests
{
    [Fact]
    public void Healthy_outcome_lights_every_cell_ok()
    {
        var outcome = new ConnectionTestOutcome
        {
            ConnectionId = Guid.NewGuid(),
            ConnectionName = "primary",
            IsHealthy = true,
            TestedAt = DateTimeOffset.UtcNow
        };

        var diagnostic = DiagnosticMapper.Map(outcome);

        Assert.Equal(6, diagnostic.Cells.Count);
        Assert.All(diagnostic.Cells, c => Assert.Equal(DiagnosticStatus.Ok, c.Status));
        Assert.False(diagnostic.AnyFailed);
    }

    [Theory]
    [InlineData("authentication failed for user", DiagnosticStep.Auth)]
    [InlineData("password authentication failed", DiagnosticStep.Auth)]
    [InlineData("permission denied for relation", DiagnosticStep.Auth)]
    [InlineData("SSL handshake failed", DiagnosticStep.Ssl)]
    [InlineData("certificate expired", DiagnosticStep.Ssl)]
    [InlineData("TLS negotiation aborted", DiagnosticStep.Ssl)]
    [InlineData("connection refused", DiagnosticStep.Tcp)]
    [InlineData("connection timeout after 30s", DiagnosticStep.Tcp)]
    [InlineData("host unreachable", DiagnosticStep.Tcp)]
    [InlineData("name resolution failed for db.example.com", DiagnosticStep.Dns)]
    [InlineData("dns lookup error", DiagnosticStep.Dns)]
    [InlineData("server version 9.6 too old; require 13", DiagnosticStep.Version)]
    [InlineData("required extension postgis is missing", DiagnosticStep.Capability)]
    public void Failed_outcome_routes_to_the_right_cell(string message, DiagnosticStep expected)
    {
        var outcome = BuildFailed(message);

        var diagnostic = DiagnosticMapper.Map(outcome);

        Assert.True(diagnostic.AnyFailed);
        Assert.Equal(DiagnosticStatus.Failed, diagnostic.GetCell(expected).Status);
        // Every other cell must remain NotAssessed — never Failed for unrelated steps.
        foreach (var cell in diagnostic.Cells)
        {
            if (cell.Step == expected)
            {
                continue;
            }
            Assert.Equal(DiagnosticStatus.NotAssessed, cell.Status);
        }
    }

    [Fact]
    public void Failed_outcome_with_unrelated_message_falls_back_to_auth_only()
    {
        var outcome = BuildFailed("something went wrong somewhere");

        var diagnostic = DiagnosticMapper.Map(outcome);

        Assert.Equal(DiagnosticStatus.Failed, diagnostic.GetCell(DiagnosticStep.Auth).Status);
        Assert.Equal(DiagnosticStatus.NotAssessed, diagnostic.GetCell(DiagnosticStep.Tcp).Status);
        Assert.Equal(DiagnosticStatus.NotAssessed, diagnostic.GetCell(DiagnosticStep.Ssl).Status);
        Assert.Equal(DiagnosticStatus.NotAssessed, diagnostic.GetCell(DiagnosticStep.Dns).Status);
        Assert.Equal(DiagnosticStatus.NotAssessed, diagnostic.GetCell(DiagnosticStep.Version).Status);
        Assert.Equal(DiagnosticStatus.NotAssessed, diagnostic.GetCell(DiagnosticStep.Capability).Status);
    }

    [Fact]
    public void Failed_outcome_with_empty_message_does_not_throw_and_falls_back_to_auth()
    {
        var outcome = BuildFailed(string.Empty);

        var diagnostic = DiagnosticMapper.Map(outcome);

        Assert.Equal(DiagnosticStatus.Failed, diagnostic.GetCell(DiagnosticStep.Auth).Status);
    }

    [Fact]
    public void Healthy_outcome_with_detail_attaches_capability_and_version_payload()
    {
        var outcome = new ConnectionTestOutcome
        {
            ConnectionId = Guid.NewGuid(),
            ConnectionName = "primary",
            IsHealthy = true,
            TestedAt = DateTimeOffset.UtcNow,
            Message = "ok"
        };
        var detail = new DataConnectionDetail
        {
            ConnectionId = outcome.ConnectionId,
            Name = "primary",
            Host = "db.example.com",
            Port = 5432,
            DatabaseName = "honua",
            Username = "honua",
            SslMode = "Require",
            StorageType = "managed",
            HealthStatus = "Postgres 15.4",
            CreatedBy = "operator"
        };

        var diagnostic = DiagnosticMapper.Map(outcome, detail);

        Assert.Equal("Require / managed=managed", diagnostic.GetCell(DiagnosticStep.Capability).Detail);
        Assert.Equal("Postgres 15.4", diagnostic.GetCell(DiagnosticStep.Version).Detail);
    }

    private static ConnectionTestOutcome BuildFailed(string message) => new()
    {
        ConnectionId = Guid.NewGuid(),
        ConnectionName = "primary",
        IsHealthy = false,
        TestedAt = DateTimeOffset.UtcNow,
        Message = message
    };
}
