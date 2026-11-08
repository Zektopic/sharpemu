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
