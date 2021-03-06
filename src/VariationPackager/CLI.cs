﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

using Mochizuki.VariationPackager.Models.Interface;
using Mochizuki.VariationPackager.Models.Json;

using Newtonsoft.Json;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Mochizuki.VariationPackager
{
    // ReSharper disable once InconsistentNaming
    public static class CLI
    {
        public static void BuildWithScene(string scene, bool isKeepUnityPackage)
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            CreatePackage(Path.Combine(Application.dataPath, "package.json"), isKeepUnityPackage);
        }

        public static void CreatePackage(string jsonPath, bool isKeepUnityPackage = false)
        {
            var meta = ReadMetadata(jsonPath);
            if (!ValidateProperties(meta))
                return;

            try
            {
                foreach (var preprocessor in meta.PreProcessors)
                    preprocessor.Run();

                foreach (var variation in meta.Describe.Variations)
                    CreatePackage(meta, variation, isKeepUnityPackage);

                foreach (var postprocessor in meta.PostProcessors)
                    postprocessor.Run();
            }
            catch (Exception e)
            {
                // ignored
                Debug.Log(e.Message);
            }
        }

        private static IPackage ReadMetadata(string jsonPath)
        {
            if (File.Exists(jsonPath))
                using (var sr = new StreamReader(jsonPath))
                    return JsonConvert.DeserializeObject<Package>(sr.ReadToEnd());

            var objects = SceneManager.GetActiveScene().GetRootGameObjects();
            return objects.SelectMany(w => w.GetComponentsInChildren<IPackage>()).First();
        }

        private static bool ValidateProperties(IPackage meta)
        {
            if (string.IsNullOrWhiteSpace(meta.Name))
                return false;
            if (string.IsNullOrWhiteSpace(meta.Version))
                return false;
            if (meta.Describe == null)
                return false;
            if (string.IsNullOrWhiteSpace(meta.Describe.Output))
                return false;
            if (meta.Describe.Variations == null)
                return false;
            if (meta.Describe.Variations.Select(w => w.Name).Distinct().Count() != meta.Describe.Variations.Count)
                return false;
            return meta.Describe.Variations.All(w => w?.UnityPackage.Includes != null && w.UnityPackage.Includes.Count > 0);
        }

        private static void CreatePackage(IPackage meta, IPackageVariation variation, bool isKeepUnityPackage)
        {
            var dest = CreateUnityPackage(meta, variation);
            if (variation.Archive == null)
                return;

            CreateZipPackage(meta, variation, dest);

            if (isKeepUnityPackage)
            {
                var basename = Path.GetDirectoryName(dest);
                var filename = Path.GetFileNameWithoutExtension(dest);
                var extension = Path.GetExtension(dest);
                File.Move(dest, $"{basename}/{filename}-{meta.Version}{extension}");
            }
            else
            {
                File.Delete(dest);
            }
        }

        private static string CreateUnityPackage(IPackage meta, IPackageVariation variation)
        {
            var matcher = new Matcher();
            matcher.AddIncludePatterns(variation.UnityPackage.Includes);
            matcher.AddExclude("**/*.meta");
            if (variation.UnityPackage.Excludes != null)
                matcher.AddExcludePatterns(variation.UnityPackage.Excludes);

            var rootDirectory = new DirectoryInfoWrapper(new DirectoryInfo(Application.dataPath));
            var assets = matcher.Execute(rootDirectory).Files.Select(w => $"Assets/{w.Path}");

            var destDirectory = Path.Combine(Application.dataPath, meta.Describe.Output);
            if (!Directory.Exists(destDirectory))
                Directory.CreateDirectory(destDirectory);

            var destName = string.IsNullOrWhiteSpace(variation.UnityPackage.Name) ? $"{meta.Name}.unitypackage" : $"{variation.UnityPackage.Name}.unitypackage";
            var publishTo = Path.Combine(destDirectory, destName);
            if (File.Exists(publishTo))
                File.Delete(publishTo);

            AssetDatabase.ExportPackage(assets.ToArray(), publishTo, ExportPackageOptions.Default);

            return publishTo;
        }

        private static void CreateZipPackage(IPackage meta, IPackageVariation variation, string publishedTo)
        {
            var matcher = new Matcher();
            if (variation.Archive?.Includes != null)
                matcher.AddIncludePatterns(variation.Archive.Includes);
            if (variation.Archive?.Excludes != null)
                matcher.AddExcludePatterns(variation.Archive.Excludes);

            var rootDirectory = new DirectoryInfoWrapper(new DirectoryInfo(Application.dataPath));
            var assets = matcher.Execute(rootDirectory).Files.Select(w => $"Assets/{w.Path}");

            var sb = new List<string>();
            sb.Add(string.IsNullOrWhiteSpace(variation.Archive?.Name) ? meta.Name : variation.Archive.Name);
            if (!string.IsNullOrWhiteSpace(variation.Name))
                sb.Add(variation.Name);
            sb.Add(meta.Version);
            var archiveName = string.Join("-", sb);
            var destDirectory = Path.Combine(Application.dataPath, meta.Describe.Output, archiveName);

            if (!Directory.Exists(destDirectory))
                Directory.CreateDirectory(destDirectory);

            foreach (var asset in assets)
            {
                var baseDir = string.IsNullOrWhiteSpace(variation.Archive?.BaseDir) ? "" : $"Assets/{variation.Archive.BaseDir}";
                var destTo = $"{destDirectory}/{(string.IsNullOrWhiteSpace(baseDir) ? asset : asset.Replace(baseDir, ""))}";
                if (!Directory.Exists(Path.GetDirectoryName(destTo)))
                    Directory.CreateDirectory(Path.GetDirectoryName(destTo) ?? string.Empty);
                File.Copy(asset, destTo, true);
            }

            File.Copy(publishedTo, $"{destDirectory}/{Path.GetFileName(publishedTo)}", true);

            ZipFile.CreateFromDirectory(destDirectory, $"{destDirectory}.zip", CompressionLevel.Optimal, true);

            Directory.Delete(destDirectory, true);
        }
    }
}