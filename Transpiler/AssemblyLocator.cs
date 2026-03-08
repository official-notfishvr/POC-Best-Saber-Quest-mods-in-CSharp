using System;
using System.IO;
using System.Linq;

namespace Transpiler;

internal static class AssemblyLocator
{
    public static string ResolveAssemblyPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new InvalidOperationException("Input path is empty.");

        var fullPath = Path.GetFullPath(inputPath);

        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            if (string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
                return ResolveBuiltProjectOutput(fullPath);

            throw new InvalidOperationException($"Unsupported input file: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            var dlls = Directory.GetFiles(fullPath, "*.dll", SearchOption.TopDirectoryOnly);
            if (dlls.Length == 1)
                return dlls[0];

            var projectPath = Directory.GetFiles(fullPath, "*.csproj", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

            if (projectPath != null)
                return ResolveBuiltProjectOutput(projectPath);

            throw new InvalidOperationException($"Could not find a .dll or .csproj in {fullPath}");
        }

        throw new FileNotFoundException($"Input path does not exist: {fullPath}");
    }

    private static string ResolveBuiltProjectOutput(string projectPath)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory();
        var binDirectory = Path.Combine(projectDirectory, "bin");

        if (!Directory.Exists(binDirectory))
            throw new InvalidOperationException($"Could not find build output for {projectPath}. Build the project first.");

        var candidate = Directory.GetFiles(binDirectory, $"{assemblyName}.dll", SearchOption.AllDirectories).Select(path => new FileInfo(path)).OrderByDescending(info => info.LastWriteTimeUtc).FirstOrDefault();

        if (candidate == null)
            throw new InvalidOperationException($"Could not find built assembly for {assemblyName} under {binDirectory}. Build the project first.");

        return candidate.FullName;
    }
}
