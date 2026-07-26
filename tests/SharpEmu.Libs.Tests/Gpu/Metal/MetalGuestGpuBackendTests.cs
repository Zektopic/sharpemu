// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using SharpEmu.Libs.Gpu;
using SharpEmu.Libs.Gpu.Metal;
using SharpEmu.ShaderCompiler;
using Xunit;

namespace SharpEmu.Libs.Tests.Gpu.Metal;

public class MetalGuestGpuBackendTests
{
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
}
