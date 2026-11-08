## 2023-11-20 - LINQ in Hot Paths

**Context:** `src/SharpEmu.Libs/Gpu/Buffers/GuestBufferCache.cs`

**Learning:** `OnBatchSubmitted` uses LINQ (`.Where(...).Select(...).ToList()`) to remove expired entries from `_hotWindows` every 1024 frames. This allocates enumerators, delegates, and intermediate lists in a graphics hot path.

**Action:** Replace `Where().Select().ToList()` pattern for Dictionary key removal with an allocation-free array pool or manually pre-allocated list/stack loop, ensuring no GC pressure is generated during routine garbage collection sweeps of internal emulator caches.

- Optimized TryUnmapBacked memory unmapping hot path allocations.
- Zero-allocation Pipeline Cache Keys in VulkanVideoPresenter.
- Optimized guest memory string comparison zero-allocation hot paths in KernelMemoryCompatExports.

## 2026-10-27 - PSO Cache Thread-Safe Struct Keys
**Context:** Vulkan PSO Caching (`ShaderPipelineCache.cs` and `PipelineKeys.cs`)
**Learning:** When migrating a caching mechanism's key from a heap-allocated `sealed class` to a zero-allocation `readonly record struct`, any existing `[ThreadStatic]` cache object must be fundamentally refactored. You cannot safely mutate and reuse a single `[ThreadStatic]` instance of a value type for dictionary lookups without risking thread-safety issues or cache poisoning, because the struct is passed by value.
**Action:** Always deconstruct the cached class into its constituent reference-type fields as separate `[ThreadStatic]` variables. Mutate those underlying reference objects, and then construct the final `readonly record struct` key directly on the stack for the lookup. This guarantees zero-allocation on the struct while preserving thread-local pooling for its heavy nested states.

## 2026-10-26 - Explicit Record Struct HashCode for Zero-Allocation Cache Keys
**Context:** `src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** The default compiler-generated `Equals` and `GetHashCode` for C# `record struct` types that contain reference types (like strings) can introduce hidden GC allocations (e.g. boxing) during high-frequency dictionary lookups, such as the PSO cache checks for Vulkan shaders.
**Action:** When designing cache keys for dictionaries in high-frequency rendering loops, explicitly implement `IEquatable<T>` and `GetHashCode()` using `HashCode.Combine` (or explicit `HashCode.Add`) and allocation-free string comparisons (e.g. `string.Equals` with `StringComparison.Ordinal`) instead of relying on the default record struct implementation.

## 2026-10-27 - PSO Caching Memory Optimization
**Context:** `src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** The C# compiler-generated default equality methods for `readonly record struct` types containing reference fields can sometimes introduce hidden GC allocations (boxing) during frequent dictionary lookups if they aren't structured carefully or when relying on older reflection-based equality checks under the hood. Using an explicit `Equals` with `IEquatable<T>` and explicitly defining `GetHashCode` with `HashCode.Combine` ensures we absolutely avoid any boxing or reflection paths, resulting in zero-allocation fast path dictionary lookups. Also, deferred string formatting to cache misses avoids creating unnecessary strings in the hot path.
**Action:** In C# hot paths, explicitly implement `IEquatable<T>` and `GetHashCode()` (using `HashCode.Combine`) for `readonly record struct` types that contain reference fields like strings and are used in dictionary lookups.

## 2023-11-06 - [Explicit HashCode for PSO Cache Keys]
**Context:** src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs (CacheShardKey struct)
**Learning:** Default struct equality in C# uses Reflection to generate HashCode and compare equality when the struct contains reference types like `string`. In hot paths like dictionary lookups during Vulkan Pipeline State Object (PSO) caching, this introduces boxing allocations and hidden GC pauses, causing frame-time spikes.
**Action:** Always implement `IEquatable<T>` explicitly and override `GetHashCode()` using `HashCode.Combine` for `readonly record struct` keys that contain reference fields like strings in dictionary lookups on performance-critical paths.

## 2026-11-07 - Hidden Boxing Allocations in Record Struct Dictionary Lookups
**Context:** Graphics Pipeline (Vulkan/MoltenVK) / `VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** In C#, declaring a `readonly record struct` that contains nullable reference type fields (like `string?`) forces the compiler-generated `Equals` and `GetHashCode` implementations to potentially box those references when checking equality via default generic comparers. When used as a key in `Dictionary<TKey, TValue>` for high-frequency operations like Vulkan Pipeline State Object (PSO) caching, this introduces hidden per-frame garbage collection allocations.
**Action:** Always explicitly implement `IEquatable<T>` and override `GetHashCode` using `HashCode.Combine(...)` for hot-path struct keys containing reference types to guarantee zero-allocation structural equality.
