// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistryTests", DisableParallelization = true)]
public sealed class GpuWaitRegistryTestsCollection
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
    public void RecordProduced_WhenNoWaiterExists_ReturnsFalseAndRecordsValue()
    {
        var memory = new object();
        ulong address = 0x1000;
        ulong value = 0x42;

        var result = GpuWaitRegistry.RecordProduced(memory, address, value);

        Assert.False(result);

        // Register a waiter after RecordProduced to test deadlock breaker retrieval
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 100,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 0x42,
            CompareFunction = 3, // Equal
        };
        GpuWaitRegistry.Register(address, waiter);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(memory, nowTicks: 1000, minAgeTicks: 500);

        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(address, broken[0].WaitAddress);
    }

    [Fact]
    public void RecordProduced_WhenMatchingWaiterExistsAndSatisfied_ReturnsTrueAndLatchesWaiter()
    {
        var memory = new object();
        ulong address = 0x2000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 0x100,
            CompareFunction = 3, // Equal
            Latched = false,
        };
        GpuWaitRegistry.Register(address, waiter);

        var result = GpuWaitRegistry.RecordProduced(memory, address, 0x100);

        Assert.True(result);

        // Verify waiter was latched and collected by CollectSatisfied
        var satisfied = GpuWaitRegistry.CollectSatisfied(memory, (addr, is64Bit) => null);
        Assert.NotNull(satisfied);
        Assert.Single(satisfied);
        Assert.True(satisfied[0].Latched);
    }

    [Fact]
    public void RecordProduced_WhenMatchingWaiterExistsAndNotSatisfied_ReturnsFalseAndDoesNotLatch()
    {
        var memory = new object();
        ulong address = 0x3000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 0x100,
            CompareFunction = 3, // Equal
            Latched = false,
        };
        GpuWaitRegistry.Register(address, waiter);

        var result = GpuWaitRegistry.RecordProduced(memory, address, 0x50);

        Assert.False(result);

        // Read function returns null so non-latched waiters remain uncollected
        var satisfied = GpuWaitRegistry.CollectSatisfied(memory, (addr, is64Bit) => null);
        Assert.Null(satisfied);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void RecordProduced_CapacityLimit_ClearsLastProducedMap()
    {
        var memory = new object();

        // Populate _lastProduced up to 8192 items
        for (ulong i = 0; i < 8192; i++)
        {
            GpuWaitRegistry.RecordProduced(memory, address: i + 0x1000, value: i);
        }

        // The 8193rd item triggers _lastProduced.Clear()
        ulong newAddress = 0x9000;
        ulong newValue = 0x9999;
        GpuWaitRegistry.RecordProduced(memory, newAddress, newValue);

        // Register a waiter for an old address (0x1000) that should have been cleared from _lastProduced
        var oldWaiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 100,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 0,
            CompareFunction = 3,
        };
        GpuWaitRegistry.Register(0x1000, oldWaiter);

        // Register a waiter for the new address (0x9000) that was recorded after clearing
        var newWaiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 100,
            Mask = 0xFFFFFFFF,
            ReferenceValue = newValue,
            CompareFunction = 3,
        };
        GpuWaitRegistry.Register(newAddress, newWaiter);

        var broken = GpuWaitRegistry.CollectDeadlockBroken(memory, nowTicks: 1000, minAgeTicks: 500);

        // Only newWaiter should be deadlock-broken because old produced values were cleared
        Assert.NotNull(broken);
        Assert.Single(broken);
        Assert.Equal(newAddress, broken[0].WaitAddress);
    }

    [Fact]
    public void RecordProduced_MemoryIsolation_DoesNotSatisfyOtherMemoryInstance()
    {
        var memory1 = new object();
        var memory2 = new object();
        ulong address = 0x4000;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            Mask = 0xFFFFFFFF,
            ReferenceValue = 0x100,
            CompareFunction = 3,
            Latched = false,
        };
        GpuWaitRegistry.Register(address, waiter);

        // Record for memory2 at the same address
        var result = GpuWaitRegistry.RecordProduced(memory2, address, 0x100);

        Assert.False(result);

        // Memory1 waiter remains unlatched
        var satisfied = GpuWaitRegistry.CollectSatisfied(memory1, (addr, is64Bit) => null);
        Assert.Null(satisfied);
    }
}
