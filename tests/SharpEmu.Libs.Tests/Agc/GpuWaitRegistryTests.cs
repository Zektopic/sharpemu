// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Diagnostics;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistryTests", DisableParallelization = true)]
public sealed class GpuWaitRegistryTestCollection : ICollectionFixture<object>
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
    public void Register_And_Count_CalculatesTotalAndPerMemoryCount()
    {
        var mem1 = new object();
        var mem2 = new object();

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
    public void Clear_RemovesAllWaitersAndProducedValues()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb { Memory = mem };
        GpuWaitRegistry.Register(0x1000, waiter);
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 42);

        GpuWaitRegistry.Clear();

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem));
    }

    [Fact]
    public void Compare_EvaluatesAllCompareFunctionsCorrectly()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0xFF,
            ReferenceValue = 10
        };

        // 0: Always true
        waiter.CompareFunction = 0;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));

        // 1: masked < reference
        waiter.CompareFunction = 1;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // 2: masked <= reference
        waiter.CompareFunction = 2;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 15));

        // 3: masked == reference
        waiter.CompareFunction = 3;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 11));

        // 4: masked != reference
        waiter.CompareFunction = 4;
        Assert.True(GpuWaitRegistry.Compare(waiter, 11));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // 5: masked >= reference
        waiter.CompareFunction = 5;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 9));

        // 6: masked > reference
        waiter.CompareFunction = 6;
        Assert.True(GpuWaitRegistry.Compare(waiter, 11));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Reserved / default: Always true
        waiter.CompareFunction = 7;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0));
    }

    [Fact]
    public void SnapshotOutstanding_ReturnsDiagnosticsInfo()
    {
        var mem = new object();
        var now = Stopwatch.GetTimestamp();

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = now - (Stopwatch.Frequency / 2),
            QueueName = "ComputeQueue",
            WaitAddress = 0x1000,
            Latched = true
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = now - (Stopwatch.Frequency / 4),
            QueueName = "GraphicsQueue",
            WaitAddress = 0x2000,
            Latched = false
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(2, snapshot.Outstanding);
        Assert.Equal(1, snapshot.Latched);
        Assert.Equal(0x1000u, snapshot.SampleWaitAddress);
        Assert.Equal("ComputeQueue", snapshot.SampleQueueName);
        Assert.True(snapshot.OldestAgeMs >= 400);
    }

    [Fact]
    public void CollectSatisfied_WakesAndRemovesMatchingWaiters()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3 // Equal
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 200,
            CompareFunction = 3 // Equal
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        // Read value returns 100 -> waiter1 satisfies, waiter2 does not
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 100);

        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(100u, woken[0].ReferenceValue);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // Subsequent collect with read value returning 200 -> waiter2 satisfies
        woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 200);

        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Equal(200u, woken[0].ReferenceValue);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectUnreportedStale_IdentifiesStaleWaitersOnce()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            StaleReported = false
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // nowTicks = 200, maxAgeTicks = 50 -> age = 100 >= 50 (stale)
        var stale = GpuWaitRegistry.CollectUnreportedStale(mem, 200, 50);

        Assert.NotNull(stale);
        Assert.Single(stale);

        // Calling again should return null since StaleReported was set to true
        var stale2 = GpuWaitRegistry.CollectUnreportedStale(mem, 200, 50);
        Assert.Null(stale2);
    }

    [Fact]
    public void SnapshotInRange_DetectsOverlappingWaitAddresses()
    {
        var mem = new object();
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false
        };
        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true
        };

        GpuWaitRegistry.Register(0x1000, waiter32);
        GpuWaitRegistry.Register(0x2000, waiter64);

        // Range [0x0F00, 0x1002) overlaps 0x1000 (32-bit: length 4)
        var matches = GpuWaitRegistry.SnapshotInRange(mem, 0x0F00, 0x0102);
        Assert.Single(matches);
        Assert.Equal(0x1000u, matches[0].Address);
        Assert.Equal(1, matches[0].Count);

        // Range [0x1000, 0x3000) overlaps both
        var allMatches = GpuWaitRegistry.SnapshotInRange(mem, 0x1000, 0x2000);
        Assert.Equal(2, allMatches.Count);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 50,
            CompareFunction = 3,
            Latched = false
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Non-matching value
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 10));

        // Matching value
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 50));

        // CollectSatisfied should drain latched waiter even if memory reader returns null
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => null);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.True(woken[0].Latched);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 500
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Before deadline
        var expired = GpuWaitRegistry.CollectExpiredRetries(mem, 400);
        Assert.Null(expired);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // At or after deadline
        expired = GpuWaitRegistry.CollectExpiredRetries(mem, 500);
        Assert.NotNull(expired);
        Assert.Single(expired);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesAllWaitersMatchingMemory()
    {
        var mem1 = new object();
        var mem2 = new object();

        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x2000, new GpuWaitRegistry.WaitingDcb { Memory = mem2 });

        var collected = GpuWaitRegistry.CollectAllForMemory(mem1);
        Assert.NotNull(collected);
        Assert.Single(collected);

        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Fact]
    public void RecordProduced_And_CollectDeadlockBroken_BreaksStaleDeadlocks()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 99,
            CompareFunction = 3,
            RegisteredTicks = 100
        };

        GpuWaitRegistry.Register(0x1000, waiter);
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 99);

        // nowTicks = 200, minAgeTicks = 150 -> age = 100 < 150 (not broken yet)
        var broken = GpuWaitRegistry.CollectDeadlockBroken(mem, 200, 150);
        Assert.Null(broken);

        // nowTicks = 300, minAgeTicks = 150 -> age = 200 >= 150 (deadlock broken)
        broken = GpuWaitRegistry.CollectDeadlockBroken(mem, 300, 150);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
