// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using SharpEmu.ShaderCompiler;
using Xunit;

namespace SharpEmu.ShaderCompiler.Tests;

public class Gen5ShaderIrTests
{
    [Fact]
    public void Gen5ComputeSystemRegisters_TryGetExpression_ShouldReturnCorrectExpressions()
    {
        var registers = new Gen5ComputeSystemRegisters(
            WorkGroupXRegister: 10,
            WorkGroupYRegister: 11,
            WorkGroupZRegister: 12,
            ThreadGroupSizeRegister: 13);

        Assert.True(registers.TryGetExpression(10, out var exprX));
        Assert.Equal("gl_WorkGroupID.x", exprX);

        Assert.True(registers.TryGetExpression(11, out var exprY));
        Assert.Equal("gl_WorkGroupID.y", exprY);

        Assert.True(registers.TryGetExpression(12, out var exprZ));
        Assert.Equal("gl_WorkGroupID.z", exprZ);

        Assert.True(registers.TryGetExpression(13, out var exprSize));
        Assert.Equal("(gl_WorkGroupSize.x * gl_WorkGroupSize.y * gl_WorkGroupSize.z)", exprSize);

        Assert.False(registers.TryGetExpression(14, out var exprUnknown));
        Assert.Equal(string.Empty, exprUnknown);
    }

    [Fact]
    public void Gen5ComputeSystemRegisters_ClearStaticValues_ShouldClearSpecifiedRegisters()
    {
        var registers = new Gen5ComputeSystemRegisters(
            WorkGroupXRegister: 0,
            WorkGroupYRegister: 1,
            WorkGroupZRegister: null,
            ThreadGroupSizeRegister: 3);

        Span<uint> scalarRegisters = stackalloc uint[] { 100, 200, 300, 400, 500 };

        registers.ClearStaticValues(scalarRegisters);

        Assert.Equal(0u, scalarRegisters[0]);
        Assert.Equal(0u, scalarRegisters[1]);
        Assert.Equal(300u, scalarRegisters[2]); // Not cleared
        Assert.Equal(0u, scalarRegisters[3]);
        Assert.Equal(500u, scalarRegisters[4]); // Not cleared
    }

    [Theory]
    [InlineData(256, Gen5OperandKind.VectorRegister, 0)]
    [InlineData(257, Gen5OperandKind.VectorRegister, 1)]
    [InlineData(249, Gen5OperandKind.LiteralConstant, 0x1234)]
    [InlineData(255, Gen5OperandKind.LiteralConstant, 0x1234)]
    [InlineData(0, Gen5OperandKind.ScalarRegister, 0)]
    [InlineData(105, Gen5OperandKind.ScalarRegister, 105)]
    [InlineData(106, Gen5OperandKind.ScalarRegister, 106)]
    [InlineData(107, Gen5OperandKind.ScalarRegister, 107)]
    [InlineData(124, Gen5OperandKind.ScalarRegister, 124)]
    [InlineData(126, Gen5OperandKind.ScalarRegister, 126)]
    [InlineData(127, Gen5OperandKind.ScalarRegister, 127)]
    [InlineData(128, Gen5OperandKind.EncodedConstant, 128)]
    [InlineData(248, Gen5OperandKind.EncodedConstant, 248)]
    public void Gen5Operand_Source_ShouldReturnCorrectOperand(uint encoded, Gen5OperandKind expectedKind, uint expectedValue)
    {
        uint? literal = encoded == 249 || encoded == 255 ? 0x1234u : null;
        var operand = Gen5Operand.Source(encoded, literal);

        Assert.Equal(expectedKind, operand.Kind);
        Assert.Equal(expectedValue, operand.Value);
    }

    [Fact]
    public void Gen5Operand_ToString_ShouldFormatCorrectly()
    {
        Assert.Equal("s5", Gen5Operand.Scalar(5).ToString());
        Assert.Equal("v10", Gen5Operand.Vector(10).ToString());
        Assert.Equal("0x0000ABCD", new Gen5Operand(Gen5OperandKind.LiteralConstant, 0xABCD).ToString());
        Assert.Equal("src[150]", new Gen5Operand(Gen5OperandKind.EncodedConstant, 150).ToString());
    }

    [Fact]
    public void Gen5ImageControl_GetAddressRegister_ShouldReturnCorrectRegister()
    {
        var control = new Gen5ImageControl(
            Dmask: 0,
            VectorAddress: 100,
            AddressRegisters: new uint[] { 10, 11, 12 },
            VectorData: 0,
            ScalarResource: 0,
            ScalarSampler: 0,
            Dimension: 0,
            IsArray: false,
            Glc: false,
            Slc: false,
            A16: false,
            D16: false);

        Assert.Equal(10u, control.GetAddressRegister(0));
        Assert.Equal(11u, control.GetAddressRegister(1));
        Assert.Equal(12u, control.GetAddressRegister(2));
        Assert.Equal(103u, control.GetAddressRegister(3)); // VectorAddress + 3
    }

    [Fact]
    public void Gen5ShaderProgram_Properties_ShouldComputeCorrectly()
    {
        var exportControl = new Gen5ExportControl(
            Target: 1, // Color target 1
            EnableMask: 0x3, // Enable red and green
            Compressed: false,
            Done: false,
            ValidMask: false);

        var imageControl = new Gen5ImageControl(
            Dmask: 0,
            VectorAddress: 0,
            AddressRegisters: Array.Empty<uint>(),
            VectorData: 0,
            ScalarResource: 0,
            ScalarSampler: 0,
            Dimension: 0,
            IsArray: false,
            Glc: false,
            Slc: false,
            A16: false,
            D16: false);

        var instructions = new List<Gen5ShaderInstruction>
        {
            // Instruction 1: Has ExportControl
            new Gen5ShaderInstruction(
                Pc: 0,
                Encoding: Gen5ShaderEncoding.Exp,
                Opcode: "Exp",
                Words: Array.Empty<uint>(),
                Sources: new[] { Gen5Operand.Scalar(5), Gen5Operand.Vector(10) },
                Destinations: new[] { Gen5Operand.Scalar(20) },
                Control: exportControl),

            // Instruction 2: Has ImageControl
            new Gen5ShaderInstruction(
                Pc: 4,
                Encoding: Gen5ShaderEncoding.Mimg,
                Opcode: "ImageSample",
                Words: Array.Empty<uint>(),
                Sources: Array.Empty<Gen5Operand>(),
                Destinations: Array.Empty<Gen5Operand>(),
                Control: imageControl)
        };

        var program = new Gen5ShaderProgram(0x1000, instructions);

        // Test PixelColorExportMasks (Target=1 -> shift by 4, EnableMask=3 -> 0x30)
        Assert.Equal(0x30u, program.PixelColorExportMasks);

        // Test ImageResources
        var imageResources = program.ImageResources.ToList();
        Assert.Single(imageResources);
        Assert.Same(imageControl, imageResources[0]);

        // Test RuntimeScalarRegisters caching
        var runtimeRegisters = program.RuntimeScalarRegisters;
        Assert.Equal(2, runtimeRegisters.Count);
        Assert.Contains(5u, runtimeRegisters);
        Assert.Contains(20u, runtimeRegisters);

        // Verify it's cached (returns same instance)
        var cachedRegisters = program.RuntimeScalarRegisters;
        Assert.Same(runtimeRegisters, cachedRegisters);
    }
}
