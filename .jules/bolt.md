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
