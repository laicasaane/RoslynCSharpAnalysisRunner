#if UNITY_EDITOR
#nullable enable

namespace RoslynCSharpAnalysisRunner.Editor
{
    internal static class DemoSource
    {
        public const string FILE_NAME = "Demo.cs";
        public const string TEXT =
            "using System;\n" +
            "\n" +
            "namespace Demo;\n" +
            "\n" +
            "public static class Calculator\n" +
            "{\n" +
            "    public static int Add(int left, int right)\n" +
            "        => left + right;\n" +
            "\n" +
            "    public static void PrintSum(int left, int right)\n" +
            "    {\n" +
            "        Console.WriteLine(Add(left, right));\n" +
            "    }\n" +
            "}";
    }
}

#endif
