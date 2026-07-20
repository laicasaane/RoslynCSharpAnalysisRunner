#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace RoslynCSharpAnalysisRunner.Editor
{
    internal static class RoslynCSharpAnalysisRunnerMenu
    {
        private const string MENU_ROOT = "Tools/Roslyn C# Analysis Runner/";

        [MenuItem(MENU_ROOT + "Analyze Demo", false, 2000)]
        private static void AnalyzeDemo()
        {
            SourceFile[] sources = { new(DemoSource.FILE_NAME, DemoSource.TEXT) };
            RunAndLog(sources, null);
        }

        [MenuItem(MENU_ROOT + "Analyze Selected C# Files", false, 2001)]
        private static void AnalyzeSelectedSources()
            => RunSelectedSources(null);

        [MenuItem(MENU_ROOT + "Analyze Selected C# Files", true)]
        private static bool CanAnalyzeSelectedSources()
            => GetSelectedSourcePaths().Length > 0;

        [MenuItem(MENU_ROOT + "Emit Selected C# Files...", false, 2002)]
        private static void EmitSelectedSources()
        {
            var emitPath = EditorUtility.SaveFilePanel(
                  "Emit Roslyn Compilation"
                , Application.dataPath
                , "Analyzed"
                , "dll"
            );

            if (string.IsNullOrEmpty(emitPath))
            {
                return;
            }

            RunSelectedSources(emitPath);
        }

        [MenuItem(MENU_ROOT + "Emit Selected C# Files...", true)]
        private static bool CanEmitSelectedSources()
            => GetSelectedSourcePaths().Length > 0;

        private static void RunSelectedSources(string? emitPath)
        {
            var sourcePaths = GetSelectedSourcePaths();
            var sources = new SourceFile[sourcePaths.Length];
            var sourceCount = sourcePaths.Length;

            for (var index = 0; index < sourceCount; index++)
            {
                sources[index] = SourceFile.Load(sourcePaths[index]);
            }

            RunAndLog(sources, emitPath);
        }

        private static void RunAndLog(IReadOnlyList<SourceFile> sources, string? emitPath)
        {
            try
            {
                using var output = new StringWriter();
                var succeeded = RoslynAnalyzer.Run(sources, emitPath, output);
                var report = output.ToString().TrimEnd();

                if (succeeded)
                {
                    LogReport(report);

                    if (emitPath is not null)
                    {
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    LogErrorReport(report);
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or InvalidOperationException)
            {
                LogError(exception.Message);
            }
        }

        private static string[] GetSelectedSourcePaths()
        {
            var assetGuids = Selection.assetGUIDs;
            var paths = new List<string>(assetGuids.Length);
            var distinctPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guidCount = assetGuids.Length;

            for (var index = 0; index < guidCount; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[index]);

                if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(assetPath);

                if (distinctPaths.Add(fullPath))
                {
                    paths.Add(fullPath);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths.ToArray();
        }

        [HideInCallstack]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogReport(string report)
            => Debug.Log(report);

        [HideInCallstack]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogErrorReport(string report)
            => Debug.LogError(report);

        [HideInCallstack]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogError(string message)
            => Debug.LogError($"error: {message}");
    }
}

#endif
