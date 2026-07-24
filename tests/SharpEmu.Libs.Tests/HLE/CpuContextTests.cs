// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Libs.Tests.HLE;

public sealed class CpuContextTests
{
    private CpuContext CreateContext()
    {
        var memory = new FakeCpuMemory(0, 0x1000);
        return new CpuContext(memory, Generation.None);
    }

    [Fact]
    public void ClearAllYmmUpper_ZerosOutAllYmmUpperRegisters()
    {
        var context = CreateContext();

        for (int i = 0; i < 16; i++)
        {
            context.SetYmmUpper(i, ulong.MaxValue, ulong.MaxValue);
        }

        context.ClearAllYmmUpper();

        for (int i = 0; i < 16; i++)
        {
            context.GetYmmUpper(i, out var low, out var high);
            Assert.Equal(0ul, low);
            Assert.Equal(0ul, high);
        }
    }

    [Fact]
    public void ClearAllYmmUpper_IsIdempotent_WhenRegistersAreAlreadyZero()
    {
        var context = CreateContext();

        // Already zero initially
        context.ClearAllYmmUpper();

        for (int i = 0; i < 16; i++)
        {
            context.GetYmmUpper(i, out var low, out var high);
            Assert.Equal(0ul, low);
            Assert.Equal(0ul, high);
        }
    }

    [Fact]
    public void ClearAllYmmUpper_DoesNotAffectOtherRegisters()
    {
        var context = CreateContext();

        // Set XMM registers
        for (int i = 0; i < 16; i++)
        {
            context.SetXmmRegister(i, ulong.MaxValue - (ulong)i, ulong.MaxValue - (ulong)i);
        }

        // Set general purpose register and other state
        context[CpuRegister.Rax] = 0x1234567890ABCDEF;
        context.Rip = 0x100;
        context.Rflags = 0x202;

        context.ClearAllYmmUpper();

        // Check XMM registers
        for (int i = 0; i < 16; i++)
        {
            context.GetXmmRegister(i, out var low, out var high);
            Assert.Equal(ulong.MaxValue - (ulong)i, low);
            Assert.Equal(ulong.MaxValue - (ulong)i, high);
        }

        // Check general purpose register and other state
        Assert.Equal(0x1234567890ABCDEFul, context[CpuRegister.Rax]);
        Assert.Equal(0x100ul, context.Rip);
        Assert.Equal(0x202ul, context.Rflags);
    }
}
