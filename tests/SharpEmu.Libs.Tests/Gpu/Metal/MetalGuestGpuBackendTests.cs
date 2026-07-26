// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Threading;
using SharpEmu.Libs.Gpu.Metal;
using Xunit;

namespace SharpEmu.Libs.Tests.Gpu.Metal;

// Because MetalVideoPresenter is a static class using locks and shared static state,
// we cannot run tests in parallel that modify its state.
[Collection("MetalVideoPresenterTests")]
public class MetalGuestGpuBackendTests
{
    private static ulong _nextAddress = 0x1000_0000;

    private static ulong GetUniqueAddress() => (ulong)Interlocked.Add(ref _nextAddress, 0x1000);

    [Fact]
    public void IsGpuGuestImageAvailable_ReturnsTrue_WhenRegisteredAndMatches()
    {
        var backend = new MetalGuestGpuBackend();
        var address = GetUniqueAddress();
        uint format = 1; // Valid format
        uint numberType = 2;

        // MetalVideoPresenter stores guest formats based on GetGuestTextureFormat logic.
        // It's encoded as: 0x8000_0000u | ((format & 0x1FFu) << 8) | (numberType & 0xFFu)
        uint expectedGuestFormat = 0x8000_0000u | ((format & 0x1FFu) << 8) | (numberType & 0xFFu);

        backend.RegisterKnownDisplayBuffer(address, expectedGuestFormat);

        Assert.True(backend.IsGpuGuestImageAvailable(address, format, numberType));
    }

    [Fact]
    public void IsGpuGuestImageAvailable_ReturnsFalse_WhenAddressUnregistered()
    {
        var backend = new MetalGuestGpuBackend();
        var address = GetUniqueAddress();

        Assert.False(backend.IsGpuGuestImageAvailable(address, 1, 2));
    }

    [Fact]
    public void IsGpuGuestImageAvailable_ReturnsFalse_WhenFormatMismatch()
    {
        var backend = new MetalGuestGpuBackend();
        var address = GetUniqueAddress();
        uint format = 1;
        uint numberType = 2;

        uint expectedGuestFormat = 0x8000_0000u | ((format & 0x1FFu) << 8) | (numberType & 0xFFu);

        backend.RegisterKnownDisplayBuffer(address, expectedGuestFormat);

        // Ask for a different numberType
        Assert.False(backend.IsGpuGuestImageAvailable(address, format, numberType + 1));

        // Ask for a different format
        Assert.False(backend.IsGpuGuestImageAvailable(address, format + 1, numberType));
    }

    [Fact]
    public void IsGpuGuestImageAvailable_ReturnsFalse_WhenFormatIsUnknown()
    {
        var backend = new MetalGuestGpuBackend();
        var address = GetUniqueAddress();

        uint unknownFormat = 20; // 20 is not in the known list
        uint numberType = 2;

        // Try to register and check
        uint expectedGuestFormat = 0x8000_0000u | ((unknownFormat & 0x1FFu) << 8) | (numberType & 0xFFu);
        backend.RegisterKnownDisplayBuffer(address, expectedGuestFormat);

        Assert.False(backend.IsGpuGuestImageAvailable(address, unknownFormat, numberType));
    }

    [Fact]
    public void IsGpuGuestImageAvailable_ReturnsFalse_WhenAddressIsZero()
    {
        var backend = new MetalGuestGpuBackend();

        Assert.False(backend.IsGpuGuestImageAvailable(0, 1, 2));
    }
}
