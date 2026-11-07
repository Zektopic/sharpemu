## 2023-11-20 - LINQ in Hot Paths

**Context:** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`

**Learning:** `OnBatchSubmitted` uses LINQ (`.Where(...).Select(...).ToList()`) to remove expired entries from `_hotWindows` every 1024 frames. This allocates enumerators, delegates, and intermediate lists in a graphics hot path.

**Action:** Replace `Where().Select().ToList()` pattern for Dictionary key removal with an allocation-free array pool or manually pre-allocated list/stack loop, ensuring no GC pressure is generated during routine garbage collection sweeps of internal emulator caches.

- Optimized TryUnmapBacked memory unmapping hot path allocations.
- Zero-allocation Pipeline Cache Keys in VulkanVideoPresenter.
- Optimized guest memory string comparison zero-allocation hot paths in KernelMemoryCompatExports.

## 2026-11-07 - Hidden Boxing Allocations in Record Struct Dictionary Lookups
**Context:** Graphics Pipeline (Vulkan/MoltenVK) / `VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** In C#, declaring a `readonly record struct` that contains nullable reference type fields (like `string?`) forces the compiler-generated `Equals` and `GetHashCode` implementations to potentially box those references when checking equality via default generic comparers. When used as a key in `Dictionary<TKey, TValue>` for high-frequency operations like Vulkan Pipeline State Object (PSO) caching, this introduces hidden per-frame garbage collection allocations.
**Action:** Always explicitly implement `IEquatable<T>` and override `GetHashCode` using `HashCode.Combine(...)` for hot-path struct keys containing reference types to guarantee zero-allocation structural equality.
