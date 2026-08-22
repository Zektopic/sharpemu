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
    public void LatchSatisfiedByValue_NoWaiters_ReturnsFalse()
    {
        var memory = new object();
        var result = GpuWaitRegistry.LatchSatisfiedByValue(memory, 0x1000, 42);
        Assert.False(result);
    }

    [Fact]
    public void LatchSatisfiedByValue_MemoryMismatch_ReturnsFalse()
    {
        var memory1 = new object();
        var memory2 = new object();
        const ulong address = 0x2000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3 // Equal
        };

        GpuWaitRegistry.Register(address, waiter);

        var latched = GpuWaitRegistry.LatchSatisfiedByValue(memory2, address, 10);
        Assert.False(latched);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void LatchSatisfiedByValue_ConditionNotMet_ReturnsFalse()
    {
        var memory = new object();
        const ulong address = 0x3000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Mask = 0xFF,
            ReferenceValue = 10,
            CompareFunction = 3 // Equal
        };

        GpuWaitRegistry.Register(address, waiter);

        var latched = GpuWaitRegistry.LatchSatisfiedByValue(memory, address, 99);
        Assert.False(latched);

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(memory);
        Assert.Equal(1, snapshot.Outstanding);
        Assert.Equal(0, snapshot.Latched);
    }

    [Fact]
    public void LatchSatisfiedByValue_ConditionMet_LatchesAndReturnsTrue()
    {
        var memory = new object();
        const ulong address = 0x4000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = address,
            Mask = 0xFF,
            ReferenceValue = 0x0A,
            CompareFunction = 3 // Equal
        };

        GpuWaitRegistry.Register(address, waiter);

        var latched = GpuWaitRegistry.LatchSatisfiedByValue(memory, address, 0x0A);
        Assert.True(latched);

        var snapshot = GpuWaitRegistry.SnapshotOutstanding(memory);
        Assert.Equal(1, snapshot.Outstanding);
        Assert.Equal(1, snapshot.Latched);

        // Subsequent call when already latched should return false as latchedAny is false
        var latchedAgain = GpuWaitRegistry.LatchSatisfiedByValue(memory, address, 0x0A);
        Assert.False(latchedAgain);
    }

    [Fact]
    public void CollectSatisfied_LatchedWaiter_WakesWithoutReadingMemory()
    {
        var memory = new object();
        const ulong address = 0x5000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = address,
            Mask = 0xFF,
            ReferenceValue = 100,
            CompareFunction = 3 // Equal
        };

        GpuWaitRegistry.Register(address, waiter);
        Assert.True(GpuWaitRegistry.LatchSatisfiedByValue(memory, address, 100));

        var memoryReadCalled = false;
        var woken = GpuWaitRegistry.CollectSatisfied(memory, (addr, is64Bit) =>
        {
            memoryReadCalled = true;
            return 0; // Does not equal 100
        });

        Assert.False(memoryReadCalled);
        Assert.NotNull(woken);
        Assert.Single(woken);
        Assert.True(woken[0].Latched);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void Compare_EvaluatesCompareFunctionsCorrectly()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Mask = 0xFF,
            ReferenceValue = 10
        };

        // Function 0: Always true
        waiter.CompareFunction = 0;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0));

        // Function 1: Masked < Reference (5 < 10)
        waiter.CompareFunction = 1;
        Assert.True(GpuWaitRegistry.Compare(waiter, 5));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 2: Masked <= Reference (10 <= 10)
        waiter.CompareFunction = 2;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 15));

        // Function 3: Masked == Reference
        waiter.CompareFunction = 3;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 11));

        // Function 4: Masked != Reference
        waiter.CompareFunction = 4;
        Assert.True(GpuWaitRegistry.Compare(waiter, 11));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 5: Masked >= Reference
        waiter.CompareFunction = 5;
        Assert.True(GpuWaitRegistry.Compare(waiter, 10));
        Assert.False(GpuWaitRegistry.Compare(waiter, 5));

        // Function 6: Masked > Reference
        waiter.CompareFunction = 6;
        Assert.True(GpuWaitRegistry.Compare(waiter, 15));
        Assert.False(GpuWaitRegistry.Compare(waiter, 10));

        // Function 7+: Reserved/Default true
        waiter.CompareFunction = 7;
        Assert.True(GpuWaitRegistry.Compare(waiter, 0));
    }

    [Fact]
    public void RecordProduced_LatchesAndStoresProducedValue()
    {
        var memory = new object();
        const ulong address = 0x6000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = address,
            Mask = 0xFFFF,
            ReferenceValue = 50,
            CompareFunction = 3,
            RegisteredTicks = 1000
        };

        GpuWaitRegistry.Register(address, waiter);

        var latched = GpuWaitRegistry.RecordProduced(memory, address, 50);
        Assert.True(latched);

        var deadlockBroken = GpuWaitRegistry.CollectDeadlockBroken(memory, nowTicks: 3000, minAgeTicks: 1000);
        Assert.NotNull(deadlockBroken);
        Assert.Single(deadlockBroken);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectExpiredRetries_RemovesExpiredWaiters()
    {
        var memory = new object();
        const ulong address = 0x7000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            WaitAddress = address,
            RetryDeadlineTicks = 500
        };

        GpuWaitRegistry.Register(address, waiter);

        var expiredBefore = GpuWaitRegistry.CollectExpiredRetries(memory, nowTicks: 400);
        Assert.Null(expiredBefore);
        Assert.Equal(1, GpuWaitRegistry.Count);

        var expiredAfter = GpuWaitRegistry.CollectExpiredRetries(memory, nowTicks: 600);
        Assert.NotNull(expiredAfter);
        Assert.Single(expiredAfter);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }
}
