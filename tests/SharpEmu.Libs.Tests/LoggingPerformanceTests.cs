using System;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace SharpEmu.Libs.Tests;

public class LoggingPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public LoggingPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestHexAllocation()
    {
        byte[] data = new byte[128];
        new System.Random(42).NextBytes(data);
        ReadOnlySpan<byte> span = data;

        // Warmup
        _ = BitConverter.ToString(span.ToArray()).Replace("-", " ");

        long beforeOld = GC.GetAllocatedBytesForCurrentThread();
        string oldResult = BitConverter.ToString(span.ToArray()).Replace("-", " ");
        long oldAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeOld;

        // Try our custom optimized method
        long beforeNew = GC.GetAllocatedBytesForCurrentThread();
        string newResult = FormatHexBytes(span);
        long newAllocations = GC.GetAllocatedBytesForCurrentThread() - beforeNew;

        _output.WriteLine($"Old Result: {oldResult}");
        _output.WriteLine($"New Result: {newResult}");
        _output.WriteLine($"Old Allocations: {oldAllocations} bytes");
        _output.WriteLine($"New Allocations: {newAllocations} bytes");

        Assert.Equal(oldResult, newResult);
        Assert.True(newAllocations < oldAllocations);
    }

    private unsafe static string FormatHexBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return string.Empty;

        fixed (byte* ptr = &MemoryMarshal.GetReference(bytes))
        {
            var ptrCopy = (IntPtr)ptr;
            int length = bytes.Length;
            return string.Create(length * 3 - 1, (ptrCopy, length), (span, state) =>
            {
                var (p, len) = state;
                var readSpan = new ReadOnlySpan<byte>((void*)p, len);
                for (int i = 0; i < readSpan.Length; i++)
                {
                    byte b = readSpan[i];
                    int highNibble = b >> 4;
                    int lowNibble = b & 0x0F;

                    span[i * 3] = (char)(highNibble < 10 ? highNibble + '0' : highNibble - 10 + 'A');
                    span[i * 3 + 1] = (char)(lowNibble < 10 ? lowNibble + '0' : lowNibble - 10 + 'A');

                    if (i < readSpan.Length - 1)
                    {
                        span[i * 3 + 2] = ' ';
                    }
                }
            });
        }
    }
}
