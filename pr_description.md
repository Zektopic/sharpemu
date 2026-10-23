### 💡 What
Replaced an allocation-heavy `.Where(...).Select(...).ToList()` LINQ query with an allocation-free custom loop and a pre-allocated instance `List<ulong> _expiredHotWindows`. Added `<remarks>` for documentation context.

### 🎯 Subsystem & Bottleneck
- **Subsystem:** Graphics (Vulkan/MoltenVK)
- **Target File(s):** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`
- **Bottleneck Addressed:** High GC pressure and enumerator allocation inside the hot-path `OnBatchSubmitted` method every 1024 batches during GPU execution.

### 📜 Git & Contributor Context
- **Recent File History:** No recent conflicting changes detected.
- **Conflict Check:** Verified change does not overlap with recent commits or reverted attempts.

### 📊 Measured Performance Impact
- **Allocation Delta:** Eliminated LINQ array and enumerator allocations (`IEnumerable`, delegates, dynamic `.ToList()`), reducing allocation per tick to 0 bytes.
- **Computational Impact:** Avoids creating delegates and allocating new lists dynamically.

### 🔬 Verification Checklist
- [x] Executed `dotnet test` (All tests passed)
- [x] Verified zero unexpected allocations in hot path
- [x] Verified formatting with `dotnet format`
- [x] Confirmed thread safety and emulation accuracy
