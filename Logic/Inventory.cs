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

        string? solutionPath = _fs.Directory
            .GetFiles(Directory.GetCurrentDirectory(), "*.sln")
            .FirstOrDefault();

        if (solutionPath is null)
        {
            Console.WriteLine("No solution (.sln) file found.");
            return;
        }

        Console.WriteLine($"Reading solution: {_fs.Path.GetFileName(solutionPath)}");

        List<string> projectPaths = GetProjectPathsFromSln(solutionPath);
        foreach (string projectPath in projectPaths)
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
        XDocument doc = XDocument.Load(csprojPath);
        XElement? root = doc.Root;

        string? sdk = root?.Attribute("Sdk")?.Value;
        XNamespace? ns = root?.Name.Namespace;

        XName? targetFrameworkName = null;
        XName? targetFrameworksName = null;
        XName? outputTypeName = null;
        XName? assemblyNameName = null;
        XName? langVersionName = null;

        if (ns is not null)
        {
            targetFrameworkName = ns + "TargetFramework";
            targetFrameworksName = ns + "TargetFrameworks";
            outputTypeName = ns + "OutputType";
            assemblyNameName = ns + "AssemblyName";
            langVersionName = ns + "LangVersion";
        }

        var targetFramework = root?.Descendants(targetFrameworkName).FirstOrDefault()?.Value ?? root?.Descendants(targetFrameworksName).FirstOrDefault()?.Value;
        var outputType = root?.Descendants(outputTypeName).FirstOrDefault()?.Value;
        var assemblyName = root?.Descendants(assemblyNameName).FirstOrDefault()?.Value;
        var langVersion = root?.Descendants(langVersionName).FirstOrDefault()?.Value;

        Console.WriteLine($" -> SDK: {sdk}");
        Console.WriteLine($" -> Target Framework: {targetFramework}");
        Console.WriteLine($" -> Output Type: {outputType}");
        Console.WriteLine($" -> Assembly Name: {assemblyName}");
        Console.WriteLine($" -> LangVersion: {langVersion}");

        XName? packageReferenceName = null;
        XName? projectReferenceDescendants = null;
        if (ns is not null)
        {
            packageReferenceName = ns + "PackageReference";
            projectReferenceDescendants = ns + "ProjectReference";
        }

        var packages = root?.Descendants(packageReferenceName)
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

        var projectRefs = root?.Descendants(projectReferenceDescendants)
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
