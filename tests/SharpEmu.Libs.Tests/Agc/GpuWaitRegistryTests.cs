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
    public void SnapshotOutstanding_EmptyRegistry_ReturnsZeroes()
    {
        var snapshot = GpuWaitRegistry.SnapshotOutstanding();

        Assert.Equal(0, snapshot.Outstanding);
        Assert.Equal(0, snapshot.Latched);
        Assert.Equal(0L, snapshot.OldestAgeMs);
        Assert.Equal(0UL, snapshot.SampleWaitAddress);
        Assert.Null(snapshot.SampleQueueName);
    }

    [Fact]
    public void SnapshotOutstanding_WithWaiters_CalculatesCountsAndOldest()
    {
        var mem1 = new object();
        var mem2 = new object();
        var now = Stopwatch.GetTimestamp();
        var oldestTicks = now - (Stopwatch.Frequency * 2); // 2 seconds ago

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            RegisteredTicks = oldestTicks,
            WaitAddress = 0x1000,
            QueueName = "GFX_QUEUE",
            Latched = true
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            RegisteredTicks = now - Stopwatch.Frequency, // 1 second ago
            WaitAddress = 0x2000,
            QueueName = "COMP_QUEUE",
            Latched = false
        };

        var waiter3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            RegisteredTicks = now - (Stopwatch.Frequency * 5), // 5 seconds ago
            WaitAddress = 0x3000,
            QueueName = "OTHER_QUEUE",
            Latched = true
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x2000, waiter2);
        GpuWaitRegistry.Register(0x3000, waiter3);

        // Snapshot all memories
        var globalSnapshot = GpuWaitRegistry.SnapshotOutstanding();
        Assert.Equal(3, globalSnapshot.Outstanding);
        Assert.Equal(2, globalSnapshot.Latched);
        Assert.Equal(0x3000UL, globalSnapshot.SampleWaitAddress);
        Assert.Equal("OTHER_QUEUE", globalSnapshot.SampleQueueName);
        Assert.True(globalSnapshot.OldestAgeMs >= 4900 && globalSnapshot.OldestAgeMs <= 6000,
            $"Expected age around 5000ms, got {globalSnapshot.OldestAgeMs}");

        // Snapshot filtered by mem1
        var mem1Snapshot = GpuWaitRegistry.SnapshotOutstanding(mem1);
        Assert.Equal(2, mem1Snapshot.Outstanding);
        Assert.Equal(1, mem1Snapshot.Latched);
        Assert.Equal(0x1000UL, mem1Snapshot.SampleWaitAddress);
        Assert.Equal("GFX_QUEUE", mem1Snapshot.SampleQueueName);
        Assert.True(mem1Snapshot.OldestAgeMs >= 1900 && mem1Snapshot.OldestAgeMs <= 3000,
            $"Expected age around 2000ms, got {mem1Snapshot.OldestAgeMs}");
    }

    [Fact]
    public void SnapshotOutstanding_ZeroRegisteredTicks_IgnoredInOldestCalculation()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 0,
            WaitAddress = 0x5000,
            QueueName = "QUEUE"
        };

        GpuWaitRegistry.Register(0x5000, waiter);

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(1, snapshot.Outstanding);
        Assert.Equal(0L, snapshot.OldestAgeMs);
        Assert.Equal(0UL, snapshot.SampleWaitAddress);
        Assert.Null(snapshot.SampleQueueName);
    }

    [Fact]
    public void CountAndCountForMemory_ReturnsCorrectValues()
    {
        var mem1 = new object();
        var mem2 = new object();

        Assert.Equal(0, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));

        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem2 });
        GpuWaitRegistry.Register(0x2000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });

        Assert.Equal(3, GpuWaitRegistry.Count);
        Assert.Equal(2, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Theory]
    [InlineData(0u, 10UL, 10UL, true)]   // Function 0 (Always true)
    [InlineData(1u, 5UL, 10UL, true)]    // Function 1 (<)
    [InlineData(1u, 10UL, 10UL, false)]
    [InlineData(2u, 10UL, 10UL, true)]   // Function 2 (<=)
    [InlineData(2u, 11UL, 10UL, false)]
    [InlineData(3u, 10UL, 10UL, true)]   // Function 3 (==)
    [InlineData(3u, 11UL, 10UL, false)]
    [InlineData(4u, 11UL, 10UL, true)]   // Function 4 (!=)
    [InlineData(4u, 10UL, 10UL, false)]
    [InlineData(5u, 10UL, 10UL, true)]   // Function 5 (>=)
    [InlineData(5u, 9UL, 10UL, false)]
    [InlineData(6u, 11UL, 10UL, true)]   // Function 6 (>)
    [InlineData(6u, 10UL, 10UL, false)]
    [InlineData(7u, 0UL, 10UL, true)]    // Reserved (Defaults to true)
    public void Compare_EvaluatesConditionsWithMask(
        uint compareFunc,
        ulong value,
        ulong reference,
        bool expectedResult)
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0x00FF_FFFFUL,
            ReferenceValue = reference,
            CompareFunction = compareFunc
        };

        // Value outside mask shouldn't affect match
        var valueWithHighBits = value | 0xFF00_0000_0000_0000UL;

        Assert.Equal(expectedResult, GpuWaitRegistry.Compare(waiter, valueWithHighBits));
    }

    [Fact]
    public void CollectSatisfied_RemovesAndReturnsWokenWaiters()
    {
        var mem = new object();
        var otherMem = new object();

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 100,
            CompareFunction = 3 // ==
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = otherMem,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 100,
            CompareFunction = 3
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        // Memory reader returns 100 for 0x1000
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 100UL);

        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.Same(mem, woken[0].Memory);

        // Only otherMem remains
        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem));
    }

    [Fact]
    public void LatchSatisfiedByValue_LatchesMatchingWaiter()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFF,
            ReferenceValue = 5,
            CompareFunction = 3, // ==
            Latched = false
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Wrong value does not latch
        Assert.False(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 10));

        // Matching value latches
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(mem, 0x1000, 5));

        // Subsequent CollectSatisfied wakes latched waiter even if readValue returns unsatisfied value
        var woken = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 0UL);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.True(woken[0].Latched);
    }

    [Fact]
    public void CollectUnreportedStale_ReportsStaleWaitersOnce()
    {
        var mem = new object();
        var now = 1000L;
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RegisteredTicks = 100L,
            StaleReported = false
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Max age 500 ticks -> 1000 - 100 = 900 >= 500 (stale)
        var stale = GpuWaitRegistry.CollectUnreportedStale(mem, now, 500L);
        Assert.NotNull(stale);
        Assert.Single(stale);

        // Second check returns null since StaleReported is now true
        var staleSecond = GpuWaitRegistry.CollectUnreportedStale(mem, now, 500L);
        Assert.Null(staleSecond);

        // Waiter remains registered
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void SnapshotInRange_FindsOverlappingWaiters()
    {
        var mem = new object();
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false // 4 bytes width: 0x1000 - 0x1004
        };
        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true // 8 bytes width: 0x2000 - 0x2008
        };

        GpuWaitRegistry.Register(0x1000, waiter32);
        GpuWaitRegistry.Register(0x2000, waiter64);

        // Range overlapping 0x1000
        var range1 = GpuWaitRegistry.SnapshotInRange(mem, 0x0FFF, 10);
        Assert.Single(range1);
        Assert.Equal(0x1000UL, range1[0].Address);
        Assert.Equal(1, range1[0].Count);

        // Range overlapping 0x2004
        var range2 = GpuWaitRegistry.SnapshotInRange(mem, 0x2004, 10);
        Assert.Single(range2);
        Assert.Equal(0x2000UL, range2[0].Address);

        // Non-overlapping range
        var rangeEmpty = GpuWaitRegistry.SnapshotInRange(mem, 0x3000, 100);
        Assert.Empty(rangeEmpty);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            RetryDeadlineTicks = 500L
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Before deadline
        var expiredBefore = GpuWaitRegistry.CollectExpiredRetries(mem, 400L);
        Assert.Null(expiredBefore);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // At deadline
        var expiredAt = GpuWaitRegistry.CollectExpiredRetries(mem, 500L);
        Assert.NotNull(expiredAt);
        Assert.Single(expiredAt);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectAllForMemory_RemovesAllWaitersForSpecificMemory()
    {
        var mem1 = new object();
        var mem2 = new object();

        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x2000, new GpuWaitRegistry.WaitingDcb { Memory = mem1 });
        GpuWaitRegistry.Register(0x1000, new GpuWaitRegistry.WaitingDcb { Memory = mem2 });

        var collected = GpuWaitRegistry.CollectAllForMemory(mem1);
        Assert.NotNull(collected);
        Assert.Equal(2, collected.Count);

        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));
    }

    [Fact]
    public void DeadlockBreaker_RecordProducedAndCollectDeadlockBroken()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 42,
            CompareFunction = 3, // ==
            RegisteredTicks = 100L
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        // Record producer write (also calls LatchSatisfiedByValue)
        GpuWaitRegistry.RecordProduced(mem, 0x1000, 42);

        // Break deadlock if age >= 200 ticks
        var now = 350L;
        var minAge = 200L; // now - registered (350 - 100 = 250) >= 200

        var broken = GpuWaitRegistry.CollectDeadlockBroken(mem, now, minAge);
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
