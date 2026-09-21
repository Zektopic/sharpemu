// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Libs.Tests;

/// <summary>
/// Guards the build-time-generated aerolib.bin embedding: the catalog must load from
/// the assembly and resolve both directions, or the loader's import naming, the
/// not-implemented diagnostics, and runtime dlsym all silently degrade.
/// </summary>
public sealed class AerolibCatalogTests
{
    [Fact]
    public void EmbeddedCatalogResolvesKnownSymbolBothWays()
    {
        Assert.True(Aerolib.Instance.TryGetByExportName("sceKernelWaitSema", out var byName));
        Assert.Equal("Zxa0VhQVTsk", byName.Nid);

        Assert.True(Aerolib.Instance.TryGetByNid("Zxa0VhQVTsk", out var byNid));
        Assert.Equal("sceKernelWaitSema", byNid.ExportName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetName_NullOrEmptyNid_ReturnsFalseAndEmptyName(string? nid)
    {
        var result = Aerolib.Instance.TryGetName(nid!, out var name);

        Assert.False(result);
        Assert.Equal(string.Empty, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetByNid_ThrowsArgumentException_WhenNidIsNullOrWhiteSpace(string? nid)
    {
        var ex = Record.Exception(() => Aerolib.Instance.TryGetByNid(nid!, out _));
        Assert.IsAssignableFrom<ArgumentException>(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContainsNid_NullOrEmptyString_ReturnsFalse(string? nid)
    {
        Assert.False(Aerolib.Instance.ContainsNid(nid!));
    }

    [Fact]
    public void TryGetByExportName_EmptyString_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => Aerolib.Instance.TryGetByExportName("", out _));
    }
}
