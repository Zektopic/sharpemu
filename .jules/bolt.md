## 2023-11-20 - LINQ in Hot Paths

**Context:** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`

**Learning:** `OnBatchSubmitted` uses LINQ (`.Where(...).Select(...).ToList()`) to remove expired entries from `_hotWindows` every 1024 frames. This allocates enumerators, delegates, and intermediate lists in a graphics hot path.

**Action:** Replace `Where().Select().ToList()` pattern for Dictionary key removal with an allocation-free array pool or manually pre-allocated list/stack loop, ensuring no GC pressure is generated during routine garbage collection sweeps of internal emulator caches.

- Optimized TryUnmapBacked memory unmapping hot path allocations.
- Zero-allocation Pipeline Cache Keys in VulkanVideoPresenter.
- Optimized guest memory string comparison zero-allocation hot paths in KernelMemoryCompatExports.

## 2026-10-26 - Explicit Record Struct HashCode for Zero-Allocation Cache Keys
**Context:** `src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** The default compiler-generated `Equals` and `GetHashCode` for C# `record struct` types that contain reference types (like strings) can introduce hidden GC allocations (e.g. boxing) during high-frequency dictionary lookups, such as the PSO cache checks for Vulkan shaders.
**Action:** When designing cache keys for dictionaries in high-frequency rendering loops, explicitly implement `IEquatable<T>` and `GetHashCode()` using `HashCode.Combine` (or explicit `HashCode.Add`) and allocation-free string comparisons (e.g. `string.Equals` with `StringComparison.Ordinal`) instead of relying on the default record struct implementation.
