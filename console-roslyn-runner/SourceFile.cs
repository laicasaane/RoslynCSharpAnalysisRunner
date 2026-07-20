using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RoslynCSharpAnalysisRunner
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
        [StackTraceHidden, DoesNotReturn]
        private static void ThrowSourceFileNotFound(string fullPath)
            => throw new FileNotFoundException(
                  $"The source file '{fullPath}' was not found."
                , fullPath
            );
    }
}
