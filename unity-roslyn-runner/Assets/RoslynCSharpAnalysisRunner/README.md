# Roslyn C# Analysis Runner

This Editor-only assembly ports the behavior of `console-roslyn-runner` to Unity. It parses C# sources,
builds a semantic compilation, reports declared types and methods, prints ordered compiler diagnostics,
and can emit a DLL when the compilation succeeds.

Use these commands from Unity's **Tools > Roslyn C# Analysis Runner** menu:

- **Analyze Demo** runs the canonical built-in example.
- **Analyze Selected C# Files** analyzes the scripts selected in the Project window.
- **Emit Selected C# Files...** analyzes the selected scripts and writes a DLL.

The source model and analyzer remain internal, matching the canonical console implementation. The Unity
port only substitutes Editor menus and logging for the CLI front end, uses C# 10-compatible source syntax,
and supplements trusted platform metadata references with assemblies loaded in the Unity Editor domain.
