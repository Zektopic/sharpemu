// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

[CollectionDefinition("GpuWaitRegistryTests", DisableParallelization = true)]
public class GpuWaitRegistryCollection : ICollectionFixture<object> { }

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
    public void CollectSatisfied_ReturnsNull_WhenNoWaitersRegistered()
    {
        var mem = new object();
        var satisfied = GpuWaitRegistry.CollectSatisfied(mem, (_, _) => 100);

        Assert.Null(satisfied);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_SatisfiesWaiter_WhenCompareConditionMet()
    {
        var mem = new object();
        var address = 0x1000UL;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address,
            ReferenceValue = 42,
            Mask = 0xFF,
            CompareFunction = 3, // Equal
            Is64Bit = false,
        };

        GpuWaitRegistry.Register(address, waiter);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // First pass: value is 10 != 42 -> unsatisfied
        var satisfied1 = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 10);
        Assert.Null(satisfied1);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // Second pass: value is 42 == 42 -> satisfied
        var satisfied2 = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => 42);
        Assert.NotNull(satisfied2);
        Assert.Single(satisfied2);
        Assert.Equal(address, satisfied2[0].WaitAddress);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_KeepsWaiter_WhenMemoryReadReturnsNull()
    {
        var mem = new object();
        var address = 0x2000UL;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address,
            ReferenceValue = 100,
            Mask = 0xFFFFFFFFUL,
            CompareFunction = 3, // Equal
            Is64Bit = true,
        };

        GpuWaitRegistry.Register(address, waiter);

        var satisfied = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => null);

        Assert.Null(satisfied);
        Assert.Equal(1, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_WakesLatchedWaiter_WithoutCallingReadValue()
    {
        var mem = new object();
        var address = 0x3000UL;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address,
            Latched = true,
            CompareFunction = 3,
        };

        GpuWaitRegistry.Register(address, waiter);

        var readCalled = false;
        var satisfied = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) =>
        {
            readCalled = true;
            return 0;
        });

        Assert.False(readCalled);
        Assert.NotNull(satisfied);
        Assert.Single(satisfied);
        Assert.True(satisfied[0].Latched);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_IsolatesDifferentMemoryObjects()
    {
        var mem1 = new object();
        var mem2 = new object();
        var address = 0x4000UL;

        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            WaitAddress = address,
            ReferenceValue = 1,
            Mask = ~0UL,
            CompareFunction = 3,
        };

        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem2,
            WaitAddress = address,
            ReferenceValue = 1,
            Mask = ~0UL,
            CompareFunction = 3,
        };

        GpuWaitRegistry.Register(address, waiter1);
        GpuWaitRegistry.Register(address, waiter2);

        Assert.Equal(2, GpuWaitRegistry.Count);
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));

        // Collect for mem1 only
        var satisfiedMem1 = GpuWaitRegistry.CollectSatisfied(mem1, (_, _) => 1);
        Assert.NotNull(satisfiedMem1);
        Assert.Single(satisfiedMem1);
        Assert.Equal(1, GpuWaitRegistry.Count);
        Assert.Equal(0, GpuWaitRegistry.CountForMemory(mem1));
        Assert.Equal(1, GpuWaitRegistry.CountForMemory(mem2));

        // Collect for mem2
        var satisfiedMem2 = GpuWaitRegistry.CollectSatisfied(mem2, (_, _) => 1);
        Assert.NotNull(satisfiedMem2);
        Assert.Single(satisfiedMem2);
        Assert.Equal(0, GpuWaitRegistry.Count);
    }

    [Fact]
    public void CollectSatisfied_CleansUpEmptiedAddressEntries()
    {
        var mem = new object();
        var address1 = 0x5000UL;
        var address2 = 0x6000UL;

        GpuWaitRegistry.Register(address1, new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address1,
            ReferenceValue = 5,
            Mask = 0xFF,
            CompareFunction = 3,
        });

        GpuWaitRegistry.Register(address2, new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address2,
            ReferenceValue = 10,
            Mask = 0xFF,
            CompareFunction = 3,
        });

        // Wakes address1
        var satisfied = GpuWaitRegistry.CollectSatisfied(mem, (addr, is64) => addr == address1 ? 5UL : 0UL);

        Assert.NotNull(satisfied);
        Assert.Single(satisfied);
        Assert.Equal(address1, satisfied[0].WaitAddress);
        Assert.Equal(1, GpuWaitRegistry.Count);

        // Snapshot should show 1 remaining item
        var snapshot = GpuWaitRegistry.SnapshotOutstanding(mem);
        Assert.Equal(1, snapshot.Outstanding);
    }

    [Theory]
    [InlineData(0, 0, 10, true)]   // Compare 0: Always true
    [InlineData(1, 5, 10, true)]   // Compare 1: < (5 < 10)
    [InlineData(1, 10, 10, false)] // Compare 1: < (10 < 10)
    [InlineData(2, 10, 10, true)]  // Compare 2: <= (10 <= 10)
    [InlineData(2, 11, 10, false)] // Compare 2: <= (11 <= 10)
    [InlineData(3, 10, 10, true)]  // Compare 3: == (10 == 10)
    [InlineData(3, 9, 10, false)]  // Compare 3: == (9 == 10)
    [InlineData(4, 9, 10, true)]   // Compare 4: != (9 != 10)
    [InlineData(4, 10, 10, false)] // Compare 4: != (10 != 10)
    [InlineData(5, 10, 10, true)]  // Compare 5: >= (10 >= 10)
    [InlineData(5, 9, 10, false)]  // Compare 5: >= (9 >= 10)
    [InlineData(6, 11, 10, true)]  // Compare 6: > (11 > 10)
    [InlineData(6, 10, 10, false)] // Compare 6: > (10 > 10)
    [InlineData(7, 0, 0, true)]    // Compare 7+: Reserved, always true
    public void CollectSatisfied_EvaluatesAllCompareFunctionsCorrectly(
        uint compareFunc,
        ulong readVal,
        ulong refVal,
        bool expectedSatisfied)
    {
        var mem = new object();
        var address = 0x7000UL;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            WaitAddress = address,
            ReferenceValue = refVal,
            Mask = ~0UL,
            CompareFunction = compareFunc,
        };

        GpuWaitRegistry.Register(address, waiter);

        var satisfied = GpuWaitRegistry.CollectSatisfied(mem, (_, _) => readVal);

        if (expectedSatisfied)
        {
            Assert.NotNull(satisfied);
            Assert.Single(satisfied);
            Assert.Equal(0, GpuWaitRegistry.Count);
        }
        else
        {
            Assert.Null(satisfied);
            Assert.Equal(1, GpuWaitRegistry.Count);
        }
    }
}
