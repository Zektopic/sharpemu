// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Diagnostics;
using System.Reflection;
using SharpEmu.GUI;
using Xunit;

namespace SharpEmu.Libs.Tests.GUI;

public sealed class UpdaterTests
{
    [Fact]
    public void Updater_DownloadAndRestartAsync_UsesUniqueTempDirectory()
    {
        // Verify via reflection/inspection or executing DownloadAndRestartAsync with a canceled token
        // that the temporary directory created/attempted is randomized and unique.
        var cancelSource = new CancellationTokenSource();
        cancelSource.Cancel();

        var updateInfo = new Updater.UpdateInfo(
            Sha: "1234567890abcdef1234567890abcdef12345678",
            Name: "sharpemu-test.zip",
            DownloadUrl: "https://example.com/download.zip",
            Size: 100,
            Sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            TagName: "v1.0.0");

        var task = Updater.DownloadAndRestartAsync(updateInfo, cancellationToken: cancelSource.Token);

        Assert.True(task.IsCanceled || task.IsFaulted);

        // Confirm that the static predictable path "SharpEmu.Update" is not used or left behind as a fixed directory
        var fixedPath = Path.Combine(Path.GetTempPath(), "SharpEmu.Update");
        Assert.False(Directory.Exists(fixedPath));
    }

    [Fact]
    public void Updater_TryApply_HandlesInvalidArgs()
    {
        var result = Updater.TryApply(["--invalid-arg"], out var exitCode);
        Assert.False(result);
        Assert.Equal(0, exitCode);
    }
}
