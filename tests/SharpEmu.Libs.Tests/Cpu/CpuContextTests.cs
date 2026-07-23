// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Libs.Tests.Cpu;

public sealed class CpuContextTests
{
    private CpuContext CreateContext()
    {
        var memory = new FakeCpuMemory(0, 1024);
        return new CpuContext(memory, Generation.Gen4);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SetXmmRegister_InvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        var cpuContext = CreateContext();
        Assert.Throws<ArgumentOutOfRangeException>(() => cpuContext.SetXmmRegister(invalidIndex, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    public void SetXmmRegister_ValidIndex_SetsValues(int validIndex)
    {
        var cpuContext = CreateContext();
        ulong expectedLow = (ulong)validIndex * 10;
        ulong expectedHigh = (ulong)validIndex * 10 + 1;

        cpuContext.SetXmmRegister(validIndex, expectedLow, expectedHigh);

        cpuContext.GetXmmRegister(validIndex, out ulong actualLow, out ulong actualHigh);
        Assert.Equal(expectedLow, actualLow);
        Assert.Equal(expectedHigh, actualHigh);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetXmmRegister_InvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        var cpuContext = CreateContext();
        Assert.Throws<ArgumentOutOfRangeException>(() => cpuContext.GetXmmRegister(invalidIndex, out _, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    public void GetXmmRegister_ValidIndex_GetsValues(int validIndex)
    {
        var cpuContext = CreateContext();
        ulong expectedLow = (ulong)validIndex * 10 + 2;
        ulong expectedHigh = (ulong)validIndex * 10 + 3;

        // Rely on SetXmmRegister (already tested) to setup state
        cpuContext.SetXmmRegister(validIndex, expectedLow, expectedHigh);

        cpuContext.GetXmmRegister(validIndex, out ulong actualLow, out ulong actualHigh);
        Assert.Equal(expectedLow, actualLow);
        Assert.Equal(expectedHigh, actualHigh);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SetYmmUpper_InvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        var cpuContext = CreateContext();
        Assert.Throws<ArgumentOutOfRangeException>(() => cpuContext.SetYmmUpper(invalidIndex, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    public void SetYmmUpper_ValidIndex_SetsValues(int validIndex)
    {
        var cpuContext = CreateContext();
        ulong expectedLow = (ulong)validIndex * 10 + 4;
        ulong expectedHigh = (ulong)validIndex * 10 + 5;

        cpuContext.SetYmmUpper(validIndex, expectedLow, expectedHigh);

        cpuContext.GetYmmUpper(validIndex, out ulong actualLow, out ulong actualHigh);
        Assert.Equal(expectedLow, actualLow);
        Assert.Equal(expectedHigh, actualHigh);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetYmmUpper_InvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        var cpuContext = CreateContext();
        Assert.Throws<ArgumentOutOfRangeException>(() => cpuContext.GetYmmUpper(invalidIndex, out _, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    public void GetYmmUpper_ValidIndex_GetsValues(int validIndex)
    {
        var cpuContext = CreateContext();
        ulong expectedLow = (ulong)validIndex * 10 + 6;
        ulong expectedHigh = (ulong)validIndex * 10 + 7;

        // Rely on SetYmmUpper (already tested) to setup state
        cpuContext.SetYmmUpper(validIndex, expectedLow, expectedHigh);

        cpuContext.GetYmmUpper(validIndex, out ulong actualLow, out ulong actualHigh);
        Assert.Equal(expectedLow, actualLow);
        Assert.Equal(expectedHigh, actualHigh);
    }
}
