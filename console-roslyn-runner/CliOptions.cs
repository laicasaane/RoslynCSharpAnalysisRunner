namespace RoslynCSharpAnalysisRunner
{
    internal readonly record struct CliOptions(
          bool ShowHelp
        , string? EmitPath
        , IReadOnlyList<string> SourcePaths
    )
    {
        public static bool TryParse(
              string[] args
            , out CliOptions options
            , out string error
        )
        {
            var showHelp = false;
            string? emitPath = null;
            List<string> sourcePaths = [];

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];

                switch (argument)
                {
                    case "-h":
                    case "--help":
                    {
                        showHelp = true;
                        break;
                    }

                    case "-o":
                    case "--emit":
                    {
                        if (emitPath is not null)
                        {
                            error = "The --emit option can only be specified once.";
                            goto FAILED;
                        }

                        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                        {
                            error = "The --emit option requires an output path.";
                            goto FAILED;
                        }

                        emitPath = args[index];
                        break;
                    }

                    case "--":
                    {
                        sourcePaths.AddRange(args[(index + 1)..]);
                        index = args.Length;
                        break;
                    }

                    default:
                    {
                        if (argument.StartsWith("-", StringComparison.Ordinal))
                        {
                            error = $"Unknown option '{argument}'.";
                            goto FAILED;
                        }

                        sourcePaths.Add(argument);
                        break;
                    }
                }
            }

            options = new(showHelp, emitPath, sourcePaths);
            error = string.Empty;
            return true;

        FAILED:
            options = default;
            return false;
        }

        public static void PrintUsage(TextWriter output)
        {
            output.WriteLine("Roslyn C# Analysis Runner");
            output.WriteLine();
            output.WriteLine("Usage:");
            output.WriteLine("  dotnet run -- [options] [source1.cs source2.cs ...]");
            output.WriteLine();
            output.WriteLine("Options:");
            output.WriteLine("  -o, --emit <path>  Emit a DLL when compilation succeeds.");
            output.WriteLine("  -h, --help         Show this help text.");
            output.WriteLine();
            output.WriteLine("With no source paths, the runner analyzes a built-in C# example.");
        }
    }
}
