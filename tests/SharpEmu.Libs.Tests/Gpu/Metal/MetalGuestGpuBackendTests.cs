// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Threading;
using SharpEmu.Libs.Gpu;
using SharpEmu.Libs.Gpu.Metal;
using SharpEmu.ShaderCompiler;
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
    public void TryCompileVertexShader_EmptyProgram_Fails()
    {
        // Arrange
        var backend = new MetalGuestGpuBackend();
        var program = new Gen5ShaderProgram(0x1000, Array.Empty<Gen5ShaderInstruction>());
        var state = new Gen5ShaderState(program, new uint[16], null);
        var evaluation = new Gen5ShaderEvaluation(
            new uint[128],
            new uint[128],
            Array.Empty<Gen5ImageBinding>(),
            Array.Empty<Gen5GlobalMemoryBinding>());

        // Act
        var result = backend.TryCompileVertexShader(
            state,
            evaluation,
            out var shader,
            out var error);

        // Assert
        Assert.False(result);
        Assert.Null(shader);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryCompileVertexShader_WithValidProgram_Succeeds()
    {
        // Arrange
        var backend = new MetalGuestGpuBackend();
        var instruction = new Gen5ShaderInstruction(
            0,
            Gen5ShaderEncoding.Sopp,
            "SEndpgm",
            new[] { 0xBF810000u },
            Array.Empty<Gen5Operand>(),
            Array.Empty<Gen5Operand>(),
            null);
        var program = new Gen5ShaderProgram(0x1000, new[] { instruction });
        var state = new Gen5ShaderState(program, new uint[16], null);
        var evaluation = new Gen5ShaderEvaluation(
            new uint[128],
            new uint[128],
            Array.Empty<Gen5ImageBinding>(),
            Array.Empty<Gen5GlobalMemoryBinding>());

        // Act
        var result = backend.TryCompileVertexShader(
            state,
            evaluation,
            out var shader,
            out var error);

        // Assert
        Assert.True(result);
        Assert.NotNull(shader);
        Assert.Equal("msl", shader.PayloadFileExtension);
        Assert.Equal(string.Empty, error);
    }

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
