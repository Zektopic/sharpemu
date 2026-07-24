// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using SharpEmu.HLE;
using Xunit;

namespace SharpEmu.Libs.Tests.HLE;

public sealed class CpuContextTests
{
    private const ulong MemoryBase = 0x1_0000_0000;
    private const int MemorySize = 0x10000;

    private static CpuContext CreateContext()
    {
        var memory = new FakeCpuMemory(MemoryBase, MemorySize);
        return new CpuContext(memory, Generation.Gen5);
    }

    [Fact]
    public void Constructor_RequiresMemory()
    {
        Assert.Throws<ArgumentNullException>("memory", () => new CpuContext(null!, Generation.Gen5));
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var context = CreateContext();

        Assert.Equal(Generation.Gen5, context.TargetGeneration);
        Assert.Equal(0x037F, context.FpuControlWord);
        Assert.Equal(0x1F80u, context.Mxcsr);
        Assert.NotNull(context.Memory);
    }

    [Theory]
    [InlineData(CpuRegister.Rax)]
    [InlineData(CpuRegister.Rcx)]
    [InlineData(CpuRegister.Rdx)]
    [InlineData(CpuRegister.Rbx)]
    [InlineData(CpuRegister.Rsp)]
    [InlineData(CpuRegister.Rbp)]
    [InlineData(CpuRegister.Rsi)]
    [InlineData(CpuRegister.Rdi)]
    [InlineData(CpuRegister.R8)]
    [InlineData(CpuRegister.R9)]
    [InlineData(CpuRegister.R10)]
    [InlineData(CpuRegister.R11)]
    [InlineData(CpuRegister.R12)]
    [InlineData(CpuRegister.R13)]
    [InlineData(CpuRegister.R14)]
    [InlineData(CpuRegister.R15)]
    public void Indexer_GetsAndSetsGeneralPurposeRegisters(CpuRegister register)
    {
        var context = CreateContext();

        context[register] = 0x1234567890ABCDEF;

        Assert.Equal(0x1234567890ABCDEFul, context[register]);
    }

    [Fact]
    public void RaxWritten_IsTrackedWhenRaxIsSet()
    {
        var context = CreateContext();

        Assert.False(context.WasRaxWritten);

        context[CpuRegister.Rbx] = 1;
        Assert.False(context.WasRaxWritten);

        context[CpuRegister.Rax] = 1;
        Assert.True(context.WasRaxWritten);

        context.ClearRaxWriteFlag();
        Assert.False(context.WasRaxWritten);
    }

    [Fact]
    public void GeneralStateProperties_GetAndSetSuccessfully()
    {
        var context = CreateContext();

        context.Rip = 0x1234;
        Assert.Equal(0x1234ul, context.Rip);

        context.Rflags = 0x5678;
        Assert.Equal(0x5678ul, context.Rflags);

        context.FsBase = 0x9ABC;
        Assert.Equal(0x9ABCul, context.FsBase);

        context.GsBase = 0xDEF0;
        Assert.Equal(0xDEF0ul, context.GsBase);

        context.FpuControlWord = 0x1111;
        Assert.Equal((ushort)0x1111, context.FpuControlWord);

        context.Mxcsr = 0x2222;
        Assert.Equal(0x2222u, context.Mxcsr);
    }

    [Fact]
    public void XmmRegisters_GetAndSetSuccessfully()
    {
        var context = CreateContext();

        context.SetXmmRegister(5, 0x1122334455667788, 0x99AABBCCDDEEFF00);
        context.GetXmmRegister(5, out var low, out var high);

        Assert.Equal(0x1122334455667788ul, low);
        Assert.Equal(0x99AABBCCDDEEFF00ul, high);
    }

    [Fact]
    public void XmmRegisters_ThrowsOnOutOfBounds()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.GetXmmRegister(16, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.SetXmmRegister(16, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.GetXmmRegister(-1, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.SetXmmRegister(-1, 0, 0));
    }

    [Fact]
    public void YmmUpperRegisters_GetAndSetSuccessfully()
    {
        var context = CreateContext();

        context.SetYmmUpper(10, 0x1234, 0x5678);
        context.GetYmmUpper(10, out var low, out var high);

        Assert.Equal(0x1234ul, low);
        Assert.Equal(0x5678ul, high);
    }

    [Fact]
    public void YmmUpperRegisters_ThrowsOnOutOfBounds()
    {
        var context = CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.GetYmmUpper(16, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.SetYmmUpper(16, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.GetYmmUpper(-1, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>("registerIndex", () => context.SetYmmUpper(-1, 0, 0));
    }

    [Fact]
    public void ClearYmmUpper_ClearsSpecificRegister()
    {
        var context = CreateContext();

        context.SetYmmUpper(3, 0x1111, 0x2222);
        context.ClearYmmUpper(3);
        context.GetYmmUpper(3, out var low, out var high);

        Assert.Equal(0ul, low);
        Assert.Equal(0ul, high);
    }

    [Fact]
    public void ClearAllYmmUpper_ClearsAllRegisters()
    {
        var context = CreateContext();

        for (int i = 0; i < 16; i++)
        {
            context.SetYmmUpper(i, 0x1, 0x2);
        }

        context.ClearAllYmmUpper();

        for (int i = 0; i < 16; i++)
        {
            context.GetYmmUpper(i, out var low, out var high);
            Assert.Equal(0ul, low);
            Assert.Equal(0ul, high);
        }
    }

    [Fact]
    public void YmmRegisters_GetAndSetSuccessfully()
    {
        var context = CreateContext();

        context.SetYmmRegister(7, 0x11, 0x22, 0x33, 0x44);
        context.GetYmmRegister(7, out var lowLow, out var lowHigh, out var highLow, out var highHigh);

        Assert.Equal(0x11ul, lowLow);
        Assert.Equal(0x22ul, lowHigh);
        Assert.Equal(0x33ul, highLow);
        Assert.Equal(0x44ul, highHigh);

        context.GetXmmRegister(7, out var xmmLow, out var xmmHigh);
        Assert.Equal(0x11ul, xmmLow);
        Assert.Equal(0x22ul, xmmHigh);

        context.GetYmmUpper(7, out var ymmUpperLow, out var ymmUpperHigh);
        Assert.Equal(0x33ul, ymmUpperLow);
        Assert.Equal(0x44ul, ymmUpperHigh);
    }

    [Fact]
    public void MemoryReadWrite_Byte_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        context.Memory.TryWrite(MemoryBase, stackalloc byte[] { 0xAB });

        Assert.True(context.TryReadByte(MemoryBase, out var readValue));
        Assert.Equal(0xAB, readValue);
    }

    [Fact]
    public void MemoryReadWrite_UInt16_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        Assert.True(context.TryWriteUInt16(MemoryBase + 2, 0x1234));
        Assert.True(context.TryReadUInt16(MemoryBase + 2, out var readValue));
        Assert.Equal(0x1234, readValue);
    }

    [Fact]
    public void MemoryReadWrite_Int32_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        Assert.True(context.TryWriteInt32(MemoryBase + 4, -123456));
        Assert.True(context.TryReadInt32(MemoryBase + 4, out var readValue));
        Assert.Equal(-123456, readValue);
    }

    [Fact]
    public void MemoryWriteInt32_WithCheckNil_FailsOnZeroAddress()
    {
        var context = CreateContext();

        Assert.False(context.TryWriteInt32(0, 123456, checkNil: true));
    }

    [Fact]
    public void MemoryReadWrite_UInt32_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        Assert.True(context.TryWriteUInt32(MemoryBase + 8, 0xDEADBEEF));
        Assert.True(context.TryReadUInt32(MemoryBase + 8, out var readValue));
        Assert.Equal(0xDEADBEEF, readValue);
    }

    [Fact]
    public void MemoryReadWrite_Int64_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        Assert.True(context.TryWriteInt64(MemoryBase + 12, -0x123456789ABCDEF));

        // Use UInt64 read since there's no TryReadInt64
        Assert.True(context.TryReadUInt64(MemoryBase + 12, out var readValue));
        Assert.Equal(unchecked((ulong)-0x123456789ABCDEF), readValue);
    }

    [Fact]
    public void MemoryReadWrite_UInt64_SucceedsOnMappedMemory()
    {
        var context = CreateContext();

        Assert.True(context.TryWriteUInt64(MemoryBase + 20, 0x1234567890ABCDEF));
        Assert.True(context.TryReadUInt64(MemoryBase + 20, out var readValue));
        Assert.Equal(0x1234567890ABCDEFul, readValue);
    }

    [Fact]
    public void MemoryOperations_FailsOnUnmappedMemory()
    {
        var context = CreateContext();
        ulong unmappedAddress = 0x2_0000_0000;

        Assert.False(context.TryReadByte(unmappedAddress, out var bVal));
        Assert.Equal(0, bVal);

        Assert.False(context.TryReadUInt16(unmappedAddress, out var usVal));
        Assert.Equal(0, usVal);

        Assert.False(context.TryWriteUInt16(unmappedAddress, 0));

        Assert.False(context.TryReadInt32(unmappedAddress, out var iVal));
        Assert.Equal(0, iVal);

        Assert.False(context.TryWriteInt32(unmappedAddress, 0));

        Assert.False(context.TryReadUInt32(unmappedAddress, out var uVal));
        Assert.Equal(0u, uVal);

        Assert.False(context.TryWriteUInt32(unmappedAddress, 0));

        Assert.False(context.TryReadUInt64(unmappedAddress, out var ulVal));
        Assert.Equal(0ul, ulVal);

        Assert.False(context.TryWriteInt64(unmappedAddress, 0));
        Assert.False(context.TryWriteUInt64(unmappedAddress, 0));
    }

    [Fact]
    public void ReadNullTerminatedUtf8_SucceedsWithValidString()
    {
        var context = CreateContext();
        string expected = "Hello, world!";
        ((FakeCpuMemory)context.Memory).WriteCString(MemoryBase, expected);

        Assert.True(context.TryReadNullTerminatedUtf8(MemoryBase, 256, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ReadNullTerminatedUtf8_FailsOnZeroAddressOrCapacity()
    {
        var context = CreateContext();

        Assert.False(context.TryReadNullTerminatedUtf8(0, 256, out var value1));
        Assert.Equal(string.Empty, value1);

        Assert.False(context.TryReadNullTerminatedUtf8(MemoryBase, 0, out var value2));
        Assert.Equal(string.Empty, value2);

        Assert.False(context.TryReadNullTerminatedUtf8(MemoryBase, -1, out var value3));
        Assert.Equal(string.Empty, value3);
    }

    [Fact]
    public void ReadNullTerminatedUtf8_FailsIfNoNullTerminatorWithinCapacity()
    {
        var context = CreateContext();

        // Write a string without null terminator within capacity
        var bytes = System.Text.Encoding.UTF8.GetBytes("abcdefghij"); // 10 bytes
        context.Memory.TryWrite(MemoryBase, bytes);

        // At the 10th byte there will be zeroes because of FakeCpuMemory initialization,
        // so let's overwrite it with non-zero bytes past the capacity to ensure it hits capacity limit.
        Span<byte> aBytes = stackalloc byte[] { 0x41 };
        for (int i = 0; i < 20; i++)
        {
            context.Memory.TryWrite(MemoryBase + (ulong)i, aBytes); // 'A'
        }

        // It should read up to capacity without finding a null terminator. The implementation uses value = Encoding.UTF8.GetString(bytes[..capacity]); and returns true if it hits capacity, meaning it truncated the string.
        Assert.True(context.TryReadNullTerminatedUtf8(MemoryBase, 10, out var value));
        Assert.Equal(new string('A', 10), value);
    }

    [Fact]
    public void ReadNullTerminatedUtf8_GracefullyHandlesMemoryBounds()
    {
        var context = CreateContext();

        // Write non-zero right up to the end of memory
        ulong endOfMemory = MemoryBase + MemorySize;
        ulong startAddress = endOfMemory - 5;

        Span<byte> bBytes = stackalloc byte[] { 0x42 };
        for (ulong i = startAddress; i < endOfMemory; i++)
        {
            context.Memory.TryWrite(i, bBytes); // 'B'
        }

        // Capacity goes past mapped memory. The function should drop to per-byte read and eventually fail on unmapped memory.
        Assert.False(context.TryReadNullTerminatedUtf8(startAddress, 10, out _));
    }

    [Fact]
    public void StackOperations_PushAndPop_SucceedOnMappedMemory()
    {
        var context = CreateContext();

        // Setup a valid Rsp address in mapped memory
        ulong initialRsp = MemoryBase + 1024;
        context[CpuRegister.Rsp] = initialRsp;

        Assert.True(context.PushUInt64(0x9988776655443322));

        // Rsp should decrease by 8 (sizeof(ulong))
        Assert.Equal(initialRsp - 8, context[CpuRegister.Rsp]);

        Assert.True(context.PopUInt64(out var poppedValue));

        // Rsp should go back to initial
        Assert.Equal(initialRsp, context[CpuRegister.Rsp]);
        Assert.Equal(0x9988776655443322ul, poppedValue);
    }

    [Fact]
    public void StackOperations_PushAndPop_FailOnUnmappedMemory()
    {
        var context = CreateContext();

        // Setup Rsp to 0, pushing will go out of bounds/wrap and unmapped
        context[CpuRegister.Rsp] = 0;

        Assert.False(context.PushUInt64(0x1234));

        // Try to pop from unmapped memory
        context[CpuRegister.Rsp] = 0x2_0000_0000;
        Assert.False(context.PopUInt64(out var value));
        Assert.Equal(0ul, value);
    }

    [Fact]
    public void SetReturn_Int_SetsRaxCorrectly()
    {
        var context = CreateContext();

        int result = context.SetReturn(-1);
        Assert.Equal(-1, result);
        Assert.Equal(unchecked((ulong)-1), context[CpuRegister.Rax]);
    }

    [Fact]
    public void SetReturn_Int_WithLongCastType_SetsRaxCorrectly()
    {
        var context = CreateContext();

        int result = context.SetReturn(-1, typeof(long));
        Assert.Equal(-1, result);
        Assert.Equal(unchecked((ulong)(long)-1), context[CpuRegister.Rax]);
    }

    [Fact]
    public void SetReturn_Int_WithUnsupportedCastType_ThrowsNotSupportedException()
    {
        var context = CreateContext();

        Assert.Throws<NotSupportedException>(() => context.SetReturn(1, typeof(string)));
    }

    [Fact]
    public void SetReturn_OrbisGen2Result_SetsRaxCorrectly()
    {
        var context = CreateContext();

        int result = context.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_PERMISSION_DENIED);
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_ERROR_PERMISSION_DENIED, result);
        Assert.Equal(unchecked((ulong)(int)OrbisGen2Result.ORBIS_GEN2_ERROR_PERMISSION_DENIED), context[CpuRegister.Rax]);
    }
}
