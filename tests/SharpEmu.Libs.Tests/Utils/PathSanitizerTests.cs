// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.IO;
using System.Linq;
using SharpEmu.Libs.Utils;
using Xunit;

namespace SharpEmu.Libs.Tests.Utils;

public class PathSanitizerTests
{
    [Fact]
    public void SanitizeFileName_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathSanitizer.SanitizeFileName(null!));
        Assert.Equal(string.Empty, PathSanitizer.SanitizeFileName(string.Empty));
    }

    [Fact]
    public void SanitizeFileName_ValidFileName_ReturnsSameReference()
    {
        const string validName = "default_app_name_12345";
        string result = PathSanitizer.SanitizeFileName(validName);

        Assert.Equal(validName, result);
        Assert.Same(validName, result);
    }

    [Fact]
    public void SanitizeFileName_InvalidChars_ReplacesWithUnderscore()
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (invalidChars.Length == 0)
        {
            return;
        }

        string input = $"app{invalidChars[0]}test{invalidChars[^1]}end";
        string expected = "app_test_end";
        string result = PathSanitizer.SanitizeFileName(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFileName_CustomReplacement_UsesCustomCharacter()
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (invalidChars.Length == 0)
        {
            return;
        }

        string input = $"app{invalidChars[0]}test";
        string expected = "app-test";
        string result = PathSanitizer.SanitizeFileName(input, '-');

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFileName_ValidString_ZeroAllocations()
    {
        const string validName = "CUSA12345_App_Title";

        // Warmup
        _ = PathSanitizer.SanitizeFileName(validName);

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1_000; i++)
        {
            _ = PathSanitizer.SanitizeFileName(validName);
        }
        long bytesAllocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

        Assert.Equal(0, bytesAllocated);
    }

    [Fact]
    public void SanitizeFileName_InvalidString_AllocatesSignificantlyLess()
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (invalidChars.Length == 0)
        {
            return;
        }

        string invalidInput = $"app{invalidChars[0]}dir/test";

        // Warmup
        _ = PathSanitizer.SanitizeFileName(invalidInput);

        const int iterations = 10_000;

        // Measure old LINQ approach allocations
        long linqBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            var invalid = Path.GetInvalidFileNameChars();
            _ = new string(invalidInput.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }
        long linqBytesAllocated = GC.GetAllocatedBytesForCurrentThread() - linqBytesBefore;

        // Measure PathSanitizer allocations
        long sanitizedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            _ = PathSanitizer.SanitizeFileName(invalidInput);
        }
        long sanitizedBytesAllocated = GC.GetAllocatedBytesForCurrentThread() - sanitizedBytesBefore;

        Assert.True(sanitizedBytesAllocated < linqBytesAllocated,
            $"PathSanitizer allocated {sanitizedBytesAllocated} bytes vs LINQ {linqBytesAllocated} bytes");
    }
}
