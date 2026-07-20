#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using UnityEngine;

namespace RoslynCSharpAnalysisRunner.Editor
{
    internal static class RoslynAnalyzer
    {
        private static readonly Lazy<ImmutableArray<MetadataReference>> s_platformReferences
            = new(CreatePlatformReferences);

        public static bool Run(
              IReadOnlyList<SourceFile> sources
            , string? emitPath
            , TextWriter output
        )
        {
            var parseOptions = CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion.Latest);
            var sourceCount = sources.Count;
            var syntaxTrees = new SyntaxTree[sourceCount];

            for (var index = 0; index < sourceCount; index++)
            {
                var source = sources[index];
                syntaxTrees[index] = CSharpSyntaxTree.ParseText(
                      SourceText.From(source.Text, Encoding.UTF8)
                    , parseOptions
                    , source.Path
                );
            }

            var compilationOptions = new CSharpCompilationOptions(
                  OutputKind.DynamicallyLinkedLibrary
                , optimizationLevel: OptimizationLevel.Release
                , nullableContextOptions: NullableContextOptions.Enable
            );
            var compilation = CSharpCompilation.Create(
                  GetAssemblyName(emitPath)
                , syntaxTrees
                , s_platformReferences.Value
                , compilationOptions
            );

            output.WriteLine(
                $"Parsed {syntaxTrees.Length} source file(s) with Roslyn {GetRoslynVersion()}."
            );
            output.WriteLine();

            for (var index = 0; index < sourceCount; index++)
            {
                PrintSyntaxSummary(compilation, syntaxTrees[index], output);
            }

            var diagnostics = compilation
                .GetDiagnostics()
                .Where(static diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                .OrderBy(GetDiagnosticPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
                .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ToArray();

            PrintDiagnostics(diagnostics, output);

            var hasErrors = diagnostics
                .Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            if (emitPath is null)
            {
                return hasErrors == false;
            }

            if (hasErrors)
            {
                output.WriteLine();
                output.WriteLine("Emit skipped because the compilation contains errors.");
                return false;
            }

            return Emit(compilation, emitPath, output);
        }

        private static void PrintSyntaxSummary(
              CSharpCompilation compilation
            , SyntaxTree syntaxTree
            , TextWriter output
        )
        {
            var root = syntaxTree.GetCompilationUnitRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var types = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
            var typeCount = types.Length;
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
            var methodCount = methods.Length;

            output.WriteLine($"Source: {syntaxTree.FilePath}");
            output.WriteLine($"  Lines: {syntaxTree.GetText().Lines.Count}");
            output.WriteLine($"  Types: {typeCount}");

            for (var index = 0; index < typeCount; index++)
            {
                var type = types[index];
                var symbol = semanticModel.GetDeclaredSymbol(type);
                var displayName = symbol?.ToDisplayString(
                    SymbolDisplayFormat.CSharpErrorMessageFormat
                )
                    ?? type.Identifier.ValueText;
                var kind = symbol?.TypeKind.ToString().ToLowerInvariant() ?? "type";
                output.WriteLine($"    {kind} {displayName}");
            }

            output.WriteLine($"  Methods: {methodCount}");

            for (var index = 0; index < methodCount; index++)
            {
                var method = methods[index];
                var symbol = semanticModel.GetDeclaredSymbol(method);
                var displayName = symbol?.ToDisplayString(
                    SymbolDisplayFormat.CSharpErrorMessageFormat
                )
                    ?? method.Identifier.ValueText;
                output.WriteLine($"    {displayName}");
            }

            output.WriteLine();
        }

        private static void PrintDiagnostics(
              IReadOnlyList<Diagnostic> diagnostics
            , TextWriter output
        )
        {
            var diagnosticCount = diagnostics.Count;
            output.WriteLine("Diagnostics:");

            if (diagnosticCount == 0)
            {
                output.WriteLine("  None");
                return;
            }

            for (var index = 0; index < diagnosticCount; index++)
            {
                output.WriteLine($"  {diagnostics[index]}");
            }
        }

        private static bool Emit(CSharpCompilation compilation, string emitPath, TextWriter output)
        {
            using var image = new MemoryStream();
            var emitResult = compilation.Emit(image);

            if (emitResult.Success == false)
            {
                output.WriteLine();
                output.WriteLine("Emit failed:");

                var diagnostics = emitResult.Diagnostics;
                var diagnosticCount = diagnostics.Length;

                for (var index = 0; index < diagnosticCount; index++)
                {
                    output.WriteLine($"  {diagnostics[index]}");
                }

                return false;
            }

            var fullPath = Path.GetFullPath(emitPath);
            var outputDirectory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(outputDirectory) == false)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            image.Position = 0;

            using (var file = File.Create(fullPath))
            {
                image.CopyTo(file);
            }

            output.WriteLine();
            output.WriteLine($"Emitted: {fullPath}");
            return true;
        }

        private static string GetAssemblyName(string? emitPath)
        {
            if (emitPath is null)
            {
                return "RoslynAnalysis";
            }

            var assemblyName = Path.GetFileNameWithoutExtension(emitPath);

            return string.IsNullOrWhiteSpace(assemblyName)
                ? "RoslynAnalysis"
                : assemblyName;
        }

        private static string GetRoslynVersion()
            => typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "unknown";

        private static string GetDiagnosticPath(Diagnostic diagnostic)
            => diagnostic.Location.IsInSource
                ? diagnostic.Location.GetLineSpan().Path
                : string.Empty;

        private static ImmutableArray<MetadataReference> CreatePlatformReferences()
        {
            var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

            if (trustedAssemblies is not null)
            {
                var trustedAssemblyPaths = trustedAssemblies.Split(
                      Path.PathSeparator
                    , StringSplitOptions.RemoveEmptyEntries
                );
                var trustedAssemblyCount = trustedAssemblyPaths.Length;

                for (var index = 0; index < trustedAssemblyCount; index++)
                {
                    AddReferencePath(referencePaths, trustedAssemblyPaths[index]);
                }
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var loadedAssemblyCount = loadedAssemblies.Length;

            for (var index = 0; index < loadedAssemblyCount; index++)
            {
                var assembly = loadedAssemblies[index];

                if (assembly.IsDynamic)
                {
                    continue;
                }

                string location;

                try
                {
                    location = assembly.Location;
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                AddReferencePath(referencePaths, location);
            }

            if (referencePaths.Count == 0)
            {
                ThrowMissingTrustedPlatformAssemblies();
            }

            return referencePaths
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToImmutableArray();
        }

        private static void AddReferencePath(HashSet<string> referencePaths, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path) == false)
            {
                return;
            }

            referencePaths.Add(Path.GetFullPath(path));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [HideInCallstack, DoesNotReturn]
        private static void ThrowMissingTrustedPlatformAssemblies()
            => throw new InvalidOperationException(
                "The runtime did not provide its trusted platform assembly list."
            );
    }
}

#endif
