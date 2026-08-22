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
    public void Count_And_CountForMemory_ReturnCorrectCounts()
    {
        var mem1 = new object();
        var mem2 = new object();

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));

        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var waiter3 = new GpuWaitRegistry.WaitingDcb { Memory = mem2 };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiter3);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(new object()));
    }

    [Fact]
    public void Compare_HandlesAllComparisonFunctions()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0xFF,
            ReferenceValue = 10,
        };

        // Function 0: Always true
        waiter.CompareFunction = 0;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));

        // Function 1: Less than
        waiter.CompareFunction = 1;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 2: Less than or equal
        waiter.CompareFunction = 2;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 11));

        // Function 3: Equal
        waiter.CompareFunction = 3;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 5));

        // Function 4: Not equal
        waiter.CompareFunction = 4;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 5: Greater than or equal
        waiter.CompareFunction = 5;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 9));

        // Function 6: Greater than
        waiter.CompareFunction = 6;
        Assert.True(GpuWaitRegistry.Compare(waiter, 11));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 7+ / Reserved: Always true
        waiter.CompareFunction = 7;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0));
        waiter.CompareFunction = 99;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0));
    }

    [Fact]
    public void SnapshotOutstanding_ReturnsDiagnosticsSnapshot()
    {
        var mem1 = new object();
        var mem2 = new object();

        var ticksOld = Stopwatch.GetTimestamp() - 1000;
        var ticksNewer = Stopwatch.GetTimestamp() - 500;

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            Latched = true,
            RegisteredTicks = ticksOld,
            WaitAddress = 0x1000,
            QueueName = "QueueA",
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            Latched = false,
            RegisteredTicks = ticksNewer,
            WaitAddress = 0x2000,
            QueueName = "QueueB",
        };

        var waiter3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            Latched = true,
            RegisteredTicks = ticksNewer,
            WaitAddress = 0x3000,
            QueueName = "QueueC",
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);
        GpuWaitRegistry.Register(0x3000, waiter3);

        var snapAll = GpuWaitRegistry.SnapshotOutstanding(null);
        Assert.Equal(3, snapAll.Outstanding);
        Assert.Equal(2, snapAll.Latched);
        Assert.Equal(0x1000u, snapAll.SampleWaitAddress);
        Assert.Equal("QueueA", snapAll.SampleQueueName);
        Assert.True(snapAll.OldestAgeMs >= 0);

        var snapMem1 = GpuWaitRegistry.SnapshotOutstanding(mem1);
        Assert.Equal(2, snapMem1.Outstanding);
        Assert.Equal(1, snapMem1.Latched);
        Assert.Equal(0x1000u, snapMem1.SampleWaitAddress);
        Assert.Equal("QueueA", snapMem1.SampleQueueName);

        var snapMem2 = GpuWaitRegistry.SnapshotOutstanding(mem2);
        Assert.Equal(1, snapMem2.Outstanding);
        Assert.Equal(1, snapMem2.Latched);
        Assert.Equal(0x3000u, snapMem2.SampleWaitAddress);
        Assert.Equal("QueueC", snapMem2.SampleQueueName);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3, // Equal
            Latched = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Value that does not satisfy
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 50));
        var snap = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(0, snap.Latched);

        // Value that satisfies
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 100));
        snap = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(1, snap.Latched);

        // Already latched waiter returns false when re-latched
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 100));
    }

    [Fact]
    public void CollectSatisfied_WakesSatisfiedAndLatchedWaiters()
    {
        var mem1 = new object();
        var mem2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3, // Equal
            Is64Bit = false,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3,
            Is64Bit = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        // Memory read returns unsatisfied value for address 0x1000
        var woken = GpuWaitRegistry.CollectSatisfied(mem1, (addr, is64) => 5);
        Assert.Null(woken);
        Assert.Equal(2, GpuWaitRegistry.Count);

        // Memory read returns satisfied value (10) for mem1
        woken = GpuWaitRegistry.CollectSatisfied(mem1, (addr, is64) => 10);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // mem2 is still registered
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Fact]
    public void CollectUnreportedStale_ReportsStaleWaitersOnce()
    {
        var mem = new object();
        var now = 1000L;
        var maxAge = 100L;

        var waiterStale = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 800L, // Age = 200 > 100
            StaleReported = false,
        };

        var waiterFresh = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 950L, // Age = 50 < 100
            StaleReported = false,
        };

        GpuWaitRegistry.Register(0x1000, waiterStale);
        GpuWaitRegistry.Register(0x2000, waiterFresh);

        var staleList = GpuWaitRegistry.CollectUnreportedStale(mem, now, maxAge);
        Assert.NotNull(staleList);
        Assert.Single(staleList);
        Assert.Equal(0x1000u, staleList[0].WaitAddress);

        // Second call returns null because StaleReported is now true
        staleList = GpuWaitRegistry.CollectUnreportedStale(mem, now, maxAge);
        Assert.Null(staleList);

        // Waiters remain registered
        Assert.Equal(2, GpuWaitRegistry.Count);
    }

    [Fact]
    public void SnapshotInRange_DetectsOverlappingWaitAddresses()
    {
        var mem = new object();

        var waiter32Bit = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false, // width = 4 bytes (0x1000 to 0x1004)
        };

        var waiter64Bit = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true, // width = 8 bytes (0x2000 to 0x2008)
        };

        GpuWaitRegistry.Register(0x1000, waiter32Bit);
        GpuWaitRegistry.Register(0x2000, waiter64Bit);

        // Zero length range returns empty
        Assert.Empty(GpuWaitRegistry.SnapshotInRange(mem, 0x1000, 0));

        // Non-overlapping range
        Assert.Empty(GpuWaitRegistry.SnapshotInRange(mem, 0x0500, 0x0100));

        // Overlapping 32-bit wait
        var matches = GpuWaitRegistry.SnapshotInRange(mem, 0x1002, 0x0010);
        Assert.Single(matches);
        Assert.Equal(0x1000u, matches[0].Address);
        Assert.Equal(1, matches[0].Count);

        // Overlapping 64-bit wait
        matches = GpuWaitRegistry.SnapshotInRange(mem, 0x1F00, 0x0200);
        Assert.Single(matches);
        Assert.Equal(0x2000u, matches[0].Address);

        // Range overlapping both waiters
        matches = GpuWaitRegistry.SnapshotInRange(mem, 0x0F00, 0x1200);
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var mem = new object();
        var now = 1000L;

        var waiterExpired = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 800L, // 800 <= 1000
        };

        var waiterPending = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 1200L, // 1200 > 1000
        };

        var waiterNoRetry = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 0L,
        };

        GpuWaitRegistry.Register(0x1000, waiterExpired);
        GpuWaitRegistry.Register(0x2000, waiterPending);
        GpuWaitRegistry.Register(0x3000, waiterNoRetry);

        var expired = GpuWaitRegistry.CollectExpiredRetries(mem, now);
        Assert.NotNull(expired);
        Assert.Single(expired);
        Assert.Equal(0x1000u, expired[0].WaitAddress);

        // Expired waiter was removed, 2 waiters remain
        Assert.Equal(2, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesAllWaitersForSpecificMemory()
    {
        var mem1 = new object();
        var mem2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var waiter3 = new GpuWaitRegistry.WaitingDcb { Memory = mem2 };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiter3);

        var collected = GpuWaitRegistry.CollectAllForMemory(mem1);
        Assert.NotNull(collected);
        Assert.Equal(2, collected.Count);

        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Fact]
    public void RecordProduced_And_CollectDeadlockBroken_ResolvesStuckWaiters()
    {
        var mem = new object();
        var now = 2000L;
        var minAge = 500L;

        var waiterStuck = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 1000L, // Age = 1000 >= 500
            Mask = 0xFF,
            ReferenceValue = 42,
            CompareFunction = 3, // Equal
        };

        GpuWaitRegistry.Register(0x1000, waiterStuck);

        // Record a produced value for 0x1000
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 42);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(mem, now, minAge);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0x1000u, broken[0].WaitAddress);

        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void Clear_ResetsRegistryAndLastProduced()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb { Memory = mem };

        GpuWaitRegistry.Register(0x1000, waiter);
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 100);

        Assert.Equal(1, GpuWaitRegistry.Count);

        GpuWaitRegistry.Clear();

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Null(GpuWaitRegistry.CollectDeadlockBroken(mem, 10000, 0));
    }
}
