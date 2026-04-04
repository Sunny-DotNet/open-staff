using OpenStaff.Core.Orchestration;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class TaskGraphTests
{
    [Fact]
    public void AddTask_ShouldStoreTask()
    {
        var graph = new TaskGraph();
        var id = Guid.NewGuid();
        graph.AddTask(id, "Test task");

        var ready = graph.GetReadyTasks(new HashSet<Guid>());
        Assert.Single(ready);
        Assert.Equal(id, ready[0].TaskId);
        Assert.Equal("Test task", ready[0].Title);
    }

    [Fact]
    public void GetReadyTasks_WithNoDependencies_ShouldReturnAllTasks()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");

        var ready = graph.GetReadyTasks(new HashSet<Guid>());
        Assert.Equal(2, ready.Count);
    }

    [Fact]
    public void GetReadyTasks_WithDependencies_ShouldReturnOnlyUnblockedTasks()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");
        graph.AddDependency(t2, t1); // t2 depends on t1

        var ready = graph.GetReadyTasks(new HashSet<Guid>());
        Assert.Single(ready);
        Assert.Equal(t1, ready[0].TaskId);
    }

    [Fact]
    public void GetReadyTasks_AfterDependencyCompleted_ShouldUnblockDependent()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");
        graph.AddDependency(t2, t1);

        var completed = new HashSet<Guid> { t1 };
        var ready = graph.GetReadyTasks(completed);
        Assert.Single(ready);
        Assert.Equal(t2, ready[0].TaskId);
    }

    [Fact]
    public void GetReadyTasks_ShouldExcludeAlreadyCompletedTasks()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");

        var completed = new HashSet<Guid> { t1 };
        var ready = graph.GetReadyTasks(completed);
        Assert.Single(ready);
        Assert.Equal(t2, ready[0].TaskId);
    }

    [Fact]
    public void GetReadyTasks_ShouldOrderByPriorityDescending()
    {
        var graph = new TaskGraph();
        var low = Guid.NewGuid();
        var high = Guid.NewGuid();
        graph.AddTask(low, "Low priority", priority: 1);
        graph.AddTask(high, "High priority", priority: 10);

        var ready = graph.GetReadyTasks(new HashSet<Guid>());
        Assert.Equal(2, ready.Count);
        Assert.Equal(high, ready[0].TaskId);
        Assert.Equal(low, ready[1].TaskId);
    }

    [Fact]
    public void HasCycle_NoCycle_ShouldReturnFalse()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");
        graph.AddDependency(t2, t1);

        Assert.False(graph.HasCycle());
    }

    [Fact]
    public void HasCycle_WithCycle_ShouldReturnTrue()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");
        graph.AddDependency(t1, t2);
        graph.AddDependency(t2, t1);

        Assert.True(graph.HasCycle());
    }

    [Fact]
    public void HasCycle_EmptyGraph_ShouldReturnFalse()
    {
        var graph = new TaskGraph();
        Assert.False(graph.HasCycle());
    }

    [Fact]
    public void AddDependency_OnNonExistentTask_ShouldNotThrow()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");

        // Adding dependency on a task ID not in the graph should be a no-op
        graph.AddDependency(Guid.NewGuid(), t1);
    }
}
