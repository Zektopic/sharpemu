using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

class Program {
    static void Main() {
        var ch = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
        var writer = ch.Writer;
        var reader = ch.Reader;
        var token = CancellationToken.None;

        long numIters = 1_000_000;

        // Start reader to clear items
        Task.Run(async () => {
            while (await reader.WaitToReadAsync()) {
                while (reader.TryRead(out int item)) {}
            }
        });

        // Wait a bit for reader to start
        Thread.Sleep(100);

        // Baseline: WriteAsync().AsTask().GetAwaiter().GetResult()
        GC.Collect();
        var sw1 = Stopwatch.StartNew();
        long mem1 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < numIters; i++) {
            writer.WriteAsync(i, token).AsTask().GetAwaiter().GetResult();
        }
        long memDiff1 = GC.GetAllocatedBytesForCurrentThread() - mem1;
        sw1.Stop();

        // Optimized: AsTask
        GC.Collect();
        var sw2 = Stopwatch.StartNew();
        long mem2 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < numIters; i++) {
            var vt = writer.WriteAsync(i, token);
            if (vt.IsCompletedSuccessfully) {
            } else {
                vt.AsTask().GetAwaiter().GetResult();
            }
        }
        long memDiff2 = GC.GetAllocatedBytesForCurrentThread() - mem2;
        sw2.Stop();

        Console.WriteLine($"Baseline Write: {sw1.ElapsedMilliseconds} ms, Allocations: {memDiff1} bytes");
        Console.WriteLine($"Optimized Write: {sw2.ElapsedMilliseconds} ms, Allocations: {memDiff2} bytes");
    }
}
