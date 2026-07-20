namespace RoslynCSharpAnalysisRunner
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (CliOptions.TryParse(args, out var options, out var error) == false)
            {
                Console.Error.WriteLine($"error: {error}");
                Console.Error.WriteLine();
                CliOptions.PrintUsage(Console.Error);
                return 2;
            }

            if (options.ShowHelp)
            {
                CliOptions.PrintUsage(Console.Out);
                return 0;
            }

            try
            {
                SourceFile[] sources;

                if (options.SourcePaths.Count == 0)
                {
                    Console.WriteLine("No source files supplied; analyzing the built-in demo.");
                    Console.WriteLine();
                    sources = [new(DemoSource.FILE_NAME, DemoSource.TEXT)];
                }
                else
                {
                    var sourceCount = options.SourcePaths.Count;
                    sources = new SourceFile[sourceCount];

                    for (var index = 0; index < sourceCount; index++)
                    {
                        sources[index] = SourceFile.Load(options.SourcePaths[index]);
                    }
                }

                return RoslynAnalyzer.Run(sources, options.EmitPath, Console.Out) ? 0 : 1;
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 2;
            }
        }
    }
}
