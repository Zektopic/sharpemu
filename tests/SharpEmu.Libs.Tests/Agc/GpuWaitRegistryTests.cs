// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistry", DisableParallelization = true)]
public class GpuWaitRegistryCollection : ICollectionFixture<object>
{
}

[Collection("GpuWaitRegistry")]
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
    public void CollectDeadlockBroken_ReturnsNullWhenNoWaiters()
    {
        var dummyMemory = new object();
        var broken = GpuWaitRegistry.CollectDeadlockBroken(dummyMemory, nowTicks: 1000, minAgeTicks: 100);
        Assert.Null(broken);
    }

    [Fact]
    public void CollectDeadlockBroken_ReturnsNullWhenAgeLessThanMinAge()
    {
        var dummyMemory = new object();
        const ulong address = 0x1000;

        GpuWaitRegistry.RecordProduced(dummyMemory, address, 10);

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = dummyMemory,
            RegisteredTicks = 950,
            CompareFunction = 3, // ==
            ReferenceValue = 10,
            Mask = 0xFFFFFFFF,
        };
        GpuWaitRegistry.Register(address, waiter);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(dummyMemory, nowTicks: 1000, minAgeTicks: 100);
        Assert.Null(broken);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectDeadlockBroken_ReturnsNullWhenMemoryDoesNotMatch()
    {
        var memory1 = new object();
        var memory2 = new object();
        const ulong address = 0x1000;

        GpuWaitRegistry.RecordProduced(memory1, address, 10);

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            RegisteredTicks = 100,
            CompareFunction = 3, // ==
            ReferenceValue = 10,
            Mask = 0xFFFFFFFF,
        };
        GpuWaitRegistry.Register(address, waiter);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(memory2, nowTicks: 1000, minAgeTicks: 100);
        Assert.Null(broken);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectDeadlockBroken_ReturnsNullWhenNotProducedOrUnsatisfied()
    {
        var dummyMemory = new object();
        const ulong address = 0x1000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = dummyMemory,
            RegisteredTicks = 100,
            CompareFunction = 3, // ==
            ReferenceValue = 10,
            Mask = 0xFFFFFFFF,
        };
        GpuWaitRegistry.Register(address, waiter);

        // Case 1: No produced value recorded
        var broken1 = GpuWaitRegistry.CollectDeadlockBroken(dummyMemory, nowTicks: 1000, minAgeTicks: 100);
        Assert.Null(broken1);

        // Case 2: Produced value doesn't satisfy compare function
        GpuWaitRegistry.RecordProduced(dummyMemory, address, 5);
        var broken2 = GpuWaitRegistry.CollectDeadlockBroken(dummyMemory, nowTicks: 1000, minAgeTicks: 100);
        Assert.Null(broken2);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectDeadlockBroken_CollectsAndRemovesSatisfiedStaleWaiters()
    {
        var dummyMemory = new object();
        const ulong address = 0x1000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CommandBufferAddress = 0x5000,
            Memory = dummyMemory,
            RegisteredTicks = 100,
            CompareFunction = 5, // >=
            ReferenceValue = 10,
            Mask = 0xFFFFFFFF,
        };
        GpuWaitRegistry.Register(address, waiter);

        GpuWaitRegistry.RecordProduced(dummyMemory, address, 15);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(dummyMemory, nowTicks: 1000, minAgeTicks: 100);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0x5000u, broken[0].CommandBufferAddress);

        // Check registry is now empty
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Theory]
    [InlineData(0, 5, 10, true)]   // Always true
    [InlineData(1, 5, 10, true)]   // < : 5 < 10
    [InlineData(1, 10, 10, false)] // < : 10 < 10
    [InlineData(2, 10, 10, true)]  // <= : 10 <= 10
    [InlineData(2, 11, 10, false)] // <= : 11 <= 10
    [InlineData(3, 10, 10, true)]  // == : 10 == 10
    [InlineData(3, 9, 10, false)]  // == : 9 == 10
    [InlineData(4, 9, 10, true)]   // != : 9 != 10
    [InlineData(4, 10, 10, false)] // != : 10 != 10
    [InlineData(5, 10, 10, true)]  // >= : 10 >= 10
    [InlineData(5, 9, 10, false)]  // >= : 9 >= 10
    [InlineData(6, 11, 10, true)]  // > : 11 > 10
    [InlineData(6, 10, 10, false)] // > : 10 > 10
    [InlineData(7, 5, 10, true)]   // Reserved -> true
    [InlineData(99, 5, 10, true)]  // Reserved -> true
    public void Compare_EvaluatesConditionsCorrectly(uint func, ulong val, ulong refVal, bool expected)
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CompareFunction = func,
            ReferenceValue = refVal,
            Mask = 0xFFFFFFFF,
        };

        var actual = GpuWaitRegistry.Compare(waiter, val);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Compare_AppliesMaskToValueAndReference()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CompareFunction = 3, // ==
            ReferenceValue = 0x00FF,
            Mask = 0x00FF,
        };

        // Value 0x01FF masked by 0x00FF becomes 0x00FF, matching reference 0x00FF.
        Assert.True(GpuWaitRegistry.Compare(waiter, 0x01FF));
        // Value 0x0100 masked by 0x00FF becomes 0x0000, not matching reference 0x00FF.
        Assert.False(GpuWaitRegistry.Compare(waiter, 0x0100));
    }

    [Fact]
    public void Register_And_CountForMemory_BehaveCorrectly()
    {
        var mem1 = new object();
        var mem2 = new object();

        var w1 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var w2 = new GpuWaitRegistry.WaitingDcb { Memory = mem1 };
        var w3 = new GpuWaitRegistry.WaitingDcb { Memory = mem2 };

        GpuWaitRegistry.Register(0x1000, w1);
        GpuWaitRegistry.Register(0x1000, w2);
        GpuWaitRegistry.Register(0x2000, w3);

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(new object()));
    }

    [Fact]
    public void SnapshotOutstanding_ReturnsDiagnosticsSnapshot()
    {
        var mem = new object();
        var now = Stopwatch.GetTimestamp();

        var w1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Latched = true,
            RegisteredTicks = now - 1000,
            WaitAddress = 0x1000,
            QueueName = "MainQueue",
        };
        var w2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Latched = false,
            RegisteredTicks = now - 500,
            WaitAddress = 0x2000,
            QueueName = "ComputeQueue",
        };

        GpuWaitRegistry.Register(0x1000, w1);
        GpuWaitRegistry.Register(0x2000, w2);

        var snap = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(2, snap.Outstanding);
        Assert.Equal(1, snap.Latched);
        Assert.Equal(0x1000u, snap.SampleWaitAddress);
        Assert.Equal("MainQueue", snap.SampleQueueName);
    }

    [Fact]
    public void CollectSatisfied_WakesLatchedAndMemoryMatchedWaiters()
    {
        var mem = new object();
        const ulong addr1 = 0x1000;
        const ulong addr2 = 0x2000;

        var w1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Latched = true,
            CommandBufferAddress = 101,
        };
        var w2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Latched = false,
            CompareFunction = 3, // ==
            ReferenceValue = 42,
            Mask = 0xFFFFFFFF,
            CommandBufferAddress = 102,
        };
        var w3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Latched = false,
            CompareFunction = 3, // ==
            ReferenceValue = 100,
            Mask = 0xFFFFFFFF,
            CommandBufferAddress = 103,
        };

        GpuWaitRegistry.Register(addr1, w1);
        GpuWaitRegistry.Register(addr2, w2);
        GpuWaitRegistry.Register(addr2, w3);

        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) =>
        {
            if (addr == addr2)
            {
                return 42u; // Satisfies w2, but not w3
            }
            return null;
        });

        Assert.NotNull(woken);
        Assert.Equal(2, woken.Count);
        Assert.Contains(woken, w => w.CommandBufferAddress == 101);
        Assert.Contains(woken, w => w.CommandBufferAddress == 102);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectUnreportedStale_ReportsStaleWaitersOnce()
    {
        var mem = new object();
        var w1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100,
            StaleReported = false,
            CommandBufferAddress = 1,
        };

        GpuWaitRegistry.Register(0x1000, w1);

        var stale1 = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks: 1000, maxAgeTicks: 500);
        Assert.NotNull(stale1);
        Assert.Single(stale1);
        Assert.Equal(1u, stale1[0].CommandBufferAddress);

        // Second call should return null because StaleReported is now true
        var stale2 = GpuWaitRegistry.CollectUnreportedStale(mem, nowTicks: 1000, maxAgeTicks: 500);
        Assert.Null(stale2);
    }

    [Fact]
    public void SnapshotInRange_MatchesOverlappingAddresses()
    {
        var mem = new object();
        var w32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };
        var w64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true,
        };

        GpuWaitRegistry.Register(0x1000, w32); // 0x1000 - 0x1004
        GpuWaitRegistry.Register(0x1010, w64); // 0x1010 - 0x1018

        var matches = GpuWaitRegistry.SnapshotInRange(mem, start: 0x1002, length: 0x10);
        Assert.Equal(2, matches.Count);

        var emptyMatch = GpuWaitRegistry.SnapshotInRange(mem, start: 0x2000, length: 0x10);
        Assert.Empty(emptyMatch);
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiters()
    {
        var mem = new object();
        const ulong addr = 0x1000;
        var w = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            CompareFunction = 3, // ==
            ReferenceValue = 99,
            Mask = 0xFFFFFFFF,
            Latched = false,
        };

        GpuWaitRegistry.Register(addr, w);

        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, addr, 99));

        // Registry should still hold the waiter, but now latched
        var snap = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(1, snap.Latched);

        // Calling again on latched waiter returns false
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, addr, 99));
    }

    [Fact]
    public void CollectExpiredRetries_CollectsWaitersPastDeadline()
    {
        var mem = new object();
        var w1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 500,
            CommandBufferAddress = 1,
        };
        var w2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 1500,
            CommandBufferAddress = 2,
        };

        GpuWaitRegistry.Register(0x1000, w1);
        GpuWaitRegistry.Register(0x2000, w2);

        var expired = GpuWaitRegistry.CollectExpiredRetries(mem, nowTicks: 1000);
        Assert.NotNull(expired);
        Assert.Single(expired);
        Assert.Equal(1u, expired[0].CommandBufferAddress);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesAllWaitersForMemory()
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
    }
}
