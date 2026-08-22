// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class GpuWaitRegistryTests
{
    [Fact]
    public void CollectUnreportedStale_WhenEmpty_ReturnsNull()
    {
        GpuWaitRegistry.Clear();
        var dummyMemory = new object();

        var stale = GpuWaitRegistry.CollectUnreportedStale(dummyMemory, nowTicks: 1000, maxAgeTicks: 100);

        Assert.Null(stale);
    }

    [Fact]
    public void CollectUnreportedStale_ReturnsOnlyStaleWaitersForMatchingMemory()
    {
        GpuWaitRegistry.Clear();
        var memory1 = new object();
        var memory2 = new object();

        // Registered at tick 1000
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            RegisteredTicks = 1000,
            WaitAddress = 0x1000,
            QueueName = "Queue1"
        };

        // Registered at tick 1800
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory1,
            RegisteredTicks = 1800,
            WaitAddress = 0x2000,
            QueueName = "Queue2"
        };

        // Registered at tick 1000 but for memory2
        var waiter3 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory2,
            RegisteredTicks = 1000,
            WaitAddress = 0x3000,
            QueueName = "Queue3"
        };

        GpuWaitRegistry.Register(waiter1.WaitAddress, waiter1);
        GpuWaitRegistry.Register(waiter2.WaitAddress, waiter2);
        GpuWaitRegistry.Register(waiter3.WaitAddress, waiter3);

        // nowTicks = 2000, maxAgeTicks = 500
        // waiter1 age = 1000 >= 500 (stale)
        // waiter2 age = 200 < 500 (not stale)
        // waiter3 age = 1000 >= 500 (stale, but different memory)
        var staleMemory1 = GpuWaitRegistry.CollectUnreportedStale(memory1, nowTicks: 2000, maxAgeTicks: 500);

        Assert.NotNull(staleMemory1);
        var staleList = Assert.Single(staleMemory1);
        Assert.Equal(0x1000u, staleList.WaitAddress);
        Assert.Equal("Queue1", staleList.QueueName);

        // Verify fail-closed: GpuWaitRegistry count remains 3 (waiters are not removed when reported stale)
        Assert.Equal(3, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectUnreportedStale_IsOneShotPerWaiter()
    {
        GpuWaitRegistry.Clear();
        var memory = new object();

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = memory,
            RegisteredTicks = 100,
            WaitAddress = 0x1000
        };

        GpuWaitRegistry.Register(waiter.WaitAddress, waiter);

        // First call marks stale and returns it
        var staleFirst = GpuWaitRegistry.CollectUnreportedStale(memory, nowTicks: 1000, maxAgeTicks: 200);
        Assert.NotNull(staleFirst);
        Assert.Single(staleFirst);

        // Second call with same nowTicks returns null because StaleReported is true
        var staleSecond = GpuWaitRegistry.CollectUnreportedStale(memory, nowTicks: 1000, maxAgeTicks: 200);
        Assert.Null(staleSecond);
    }
}
