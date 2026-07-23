using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

public class Program
{
    private enum RelocationValueKind : byte { Pointer = 0, TlsModuleId = 1, TlsOffset = 2, PcRelative = 3, SymbolSize = 4 }
    private enum RelocationWriteKind : byte { UInt64 = 0, UInt32 = 1, Int32 = 2 }
    private readonly record struct RelocationDescriptor(ulong TargetAddress, long Addend, string ImportNid, ulong SymbolValue, RelocationValueKind ValueKind, bool IsDataImport, RelocationWriteKind WriteKind = RelocationWriteKind.UInt64, bool IsWeak = false);

    private static bool ShouldCreateImportStub(string nid, IReadOnlyList<RelocationDescriptor> descriptors)
    {
        for (var i = 0; i < descriptors.Count; i++)
        {
            var descriptor = descriptors[i];
            if (!string.Equals(descriptor.ImportNid, nid, StringComparison.Ordinal)) continue;
            if (!descriptor.IsWeak) return true;
        }
        return false;
    }

    public static void RunBaseline()
    {
        var orderedImportNids = new List<string>();
        var descriptors = new List<RelocationDescriptor>();
        for (int i = 0; i < 1000; i++)
        {
            string nid = $"nid_{i}";
            orderedImportNids.Add(nid);
            descriptors.Add(new RelocationDescriptor(0, 0, nid, 0, RelocationValueKind.Pointer, false, RelocationWriteKind.UInt64, i % 2 == 0));
        }

        long initialMemory = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < 1000; iter++)
        {
            var stubImportNids = orderedImportNids.Where(nid => ShouldCreateImportStub(nid, descriptors)).ToArray();
            var stubsByAddress = new Dictionary<ulong, string>();
            for(int i = 0; i < stubImportNids.Length; i++) { stubsByAddress[(ulong)i] = stubImportNids[i]; }
            int printCount = Math.Min(10, stubImportNids.Length);
            for (int i = 0; i < printCount; i++)
            {
                var nid = stubImportNids[i];
                var addr = stubsByAddress.First(x => x.Value == nid).Key;
            }
        }
        sw.Stop();
        Console.WriteLine($"Baseline - Time: {sw.ElapsedMilliseconds}ms, Allocated: {GC.GetAllocatedBytesForCurrentThread() - initialMemory} bytes");
    }

    public static void RunOptimized()
    {
        var orderedImportNids = new List<string>();
        var descriptors = new List<RelocationDescriptor>();
        for (int i = 0; i < 1000; i++)
        {
            string nid = $"nid_{i}";
            orderedImportNids.Add(nid);
            descriptors.Add(new RelocationDescriptor(0, 0, nid, 0, RelocationValueKind.Pointer, false, RelocationWriteKind.UInt64, i % 2 == 0));
        }

        long initialMemory = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        for (int iter = 0; iter < 1000; iter++)
        {
            var stubImportNids = new List<string>(orderedImportNids.Count);
            foreach (var nid in orderedImportNids)
            {
                if (ShouldCreateImportStub(nid, descriptors)) { stubImportNids.Add(nid); }
            }
            var stubsByAddress = new Dictionary<ulong, string>();
            for(int i = 0; i < stubImportNids.Count; i++) { stubsByAddress[(ulong)i] = stubImportNids[i]; }
            int printCount = Math.Min(10, stubImportNids.Count);
            for (int i = 0; i < printCount; i++)
            {
                var nid = stubImportNids[i];
                ulong addr = 0;
                foreach (var kvp in stubsByAddress) { if (kvp.Value == nid) { addr = kvp.Key; break; } }
            }
        }
        sw.Stop();
        Console.WriteLine($"Optimized - Time: {sw.ElapsedMilliseconds}ms, Allocated: {GC.GetAllocatedBytesForCurrentThread() - initialMemory} bytes");
    }

    public static void Main()
    {
        RunBaseline(); RunOptimized();
        Console.WriteLine("--- Actual ---");
        RunBaseline(); RunOptimized();
    }
}
