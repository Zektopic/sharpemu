<!--
SPDX-FileCopyrightText: 2026 SharpEmu Emulator Project
SPDX-License-Identifier: GPL-2.0-or-later
-->

## 2026-07-25 - Replace reference records with readonly structs for Dictionary keys
**Context:** Metal Video Presenter / PSO Cache (`PipelineKey`)
**Learning:** Using `sealed record` for Dictionary keys causes implicit heap allocations on every lookup (due to it being a class reference type). Since PSO caching lookups happen per-frame or per-draw, this creates significant GC pressure.
**Action:** Replace `sealed record` with `readonly struct` implementing `IEquatable<T>` and explicitly overriding `GetHashCode()` with `HashCode.Combine()` to eliminate allocations in hot dictionary lookups.
