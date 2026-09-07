// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Core.Loader;
using SharpEmu.HLE;
using System.Runtime.InteropServices;

namespace SharpEmu.Core.Memory;

public sealed class VirtualMemory : IVirtualMemory
{
    private readonly object _gate = new();
    private readonly List<MappedRegion> _regions = new();

    public void Clear()
    {
        lock (_gate)
        {
            _regions.Clear();
        }
    }

    public void Map(ulong virtualAddress, ulong memorySize, ulong fileOffset, ReadOnlySpan<byte> fileData, ProgramHeaderFlags protection)
    {
        if (memorySize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(memorySize), "Memory size must be greater than zero.");
        }

        if ((ulong)fileData.Length > memorySize)
        {
            throw new ArgumentOutOfRangeException(nameof(fileData), "File size cannot exceed memory size.");
        }

        if (memorySize > int.MaxValue)
        {
            throw new NotSupportedException("Virtual memory regions larger than 2 GB are not currently supported.");
        }

        var endAddress = checked(virtualAddress + memorySize);
        var backingMemory = new byte[(int)memorySize];
        fileData.CopyTo(backingMemory);

        lock (_gate)
        {
            var insertionIndex = FindInsertionIndex(virtualAddress);
            if ((insertionIndex > 0 && virtualAddress < _regions[insertionIndex - 1].EndAddress) ||
                (insertionIndex < _regions.Count && endAddress > _regions[insertionIndex].Region.VirtualAddress))
            {
                throw new InvalidOperationException("Attempted to map an overlapping virtual memory region.");
            }

            _regions.Insert(insertionIndex, new MappedRegion(
                new VirtualMemoryRegion(virtualAddress, memorySize, fileOffset, (ulong)fileData.Length, protection),
                endAddress,
                backingMemory));
        }
    }

    public IReadOnlyList<VirtualMemoryRegion> SnapshotRegions()
    {
        lock (_gate)
        {
            var span = CollectionsMarshal.AsSpan(_regions);
            var snapshot = new VirtualMemoryRegion[span.Length];
            for (var i = 0; i < span.Length; i++)
            {
                snapshot[i] = span[i].Region;
            }

            return snapshot;
        }
    }

    public bool TryRead(ulong virtualAddress, Span<byte> destination)
    {
        lock (_gate)
        {
            if (!TryValidateRange(virtualAddress, destination.Length, ProgramHeaderFlags.Read, out var regionIndex))
            {
                return false;
            }

            CopyFromRegions(virtualAddress, destination, regionIndex);
            return true;
        }
    }

    public bool TryWrite(ulong virtualAddress, ReadOnlySpan<byte> source)
    {
        lock (_gate)
        {
            if (!TryValidateRange(virtualAddress, source.Length, ProgramHeaderFlags.Write, out var regionIndex))
            {
                return false;
            }

            CopyToRegions(virtualAddress, source, regionIndex);
        }

        if (GuestWriteWatch.Armed)
        {
            GuestWriteWatch.Check(virtualAddress, source);
        }

        return true;
    }

    private bool TryValidateRange(
        ulong virtualAddress,
        int length,
        ProgramHeaderFlags requiredProtection,
        out int regionIndex)
    {
        regionIndex = FindContainingRegionIndex(virtualAddress);
        if (regionIndex < 0)
        {
            return false;
        }

        var span = CollectionsMarshal.AsSpan(_regions);
        var currentAddress = virtualAddress;
        var remaining = length;
        var currentIndex = regionIndex;
        while (true)
        {
            if (currentIndex >= span.Length)
            {
                return false;
            }

            var region = span[currentIndex];
            if (currentAddress < region.Region.VirtualAddress ||
                currentAddress >= region.EndAddress ||
                (region.Region.Protection & requiredProtection) == 0)
            {
                return false;
            }

            if (remaining == 0)
            {
                return true;
            }

            var available = region.EndAddress - currentAddress;
            var chunkLength = (int)Math.Min((ulong)remaining, available);
            remaining -= chunkLength;
            if (remaining == 0)
            {
                return true;
            }

            currentAddress += (ulong)chunkLength;
            currentIndex++;
        }
    }

    private int FindContainingRegionIndex(ulong virtualAddress)
    {
        var span = CollectionsMarshal.AsSpan(_regions);
        var insertionIndex = FindInsertionIndex(virtualAddress);
        if (insertionIndex < span.Length &&
            span[insertionIndex].Region.VirtualAddress == virtualAddress)
        {
            return insertionIndex;
        }

        var candidateIndex = insertionIndex - 1;
        return candidateIndex >= 0 && virtualAddress < span[candidateIndex].EndAddress
            ? candidateIndex
            : -1;
    }

    private void CopyFromRegions(ulong virtualAddress, Span<byte> destination, int regionIndex)
    {
        var span = CollectionsMarshal.AsSpan(_regions);
        var copied = 0;
        var currentAddress = virtualAddress;
        while (copied < destination.Length)
        {
            var region = span[regionIndex++];
            var regionOffset = checked((int)(currentAddress - region.Region.VirtualAddress));
            var chunkLength = Math.Min(destination.Length - copied, region.BackingMemory.Length - regionOffset);
            region.BackingMemory.AsSpan(regionOffset, chunkLength).CopyTo(destination[copied..]);
            copied += chunkLength;
            currentAddress += (ulong)chunkLength;
        }
    }

    private void CopyToRegions(ulong virtualAddress, ReadOnlySpan<byte> source, int regionIndex)
    {
        var span = CollectionsMarshal.AsSpan(_regions);
        var copied = 0;
        var currentAddress = virtualAddress;
        while (copied < source.Length)
        {
            var region = span[regionIndex++];
            var regionOffset = checked((int)(currentAddress - region.Region.VirtualAddress));
            var chunkLength = Math.Min(source.Length - copied, region.BackingMemory.Length - regionOffset);
            source.Slice(copied, chunkLength).CopyTo(region.BackingMemory.AsSpan(regionOffset, chunkLength));
            copied += chunkLength;
            currentAddress += (ulong)chunkLength;
        }
    }

    /// <summary>
    /// Finds the insertion index for a given virtual address using binary search.
    /// Optimized with <see cref="CollectionsMarshal.AsSpan"/> and the unsigned right shift
    /// operator (<c>&gt;&gt;&gt;</c>) to bypass bounds-checking and calculate the middle
    /// index efficiently, avoiding MSIL instruction overhead in this hot path.
    /// </summary>
    private int FindInsertionIndex(ulong virtualAddress)
    {
        var span = CollectionsMarshal.AsSpan(_regions);
        var lower = 0;
        var upper = span.Length;
        while (lower < upper)
        {
            var middle = (lower + upper) >>> 1;
            if (span[middle].Region.VirtualAddress < virtualAddress)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
    }

    private readonly record struct MappedRegion(VirtualMemoryRegion Region, ulong EndAddress, byte[] BackingMemory);
}
