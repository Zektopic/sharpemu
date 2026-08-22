// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

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
    public void CollectAllForMemory_ReturnsAndRemovesWaitersForMatchingMemoryOnly()
    {
        var mem1 = new object();
        var mem2 = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            WaitAddress = 0x1000,
            CommandBufferAddress = 0x5000,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            WaitAddress = 0x1000,
            CommandBufferAddress = 0x6000,
        };

        var waiter3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            WaitAddress = 0x2000,
            CommandBufferAddress = 0x7000,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);
        GpuWaitRegistry.Register(0x2000, waiter3);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));

        var collected1 = GpuWaitRegistry.CollectAllForMemory(mem1);

        Assert.NotNull(collected1);
        Assert.Equal(2, collected1!.Count);
        Assert.Contains(collected1, w => w.CommandBufferAddress == 0x5000);
        Assert.Contains(collected1, w => w.CommandBufferAddress == 0x7000);

        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));

        var collected2 = GpuWaitRegistry.CollectAllForMemory(mem2);

        Assert.NotNull(collected2);
        Assert.Single(collected2!);
        Assert.Equal(0x6000u, collected2![0].CommandBufferAddress);

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Null(GpuWaitRegistry.CollectAllForMemory(mem1));
    }

    [Fact]
    public void Compare_HandlesAllComparisonOperators()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0xFF,
            ReferenceValue = 10,
        };

        // CompareFunction: 0 => always true
        waiter.CompareFunction = 0;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));

        // CompareFunction: 1 => <
        waiter.CompareFunction = 1;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: 2 => <=
        waiter.CompareFunction = 2;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: 3 => ==
        waiter.CompareFunction = 3;
        Assert.False(GpuWaitRegistry.Compare(waiter, 5));
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: 4 => !=
        waiter.CompareFunction = 4;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));
        Assert.True(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: 5 => >=
        waiter.CompareFunction = 5;
        Assert.False(GpuWaitRegistry.Compare(waiter, 5));
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.True(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: 6 => >
        waiter.CompareFunction = 6;
        Assert.False(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));
        Assert.True(GpuWaitRegistry.Compare(waiter, 15));

        // CompareFunction: reserved => default true
        waiter.CompareFunction = 7;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
    }

    [Fact]
    public void CollectSatisfied_CollectsLatchedAndEvaluatedWaiters()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3, // ==
            CommandBufferAddress = 0x100,
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 20,
            CompareFunction = 3, // ==
            CommandBufferAddress = 0x200,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        // Value in memory is 10 for 0x1000
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => addr == 0x1000 ? 10UL : null);

        Assert.NotNull(woken);
        Assert.Single(woken!);
        Assert.Equal(0x100u, woken![0].CommandBufferAddress);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // Next evaluation with value 20 wakes second waiter
        var woken2 = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 20UL);
        Assert.NotNull(woken2);
        Assert.Single(woken2!);
        Assert.Equal(0x200u, woken2![0].CommandBufferAddress);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 42,
            CompareFunction = 3, // ==
            CommandBufferAddress = 0x100,
        };

        GpuWaitRegistry.Register(0x2000, waiter);

        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x2000, 10)); // mismatch
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x2000, 42)); // match

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(1, snapshot.Outstanding);
        Assert.Equal(1, snapshot.Latched);

        // CollectSatisfied wakes latched waiter even if memory read fails (returns null)
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => null);
        Assert.NotNull(woken);
        Assert.Single(woken!);
        Assert.Equal(0x100u, woken![0].CommandBufferAddress);
    }

    [Fact]
    public void CollectUnreportedStale_ReturnsOnlyUnreportedStaleWaiters()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            CommandBufferAddress = 0x100,
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 500,
            CommandBufferAddress = 0x200,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);

        var nowTicks = 600;
        var maxAgeTicks = 200; // waiter1 age is 500 (stale), waiter2 age is 100 (not stale)

        var stale = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks, maxAgeTicks);
        Assert.NotNull(stale);
        Assert.Single(stale!);
        Assert.Equal(0x100u, stale![0].CommandBufferAddress);

        // Second call returns null since stale status was already reported
        var staleSecond = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks, maxAgeTicks);
        Assert.Null(staleSecond);
    }

    [Fact]
    public void SnapshotInRange_FindsOverlappingWaitAddresses()
    {
        var mem = new object();
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false, // 4-byte width
        };
        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true, // 8-byte width
        };

        GpuWaitRegistry.Register(0x1000, waiter32);
        GpuWaitRegistry.Register(0x2000, waiter64);

        // Overlaps 0x1000..0x1004
        var matches1 = GpuWaitRegistry.SnapshotInRange(mem, 0x0FF0, 0x20);
        Assert.Single(matches1);
        Assert.Equal(0x1000u, matches1[0].Address);
        Assert.Equal(1, matches1[0].Count);

        // Overlaps 0x2000..0x2008
        var matches2 = GpuWaitRegistry.SnapshotInRange(mem, 0x2004, 0x10);
        Assert.Single(matches2);
        Assert.Equal(0x2000u, matches2[0].Address);

        // Zero length returns empty list
        var matches3 = GpuWaitRegistry.SnapshotInRange(mem, 0x1000, 0);
        Assert.Empty(matches3);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 500,
            CommandBufferAddress = 0x300,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        var expiredBefore = GpuWaitRegistry.CollectExpiredRetries(mem, 400);
        Assert.Null(expiredBefore);
        Assert.Equal(1, GpuWaitRegistry.Count);

        var expiredAfter = GpuWaitRegistry.CollectExpiredRetries(mem, 500);
        Assert.NotNull(expiredAfter);
        Assert.Single(expiredAfter!);
        Assert.Equal(0x300u, expiredAfter![0].CommandBufferAddress);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void RecordProduced_And_CollectDeadlockBroken()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3, // ==
            CommandBufferAddress = 0x400,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Record produced value that satisfies condition
        var latched = GpuWaitRegistry.RecordProduced(mem, 0x1000, 100);
        Assert.True(latched);

        // Collect deadlock broken
        var broken = GpuWaitRegistry.CollectDeadlockBroken(mem, nowTicks: 1000, minAgeTicks: 500);
        Assert.NotNull(broken);
        Assert.Single(broken!);
        Assert.Equal(0x400u, broken![0].CommandBufferAddress);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
