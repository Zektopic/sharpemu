// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Libs.Tests;

public sealed class AerolibCatalogTests
{
    [Fact]
    public void EmbeddedCatalogResolvesKnownSymbolBothWays()
    {
        // 0x1C6A035610153249 -> sceKernelWaitSema
        Assert.True(Aerolib.Instance.TryGetByName("sceKernelWaitSema", out var byName));
        Assert.Equal("Zxa0VhQVTsk", byName.Nid);
        Assert.Equal(0x1C6A035610153249UL, byName.NumericNid);

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
}
