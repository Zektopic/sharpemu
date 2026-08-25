## 2026-08-25 - VirtualMemory loop indexing optimization
**Context:** Guest MMU / src/SharpEmu.Core/Memory/VirtualMemory.cs
**Learning:** Standard `List<T>` indexing inside highly repetitive loop structures (`TryValidateRange`, `CopyFromRegions`, etc.) incurs measurable MSIL bounds-checking and virtual call overhead per element access.
**Action:** Use `System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list)` to bypass indexing overhead in hot paths, and pair binary searches with unsigned right shift `>>> 1` for optimal instruction generation.
