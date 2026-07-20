# Roslyn CSharp Analysis Runner

> [!NOTE]
> This repository was created by an AI assistant to demonstrate the tasks described in
> [Purpose](#purpose).
>
> Additionally, the README is structured to support AI-assisted development workflows.

## Table of contents

- [Purpose](#purpose)
- [Technology and Roslyn packages](#technology-and-roslyn-packages)
- [Structure](#structure)
- [Analysis pipeline](#analysis-pipeline)
- [Run from .NET CLI](#run-from-net-cli)
  - [Restore and build](#restore-and-build)
  - [Analyze built-in source](#analyze-built-in-source)
  - [Analyze files together](#analyze-files-together)
  - [Emit a DLL](#emit-a-dll)
  - [Show options](#show-options)
  - [Read results](#read-results)
- [Run from Unity Editor](#run-from-unity-editor)
- [Analyze in-memory C# text](#analyze-in-memory-c-text)
- [Add C# analysis functionality](#add-c-analysis-functionality)
  - [Implementation order](#implementation-order)
  - [Choose Roslyn API](#choose-roslyn-api)
  - [Exact extension points](#exact-extension-points)
  - [Recommended feature contract](#recommended-feature-contract)
  - [Print feature results](#print-feature-results)
  - [Return rule diagnostics](#return-rule-diagnostics)
  - [Expose structured results](#expose-structured-results)
- [Analyze sources with external dependencies](#analyze-sources-with-external-dependencies)
- [Match original project settings](#match-original-project-settings)
- [Port analysis to Unity](#port-analysis-to-unity)
- [Validation](#validation)
- [Limits](#limits)

## Purpose

- Host `Microsoft.CodeAnalysis.CSharp` from .NET CLI and Unity Editor.
- Parse one or more C# sources.
- Build one semantic compilation for all supplied sources.
- Read syntax nodes, semantic models, and symbols.
- Print declared types, methods, and compiler diagnostics.
- Emit a DLL when compilation succeeds.
- Provide clear extension points for new C# analysis.

> [!NOTE]
> Input files do not inherit a `.csproj`. Runner supplies language version, compiler options,
> and metadata references.

## Technology and Roslyn packages

| Host | Runtime | Runner language | Roslyn package |
|---|---|---|---|
| Console | [`.NET 10`, line 5](console-roslyn-runner/RoslynCSharpAnalysisRunner.csproj#L5) | [`latest`, line 8](console-roslyn-runner/RoslynCSharpAnalysisRunner.csproj#L8) | [`Microsoft.CodeAnalysis.CSharp` 5.6.0, line 13](console-roslyn-runner/RoslynCSharpAnalysisRunner.csproj#L13) |
| Unity | [Unity 6000.3.20f1](unity-roslyn-runner/ProjectSettings/ProjectVersion.txt#L1-L2) | [C# 10](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/csc.rsp#L1) | [`org.nuget.microsoft.codeanalysis.csharp` 5.6.0](unity-roslyn-runner/Packages/manifest.json#L34) |

Console package graph:

- Direct: `Microsoft.CodeAnalysis.CSharp` 5.6.0.
- Transitive: `Microsoft.CodeAnalysis.Common` 5.6.0.
- Transitive build checks: `Microsoft.CodeAnalysis.Analyzers` 5.3.0.

Unity Roslyn package:

- Loaded through configured OpenUPM registry:
  [`manifest.json`, lines 36-54](unity-roslyn-runner/Packages/manifest.json#L36-L54).
- Resolved Roslyn dependencies:
  [`packages-lock.json`, lines 168-278](unity-roslyn-runner/Packages/packages-lock.json#L168-L278).
- Editor-only assembly:
  [`RoslynCSharpAnalysisRunner.asmdef`, lines 1-16](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/RoslynCSharpAnalysisRunner.asmdef#L1-L16).

> [!IMPORTANT]
> Runner source language and analyzed-source language are separate. Both hosts parse input with
> `LanguageVersion.Latest` supported by Roslyn 5.6.0:
> [`RoslynAnalyzer.Run`, lines 24-36](console-roslyn-runner/RoslynAnalyzer.cs#L24-L36).

## Structure

```text
console-roslyn-runner/
├── RoslynCSharpAnalysisRunner.csproj   .NET target and Roslyn package
├── Program.cs               CLI host and exit codes
├── CliOptions.cs            CLI arguments
├── SourceFile.cs            Source path and text
├── DemoSource.cs            Built-in source
└── RoslynAnalyzer.cs        Parse, compile, analyze, report, emit

unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/
├── RoslynCSharpAnalysisRunner.asmdef
├── csc.rsp
└── Editor/
    ├── RoslynCSharpAnalysisRunnerMenu.cs
    ├── RoslynAnalyzer.cs
    ├── SourceFile.cs
    ├── DemoSource.cs
    └── IsExternalInit.cs
```

## Analysis pipeline

| Order | Type or member | Behavior |
|---:|---|---|
| 1 | [`Program.Main(string[])`](console-roslyn-runner/Program.cs#L5-L52) | Parse arguments, select demo or input files, map result to exit code |
| 2 | [`SourceFile.Load(string)`](console-roslyn-runner/SourceFile.cs#L9-L19) | Normalize path and read source text |
| 3 | [`CSharpSyntaxTree.ParseText(...)`](console-roslyn-runner/RoslynAnalyzer.cs#L29-L37) | Parse UTF-8 text with latest Roslyn language version |
| 4 | [`CSharpCompilation.Create(...)`](console-roslyn-runner/RoslynAnalyzer.cs#L39-L49) | Create one Release, nullable-enabled DLL compilation |
| 5 | [`PrintSyntaxSummary(...)`](console-roslyn-runner/RoslynAnalyzer.cs#L89-L132) | Read roots, semantic models, type symbols, and method symbols |
| 6 | [`Compilation.GetDiagnostics()`](console-roslyn-runner/RoslynAnalyzer.cs#L61-L67) | Collect non-hidden diagnostics in stable order |
| 7 | [`Emit(...)`](console-roslyn-runner/RoslynAnalyzer.cs#L154-L193) | Emit one DLL after error-free compilation |

Metadata references:

- Console: runtime `TRUSTED_PLATFORM_ASSEMBLIES`:
  [`CreatePlatformReferences()`, lines 217-230](console-roslyn-runner/RoslynAnalyzer.cs#L217-L230).
- Unity: runtime references plus loaded non-dynamic Editor assemblies:
  [`CreatePlatformReferences()`, lines 224-278](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/RoslynAnalyzer.cs#L224-L278).

## Run from .NET CLI

### Restore and build

```powershell
dotnet restore .\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj
dotnet build .\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj -c Release
```

### Analyze built-in source

```powershell
dotnet run --project .\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj
```

Expected summary:

- Source: `Demo.cs`.
- Type: `Demo.Calculator`.
- Methods: `Add(int, int)` and `PrintSum(int, int)`.
- Diagnostics: none.

### Analyze files together

```powershell
dotnet run --project .\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj -- .\path\First.cs .\path\Second.cs
```

> [!IMPORTANT]
> All files enter one compilation. Supply every source needed for cross-file symbols.

- Every path must name one file.
- Runner does not expand folders or wildcards.

### Emit a DLL

```powershell
$project = '.\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj'
dotnet run --project $project -- --emit .\artifacts\Analyzed.dll .\path\First.cs .\path\Second.cs
```

`-o` is short form of `--emit`.

### Show options

```powershell
dotnet run --project .\console-roslyn-runner\RoslynCSharpAnalysisRunner.csproj -- --help
```

Argument contract:
[`CliOptions.TryParse(...)`, lines 9-79](console-roslyn-runner/CliOptions.cs#L9-L79) and
[`CliOptions.PrintUsage(...)`, lines 81-93](console-roslyn-runner/CliOptions.cs#L81-L93).

### Read results

| Exit code | Meaning |
|---:|---|
| `0` | No compiler error; requested emit succeeded |
| `1` | Compiler error or emit failure |
| `2` | Invalid arguments, missing file, invalid path, denied access, I/O failure, or unavailable runtime references |

- Human report: standard output.
- Host and I/O errors: standard error.
- Exit mapping: [`Program.Main`, lines 7-18 and 42-51](console-roslyn-runner/Program.cs#L7-L18).

Report contains:

1. Parsed source count and Roslyn version.
2. Per-source line count.
3. Declared type names.
4. Declared method signatures.
5. Ordered diagnostics.
6. Optional emitted DLL path.

> [!CAUTION]
> Current programmatic result is `bool` from
> [`RoslynAnalyzer.Run(...)`, lines 18-87](console-roslyn-runner/RoslynAnalyzer.cs#L18-L87).
> Do not parse human report text for a durable integration; its format is for people.

## Run from Unity Editor

1. Open `unity-roslyn-runner` with Unity 6000.3.20f1.
2. Wait for package restore and script compilation.
3. Use `Tools > Roslyn C# Analysis Runner > Analyze Demo`; or
4. Select one or more `.cs` assets and use `Analyze Selected C# Files`; or
5. Use `Emit Selected C# Files...` to write a DLL.
6. Read result in Unity Console.

Menu members:

- [`AnalyzeDemo()`, lines 17-22](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/RoslynCSharpAnalysisRunnerMenu.cs#L17-L22).
- [`AnalyzeSelectedSources()`, lines 24-30](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/RoslynCSharpAnalysisRunnerMenu.cs#L24-L30).
- [`EmitSelectedSources()`, lines 32-52](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/RoslynCSharpAnalysisRunnerMenu.cs#L32-L52).
- [`RunAndLog(...)`, lines 68-98](unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/RoslynCSharpAnalysisRunnerMenu.cs#L68-L98).

Unity captures analyzer output in `StringWriter`. Success uses `Debug.Log`; failure uses
`Debug.LogError`.

## Analyze in-memory C# text

`SourceFile` stores a logical path and source text. Caller inside same assembly can skip disk:

```csharp
SourceFile[] sources =
{
    new("Input.cs", "public sealed class Input { }")
};

using var output = new StringWriter();
var succeeded = RoslynAnalyzer.Run(sources, null, output);
var report = output.ToString();
```

- `succeeded`: compiler and emit status.
- `report`: current human output.
- `Input.cs`: diagnostic path.
- Multiple `SourceFile` values: one cross-file compilation.

Contracts:
[`SourceFile`, line 7](console-roslyn-runner/SourceFile.cs#L7) and
[`RoslynAnalyzer.Run(...)`, lines 18-87](console-roslyn-runner/RoslynAnalyzer.cs#L18-L87).

> [!IMPORTANT]
> Both types are `internal`. External callers need a public core library or selected public contracts.

## Add C# analysis functionality

### Implementation order

1. Read [`CODING-CONVENTIONS.md`](CODING-CONVENTIONS.md).
2. Implement console version first.
3. Build and run console version.
4. Port same analysis and result types to Unity.
5. Keep host-specific reference discovery unchanged.
6. Compile Unity and run same fixture.
7. Commit each new Unity `.cs` with matching `.meta`.

### Choose Roslyn API

| Need | API |
|---|---|
| Tokens or source shape | `SyntaxTree`, `SyntaxNode`, `SyntaxToken` |
| Declarations | `CompilationUnitSyntax`, declaration syntax nodes |
| Declared type or member | `SemanticModel.GetDeclaredSymbol(...)` |
| Referenced type or member | `SemanticModel.GetSymbolInfo(...)` |
| Expression type | `SemanticModel.GetTypeInfo(...)` |
| Constant value | `SemanticModel.GetConstantValue(...)` |
| Cross-file data | `CSharpCompilation` |
| Known type | `Compilation.GetTypeByMetadataName(...)` |
| Whole input assembly | `Compilation.Assembly.GlobalNamespace` |
| Rule result | `DiagnosticDescriptor`, `Diagnostic.Create(...)` |

> [!TIP]
> Use syntax analysis for source shape and incomplete code. Use semantic analysis for resolved meaning.
> Use symbols for identity, and compare them with `SymbolEqualityComparer.Default`.

### Exact extension points

Per-file feature:

1. Reuse compilation created at
   [`RoslynAnalyzer.Run`, lines 44-49](console-roslyn-runner/RoslynAnalyzer.cs#L44-L49).
2. Add feature call inside existing syntax-tree loop at
   [`RoslynAnalyzer.Run`, lines 56-59](console-roslyn-runner/RoslynAnalyzer.cs#L56-L59).
3. Get semantic model with `compilation.GetSemanticModel(syntaxTree)`.
4. Keep all input files in same compilation.

Whole-compilation feature:

1. Run once after syntax-tree loop.
2. Place call before diagnostic collection at
   [`RoslynAnalyzer.Run`, line 61](console-roslyn-runner/RoslynAnalyzer.cs#L61).
3. Walk `compilation.SyntaxTrees`, symbols, or `compilation.Assembly.GlobalNamespace`.

New CLI option:

1. Add field to [`CliOptions`, lines 3-7](console-roslyn-runner/CliOptions.cs#L3-L7).
2. Parse it in [`CliOptions.TryParse`, lines 23-69](console-roslyn-runner/CliOptions.cs#L23-L69).
3. Add help text in [`CliOptions.PrintUsage`, lines 81-93](console-roslyn-runner/CliOptions.cs#L81-L93).
4. Pass explicit value from [`Program.Main`, line 42](console-roslyn-runner/Program.cs#L42).
5. Keep argument parsing outside `RoslynAnalyzer`.

### Recommended feature contract

> [!TIP]
> Keep Roslyn discovery separate from result formatting.

```text
PublicMethodAnalysis.cs   Roslyn traversal and symbol checks
PublicMethodResult.cs     Immutable values for caller
```

Example result:

```csharp
namespace RoslynCSharpAnalysisRunner
{
    internal readonly record struct PublicMethodResult(
          string SourcePath
        , string SymbolName
        , int Line
        , int Column
    );
}
```

Example semantic feature:

```csharp
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynCSharpAnalysisRunner
{
    internal static class PublicMethodAnalysis
    {
        public static ImmutableArray<PublicMethodResult> Analyze(
              CSharpCompilation compilation
            , SyntaxTree syntaxTree
        )
        {
            var root = syntaxTree.GetCompilationUnitRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .OrderBy(static method => method.SpanStart)
                .ToArray();
            var methodCount = methods.Length;
            var results = ImmutableArray.CreateBuilder<PublicMethodResult>();

            for (var index = 0; index < methodCount; index++)
            {
                var method = methods[index];
                var symbol = semanticModel.GetDeclaredSymbol(method);

                if (symbol is null || symbol.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                var start = method.Identifier.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition;
                var symbolName = symbol.ToDisplayString(
                    SymbolDisplayFormat.CSharpErrorMessageFormat
                );

                results.Add(new(
                      syntaxTree.FilePath
                    , symbolName
                    , start.Line + 1
                    , start.Character + 1
                ));
            }

            return results.ToImmutable();
        }
    }
}
```

Required result rules:

- Return immutable values.
- Handle `null` symbols from broken or incomplete code.
- Check `SymbolInfo.CandidateSymbols` when symbol resolution fails.
- Convert Roslyn zero-based locations to 1-based line and column values for display.
- Use `SymbolDisplayFormat.CSharpErrorMessageFormat` for readable signatures.
- Sort by source path, `Location.SourceSpan.Start`, then stable name or rule ID.
- Do not serialize or retain only live `SyntaxNode`, `SemanticModel`, or `ISymbol` objects.

### Print feature results

Add renderer to `RoslynAnalyzer`:

```csharp
private static void PrintPublicMethods(
      IReadOnlyList<PublicMethodResult> results
    , TextWriter output
)
{
    var resultCount = results.Count;
    output.WriteLine("Public methods:");

    if (resultCount == 0)
    {
        output.WriteLine("  None");
        return;
    }

    for (var index = 0; index < resultCount; index++)
    {
        var result = results[index];
        output.WriteLine(
            $"  {result.SourcePath}({result.Line},{result.Column}): {result.SymbolName}"
        );
    }
}
```

Use result:

- CLI human output: write through supplied `TextWriter`.
- Unity human output: `RunAndLog(...)` captures same text.
- Tests and code: inspect `ImmutableArray<PublicMethodResult>`.
- Machine output: serialize plain result values, not formatted report.

> [!IMPORTANT]
> Do not call `Console.WriteLine`, `Debug.Log`, or file APIs inside discovery code. Return results to
> host or renderer instead.

### Return rule diagnostics

Use `Diagnostic` for rule ID, severity, location, message, and CI status.

```csharp
private static readonly DiagnosticDescriptor s_publicMethodRule = new(
      id: "RCR001"
    , title: "Public method found"
    , messageFormat: "Public method '{0}' was found"
    , category: "Design"
    , defaultSeverity: DiagnosticSeverity.Warning
    , isEnabledByDefault: true
);
```

```csharp
var diagnostic = Diagnostic.Create(
      s_publicMethodRule
    , method.Identifier.GetLocation()
    , symbol.Name
);
```

Integration:

1. Return `ImmutableArray<Diagnostic>` from rule.
2. Concatenate rule diagnostics with `compilation.GetDiagnostics()`.
3. Reuse filter and ordering at
   [`RoslynAnalyzer.Run`, lines 61-67](console-roslyn-runner/RoslynAnalyzer.cs#L61-L67).
4. Print through
   [`PrintDiagnostics(...)`, lines 134-152](console-roslyn-runner/RoslynAnalyzer.cs#L134-L152).
5. Include custom `DiagnosticSeverity.Error` in existing `hasErrors` calculation.
6. Keep warnings non-failing unless explicit option defines another threshold.

### Expose structured results

Current `Run(...)` returns only `bool`. Add a typed result when code needs findings:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoslynCSharpAnalysisRunner
{
    internal sealed record AnalysisResult(
          CSharpCompilation Compilation
        , ImmutableArray<Diagnostic> Diagnostics
        , ImmutableArray<PublicMethodResult> PublicMethods
        , bool HasErrors
    );
}
```

Recommended split:

| Member | Responsibility |
|---|---|
| `RoslynAnalyzer.Analyze(...)` | Parse, compile, run features, return `AnalysisResult` |
| `RoslynAnalyzer.WriteReport(...)` | Convert result to human text |
| `RoslynAnalyzer.Emit(...)` | Write DLL |
| Existing `RoslynAnalyzer.Run(...)` | Preserve current host contract |

For JSON:

- Map diagnostics and symbols to plain path, line, column, ID, severity, and message values.
- Add `--format text|json`; keep `text` default.
- Write only JSON to standard output in JSON mode.
- Keep host and I/O failures on standard error.
- Preserve exit-code meaning.

> [!IMPORTANT]
> External callers cannot use `internal` contracts. Move shared analysis into a public class library,
> or expose only required types.

## Analyze sources with external dependencies

> [!WARNING]
> Current console has no `--reference` option. Code using project or NuGet assemblies can report
> `CS0012`, `CS0234`, or `CS0246` until required metadata references are added.

Add reference support:

1. Add repeatable `-r <path>` and `--reference <path>` to `CliOptions`.
2. Store paths in `IReadOnlyList<string> ReferencePaths`.
3. Normalize each path with `Path.GetFullPath`.
4. Require each file to exist.
5. Create reference with `MetadataReference.CreateFromFile(fullPath)`.
6. Combine with `s_platformReferences.Value`.
7. De-duplicate normalized paths.
8. Build a new immutable array; do not mutate cached platform references.
9. Pass combined array to
   [`CSharpCompilation.Create`, lines 44-49](console-roslyn-runner/RoslynAnalyzer.cs#L44-L49).

> [!NOTE]
> Unity includes assemblies loaded when platform-reference cache is first created. An assembly loaded
> later needs explicit reference support, cache rebuild, or domain reload.

## Match original project settings

| Needed project behavior | Roslyn API |
|---|---|
| Preprocessor symbols | `CSharpParseOptions.WithPreprocessorSymbols(...)` |
| Language version | `CSharpParseOptions.WithLanguageVersion(...)` |
| Nullable mode | `CSharpCompilationOptions.WithNullableContextOptions(...)` |
| Output kind | `CSharpCompilationOptions.WithOutputKind(...)` |
| Unsafe code | `CSharpCompilationOptions.WithAllowUnsafe(...)` |
| Referenced DLLs | `MetadataReference.CreateFromFile(...)` |

> [!NOTE]
> Exact `.csproj` analysis needs a separate loader using
> `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.Build.Locator`. These packages are not
> installed. Keep direct-source mode for isolated analysis.

## Port analysis to Unity

1. Copy analysis and result files into `unity-roslyn-runner/Assets/RoslynCSharpAnalysisRunner/Editor/`.
2. Change namespace to `RoslynCSharpAnalysisRunner.Editor`.
3. Wrap entire file in `#if UNITY_EDITOR`.
4. Keep `#nullable enable`.
5. Add explicit `System` usings.
6. Use C# 10 syntax.
7. Keep `IsExternalInit.cs` for records.
8. Keep Unity `CreatePlatformReferences()` implementation.
9. Keep `Debug.Log` calls in menu host only.
10. Let Unity generate `.meta`; commit source and `.meta` together.

## Validation

Console:

- Build Release with zero errors and warnings.
- Run built-in demo.
- Run valid single-file and cross-file fixtures.
- Run syntax-error and unresolved-reference fixtures.
- Confirm stable result ordering.
- Confirm successful emit writes DLL.

Unity:

- Wait for package restore and Editor compile.
- Run demo and selected-source actions.
- Run same fixtures used by console.
- Confirm console and Unity findings match.
- Confirm every new `.cs` has `.meta`.

## Limits

- No stdin input.
- No folder or wildcard expansion.
- No `.csproj` or solution loading.
- No CLI settings for defines, nullable mode, language version, or external references.
- Type summary excludes delegates.
- Method summary excludes constructors, destructors, operators, accessors, local functions, and lambdas.
- Emit writes DLL only; no PDB, XML documentation, or dependency copy.
- Console and Unity analysis code are separate copies and need parity review.
