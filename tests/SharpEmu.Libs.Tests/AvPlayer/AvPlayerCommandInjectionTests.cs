// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.AvPlayer;
using Xunit;

namespace SharpEmu.Libs.Tests.AvPlayer;

public sealed class AvPlayerCommandInjectionTests
{
    [Fact]
    public void ContainsInvalidMediaPathCharacters_RejectsDoubleQuotes()
    {
        Assert.Null(AvPlayerExports.ResolveGuestPath("app0:/test\"file.mp4"));
        Assert.Null(AvPlayerExports.ResolveGuestPath("app0:/\"test\"file.mp4"));
    }
}
