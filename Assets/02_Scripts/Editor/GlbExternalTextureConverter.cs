using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class GlbExternalTextureConverter
{
    private const uint GlbMagic = 0x46546C67;
    private const uint JsonChunkType = 0x4E4F534A;
    private const string WebPlatformName = "WebGL";
    private const string DialogueTexturePath = "Assets/04_Images/Dialogue/Chr_Animation.png";
    private const string NativeTextureRoot = "Assets/08_Materials";
    private const string KnifeMaterialPath =
        "Assets/08_Materials/TutorialGuide/TutorialGuide_Slasher_Knife.mat";

    private static readonly string[] TargetGlbPaths =
    {
        "Assets/05_Models/Maps/Map_001_Bar/Map_001.glb",
        "Assets/05_Models/Maps/Map_002_Museum/Map_002.glb",
        "Assets/05_Models/Maps/Map_003_Warehouse/Map_003.glb",
        "Assets/05_Models/Maps/Map_Table/tit.glb",
        "Assets/05_Models/Obstacles/Box01/OBs_Box01.glb",
        "Assets/05_Models/Obstacles/Box02/OBs_Box02.glb",
        "Assets/05_Models/Obstacles/Drum/OBs_Drum.glb",
        "Assets/05_Models/Obstacles/DrumOld/OBs_DrumOld.glb",
        "Assets/05_Models/Obstacles/Lion/OBs_Lion.glb",
        "Assets/05_Models/Obstacles/Sofa/OBs_Sofa.glb",
        "Assets/05_Models/Obstacles/Stool/OBs_Stool.glb",
        "Assets/05_Models/Obstacles/VBox/OBs_VBox.glb",
        "Assets/05_Models/Civilian/Citizen.glb",
        "Assets/05_Models/Civilian/Eliza.glb",
        "Assets/05_Models/Character/Player/Player_Knife/Knife.glb",
    };

    private enum TextureSemantic
    {
        Unknown,
        BaseColor,
        Emissive,
        Orm,
        Normal,
    }

    private sealed class GlbChunk
    {
        public uint Type;
        public byte[] Data;
    }

    private sealed class ExtractedTexture
    {
        public string AssetPath;
        public TextureSemantic Semantic;
    }

    [MenuItem("Rebellion/Optimize/Web Assets")]
    public static void OptimizeWebAssets()
    {
        int convertedGlbs = 0;
        int extractedImages = 0;
        List<ExtractedTexture> extractedTextures = new();

        try
        {
            AssetDatabase.DisallowAutoRefresh();
            foreach (string glbPath in TargetGlbPaths)
            {
                if (ConvertGlb(glbPath, extractedTextures, out int imageCount))
                {
                    convertedGlbs++;
                    extractedImages += imageCount;
                }
            }
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        foreach (ExtractedTexture texture in extractedTextures)
        {
            ApplyWebTextureSettings(texture.AssetPath, texture.Semantic);
        }

        int nativeTextureCount = CompressNativeCharacterTextures();
        ApplyDialogueTextureSettings();
        ReconnectKnifeMaterial();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Debug.Log(
            $"[WebOptimize] Complete. GLBs={convertedGlbs}, " +
            $"extractedImages={extractedImages}, nativeTextures={nativeTextureCount}.");
    }

    private static bool ConvertGlb(
        string assetPath,
        ICollection<ExtractedTexture> extractedTextures,
        out int extractedImageCount)
    {
        extractedImageCount = 0;
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[WebOptimize] GLB not found: {assetPath}");
            return false;
        }

        List<GlbChunk> chunks = ReadChunks(fullPath, out uint version);
        GlbChunk jsonChunk = chunks.FirstOrDefault(chunk => chunk.Type == JsonChunkType);
        GlbChunk binChunk = chunks.FirstOrDefault(chunk => chunk.Type == 0x004E4942);
        if (jsonChunk == null || binChunk == null)
        {
            Debug.LogWarning($"[WebOptimize] GLB has no JSON or BIN chunk: {assetPath}");
            return false;
        }

        string json = Encoding.UTF8.GetString(jsonChunk.Data).TrimEnd('\0', ' ', '\t', '\r', '\n');
        JObject root = JObject.Parse(json);
        JArray images = root["images"] as JArray;
        JArray bufferViews = root["bufferViews"] as JArray;
        if (images == null || bufferViews == null)
        {
            return false;
        }

        Dictionary<int, TextureSemantic> semantics = CollectImageSemantics(root);
        string glbDirectory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string textureFolderName = Path.GetFileNameWithoutExtension(assetPath) + "_Textures";
        string textureFolder = $"{glbDirectory}/{textureFolderName}";
        Directory.CreateDirectory(Path.GetFullPath(textureFolder));

        bool changed = false;
        for (int imageIndex = 0; imageIndex < images.Count; imageIndex++)
        {
            if (images[imageIndex] is not JObject image || image["bufferView"] == null)
            {
                if (images[imageIndex]?["uri"] != null)
                {
                    string existingAssetPath =
                        $"{glbDirectory}/{images[imageIndex]!["uri"]!.Value<string>()}";
                    extractedTextures.Add(new ExtractedTexture
                    {
                        AssetPath = existingAssetPath,
                        Semantic = semantics.GetValueOrDefault(imageIndex, TextureSemantic.Unknown),
                    });
                }

                continue;
            }

            int bufferViewIndex = image["bufferView"]!.Value<int>();
            JObject bufferView = (JObject)bufferViews[bufferViewIndex]!;
            int byteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;
            int byteLength = bufferView["byteLength"]!.Value<int>();
            string mimeType = image["mimeType"]?.Value<string>() ?? "image/png";
            string extension = mimeType.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
                ? ".jpg"
                : ".png";
            string sourceName = image["name"]?.Value<string>();
            string fileName = $"{imageIndex:D2}_{SanitizeFileName(sourceName)}{extension}";
            string textureAssetPath = $"{textureFolder}/{fileName}";
            string textureFullPath = Path.GetFullPath(textureAssetPath);

            byte[] imageBytes = new byte[byteLength];
            Buffer.BlockCopy(binChunk.Data, byteOffset, imageBytes, 0, byteLength);
            File.WriteAllBytes(textureFullPath, imageBytes);

            image.Remove("bufferView");
            image.Remove("mimeType");
            image["uri"] = $"{textureFolderName}/{fileName}";
            extractedTextures.Add(new ExtractedTexture
            {
                AssetPath = textureAssetPath,
                Semantic = semantics.GetValueOrDefault(imageIndex, TextureSemantic.Unknown),
            });
            extractedImageCount++;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        jsonChunk.Data = Encoding.UTF8.GetBytes(root.ToString(Formatting.None));
        WriteGlb(fullPath, version, chunks);
        return true;
    }

    private static Dictionary<int, TextureSemantic> CollectImageSemantics(JObject root)
    {
        Dictionary<int, TextureSemantic> result = new();
        JArray textures = root["textures"] as JArray;
        JArray materials = root["materials"] as JArray;
        if (textures == null || materials == null)
        {
            return result;
        }

        foreach (JObject material in materials.OfType<JObject>())
        {
            JObject pbr = material["pbrMetallicRoughness"] as JObject;
            RegisterTexture(result, textures, pbr?["baseColorTexture"], TextureSemantic.BaseColor);
            RegisterTexture(result, textures, pbr?["metallicRoughnessTexture"], TextureSemantic.Orm);
            RegisterTexture(result, textures, material["normalTexture"], TextureSemantic.Normal);
            RegisterTexture(result, textures, material["occlusionTexture"], TextureSemantic.Orm);
            RegisterTexture(result, textures, material["emissiveTexture"], TextureSemantic.Emissive);
        }

        return result;
    }

    private static void RegisterTexture(
        IDictionary<int, TextureSemantic> result,
        JArray textures,
        JToken textureInfo,
        TextureSemantic semantic)
    {
        int? textureIndex = textureInfo?["index"]?.Value<int>();
        if (textureIndex == null || textureIndex < 0 || textureIndex >= textures.Count)
        {
            return;
        }

        int? imageIndex = textures[textureIndex.Value]?["source"]?.Value<int>();
        if (imageIndex == null)
        {
            return;
        }

        if (!result.TryGetValue(imageIndex.Value, out TextureSemantic current) ||
            semantic is TextureSemantic.BaseColor or TextureSemantic.Emissive ||
            current == TextureSemantic.Unknown)
        {
            result[imageIndex.Value] = semantic;
        }
    }

    private static void ApplyWebTextureSettings(string assetPath, TextureSemantic semantic)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            Debug.LogWarning($"[WebOptimize] TextureImporter not found: {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = semantic is TextureSemantic.BaseColor or TextureSemantic.Emissive;
        importer.mipmapEnabled = false;
        TextureImporterPlatformSettings webSettings = importer.GetPlatformTextureSettings(WebPlatformName);
        webSettings.overridden = true;
        webSettings.maxTextureSize = GetWebMaxTextureSize(assetPath);
        webSettings.format = TextureImporterFormat.DXT5Crunched;
        webSettings.textureCompression = TextureImporterCompression.CompressedHQ;
        webSettings.compressionQuality = 70;
        webSettings.crunchedCompression = true;
        importer.SetPlatformTextureSettings(webSettings);
        importer.SaveAndReimport();
    }

    private static int GetWebMaxTextureSize(string assetPath)
    {
        return assetPath.Contains("/Maps/", StringComparison.Ordinal) ? 2048 : 1024;
    }

    private static int CompressNativeCharacterTextures()
    {
        int compressedCount = 0;
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { NativeTextureRoot });
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".texture2D", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width > 2048 || texture.height > 2048)
            {
                continue;
            }

            if (texture.format is not (TextureFormat.ARGB32 or TextureFormat.RGBA32))
            {
                continue;
            }

            EditorUtility.CompressTexture(
                texture,
                TextureFormat.DXT5Crunched,
                TextureCompressionQuality.Best);
            EditorUtility.SetDirty(texture);
            compressedCount++;
        }

        return compressedCount;
    }

    private static void ApplyDialogueTextureSettings()
    {
        if (AssetImporter.GetAtPath(DialogueTexturePath) is not TextureImporter importer)
        {
            Debug.LogWarning($"[WebOptimize] Dialogue texture importer not found: {DialogueTexturePath}");
            return;
        }

        TextureImporterPlatformSettings webSettings = importer.GetPlatformTextureSettings(WebPlatformName);
        webSettings.overridden = true;
        webSettings.maxTextureSize = 8192;
        webSettings.format = TextureImporterFormat.DXT5Crunched;
        webSettings.textureCompression = TextureImporterCompression.CompressedHQ;
        webSettings.compressionQuality = 70;
        webSettings.crunchedCompression = true;
        importer.SetPlatformTextureSettings(webSettings);
        importer.SaveAndReimport();
    }

    private static void ReconnectKnifeMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(KnifeMaterialPath);
        if (material == null)
        {
            return;
        }

        string textureFolder =
            "Assets/05_Models/Character/Player/Player_Knife/Knife_Textures";
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
        Texture2D baseColor = null;
        Texture2D orm = null;
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string lowerName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (baseColor == null && (lowerName.Contains("base") || lowerName.Contains("color")))
            {
                baseColor = texture;
            }
            else if (orm == null &&
                     (lowerName.Contains("orm") || lowerName.Contains("metal") || lowerName.Contains("rough")))
            {
                orm = texture;
            }
        }

        if (baseColor != null)
        {
            material.SetTexture("_BaseMap", baseColor);
        }

        if (orm != null)
        {
            material.SetTexture("metallicRoughnessTexture", orm);
        }

        EditorUtility.SetDirty(material);
    }

    private static List<GlbChunk> ReadChunks(string fullPath, out uint version)
    {
        using FileStream stream = File.OpenRead(fullPath);
        using BinaryReader reader = new(stream);
        if (reader.ReadUInt32() != GlbMagic)
        {
            throw new InvalidDataException($"Not a GLB file: {fullPath}");
        }

        version = reader.ReadUInt32();
        uint totalLength = reader.ReadUInt32();
        List<GlbChunk> chunks = new();
        while (stream.Position < totalLength)
        {
            uint chunkLength = reader.ReadUInt32();
            uint chunkType = reader.ReadUInt32();
            chunks.Add(new GlbChunk
            {
                Type = chunkType,
                Data = reader.ReadBytes(checked((int)chunkLength)),
            });
        }

        return chunks;
    }

    private static void WriteGlb(string fullPath, uint version, IReadOnlyList<GlbChunk> chunks)
    {
        List<byte[]> paddedChunks = chunks
            .Select(chunk => PadChunk(chunk.Data, chunk.Type == JsonChunkType ? (byte)' ' : (byte)0))
            .ToList();
        uint totalLength = checked((uint)(12 + paddedChunks.Sum(data => 8 + data.Length)));

        using FileStream stream = File.Create(fullPath);
        using BinaryWriter writer = new(stream);
        writer.Write(GlbMagic);
        writer.Write(version);
        writer.Write(totalLength);
        for (int index = 0; index < chunks.Count; index++)
        {
            writer.Write(checked((uint)paddedChunks[index].Length));
            writer.Write(chunks[index].Type);
            writer.Write(paddedChunks[index]);
        }
    }

    private static byte[] PadChunk(byte[] data, byte paddingByte)
    {
        int paddedLength = (data.Length + 3) & ~3;
        if (paddedLength == data.Length)
        {
            return data;
        }

        byte[] padded = new byte[paddedLength];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        for (int index = data.Length; index < padded.Length; index++)
        {
            padded[index] = paddingByte;
        }

        return padded;
    }

    private static string SanitizeFileName(string value)
    {
        string fallback = string.IsNullOrWhiteSpace(value) ? "image" : value.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return new string(fallback.Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray());
    }
}

public static class WebBuildPackageUtility
{
    private const long ItchTotalLimitBytes = 500L * 1024L * 1024L;
    private const long ItchSingleFileLimitBytes = 200L * 1024L * 1024L;
    private const int ItchFileCountLimit = 1000;
    private const int ItchPathLengthLimit = 240;
    private const long InternalTotalLimitBytes = 450L * 1024L * 1024L;
    private const long InternalSingleFileLimitBytes = 180L * 1024L * 1024L;

    public static bool ValidateAndPackage(string outputDirectory, string buildVersion)
    {
        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        FileInfo[] files = new DirectoryInfo(fullOutputDirectory).GetFiles("*", SearchOption.AllDirectories);
        long totalBytes = files.Sum(file => file.Length);
        FileInfo largestFile = files.OrderByDescending(file => file.Length).FirstOrDefault();
        int longestPathLength = files
            .Select(file => GetRelativePath(fullOutputDirectory, file.FullName).Length)
            .DefaultIfEmpty(0)
            .Max();
        bool hasRootIndex = File.Exists(Path.Combine(fullOutputDirectory, "index.html"));

        bool itchPassed = hasRootIndex &&
                          files.Length <= ItchFileCountLimit &&
                          totalBytes <= ItchTotalLimitBytes &&
                          (largestFile?.Length ?? 0) <= ItchSingleFileLimitBytes &&
                          longestPathLength <= ItchPathLengthLimit;
        bool internalPassed = itchPassed &&
                              totalBytes <= InternalTotalLimitBytes &&
                              (largestFile?.Length ?? 0) <= InternalSingleFileLimitBytes;

        string packagePath = Path.Combine(
            Path.GetDirectoryName(fullOutputDirectory) ?? fullOutputDirectory,
            $"Rebellion-Web-{buildVersion}-itch.zip");
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        string summary =
            $"[WebPackage] files={files.Length}, total={ToMegabytes(totalBytes):F2}MB, " +
            $"largest={largestFile?.Name ?? "none"} " +
            $"({ToMegabytes(largestFile?.Length ?? 0):F2}MB), " +
            $"longestPath={longestPathLength}, rootIndex={hasRootIndex}, " +
            $"itchPassed={itchPassed}, internalPassed={internalPassed}.";

        if (!itchPassed)
        {
            Debug.LogError(summary + " ZIP was not created.");
            return false;
        }

        using (FileStream zipStream = File.Create(packagePath))
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create))
        {
            foreach (FileInfo file in files)
            {
                string relativePath = GetRelativePath(fullOutputDirectory, file.FullName);
                ZipArchiveEntry entry = archive.CreateEntry(
                    relativePath,
                    System.IO.Compression.CompressionLevel.Optimal);
                using Stream input = file.OpenRead();
                using Stream output = entry.Open();
                input.CopyTo(output);
            }
        }

        Debug.Log(summary + $" Package: {packagePath}");
        return true;
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        return filePath[(rootPath.Length + 1)..].Replace('\\', '/');
    }

    private static double ToMegabytes(long bytes)
    {
        return bytes / (1024d * 1024d);
    }
}
