using FluentAssertions;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Domain.Services;

public class ActionWorkflowTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    private static ActionWorkflow CreateWorkflow() => new(new FakeClock(Now));

    private static InventoryAction CreateAction(ActionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = Guid.NewGuid(),
        Type = ActionType.PriceReduction,
        Status = status,
        Note = "test",
        CreatedAt = Now.AddDays(-1),
        UpdatedAt = Now.AddDays(-1),
    };

    public static IEnumerable<object[]> AllStatusPairs()
    {
        foreach (ActionStatus from in Enum.GetValues<ActionStatus>())
        {
            foreach (ActionStatus to in Enum.GetValues<ActionStatus>())
            {
                yield return [from, to];
            }
        }
    }

    private static readonly HashSet<(ActionStatus From, ActionStatus To)> ValidPairs = new()
    {
        (ActionStatus.Proposed, ActionStatus.Approved),
        (ActionStatus.Approved, ActionStatus.InProgress),
        (ActionStatus.InProgress, ActionStatus.Resolved),
    };

    [Theory]
    [MemberData(nameof(AllStatusPairs))]
    public void CanTransition_MatchesTheDesignedLifecycle_ForEveryStatusPair(ActionStatus from, ActionStatus to)
    {
        var workflow = CreateWorkflow();
        var expected = ValidPairs.Contains((from, to));

        workflow.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(ActionStatus.Proposed, ActionStatus.Approved)]
    [InlineData(ActionStatus.Approved, ActionStatus.InProgress)]
    public void TryTransition_Succeeds_ForValidNonTerminalTransitions(ActionStatus from, ActionStatus to)
    {
        var workflow = CreateWorkflow();
        var action = CreateAction(from);

        var result = workflow.TryTransition(action, to);

        result.Success.Should().BeTrue();
        action.Status.Should().Be(to);
        action.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void TryTransition_Succeeds_WhenResolvingWithAnOutcome()
    {
        var workflow = CreateWorkflow();
        var action = CreateAction(ActionStatus.InProgress);

        var result = workflow.TryTransition(action, ActionStatus.Resolved, ActionOutcome.Sold);

        result.Success.Should().BeTrue();
        action.Status.Should().Be(ActionStatus.Resolved);
        action.Outcome.Should().Be(ActionOutcome.Sold);
        action.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void TryTransition_Fails_WhenResolvingWithoutAnOutcome()
    {
        var workflow = CreateWorkflow();
        var action = CreateAction(ActionStatus.InProgress);
        var originalUpdatedAt = action.UpdatedAt;

        var result = workflow.TryTransition(action, ActionStatus.Resolved);

        result.Success.Should().BeFalse();
        action.Status.Should().Be(ActionStatus.InProgress);
        action.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Theory]
    [InlineData(ActionStatus.Proposed, ActionStatus.InProgress)]
    [InlineData(ActionStatus.Proposed, ActionStatus.Resolved)]
    [InlineData(ActionStatus.Proposed, ActionStatus.Proposed)]
    [InlineData(ActionStatus.Approved, ActionStatus.Proposed)]
    [InlineData(ActionStatus.Approved, ActionStatus.Resolved)]
    [InlineData(ActionStatus.Approved, ActionStatus.Approved)]
    [InlineData(ActionStatus.InProgress, ActionStatus.Proposed)]
    [InlineData(ActionStatus.InProgress, ActionStatus.Approved)]
    [InlineData(ActionStatus.InProgress, ActionStatus.InProgress)]
    [InlineData(ActionStatus.Resolved, ActionStatus.Proposed)]
    [InlineData(ActionStatus.Resolved, ActionStatus.Approved)]
    [InlineData(ActionStatus.Resolved, ActionStatus.InProgress)]
    [InlineData(ActionStatus.Resolved, ActionStatus.Resolved)]
    public void TryTransition_Fails_ForEveryInvalidTransition(ActionStatus from, ActionStatus to)
    {
        var workflow = CreateWorkflow();
        var action = CreateAction(from);
        var originalUpdatedAt = action.UpdatedAt;

        var result = workflow.TryTransition(action, to, ActionOutcome.Sold);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        action.Status.Should().Be(from);
        action.UpdatedAt.Should().Be(originalUpdatedAt);
    }
}
