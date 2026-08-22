// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Buffers;
using System.IO;

namespace SharpEmu.Libs.Utils;

/// <summary>
/// Provides high-performance file name and path sanitization utilities.
/// </summary>
internal static class PathSanitizer
{
    /// <summary>
    /// SIMD-accelerated SearchValues lookup set of invalid file name characters.
    /// </summary>
    private static readonly SearchValues<char> InvalidFileNameCharsSearchValues = SearchValues.Create(Path.GetInvalidFileNameChars());

    /// <summary>
    /// Replaces invalid file name characters in <paramref name="fileName"/> with <paramref name="replacement"/>.
    /// </summary>
    /// <param name="fileName">The file name or application string to sanitize.</param>
    /// <param name="replacement">The character to replace invalid characters with. Defaults to '_'.</param>
    /// <returns>The sanitized file name, or the original string reference if no invalid characters were found.</returns>
    /// <remarks>
    /// Performance Rationale: Bypasses O(N^2) LINQ string transformations, delegate allocations, and intermediate array allocations.
    /// Uses SearchValues SIMD vectorization for an allocation-free O(1) fast path when no invalid characters exist,
    /// and single-pass string.Create when replacement is required.
    /// </remarks>
    public static string SanitizeFileName(string fileName, char replacement = '_')
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = fileName.AsSpan();
        int firstInvalidIndex = span.IndexOfAny(InvalidFileNameCharsSearchValues);
        if (firstInvalidIndex < 0)
        {
            return fileName;
        }

        return string.Create(fileName.Length, (fileName, replacement), static (destination, state) =>
        {
            ReadOnlySpan<char> source = state.fileName.AsSpan();
            char repl = state.replacement;
            for (int i = 0; i < source.Length; i++)
            {
                char ch = source[i];
                destination[i] = InvalidFileNameCharsSearchValues.Contains(ch) ? repl : ch;
            }
        });
    }
}
