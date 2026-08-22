// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Diagnostics;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class GpuWaitRegistryTests : IDisposable
{
    public GpuWaitRegistryTests()
    {
        GpuWaitRegistry.Clear();
    }

    public void Dispose()
    {
        GpuWaitRegistry.Clear();
    }

    [Fact]
    public void SnapshotOutstanding_WhenEmpty_ReturnsZeroSnapshot()
    {
        var snapshot = GpuWaitRegistry.SnapshotOutstanding();

        Assert.Equal(0, snapshot.Outstanding);
        Assert.Equal(0, snapshot.Latched);
        Assert.Equal(0L, snapshot.OldestAgeMs);
        Assert.Equal(0UL, snapshot.SampleWaitAddress);
        Assert.Null(snapshot.SampleQueueName);
    }

    [Fact]
    public void SnapshotOutstanding_WithMultipleWaiters_FiltersByMemoryAndCalculatesOldestAge()
    {
        var memory1 = new object();
        var memory2 = new object();
        var nowTicks = Stopwatch.GetTimestamp();
        var freq = Stopwatch.Frequency;
        var oldestTicks = nowTicks - (freq * 2); // 2 seconds ago

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            Latched = true,
            RegisteredTicks = oldestTicks,
            WaitAddress = 0x1000UL,
            QueueName = "Queue_Compute_0"
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            Latched = false,
            RegisteredTicks = nowTicks - freq, // 1 second ago
            WaitAddress = 0x2000UL,
            QueueName = "Queue_Graphics_0"
        };

        var waiterOtherMemory = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory2,
            Latched = true,
            RegisteredTicks = nowTicks - (freq * 5),
            WaitAddress = 0x3000UL,
            QueueName = "Queue_Other"
        };

        GpuWaitRegistry.Register(0x1000UL, waiter1);
        GpuWaitRegistry.Register(0x2000UL, waiter2);
        GpuWaitRegistry.Register(0x3000UL, waiterOtherMemory);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(memory1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(memory2));

        var snapshotMemory1 = GpuWaitRegistry.SnapshotOutstanding(memory1);
        Assert.Equal(2, snapshotMemory1.Outstanding);
        Assert.Equal(1, snapshotMemory1.Latched);
        Assert.True(snapshotMemory1.OldestAgeMs >= 1900 && snapshotMemory1.OldestAgeMs <= 3000);
        Assert.Equal(0x1000UL, snapshotMemory1.SampleWaitAddress);
        Assert.Equal("Queue_Compute_0", snapshotMemory1.SampleQueueName);
    }

    [Fact]
    public void SnapshotOutstanding_WithNullMemory_SummarizesAllWaiters()
    {
        var memory1 = new object();
        var memory2 = new object();
        var nowTicks = Stopwatch.GetTimestamp();
        var freq = Stopwatch.Frequency;

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            Latched = true,
            RegisteredTicks = nowTicks - freq,
            WaitAddress = 0x1000UL,
            QueueName = "Queue_1"
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory2,
            Latched = true,
            RegisteredTicks = nowTicks - (freq * 3),
            WaitAddress = 0x2000UL,
            QueueName = "Queue_2"
        };

        GpuWaitRegistry.Register(0x1000UL, waiter1);
        GpuWaitRegistry.Register(0x2000UL, waiter2);

        var snapshotAll = GpuWaitRegistry.SnapshotOutstanding(null);
        Assert.Equal(2, snapshotAll.Outstanding);
        Assert.Equal(2, snapshotAll.Latched);
        Assert.True(snapshotAll.OldestAgeMs >= 2900);
        Assert.Equal(0x2000UL, snapshotAll.SampleWaitAddress);
        Assert.Equal("Queue_2", snapshotAll.SampleQueueName);
    }

    [Fact]
    public void Register_And_Clear_ManagesWaitersState()
    {
        var memory = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1000UL
        };

        GpuWaitRegistry.Register(0x1000UL, waiter);
        Assert.Equal(1, GpuWaitRegistry.Count);

        GpuWaitRegistry.Clear();
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_LatchAndReadValue()
    {
        var memory = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Latched = true,
            WaitAddress = 0x1000UL,
            Is64Bit = false,
            CompareFunction = 3, // Equal
            ReferenceValue = 100,
            Mask = 0xFFFFFFFF
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Latched = false,
            WaitAddress = 0x2000UL,
            Is64Bit = false,
            CompareFunction = 5, // GreaterEqual
            ReferenceValue = 50,
            Mask = 0xFFFFFFFF
        };

        var waiterUnsatisfied = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Latched = false,
            WaitAddress = 0x3000UL,
            Is64Bit = false,
            CompareFunction = 3, // Equal
            ReferenceValue = 100,
            Mask = 0xFFFFFFFF
        };

        GpuWaitRegistry.Register(0x1000UL, waiter1);
        GpuWaitRegistry.Register(0x2000UL, waiter2);
        GpuWaitRegistry.Register(0x3000UL, waiterUnsatisfied);

        var woken = GpuWaitRegistry.CollectSatisfied(memory, (addr, is64) =>
        {
            return addr switch
            {
                0x2000UL => 60UL,
                0x3000UL => 10UL,
                _ => null
            };
        });

        Assert.NotNull(woken);
        Assert.Equal(2, woken.Count);
        Assert.Contains(woken, w => w.WaitAddress == 0x1000UL);
        Assert.Contains(woken, w => w.WaitAddress == 0x2000UL);

        Assert.Equal(1, GpuWaitRegistry.CountForMemory(memory));
    }

    [Fact]
    public void CollectUnreportedStale_IdentifiesStaleWaitersOnce()
    {
        var memory = new object();
        var nowTicks = 10000L;
        var waiterStale = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 1000L,
            StaleReported = false
        };

        var waiterFresh = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 9500L,
            StaleReported = false
        };

        GpuWaitRegistry.Register(0x1000UL, waiterStale);
        GpuWaitRegistry.Register(0x2000UL, waiterFresh);

        var staleFirstCall = GpuWaitRegistry.CollectUnreportedStale(memory, nowTicks, maxAgeTicks: 5000L);
        Assert.NotNull(staleFirstCall);
        Assert.Single(staleFirstCall);
        Assert.Equal(0x1000UL, staleFirstCall[0].WaitAddress);

        // Second call should return null since StaleReported flag was set to true
        var staleSecondCall = GpuWaitRegistry.CollectUnreportedStale(memory, nowTicks, maxAgeTicks: 5000L);
        Assert.Null(staleSecondCall);
    }

    [Fact]
    public void SnapshotInRange_ReturnsOverlappingWaiters()
    {
        var memory = new object();
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1000UL,
            Is64Bit = false
        };

        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1008UL,
            Is64Bit = true
        };

        GpuWaitRegistry.Register(0x1000UL, waiter32);
        GpuWaitRegistry.Register(0x1008UL, waiter64);

        // Zero length range
        Assert.Empty(GpuWaitRegistry.SnapshotInRange(memory, 0x1000UL, 0));

        // Range covering waiter32 (0x1000 to 0x1004)
        var matches32 = GpuWaitRegistry.SnapshotInRange(memory, 0x0FF0UL, 0x20);
        Assert.Equal(2, matches32.Count);

        // Range missing all waiters
        var matchesNone = GpuWaitRegistry.SnapshotInRange(memory, 0x2000UL, 0x10);
        Assert.Empty(matchesNone);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var memory = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1000UL,
            CompareFunction = 3, // Equal
            ReferenceValue = 42UL,
            Mask = 0xFFFFFFFFUL,
            Latched = false
        };

        GpuWaitRegistry.Register(0x1000UL, waiter);

        // Non-matching write value
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(memory, 0x1000UL, 10UL));

        // Matching write value
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(memory, 0x1000UL, 42UL));

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(memory);
        Assert.Equal(1, snapshot.Latched);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var memory = new object();
        var waiterExpired = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1000UL,
            RetryDeadlineTicks = 5000L
        };

        var waiterNotExpired = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x2000UL,
            RetryDeadlineTicks = 15000L
        };

        GpuWaitRegistry.Register(0x1000UL, waiterExpired);
        GpuWaitRegistry.Register(0x2000UL, waiterNotExpired);

        var expired = GpuWaitRegistry.CollectExpiredRetries(memory, nowTicks: 10000L);
        Assert.NotNull(expired);
        Assert.Single(expired);
        Assert.Equal(0x1000UL, expired[0].WaitAddress);

        Assert.Equal(1, GpuWaitRegistry.CountForMemory(memory));
    }

    [Fact]
    public void CollectAllForMemory_RemovesWaitersForMemory()
    {
        var memory1 = new object();
        var memory2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = memory1, WaitAddress = 0x1000UL };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = memory2, WaitAddress = 0x2000UL };

        GpuWaitRegistry.Register(0x1000UL, waiter1);
        GpuWaitRegistry.Register(0x2000UL, waiter2);

        var collected = GpuWaitRegistry.CollectAllForMemory(memory1);
        Assert.NotNull(collected);
        Assert.Single(collected);
        Assert.Equal(0x1000UL, collected[0].WaitAddress);

        Assert.Equal(0, GpuWaitRegistry.CountForMemory(memory1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(memory2));
    }

    [Fact]
    public void RecordProduced_And_CollectDeadlockBroken()
    {
        var memory = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = 0x1000UL,
            RegisteredTicks = 1000L,
            CompareFunction = 3, // Equal
            ReferenceValue = 100UL,
            Mask = 0xFFFFFFFFUL
        };

        GpuWaitRegistry.Register(0x1000UL, waiter);

        // Record a produced value matching the waiter's condition
        Assert.True(GpuWaitRegistry.RecordProduced(memory, 0x1000UL, 100UL));

        // Collect deadlock broken waiters
        var broken = GpuWaitRegistry.CollectDeadlockBroken(memory, nowTicks: 10000L, minAgeTicks: 5000L);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0x1000UL, broken[0].WaitAddress);

        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Theory]
    [InlineData(0, 5, 10, true)]   // Always true
    [InlineData(1, 5, 10, true)]   // LessThan
    [InlineData(1, 10, 5, false)]
    [InlineData(2, 5, 5, true)]    // LessEqual
    [InlineData(2, 6, 5, false)]
    [InlineData(3, 5, 5, true)]    // Equal
    [InlineData(3, 5, 6, false)]
    [InlineData(4, 5, 6, true)]    // NotEqual
    [InlineData(4, 5, 5, false)]
    [InlineData(5, 10, 5, true)]   // GreaterEqual
    [InlineData(5, 4, 5, false)]
    [InlineData(6, 10, 5, true)]   // GreaterThan
    [InlineData(6, 5, 5, false)]
    [InlineData(7, 5, 10, true)]   // Reserved (treat as true)
    [InlineData(99, 5, 10, true)]  // Fallthrough (treat as true)
    public void Compare_EvaluatesComparisonFunctions(uint func, ulong value, ulong reference, bool expected)
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CompareFunction = func,
            ReferenceValue = reference,
            Mask = 0xFFFFFFFFUL
        };

        Assert.Equal(expected, GpuWaitRegistry.Compare(waiter, value));
    }
}
