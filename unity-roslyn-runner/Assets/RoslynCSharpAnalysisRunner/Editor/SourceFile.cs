#if UNITY_EDITOR
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RoslynCSharpAnalysisRunner.Editor
{
    internal readonly record struct SourceFile(string Path, string Text)
    {
        public static SourceFile Load(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);

            if (File.Exists(fullPath) == false)
            {
                ThrowSourceFileNotFound(fullPath);
            }

            return new(fullPath, File.ReadAllText(fullPath));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [HideInCallstack, DoesNotReturn]
        private static void ThrowSourceFileNotFound(string fullPath)
            => throw new FileNotFoundException(
                  $"The source file '{fullPath}' was not found."
                , fullPath
            );
    }
}

#endif
