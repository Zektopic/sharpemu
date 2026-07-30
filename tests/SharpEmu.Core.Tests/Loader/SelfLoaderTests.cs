// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using Xunit;

namespace SharpEmu.Core.Tests.Loader;

/// <summary>
/// Covers the import-stub eligibility rules and relocation value arithmetic in
/// <see cref="SelfLoader"/>. The loader carries equivalent assertions in
/// RunRelocationSelfChecks, but that method is [Conditional("DEBUG")] and CI
/// builds Release, so these paths were never actually verified by a build.
/// </summary>
public class SelfLoaderTests
{
    private static SelfLoader.RelocationDescriptor Import(
        string nid,
        bool isWeak,
        ulong targetAddress = 0x1000,
        long addend = 0) =>
        new(
            TargetAddress: targetAddress,
            Addend: addend,
            ImportNid: nid,
            SymbolValue: 0,
            ValueKind: SelfLoader.RelocationValueKind.Pointer,
            IsDataImport: false,
            WriteKind: SelfLoader.RelocationWriteKind.UInt64,
            IsWeak: isWeak);

    [Fact]
    public void ShouldCreateImportStub_StrongSymbol_CreatesStub()
    {
        var strong = Import("strong", isWeak: false);

        Assert.True(SelfLoader.ShouldCreateImportStub("strong", [strong], moduleManager: null));
    }

    [Fact]
    public void ShouldCreateImportStub_UnresolvedWeakSymbol_DoesNotCreateStub()
    {
        // An unresolved weak import must stay null rather than trapping, so it
        // must not receive a stub.
        var weak = Import("weak", isWeak: true);

        Assert.False(SelfLoader.ShouldCreateImportStub("weak", [weak], moduleManager: null));
    }

    [Fact]
    public void ShouldCreateImportStub_UnknownNid_DoesNotCreateStub()
    {
        var strong = Import("strong", isWeak: false);

        Assert.False(SelfLoader.ShouldCreateImportStub("absent", [strong], moduleManager: null));
    }

    [Fact]
    public void ShouldCreateImportStub_NidWithBothWeakAndStrongDescriptors_CreatesStub()
    {
        // A single non-weak descriptor qualifies the NID even when a weak
        // descriptor for the same NID is seen first.
        var weak = Import("mixed", isWeak: true, targetAddress: 0x2000);
        var strong = Import("mixed", isWeak: false, targetAddress: 0x3000);

        Assert.True(SelfLoader.ShouldCreateImportStub("mixed", [weak, strong], moduleManager: null));
    }

    [Fact]
    public void ShouldCreateImportStub_MatchesNidOrdinally()
    {
        var strong = Import("Strong", isWeak: false);

        Assert.False(SelfLoader.ShouldCreateImportStub("strong", [strong], moduleManager: null));
    }

    [Fact]
    public void CollectStubEligibleNids_KeepsStrongAndDropsUnresolvedWeak()
    {
        var weak = Import("weak", isWeak: true, targetAddress: 0x2000);
        var strong = Import("strong", isWeak: false, targetAddress: 0x3000);

        var eligible = SelfLoader.CollectStubEligibleNids([weak, strong], moduleManager: null);

        Assert.Contains("strong", eligible);
        Assert.DoesNotContain("weak", eligible);
    }

    [Fact]
    public void CollectStubEligibleNids_IgnoresDescriptorsWithoutAnImportNid()
    {
        var local = new SelfLoader.RelocationDescriptor(
            TargetAddress: 0x4000,
            Addend: 0,
            ImportNid: null,
            SymbolValue: 0x8000,
            ValueKind: SelfLoader.RelocationValueKind.Pointer,
            IsDataImport: false);

        Assert.Empty(SelfLoader.CollectStubEligibleNids([local], moduleManager: null));
    }

    [Fact]
    public void CollectStubEligibleNids_AgreesWithPerNidRule()
    {
        // CollectStubEligibleNids exists purely as an O(descriptors) replacement
        // for rescanning with ShouldCreateImportStub per NID. The two must not
        // disagree, which is the invariant its own comment claims.
        SelfLoader.RelocationDescriptor[] descriptors =
        [
            Import("alpha", isWeak: false, targetAddress: 0x1000),
            Import("beta", isWeak: true, targetAddress: 0x1008),
            Import("gamma", isWeak: true, targetAddress: 0x1010),
            Import("gamma", isWeak: false, targetAddress: 0x1018),
            Import("delta", isWeak: false, targetAddress: 0x1020),
            Import("beta", isWeak: true, targetAddress: 0x1028),
        ];

        var eligible = SelfLoader.CollectStubEligibleNids(descriptors, moduleManager: null);

        foreach (var nid in new[] { "alpha", "beta", "gamma", "delta", "absent" })
        {
            Assert.Equal(
                SelfLoader.ShouldCreateImportStub(nid, descriptors, moduleManager: null),
                eligible.Contains(nid));
        }
    }

    [Fact]
    public void ComputeRelocationValue_PcRelative_AppliesSymbolPlusAddendMinusPlace()
    {
        // R_X86_64_PC32: S + A - P => 0x1800 + (-4) - 0x1000 == 0x7FC.
        var pc32 = new SelfLoader.RelocationDescriptor(
            TargetAddress: 0x1000,
            Addend: -4,
            ImportNid: null,
            SymbolValue: 0x1800,
            ValueKind: SelfLoader.RelocationValueKind.PcRelative,
            IsDataImport: false,
            WriteKind: SelfLoader.RelocationWriteKind.Int32);

        Assert.Equal(
            0x7FC,
            unchecked((long)SelfLoader.ComputeRelocationValue(pc32, pc32.SymbolValue)));
    }

    [Fact]
    public void ComputeRelocationValue_UnresolvedWeak_UsesZeroSymbolValue()
    {
        // An unresolved weak relocation resolves S to 0, leaving just the addend.
        var weak = Import("weak", isWeak: true, targetAddress: 0x2000, addend: 7);

        Assert.Equal(7UL, SelfLoader.ComputeRelocationValue(weak, resolvedSymbolValue: 0));
    }

    [Fact]
    public void ComputeRelocationValue_SymbolSize_PrefersDescriptorSymbolValue()
    {
        // SymbolSize encodes the size in the descriptor itself, so the resolved
        // symbol address must be ignored.
        var sizeReloc = new SelfLoader.RelocationDescriptor(
            TargetAddress: 0x5000,
            Addend: 3,
            ImportNid: null,
            SymbolValue: 0x40,
            ValueKind: SelfLoader.RelocationValueKind.SymbolSize,
            IsDataImport: false);

        Assert.Equal(0x43UL, SelfLoader.ComputeRelocationValue(sizeReloc, resolvedSymbolValue: 0xDEAD));
    }

    [Fact]
    public void ComputeRelocationValue_Pointer_AddsAddendToResolvedSymbol()
    {
        var pointer = new SelfLoader.RelocationDescriptor(
            TargetAddress: 0x6000,
            Addend: 0x10,
            ImportNid: null,
            SymbolValue: 0,
            ValueKind: SelfLoader.RelocationValueKind.Pointer,
            IsDataImport: false);

        Assert.Equal(0x1234_0010UL, SelfLoader.ComputeRelocationValue(pointer, resolvedSymbolValue: 0x1234_0000));
    }
}
