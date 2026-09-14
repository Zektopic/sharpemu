<!-- SPDX-License-Identifier: GPL-2.0-or-later -->
<!-- Copyright (C) 2026 SharpEmu Emulator Project -->
## 2026-09-14 - Scoped Dotnet Format Execution
**Context:** src/SharpEmu.Core/Memory/VirtualMemory.cs
**Learning:** Running `dotnet format` globally modifies dozens of unrelated files, breaking the strict `< 80 lines` boundary rule and causing the PR review to fail for excessive scope.
**Action:** Always scope code formatting commands specifically to the modified project and file, e.g., `dotnet format src/SharpEmu.Core/SharpEmu.Core.csproj --include src/SharpEmu.Core/Memory/VirtualMemory.cs --verify-no-changes`.
