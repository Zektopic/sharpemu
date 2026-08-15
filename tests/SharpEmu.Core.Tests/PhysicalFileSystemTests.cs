// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.IO;
using Xunit;
using SharpEmu.Core;

namespace SharpEmu.Core.Tests;

public sealed class PhysicalFileSystemTests : IDisposable
{
    private readonly PhysicalFileSystem _fileSystem;
    private readonly string _tempFile;

    public PhysicalFileSystemTests()
    {
        _fileSystem = new PhysicalFileSystem();
        _tempFile = Path.GetTempFileName();
        File.WriteAllBytes(_tempFile, new byte[] { 1, 2, 3, 4 });
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try
            {
                File.Delete(_tempFile);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Exists_WhenPathIsNullOrWhiteSpace_ReturnsFalse(string path)
    {
        var result = _fileSystem.Exists(path);

        Assert.False(result);
    }

    [Fact]
    public void Exists_WhenFileExists_ReturnsTrue()
    {
        var result = _fileSystem.Exists(_tempFile);

        Assert.True(result);
    }

    [Fact]
    public void Exists_WhenFileDoesNotExist_ReturnsFalse()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = _fileSystem.Exists(nonExistentPath);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryReadAllBytes_WhenPathIsNullOrWhiteSpace_ReturnsFalseAndEmptyArray(string path)
    {
        var result = _fileSystem.TryReadAllBytes(path, out var data);

        Assert.False(result);
        Assert.Empty(data);
    }

    [Fact]
    public void TryReadAllBytes_WhenFileExists_ReturnsTrueAndFileData()
    {
        var result = _fileSystem.TryReadAllBytes(_tempFile, out var data);

        Assert.True(result);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, data);
    }

    [Fact]
    public void TryReadAllBytes_WhenFileDoesNotExist_ReturnsFalseAndEmptyArray()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var result = _fileSystem.TryReadAllBytes(nonExistentPath, out var data);

        Assert.False(result);
        Assert.Empty(data);
    }

    [Fact]
    public void TryReadAllBytes_WhenExceptionOccurs_ReturnsFalseAndEmptyArray()
    {
        // Lock the file exclusively so that TryReadAllBytes throws an IOException when attempting to read
        using var stream = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = _fileSystem.TryReadAllBytes(_tempFile, out var data);

        Assert.False(result);
        Assert.Empty(data);
    }
}
