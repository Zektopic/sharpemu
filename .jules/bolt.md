## 2023-10-27 - Zero-Allocation Pipeline Cache Keys
**Context:** `src/SharpEmu.Libs/VideoOut/VulkanVideoPresenter.PipelineCacheShards.cs`
**Learning:** Hidden GC pressure and allocations occurred during Vulkan PSO lookups in `_pipelineCacheShards` because string formatting (`$"c-{hash:X16}"` or `$"g-{vHash:X16}-{pHash:X16}"`) was used as dictionary keys in hot loops.
**Action:** Replace string-formatted cache keys with `readonly record struct` implementing `IEquatable<T>` for zero-allocation cache hits. Defer the string formatting solely to cache-miss scenarios (e.g. creating file paths on disk).
