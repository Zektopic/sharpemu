// SPDX-FileCopyrightText: 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using SharpEmu.SourceGenerators;
using Xunit;

namespace SharpEmu.SourceGenerators.Tests;

public class GenerateAerolibBinaryTaskTests : IDisposable
{
    private class FakeBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = new();
        public List<BuildMessageEventArgs> Messages { get; } = new();

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "";

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) { }
    }

    private readonly string _tempDirectory;
    private readonly string _namesFile;
    private readonly string _outputFile;

    public GenerateAerolibBinaryTaskTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _namesFile = Path.Combine(_tempDirectory, "names.txt");
        _outputFile = Path.Combine(_tempDirectory, "aerolib.bin");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void Execute_ValidNamesFile_GeneratesCorrectBinary()
    {
        var names = new[] { "sceKernelWaitSema", "sceKernelSignalSema", "sceKernelCreateSema" };
        File.WriteAllLines(_namesFile, names);

        var engine = new FakeBuildEngine();
        var task = new GenerateAerolibBinaryTask
        {
            BuildEngine = engine,
            NamesFile = _namesFile,
            OutputFile = _outputFile
        };

        var result = task.Execute();

        Assert.True(result);
        Assert.True(File.Exists(_outputFile));
        Assert.Empty(engine.Errors);
        Assert.Single(engine.Messages);
        Assert.Contains("3 symbols", engine.Messages[0].Message);

        using var sha1 = System.Security.Cryptography.SHA1.Create();
        using var stream = File.OpenRead(_outputFile);
        using var reader = new BinaryReader(stream);

        var entryCount = reader.ReadUInt32();
        Assert.Equal(3u, entryCount);

        for (int i = 0; i < names.Length; i++)
        {
            var expectedNid = Ps5Nid.Compute(names[i], sha1);
            var expectedNidBytes = Encoding.UTF8.GetBytes(expectedNid);
            var expectedNameBytes = Encoding.UTF8.GetBytes(names[i]);

            var nidLength = reader.ReadByte();
            Assert.Equal(expectedNidBytes.Length, nidLength);

            var nidBytes = reader.ReadBytes(nidLength);
            Assert.Equal(expectedNidBytes, nidBytes);

            var nameLength = reader.ReadUInt16();
            Assert.Equal(expectedNameBytes.Length, nameLength);

            var nameBytes = reader.ReadBytes(nameLength);
            Assert.Equal(expectedNameBytes, nameBytes);
        }

        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void Execute_MissingNamesFile_LogsErrorAndReturnsFalse()
    {
        var engine = new FakeBuildEngine();
        var task = new GenerateAerolibBinaryTask
        {
            BuildEngine = engine,
            NamesFile = _namesFile, // File doesn't exist
            OutputFile = _outputFile
        };

        var result = task.Execute();

        Assert.False(result);
        Assert.False(File.Exists(_outputFile));
        Assert.Single(engine.Errors);
        Assert.Equal("SHEMAERO", engine.Errors[0].Code);
        Assert.Contains("FileNotFoundException", engine.Errors[0].Message);
    }

    [Fact]
    public void Execute_NameTooLong_LogsErrorAndReturnsFalse()
    {
        var veryLongName = new string('A', ushort.MaxValue + 1);
        File.WriteAllLines(_namesFile, new[] { veryLongName });

        var engine = new FakeBuildEngine();
        var task = new GenerateAerolibBinaryTask
        {
            BuildEngine = engine,
            NamesFile = _namesFile,
            OutputFile = _outputFile
        };

        var result = task.Execute();

        Assert.False(result);
        Assert.Single(engine.Errors);
        Assert.Equal("SHEMAERO", engine.Errors[0].Code);
        Assert.Contains("exceeds the format's ushort length prefix", engine.Errors[0].Message);
    }
}
