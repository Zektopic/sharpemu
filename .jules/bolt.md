## 2023-11-20 - LINQ in Hot Paths

**Context:** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`

**Learning:** `OnBatchSubmitted` uses LINQ (`.Where(...).Select(...).ToList()`) to remove expired entries from `_hotWindows` every 1024 frames. This allocates enumerators, delegates, and intermediate lists in a graphics hot path.

**Action:** Replace `Where().Select().ToList()` pattern for Dictionary key removal with an allocation-free array pool or manually pre-allocated list/stack loop, ensuring no GC pressure is generated during routine garbage collection sweeps of internal emulator caches.

- Optimized TryUnmapBacked memory unmapping hot path allocations.
- Zero-allocation Pipeline Cache Keys in VulkanVideoPresenter.
- Optimized guest memory string comparison zero-allocation hot paths in KernelMemoryCompatExports.

## 2023-11-06 - [Explicit HashCode for PSO Cache Keys]
**Context:** src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs (CacheShardKey struct)
**Learning:** Default struct equality in C# uses Reflection to generate HashCode and compare equality when the struct contains reference types like `string`. In hot paths like dictionary lookups during Vulkan Pipeline State Object (PSO) caching, this introduces boxing allocations and hidden GC pauses, causing frame-time spikes.
**Action:** Always implement `IEquatable<T>` explicitly and override `GetHashCode()` using `HashCode.Combine` for `readonly record struct` keys that contain reference fields like strings in dictionary lookups on performance-critical paths.
