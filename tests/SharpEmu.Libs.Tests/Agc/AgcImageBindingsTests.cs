// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using SharpEmu.ShaderCompiler;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcImageBindingsTests
{
    [Fact]
    public void RequiresStorageImage_AcceptsEnumerableWithoutArrayAllocation()
    {
        var resourceDesc = new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var loadBinding = new Gen5ImageBinding(
            Pc: 0x100,
            Opcode: "ImageLoad",
            Control: default!,
            ResourceDescriptor: resourceDesc,
            SamplerDescriptor: Array.Empty<uint>(),
            MipLevel: null);

        var storeBinding = new Gen5ImageBinding(
            Pc: 0x200,
            Opcode: "ImageStore",
            Control: default!,
            ResourceDescriptor: resourceDesc,
            SamplerDescriptor: Array.Empty<uint>(),
            MipLevel: null);

        var pixelBindings = new List<Gen5ImageBinding> { loadBinding };
        var exportBindings = new List<Gen5ImageBinding> { storeBinding };

        // Test IEnumerable overload without calling ToArray()
        IEnumerable<Gen5ImageBinding> combinedEnumerable = pixelBindings.Concat(exportBindings);

        bool requiresStorage = Gen5ShaderTranslator.RequiresStorageImage(loadBinding, combinedEnumerable);
        Assert.True(requiresStorage);

        // Verify single collection creation allocation delta: Concat iterator vs ToArray
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startConcat = GC.GetAllocatedBytesForCurrentThread();
        var enumerable = pixelBindings.Concat(exportBindings);
        long bytesConcat = GC.GetAllocatedBytesForCurrentThread() - startConcat;

        long startToArray = GC.GetAllocatedBytesForCurrentThread();
        var array = pixelBindings.Concat(exportBindings).ToArray();
        long bytesToArray = GC.GetAllocatedBytesForCurrentThread() - startToArray;

        Assert.NotNull(enumerable);
        Assert.NotNull(array);
        Assert.True(bytesConcat < bytesToArray, $"Expected Concat allocation ({bytesConcat} bytes) < ToArray allocation ({bytesToArray} bytes)");
    }
}
