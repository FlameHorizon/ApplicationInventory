using System.Xml.Linq;
using System.IO.Abstractions;

public class Inventory
{
    // Represents file system used. Can be swapped during testing.
    private readonly IFileSystem _fs;

    public Inventory(IFileSystem fs)
    {
        _fs = fs;
    }

    /// <summary>Starts process of analyzing solution and attached projects to it.
    /// </summary>
    public void Start(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));
        string solutionPath = _fs.Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln").FirstOrDefault();
        if (solutionPath == null)
        {
            Console.WriteLine("No solution (.sln) file found.");
            return;
        }

        Console.WriteLine($"Reading solution: {_fs.Path.GetFileName(solutionPath)}");

        var projectPaths = GetProjectPathsFromSln(solutionPath);
        foreach (var projectPath in projectPaths)
        {
            Console.WriteLine($"\nProject: {_fs.Path.GetFileName(projectPath)}");
            if (_fs.File.Exists(projectPath))
            {
                ReadProjectFile(projectPath);
            }
            else
            {
                Console.WriteLine(" -> Project file not found.");
            }
        }
    }

    private List<string> GetProjectPathsFromSln(string slnPath)
    {
        var result = new List<string>();
        var lines = _fs.File.ReadAllLines(slnPath);
        foreach (var line in lines)
        {
            if (line.StartsWith("Project("))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    var rawPath = parts[1].Trim().Trim('"');
                    var fullPath = _fs.Path.GetFullPath(
                            _fs.Path.Combine(
                                _fs.Path.GetDirectoryName(slnPath)!, rawPath));
                    result.Add(fullPath);
                }
            }
        }
        return result;
    }

    private void ReadProjectFile(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var root = doc.Root;

        string? sdk = root?.Attribute("Sdk")?.Value;
        var ns = root?.Name.Namespace;

        var targetFramework = root?.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value ??
          root?.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
        var outputType = root?.Descendants(ns + "OutputType").FirstOrDefault()?.Value;
        var assemblyName = root?.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value;
        var langVersion = root?.Descendants(ns + "LangVersion").FirstOrDefault()?.Value;

        Console.WriteLine($" -> SDK: {sdk}");
        Console.WriteLine($" -> Target Framework: {targetFramework}");
        Console.WriteLine($" -> Output Type: {outputType}");
        Console.WriteLine($" -> Assembly Name: {assemblyName}");
        Console.WriteLine($" -> LangVersion: {langVersion}");

        var packages = root?.Descendants(ns + "PackageReference")
          .Select(p => new
          {
              Name = p.Attribute("Include")?.Value,
              Version = p.Attribute("Version")?.Value
          });

        if (packages != null && packages.Any())
        {
            Console.WriteLine(" -> NuGet Packages:");
            foreach (var pkg in packages)
            {
                Console.WriteLine($"    - {pkg.Name} ({pkg.Version})");
            }
        }

        var projectRefs = root?.Descendants(ns + "ProjectReference")
          .Select(p => p.Attribute("Include")?.Value);

        if (projectRefs != null && projectRefs.Any())
        {
            Console.WriteLine(" -> Project References:");
            foreach (var projRef in projectRefs)
            {
                var fullPath = _fs.Path.GetFullPath(_fs.Path.Combine(_fs.Path.GetDirectoryName(csprojPath)!, projRef!));
                Console.WriteLine($"    - {fullPath}");
            }
        }
    }
}
