## 2023-11-20 - LINQ in Hot Paths

**Context:** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`

**Learning:** `OnBatchSubmitted` uses LINQ (`.Where(...).Select(...).ToList()`) to remove expired entries from `_hotWindows` every 1024 frames. This allocates enumerators, delegates, and intermediate lists in a graphics hot path.

**Action:** Replace `Where().Select().ToList()` pattern for Dictionary key removal with an allocation-free array pool or manually pre-allocated list/stack loop, ensuring no GC pressure is generated during routine garbage collection sweeps of internal emulator caches.

- Optimized TryUnmapBacked memory unmapping hot path allocations.
- Zero-allocation Pipeline Cache Keys in VulkanVideoPresenter.
