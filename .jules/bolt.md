<!--
  Copyright (C) 2026 SharpEmu Emulator Project
  SPDX-License-Identifier: GPL-2.0-or-later
-->

## 2026-07-25 - Replace reference records with readonly structs for Dictionary keys
**Context:** Metal Video Presenter / PSO Cache (`PipelineKey`)
**Learning:** Using `sealed record` for Dictionary keys causes implicit heap allocations on every lookup (due to it being a class reference type). Since PSO caching lookups happen per-frame or per-draw, this creates significant GC pressure.
**Action:** Replace `sealed record` with `readonly struct` implementing `IEquatable<T>` and explicitly overriding `GetHashCode()` with `HashCode.Combine()` to eliminate allocations in hot dictionary lookups.

## 2024-05-30 - [Kernel String Reading Optimization]
**Context:** src/SharpEmu.Libs/Kernel/KernelMemoryCompatExports.cs
**Learning:** Guest string reading operations (TryCompareStrings, Strchr, Strrchr, Memchr, TryCompareStringsCaseInsensitive) read character by character in loops, making a new TryReadCompat call (which grabs locks and searches trees in VirtualMemory) for every single byte. This creates huge overhead for long strings, which could be reduced with chunked reads or reading the whole thing using the existing CString functions.
**Action:** Use TryReadCString or chunked reads instead of 1-byte read loops.

## 2026-07-26 - Testing Roslyn Incremental Generator Equality Models

**Context:** When working with C# Source Generators, `IIncrementalGenerator` caching relies heavily on the `Equals` and `GetHashCode` implementations of intermediate data models. If these methods are incomplete or flawed, Roslyn will incorrectly cache or regenerate output, leading to performance issues or stale state. In the context of `SysAbiExportGenerator`, the `ExportModel` is a private, nested type that was largely untested for equality logic.

**Learning:** To guarantee 100% line and branch coverage on these critical internal data models, the most direct and reliable approach is to use reflection within standard xUnit tests. This circumvents the need to construct complex `CSharpCompilation` trees and invoke `CSharpGeneratorDriver` incrementally just to trigger `IEquatable` calls, reducing test brittleness and focusing on structural equality.

**Action:** Created `ExportModelEqualityTests` in `SysAbiExportGeneratorTests.cs` using `GetNestedType` and `Activator.CreateInstance` to thoroughly test all branches of `ExportModel` equality logic, improving code reliability for the generator pipeline.

## 2024-05-18 - Optimize PhysicalVirtualMemory FindRegion Bottleneck
**Context:** `src/SharpEmu.Core/Memory/PhysicalVirtualMemory.cs`
**Learning:** The `List<T>` indexer inside the `FindRegion` binary search hot path incurred bounds-checking and method-call overhead. Accessing it via `CollectionsMarshal.AsSpan` yielded a measurable decrease in execution time on millions of iterations.
**Action:** Use `CollectionsMarshal.AsSpan` for hot path reads of `List<T>` instead of indexers when zero-allocation and bounds-checking elision is needed.

## 2026-08-26 - Direct Span Access in Virtual Memory Search
**Context:** `src/SharpEmu.Core/Memory/VirtualMemory.cs` (`FindInsertionIndex`)
**Learning:** Standard C# `List<T>` accesses inside high-frequency binary searches introduce unnecessary overhead via indexer property access and bounds checking. The same optimization pattern recently used in `PhysicalVirtualMemory.cs` (commit 980b47b) applies directly to `VirtualMemory.cs`. Bypassing this via `CollectionsMarshal.AsSpan(list)` completely elides these checks, turning the operation into direct O(1) span memory access.
**Action:** When optimizing binary search loops or hot paths over `List<T>`, immediately refactor to use `CollectionsMarshal.AsSpan()` to access elements and `span.Length` for bounds, alongside the `>>> 1` operator for division.
