namespace RoslynCSharpAnalysisRunner
{
    internal static class DemoSource
    {
        public const string FILE_NAME = "Demo.cs";
        public const string TEXT = """
            using System;

            namespace Demo;

            public static class Calculator
            {
                public static int Add(int left, int right)
                    => left + right;

                public static void PrintSum(int left, int right)
                {
                    Console.WriteLine(Add(left, right));
                }
            }
            """;
    }
}
