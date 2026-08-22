// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistryTests", DisableParallelization = true)]
public class GpuWaitRegistryCollection : ICollectionFixture<object>
{
}

[Collection("GpuWaitRegistryTests")]
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
    public void CountForMemory_ReturnsZero_WhenRegistryIsEmpty()
    {
        var mem = new object();
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem));
    }

    [Fact]
    public void CountForMemory_FiltersByMemoryReference()
    {
        var mem1 = new object();
        var mem2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = mem2 };
        var waiterNull = new GpuWaitRegistry.WaitingDcb { Memory = null };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiterNull);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(new object()));
    }

    [Fact]
    public void CountForMemory_CountsWaitersAcrossMultipleAddresses()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = mem };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = mem };
        var waiter3 = new GpuWaitRegistry.WaitingDcb { Memory = mem };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiter3);

        Assert.Equal(3, GpuWaitRegistry.CountForMemory(mem));
    }

    [Fact]
    public void CountForMemory_UpdatesAfterCollectAndClear()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 0x10,
            CompareFunction = 3, // Equal
        };

        GpuWaitRegistry.Register(0x1000, waiter);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem));

        // Satisfy condition
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 0x10);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem));

        // Re-register and clear
        GpuWaitRegistry.Register(0x1000, waiter);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem));

        GpuWaitRegistry.Clear();
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem));
    }

    [Fact]
    public void Compare_EvaluatesFunctionsCorrectly()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0x0F,
            ReferenceValue = 0x05,
        };

        // Function 0: Always true
        waiter.CompareFunction = 0;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x00));

        // Function 1: Less than (val < ref) -> 4 < 5
        waiter.CompareFunction = 1;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x04));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x05));

        // Function 2: Less than or equal (val <= ref)
        waiter.CompareFunction = 2;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x05));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x06));

        // Function 3: Equal (val == ref)
        waiter.CompareFunction = 3;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x05));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x06));

        // Function 4: Not equal (val != ref)
        waiter.CompareFunction = 4;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x06));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x05));

        // Function 5: Greater than or equal (val >= ref)
        waiter.CompareFunction = 5;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x05));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x04));

        // Function 6: Greater than (val > ref)
        waiter.CompareFunction = 6;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x06));
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x05));

        // Function > 6: Reserved (always true)
        waiter.CompareFunction = 7;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x00));
    }

    [Fact]
    public void SnapshotOutstanding_ReturnsDiagnostics()
    {
        var mem1 = new object();
        var mem2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            RegisteredTicks = 100,
            WaitAddress = 0x1000,
            QueueName = "QueueA",
            Latched = true,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            RegisteredTicks = 50,
            WaitAddress = 0x2000,
            QueueName = "QueueB",
            Latched = false,
        };

        var waiter3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            RegisteredTicks = 10,
            WaitAddress = 0x3000,
            QueueName = "QueueC",
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);
        GpuWaitRegistry.Register(0x3000, waiter3);

        var snapshotAll = GpuWaitRegistry.SnapshotOutstanding(null);
        Assert.Equal(3, snapshotAll.Outstanding);
        Assert.Equal(1, snapshotAll.Latched);

        var snapshotMem1 = GpuWaitRegistry.SnapshotOutstanding(mem1);
        Assert.Equal(2, snapshotMem1.Outstanding);
        Assert.Equal(1, snapshotMem1.Latched);
        Assert.Equal(0x2000u, snapshotMem1.SampleWaitAddress);
        Assert.Equal("QueueB", snapshotMem1.SampleQueueName);
    }

    [Fact]
    public void SnapshotInRange_FindsOverlappingWaiters()
    {
        var mem = new object();

        var waiter32Bit = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };

        var waiter64Bit = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true,
        };

        GpuWaitRegistry.Register(0x1000, waiter32Bit);
        GpuWaitRegistry.Register(0x2000, waiter64Bit);

        // Zero length
        Assert.Empty(GpuWaitRegistry.SnapshotInRange(mem, 0x1000, 0));

        // Matching ranges
        var matches = GpuWaitRegistry.SnapshotInRange(mem, 0x0FF0, 0x20);
        Assert.Single(matches);
        Assert.Equal(0x1000u, matches[0].Address);
        Assert.Equal(1, matches[0].Count);

        var matchesAll = GpuWaitRegistry.SnapshotInRange(mem, 0x0, 0x10000);
        Assert.Equal(2, matchesAll.Count);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesWaiterMatchingValue()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 42,
            CompareFunction = 3, // Equal
            Latched = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Value does not satisfy
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 10));

        // Address not registered
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x2000, 42));

        // Value satisfies
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 42));

        // Collect satisfied will drain latched waiter
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => null);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.True(woken[0].Latched);
    }

    [Fact]
    public void CollectUnreportedStale_ReturnsOnlyUnreportedOlderThanMaxAge()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            StaleReported = false,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 50,
            StaleReported = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);

        // Current time 120, max age 50 => registered <= 70 is stale (waiter2 at 50)
        var stale = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks: 120, maxAgeTicks: 50);
        Assert.NotNull(stale);
        Assert.Single(stale);
        Assert.Equal(0x2000u, stale[0].WaitAddress);

        // Calling again returns null because stale is now reported
        var stale2 = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks: 120, maxAgeTicks: 50);
        Assert.Null(stale2);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 200,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 0, // No retry deadline
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);

        // Before deadline
        var expiredBefore = GpuWaitRegistry.CollectExpiredRetries(mem, nowTicks: 150);
        Assert.Null(expiredBefore);

        // After deadline
        var expiredAfter = GpuWaitRegistry.CollectExpiredRetries(mem, nowTicks: 250);
        Assert.NotNull(expiredAfter);
        Assert.Single(expiredAfter);
        Assert.Equal(0x1000u, expiredAfter[0].WaitAddress);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesAllWaitersForMemory()
    {
        var mem1 = new object();
        var mem2 = new object();

        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x2000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x3000, new GpuWaitRegistry.WaitingDcb { Memory = mem2 });

        var collected = GpuWaitRegistry.CollectAllForMemory(mem1);
        Assert.NotNull(collected);
        Assert.Equal(2, collected.Count);
        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Fact]
    public void RecordProduced_And_CollectDeadlockBroken_WorkAsExpected()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3, // Equal
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Record value produced
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 100);

        // Waiter was latched by RecordProduced
        Assert.Equal(1, GpuWaitRegistry.SnapshotOutstanding(mem).Latched);

        // Deadlock breaker collects stuck waiters
        var broken = GpuWaitRegistry.CollectDeadlockBroken(mem, nowTicks: 300, minAgeTicks: 100);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
