// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class GpuWaitRegistryTests
{
    [Theory]
    [InlineData(0u, 100ul, 200ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(1u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(1u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(1u, 150ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(2u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(2u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(2u, 150ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(3u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(3u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(4u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(4u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(5u, 150ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(5u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(5u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(6u, 150ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(6u, 100ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(6u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, false)]
    [InlineData(7u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    [InlineData(8u, 50ul, 100ul, 0xFFFF_FFFF_FFFF_FFFFul, true)]
    public void Compare_EvaluatesCompareFunctionCorrectly(
        uint compareFunction,
        ulong value,
        ulong referenceValue,
        ulong mask,
        bool expected)
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CompareFunction = compareFunction,
            ReferenceValue = referenceValue,
            Mask = mask,
        };

        var result = GpuWaitRegistry.Compare(waiter, value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Compare_AppliesMaskToValueAndReference()
    {
        var waiter = new GpuWaitRegistry.WaitingDcb
        {
            CompareFunction = 3,
            ReferenceValue = 0xFF00_0005ul,
            Mask = 0x0000_FFFFul,
        };

        var result = GpuWaitRegistry.Compare(waiter, 0x1234_0005ul);

        Assert.True(result);
    }
}
