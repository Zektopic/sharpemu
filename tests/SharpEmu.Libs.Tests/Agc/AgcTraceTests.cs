// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Reflection;
using SharpEmu.ShaderCompiler;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public class AgcTraceTests
{
    [Fact]
    public void TraceAstroTitlePixelGlobalProbe_ExecutesWithoutError()
    {
        // Arrange
        var binding1 = new Gen5GlobalMemoryBinding(
            ScalarAddress: 0,
            BaseAddress: 0x1000,
            InstructionPcs: new List<uint>(),
            Data: new byte[20000],
            DataLength: 20000,
            DataPooled: false)
        {
            Writable = false
        };

        var binding2 = new Gen5GlobalMemoryBinding(
            ScalarAddress: 4,
            BaseAddress: 0x2000,
            InstructionPcs: new List<uint>(),
            Data: new byte[1000],
            DataLength: 1000, // < 17216 + 16, so probe logic will continue
            DataPooled: false)
        {
            Writable = false
        };

        var bindings = new List<Gen5GlobalMemoryBinding> { binding1, binding2 };

        var evaluation = new Gen5ShaderEvaluation(
            InitialScalarRegisters: new List<uint>(),
            ScalarRegisters: new List<uint>(),
            ImageBindings: new List<Gen5ImageBinding>(),
            GlobalMemoryBindings: bindings);

        var method = typeof(SharpEmu.Libs.Agc.AgcExports).GetMethod(
            "TraceAstroTitlePixelGlobalProbe",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        // Act & Assert (Should execute without throwing)
        method.Invoke(null, new object[] { evaluation });
    }
}
