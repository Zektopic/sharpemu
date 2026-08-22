// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistryState", DisableParallelization = true)]
public class GpuWaitRegistryCollection : ICollectionFixture<object>
{
}

[Collection("GpuWaitRegistryState")]
public sealed class GpuWaitRegistryTests : IDisposable
{
    private readonly object _dummyMemory1 = new();
    private readonly object _dummyMemory2 = new();

    public GpuWaitRegistryTests()
    {
        GpuWaitRegistry.Clear();
    }

    public void Dispose()
    {
        GpuWaitRegistry.Clear();
    }

    [Fact]
    public void Clear_EmptiesAllWaitersAndLastProduced()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            Mask = 0xFF,
            ReferenceValue = 0x10,
            CompareFunction = 3
        };

        GpuWaitRegistry.Register(0x1000, waiter);
        GpuWaitRegistry.RecordProduced(_dummyMemory1, 0x1000, 0x10);

        Assert.Equal(1, GpuWaitRegistry.Count);

        GpuWaitRegistry.Clear();

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(_dummyMemory1));

        // After clear, deadlock broken shouldn't find previously produced values
        GpuWaitRegistry.Register(0x1000, waiter);
        var broken = GpuWaitRegistry.CollectDeadlockBroken(_dummyMemory1, 1000, 100);
        Assert.Null(broken);
    }

    [Fact]
    public void RegisterAndCount_TracksWaitersByMemory()
    {
        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = _dummyMemory1 };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = _dummyMemory1 };
        var waiter3 = new GpuWaitRegistry.WaitingDcb { Memory = _dummyMemory2 };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiter3);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(_dummyMemory1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(_dummyMemory2));
    }

    [Theory]
    [InlineData(0, 0x10, 0x20, true)] // 0 => true
    [InlineData(1, 0x10, 0x20, true)] // 1 => masked < reference (0x10 < 0x20)
    [InlineData(1, 0x20, 0x20, false)]
    [InlineData(2, 0x20, 0x20, true)] // 2 => masked <= reference
    [InlineData(2, 0x30, 0x20, false)]
    [InlineData(3, 0x20, 0x20, true)] // 3 => masked == reference
    [InlineData(3, 0x21, 0x20, false)]
    [InlineData(4, 0x21, 0x20, true)] // 4 => masked != reference
    [InlineData(4, 0x20, 0x20, false)]
    [InlineData(5, 0x20, 0x20, true)] // 5 => masked >= reference
    [InlineData(5, 0x10, 0x20, false)]
    [InlineData(6, 0x30, 0x20, true)] // 6 => masked > reference
    [InlineData(6, 0x20, 0x20, false)]
    [InlineData(7, 0x00, 0x20, true)] // Reserved treating as true
    [InlineData(99, 0x00, 0x20, true)] // Fallthrough treating as true
    public void Compare_EvaluatesFunctionsAndMasksCorrectly(uint compareFunc, ulong value, ulong reference, bool expected)
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0x00FF,
            ReferenceValue = reference,
            CompareFunction = compareFunc
        };

        // value will be masked with 0x00FF
        // reference will be masked with 0x00FF
        Assert.Equal(expected, GpuWaitRegistry.Compare(waiter, value | 0xFF00));
    }

    [Fact]
    public void CollectSatisfied_WakesSatisfiedWaitersAndCleansUp()
    {
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3 // ==
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            Mask = 0xFF,
            ReferenceValue = 20,
            CompareFunction = 3 // ==
        };
        var waiterOtherMem = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory2,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3 // ==
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x1000, waiterOtherMem);

        // Read func returns 10 for 0x1000
        var woken = GpuWaitRegistry.CollectSatisfied(_dummyMemory1, (addr, is64) => addr == 0x1000 ? 10u : null);

        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(10u, woken[0].ReferenceValue);

        // Memory 1 still has waiter2 pending
        Assert.Equal(2, GpuWaitRegistry.Count);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(_dummyMemory1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(_dummyMemory2));
    }

    [Fact]
    public void CollectSatisfied_RespectsLatchedState()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3,
            Latched = true
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Memory read returns unsatisfied value 0, but since Latched is true, it wakes
        var woken = GpuWaitRegistry.CollectSatisfied(_dummyMemory1, (_, _) => 0u);

        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            Mask = 0xFF,
            ReferenceValue = 42,
            CompareFunction = 3
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(_dummyMemory1, 0x2000, 42)); // wrong address
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(_dummyMemory1, 0x1000, 10)); // wrong value

        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(_dummyMemory1, 0x1000, 42)); // matches!

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(_dummyMemory1);
        Assert.Equal(1, snapshot.Latched);
    }

    [Fact]
    public void SnapshotOutstanding_ReturnsDiagnosticsInfo()
    {
        var now = Stopwatch.GetTimestamp();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            WaitAddress = 0x1000,
            QueueName = "TestQueue",
            RegisteredTicks = now - (Stopwatch.Frequency / 2), // ~500ms ago
            Latched = true
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        var snap = GpuWaitRegistry.SnapshotOutstanding(_dummyMemory1);
        Assert.Equal(1, snap.Outstanding);
        Assert.Equal(1, snap.Latched);
        Assert.Equal(0x1000u, snap.SampleWaitAddress);
        Assert.Equal("TestQueue", snap.SampleQueueName);
        Assert.True(snap.OldestAgeMs >= 400 && snap.OldestAgeMs <= 2000);

        var emptySnap = GpuWaitRegistry.SnapshotOutstanding(_dummyMemory2);
        Assert.Equal(0, emptySnap.Outstanding);
    }

    [Fact]
    public void SnapshotInRange_FindsOverlappingWaitAddresses()
    {
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            WaitAddress = 0x1000,
            Is64Bit = false
        };
        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            WaitAddress = 0x1004,
            Is64Bit = true
        };

        GpuWaitRegistry.Register(0x1000, waiter32);
        GpuWaitRegistry.Register(0x1004, waiter64);

        // Querying empty range
        Assert.Empty(GpuWaitRegistry.SnapshotInRange(_dummyMemory1, 0x1000, 0));

        // Querying range [0x1000, 0x1004) - matches waiter32 (0x1000 .. 0x1004)
        var matches = GpuWaitRegistry.SnapshotInRange(_dummyMemory1, 0x1000, 4);
        Assert.Single(matches);
        Assert.Equal(0x1000u, matches[0].Address);
        Assert.Equal(1, matches[0].Count);

        // Querying range [0x1000, 0x100C) - matches both
        var matchesBoth = GpuWaitRegistry.SnapshotInRange(_dummyMemory1, 0x1000, 12);
        Assert.Equal(2, matchesBoth.Count);
    }

    [Fact]
    public void CollectUnreportedStale_ReportsStaleOnce()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            RegisteredTicks = 100,
            StaleReported = false
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Not stale yet (now 150 - registered 100 < maxAge 100)
        Assert.Null(GpuWaitRegistry.CollectUnreportedStale(_dummyMemory1, 150, 100));

        // Stale now (now 250 - registered 100 >= maxAge 100)
        var stale = GpuWaitRegistry.CollectUnreportedStale(_dummyMemory1, 250, 100);
        Assert.NotNull(stale);
        Assert.Single(stale);

        // Subsequent call returns null because StaleReported is true
        Assert.Null(GpuWaitRegistry.CollectUnreportedStale(_dummyMemory1, 250, 100));
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            RetryDeadlineTicks = 500
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Before deadline
        Assert.Null(GpuWaitRegistry.CollectExpiredRetries(_dummyMemory1, 400));
        Assert.Equal(1, GpuWaitRegistry.Count);

        // At/after deadline
        var expired = GpuWaitRegistry.CollectExpiredRetries(_dummyMemory1, 500);
        Assert.NotNull(expired);
        Assert.Single(expired);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesOnlyTargetMemoryWaiters()
    {
        var waiter1 = new GpuWaitRegistry.WaitingDcb { Memory = _dummyMemory1 };
        var waiter2 = new GpuWaitRegistry.WaitingDcb { Memory = _dummyMemory2 };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        var collected = GpuWaitRegistry.CollectAllForMemory(_dummyMemory1);
        Assert.NotNull(collected);
        Assert.Single(collected);

        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(_dummyMemory2));
    }

    [Fact]
    public void RecordProducedAndCollectDeadlockBroken_RecoversStuckWaiters()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = _dummyMemory1,
            RegisteredTicks = 100,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Record a produced value matching the waiter
        GpuWaitRegistry.RecordProduced(_dummyMemory1, 0x1000, 100);

        // Too young
        Assert.Null(GpuWaitRegistry.CollectDeadlockBroken(_dummyMemory1, 150, 100));

        // Old enough -> recovers waiter
        var broken = GpuWaitRegistry.CollectDeadlockBroken(_dummyMemory1, 250, 100);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
