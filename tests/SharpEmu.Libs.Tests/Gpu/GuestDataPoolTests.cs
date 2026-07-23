// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers;
using SharpEmu.Libs.Gpu;
using Xunit;

namespace SharpEmu.Libs.Tests.Gpu;

public class GuestDataPoolTests
{
    [Fact]
    public void Trim_ClearsPooledArrays_ReturnsNewReferences()
    {
        // Arrange
        var pool = new GuestDataPool.BoundedByteArrayPool(
            maxArrayLength: 1024,
            maxCachedBytes: 1024 * 1024,
            maxArraysPerBucket: 2);

        // Act - Rent and return to pool the array
        byte[] array1 = pool.Rent(256);
        pool.Return(array1);

        // Assert - It was pooled
        Assert.Equal(1, pool.CachedArraysCount);
        Assert.Equal(256UL, pool.CachedBytes);

        // Act - Trim
        pool.Trim();

        // Assert - Pool is empty
        Assert.Equal(0, pool.CachedArraysCount);
        Assert.Equal(0UL, pool.CachedBytes);

        // Act - Rent again, it shouldn't be the same array
        byte[] array2 = pool.Rent(256);
        Assert.NotSame(array1, array2);
    }

    [Fact]
    public void Return_BeyondMaxArraysPerBucket_DiscardsExtraArrays()
    {
        // Arrange
        var pool = new GuestDataPool.BoundedByteArrayPool(
            maxArrayLength: 1024,
            maxCachedBytes: 1024 * 1024,
            maxArraysPerBucket: 1); // Only 1 array per bucket allowed

        byte[] array1 = pool.Rent(256);
        byte[] array2 = pool.Rent(256);

        // Assert they are from the same bucket size
        Assert.Equal(256, array1.Length);
        Assert.Equal(256, array2.Length);

        // Act
        pool.Return(array1);

        // Assert - First is pooled
        Assert.Equal(1, pool.CachedArraysCount);
        Assert.Equal(256UL, pool.CachedBytes);

        // Act
        pool.Return(array2);

        // Assert - Second is discarded, counts don't increase
        Assert.Equal(1, pool.CachedArraysCount);
        Assert.Equal(256UL, pool.CachedBytes);

        // Act - Rent should get array1
        byte[] rented1 = pool.Rent(256);
        Assert.Same(array1, rented1);

        // Act - Next rent should be a new array, not array2
        byte[] rented2 = pool.Rent(256);
        Assert.NotSame(array2, rented2);
    }

    [Fact]
    public void Return_BeyondMaxCachedBytes_DiscardsExtraArrays()
    {
        // Arrange
        var pool = new GuestDataPool.BoundedByteArrayPool(
            maxArrayLength: 1024,
            maxCachedBytes: 512, // Only 512 bytes allowed
            maxArraysPerBucket: 5);

        byte[] array1 = pool.Rent(256);
        byte[] array2 = pool.Rent(256);
        byte[] array3 = pool.Rent(256);

        // Act & Assert
        pool.Return(array1);
        Assert.Equal(1, pool.CachedArraysCount);
        Assert.Equal(256UL, pool.CachedBytes);

        pool.Return(array2);
        Assert.Equal(2, pool.CachedArraysCount);
        Assert.Equal(512UL, pool.CachedBytes);

        // This one goes beyond maxCachedBytes (512 + 256 > 512)
        pool.Return(array3);

        // It shouldn't be added
        Assert.Equal(2, pool.CachedArraysCount);
        Assert.Equal(512UL, pool.CachedBytes);
    }

    [Fact]
    public void GuestDataPool_Trim_ClearsSharedPool()
    {
        // Act - Rent and return something to the shared pool
        byte[] array1 = GuestDataPool.Shared.Rent(256);
        GuestDataPool.Shared.Return(array1);

        // Ensure there is at least something cached by looking at counts
        var pool = (GuestDataPool.BoundedByteArrayPool)GuestDataPool.Shared;
        Assert.True(pool.CachedArraysCount > 0);
        Assert.True(pool.CachedBytes > 0);

        // Act - Trim
        GuestDataPool.Trim();

        // Assert - Empty
        Assert.Equal(0, pool.CachedArraysCount);
        Assert.Equal(0UL, pool.CachedBytes);

        // Next rent should be a new array
        byte[] array2 = GuestDataPool.Shared.Rent(256);
        Assert.NotSame(array1, array2);
    }
}
