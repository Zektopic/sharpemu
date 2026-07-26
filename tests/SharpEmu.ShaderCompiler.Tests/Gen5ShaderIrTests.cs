// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.ShaderCompiler;
using Xunit;

namespace SharpEmu.ShaderCompiler.Tests;

public sealed class Gen5ShaderIrTests
{
    [Fact]
    public void TryGetExpression_WithWorkGroupXRegister_ReturnsTrueAndCorrectExpression()
    {
        var registers = new Gen5ComputeSystemRegisters(WorkGroupXRegister: 1, null, null, null);

        var result = registers.TryGetExpression(1, out var expression);

        Assert.True(result);
        Assert.Equal("gl_WorkGroupID.x", expression);
    }

    [Fact]
    public void TryGetExpression_WithWorkGroupYRegister_ReturnsTrueAndCorrectExpression()
    {
        var registers = new Gen5ComputeSystemRegisters(null, WorkGroupYRegister: 2, null, null);

        var result = registers.TryGetExpression(2, out var expression);

        Assert.True(result);
        Assert.Equal("gl_WorkGroupID.y", expression);
    }

    [Fact]
    public void TryGetExpression_WithWorkGroupZRegister_ReturnsTrueAndCorrectExpression()
    {
        var registers = new Gen5ComputeSystemRegisters(null, null, WorkGroupZRegister: 3, null);

        var result = registers.TryGetExpression(3, out var expression);

        Assert.True(result);
        Assert.Equal("gl_WorkGroupID.z", expression);
    }

    [Fact]
    public void TryGetExpression_WithThreadGroupSizeRegister_ReturnsTrueAndCorrectExpression()
    {
        var registers = new Gen5ComputeSystemRegisters(null, null, null, ThreadGroupSizeRegister: 4);

        var result = registers.TryGetExpression(4, out var expression);

        Assert.True(result);
        Assert.Equal("(gl_WorkGroupSize.x * gl_WorkGroupSize.y * gl_WorkGroupSize.z)", expression);
    }

    [Fact]
    public void TryGetExpression_WithUnmatchedRegister_ReturnsFalseAndEmptyString()
    {
        var registers = new Gen5ComputeSystemRegisters(1, 2, 3, 4);

        var result = registers.TryGetExpression(5, out var expression);

        Assert.False(result);
        Assert.Equal(string.Empty, expression);
    }

    [Fact]
    public void TryGetExpression_WithNullRegisters_DoesNotMatchZero()
    {
        var registers = new Gen5ComputeSystemRegisters(null, null, null, null);

        var result = registers.TryGetExpression(0, out var expression);

        Assert.False(result);
        Assert.Equal(string.Empty, expression);
    }
}
