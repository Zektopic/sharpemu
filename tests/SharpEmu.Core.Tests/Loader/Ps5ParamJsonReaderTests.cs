// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Text;
using SharpEmu.Core;
using SharpEmu.Core.Loader;
using Xunit;

namespace SharpEmu.Core.Tests.Loader;

public class Ps5ParamJsonReaderTests
{
    private class MockFileSystem : IFileSystem
    {
        public bool FileExists { get; set; } = true;
        public byte[]? FileData { get; set; }

        public bool Exists(string path) => FileExists;

        public bool TryReadAllBytes(string path, out byte[] data)
        {
            if (FileData != null)
            {
                data = FileData;
                return true;
            }
            data = Array.Empty<byte>();
            return false;
        }
    }

    [Fact]
    public void TryReadPs5Param_ValidJsonWithTitleIdAndContentVersion_ReturnsCorrectData()
    {
        string json = @"
        {
            ""titleId"": ""PUSA12345"",
            ""contentVersion"": ""01.00""
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Equal("PUSA12345", titleId);
        Assert.Equal("01.00", version);
    }

    [Fact]
    public void TryReadPs5Param_ValidJsonWithMasterVersion_ReturnsCorrectData()
    {
        string json = @"
        {
            ""titleId"": ""PUSA12345"",
            ""masterVersion"": ""02.00""
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Equal("PUSA12345", titleId);
        Assert.Equal("02.00", version);
    }

    [Fact]
    public void TryReadPs5Param_ValidJsonWithTargetContentVersion_ReturnsCorrectData()
    {
        string json = @"
        {
            ""titleId"": ""PUSA12345"",
            ""targetContentVersion"": ""03.00""
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Equal("PUSA12345", titleId);
        Assert.Equal("03.00", version);
    }

    [Fact]
    public void TryReadPs5Param_NullOrEmptyData_ReturnsNulls()
    {
        var (title1, titleId1, version1) = Ps5ParamJsonReader.TryReadPs5Param((byte[])null!);
        var (title2, titleId2, version2) = Ps5ParamJsonReader.TryReadPs5Param(Array.Empty<byte>());

        Assert.Null(title1);
        Assert.Null(titleId1);
        Assert.Null(version1);

        Assert.Null(title2);
        Assert.Null(titleId2);
        Assert.Null(version2);
    }

    [Fact]
    public void TryReadPs5Param_InvalidJson_ReturnsNulls()
    {
        byte[] data = Encoding.UTF8.GetBytes("{ invalid json }");
        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Null(titleId);
        Assert.Null(version);
    }

    [Fact]
    public void TryReadPs5Param_JsonNotObject_ReturnsNulls()
    {
        byte[] data = Encoding.UTF8.GetBytes("[]");
        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Null(titleId);
        Assert.Null(version);
    }

    [Fact]
    public void TryReadPs5Param_WithBom_ReturnsCorrectData()
    {
        string json = @"
        {
            ""titleId"": ""BOM12345""
        }";
        byte[] utf8Bom = { 0xEF, 0xBB, 0xBF };
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] data = new byte[utf8Bom.Length + jsonBytes.Length];
        Buffer.BlockCopy(utf8Bom, 0, data, 0, utf8Bom.Length);
        Buffer.BlockCopy(jsonBytes, 0, data, utf8Bom.Length, jsonBytes.Length);

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
        Assert.Equal("BOM12345", titleId);
        Assert.Null(version);
    }

    [Fact]
    public void TryReadPs5Param_ExtractTitleNameFromLocalizedParameters_DefaultLanguage()
    {
        string json = @"
        {
            ""localizedParameters"": {
                ""defaultLanguage"": ""ja-JP"",
                ""ja-JP"": { ""titleName"": ""Japanese Title"" },
                ""en-US"": { ""titleName"": ""English Title"" }
            }
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, _, _) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Equal("Japanese Title", title);
    }

    [Fact]
    public void TryReadPs5Param_ExtractTitleNameFromLocalizedParameters_EnUsFallback()
    {
        string json = @"
        {
            ""localizedParameters"": {
                ""defaultLanguage"": ""fr-FR"",
                ""en-US"": { ""titleName"": ""English Title"" }
            }
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, _, _) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Equal("English Title", title);
    }

    [Fact]
    public void TryReadPs5Param_ExtractTitleNameFromLocalizedParameters_FirstAvailable()
    {
        string json = @"
        {
            ""localizedParameters"": {
                ""ko-KR"": { ""titleName"": ""Korean Title"" }
            }
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, _, _) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Equal("Korean Title", title);
    }

    [Fact]
    public void TryReadPs5Param_ExtractTitleNameFromDisc_LocalizedParameters()
    {
        string json = @"
        {
            ""disc"": {
                ""localizedParameters"": {
                    ""defaultLanguage"": ""ja-JP"",
                    ""ja-JP"": { ""titleName"": ""Disc Japanese Title"" }
                }
            }
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, _, _) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Equal("Disc Japanese Title", title);
    }

    [Fact]
    public void TryReadPs5Param_ExtractTitleName_NoValidTitleName_ReturnsNull()
    {
        string json = @"
        {
            ""localizedParameters"": {
                ""ja-JP"": { ""notTitleName"": ""Japanese Title"" }
            }
        }";
        byte[] data = Encoding.UTF8.GetBytes(json);

        var (title, _, _) = Ps5ParamJsonReader.TryReadPs5Param(data);

        Assert.Null(title);
    }

    [Fact]
    public void TryReadPs5Param_IFileSystem_FileDoesNotExist_ReturnsNulls()
    {
        var fs = new MockFileSystem { FileExists = false };

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(fs, "param.json");

        Assert.Null(title);
        Assert.Null(titleId);
        Assert.Null(version);
    }

    [Fact]
    public void TryReadPs5Param_IFileSystem_FileExistsButCannotRead_ReturnsNulls()
    {
        var fs = new MockFileSystem { FileExists = true, FileData = null };

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(fs, "param.json");

        Assert.Null(title);
        Assert.Null(titleId);
        Assert.Null(version);
    }

    [Fact]
    public void TryReadPs5Param_IFileSystem_FileExistsAndCanRead_ReturnsCorrectData()
    {
        string json = @"
        {
            ""titleId"": ""PUSA12345"",
            ""contentVersion"": ""01.00""
        }";
        var fs = new MockFileSystem { FileExists = true, FileData = Encoding.UTF8.GetBytes(json) };

        var (title, titleId, version) = Ps5ParamJsonReader.TryReadPs5Param(fs, "param.json");

        Assert.Null(title);
        Assert.Equal("PUSA12345", titleId);
        Assert.Equal("01.00", version);
    }
}
