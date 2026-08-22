// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

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
    public void SnapshotInRange_ReturnsEmpty_WhenLengthIsZero()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        var matches = GpuWaitRegistry.SnapshotInRange(mem, 0x1000, 0);

        Assert.Empty(matches);
    }

    [Fact]
    public void SnapshotInRange_ReturnsMatches_WhenWaitersMatchMemoryAndRange()
    {
        var mem = new object();
        var waiter1 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };
        var waiter2 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true,
        };

        GpuWaitRegistry.Register(0x1000, waiter1);
        GpuWaitRegistry.Register(0x1000, waiter2);

        var matches = GpuWaitRegistry.SnapshotInRange(mem, 0x0F00, 0x200);

        Assert.Single(matches);
        Assert.Equal((0x1000u, 2), matches[0]);
    }

    [Fact]
    public void SnapshotInRange_IgnoresWaitersWithDifferentMemory()
    {
        var mem1 = new object();
        var mem2 = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem1,
            Is64Bit = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter);

        var matches = GpuWaitRegistry.SnapshotInRange(mem2, 0x1000, 0x10);

        Assert.Empty(matches);
    }

    [Fact]
    public void SnapshotInRange_Distinguishes32BitAnd64BitWidths()
    {
        var mem = new object();

        // 32-bit waiter at 0x1000 occupies [0x1000, 0x1004)
        var waiter32 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };
        GpuWaitRegistry.Register(0x1000, waiter32);

        // Query range [0x1004, 0x1008) -> Should not match 32-bit waiter
        var matches32 = GpuWaitRegistry.SnapshotInRange(mem, 0x1004, 4);
        Assert.Empty(matches32);

        GpuWaitRegistry.Clear();

        // 64-bit waiter at 0x1000 occupies [0x1000, 0x1008)
        var waiter64 = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true,
        };
        GpuWaitRegistry.Register(0x1000, waiter64);

        // Query range [0x1004, 0x1008) -> Should match 64-bit waiter
        var matches64 = GpuWaitRegistry.SnapshotInRange(mem, 0x1004, 4);
        Assert.Single(matches64);
        Assert.Equal((0x1000u, 1), matches64[0]);
    }

    [Fact]
    public void SnapshotInRange_ReturnsEmpty_WhenAddressIsOutOfRange()
    {
        var mem = new object();
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = false,
        };

        GpuWaitRegistry.Register(0x1000, waiter); // Waiter range [0x1000, 0x1004)

        // Query range entirely before waiter
        var matchesBefore = GpuWaitRegistry.SnapshotInRange(mem, 0x0000, 0x0F00);
        Assert.Empty(matchesBefore);

        // Query range starting at or after waiter end
        var matchesAfter = GpuWaitRegistry.SnapshotInRange(mem, 0x1004, 0x0500);
        Assert.Empty(matchesAfter);
    }

    [Fact]
    public void SnapshotInRange_HandlesAddressOverflowNearUlongMax()
    {
        var mem = new object();
        var address = ulong.MaxValue - 2;

        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            Memory = mem,
            Is64Bit = true, // width 8, waitEnd overflows to ulong.MaxValue
        };

        GpuWaitRegistry.Register(address, waiter);

        // Query range starting near max and length overflowing
        var matches = GpuWaitRegistry.SnapshotInRange(mem, ulong.MaxValue - 10, 20);

        Assert.Single(matches);
        Assert.Equal((address, 1), matches[0]);
    }
}
