using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class DeployControlTests
{
    private readonly HonuaServerFixture _fixture;

    public DeployControlTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetDeployPreflight_ReturnsResult()
    {
        // Act
        var result = await _fixture.Client.GetDeployPreflightAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Checks);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public async Task CreateDeployPlan_ReturnsPlan()
    {
        // Arrange
        var request = new CreateDeployPlanRequest
        {
            IncludeDataMigrations = true
        };

        // Act
        var plan = await _fixture.Client.CreateDeployPlanAsync(request);

        // Assert
        Assert.NotNull(plan);
        Assert.NotEmpty(plan.PlanId);
        Assert.NotNull(plan.Operations);
    }

    [Fact]
    public async Task CreateDeployOperation_ReturnsOperation()
    {
        // Arrange - create a plan first
        var planRequest = new CreateDeployPlanRequest
        {
            IncludeDataMigrations = true
        };
        var plan = await _fixture.Client.CreateDeployPlanAsync(planRequest);

        var operationRequest = new CreateDeployOperationRequest
        {
            PlanId = plan.PlanId
        };

        // Act
        var operation = await _fixture.Client.CreateDeployOperationAsync(operationRequest);

        // Assert
        Assert.NotNull(operation);
        Assert.NotEmpty(operation.OperationId);
        Assert.Equal(plan.PlanId, operation.PlanId);
        Assert.NotEmpty(operation.Status);
    }
}
