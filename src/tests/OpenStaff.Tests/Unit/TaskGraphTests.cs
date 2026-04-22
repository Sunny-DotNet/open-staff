using OpenStaff.Core.Orchestration;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class TaskGraphTests
{
    /// <summary>
    /// zh-CN: 验证添加一个无依赖任务后，任务图会把它视为可立即执行的任务。
    /// en: Verifies adding a task with no dependencies makes it immediately available as a ready task.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证当多个任务都没有依赖时，调度器会全部返回而不会错误阻塞。
    /// en: Verifies the scheduler returns every task when none of them has dependencies to block execution.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证依赖尚未完成时，只会返回未被阻塞的前置任务。
    /// en: Verifies only unblocked prerequisite tasks are returned while a dependency chain is still incomplete.
    /// </summary>
    [Fact]
    public void GetReadyTasks_WithDependencies_ShouldReturnOnlyUnblockedTasks()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");
        graph.AddTask(t2, "Task 2");
        graph.AddDependency(t2, t1); // zh-CN: t2 依赖 t1。 en: t2 depends on t1.

        var ready = graph.GetReadyTasks(new HashSet<Guid>());
        Assert.Single(ready);
        Assert.Equal(t1, ready[0].TaskId);
    }

    /// <summary>
    /// zh-CN: 验证当前置任务完成后，原先被阻塞的后续任务会变为可执行。
    /// en: Verifies a dependent task becomes ready once its prerequisite has been marked as completed.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证已经完成的任务不会再次出现在待执行列表中。
    /// en: Verifies tasks that are already completed are excluded from the ready list.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证同样可执行的任务会按优先级从高到低排序，保护调度顺序。
    /// en: Verifies equally ready tasks are ordered from highest to lowest priority to preserve scheduling intent.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证普通的有向无环依赖关系不会被误判为循环。
    /// en: Verifies a normal acyclic dependency chain is not mistakenly reported as a cycle.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证相互依赖的任务会被识别为循环，避免调度器陷入死锁。
    /// en: Verifies mutually dependent tasks are detected as a cycle so the scheduler can avoid deadlock.
    /// </summary>
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

    /// <summary>
    /// zh-CN: 验证空任务图默认不存在循环，为调度器提供安全基线。
    /// en: Verifies an empty task graph reports no cycle, providing a safe baseline for the scheduler.
    /// </summary>
    [Fact]
    public void HasCycle_EmptyGraph_ShouldReturnFalse()
    {
        var graph = new TaskGraph();
        Assert.False(graph.HasCycle());
    }

    /// <summary>
    /// zh-CN: 验证向不存在的任务添加依赖时保持幂等，不会破坏现有任务图。
    /// en: Verifies adding a dependency for a non-existent task is a harmless no-op that does not damage the graph.
    /// </summary>
    [Fact]
    public void AddDependency_OnNonExistentTask_ShouldNotThrow()
    {
        var graph = new TaskGraph();
        var t1 = Guid.NewGuid();
        graph.AddTask(t1, "Task 1");

        // zh-CN: 向图中不存在的任务添加依赖应当是空操作。
        // en: Adding a dependency for a task ID not in the graph should be a no-op.
        graph.AddDependency(Guid.NewGuid(), t1);
    }
}
