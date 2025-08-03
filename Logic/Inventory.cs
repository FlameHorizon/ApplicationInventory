using System.Xml.Linq;
using System.IO.Abstractions;

public class Inventory
{
    // TODO: Replace logging into console to Serilog.

    /// <summary>
    /// Contains list of solution after running Start method.
    /// Each element is a path to solution.
    /// </summary>
    public readonly List<string> SolutionsFound = [];

    /// <summary>
    /// Contains list of projects found which are referenced by the solution.
    /// Each element is a path to a project.
    /// </summary>
    public readonly List<string> ProjectsFound = [];

    /// <summary>
    /// Contains a list of project related information.
    /// Things like SDK, target framework, language version and so on.
    /// </summary>
    public readonly List<ProjectInfo> ProjectsInfos = [];

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
            .GetFiles(path, "*.sln")
            .FirstOrDefault();

        if (solutionPath is null)
        {
            Console.WriteLine("No solution (.sln) file found.");
            return;
        }

        SolutionsFound.Add(solutionPath);

        Console.WriteLine($"Reading solution: {_fs.Path.GetFileName(solutionPath)}");

        List<string> projectPaths = GetProjectPathsFromSln(solutionPath);
        ProjectsFound.AddRange(projectPaths);

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
        FileSystemStream ts = _fs.FileStream.New(csprojPath, FileMode.Open);
        XDocument doc = XDocument.Load(ts);
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

        var result = new ProjectInfo();

        Console.WriteLine($" -> SDK: {sdk}");
        result.Sdk = sdk;

        Console.WriteLine($" -> Target Framework: {targetFramework}");
        result.TargetFramework = targetFramework;

        Console.WriteLine($" -> Output Type: {outputType}");
        result.OutputType = outputType;

        Console.WriteLine($" -> Assembly Name: {assemblyName}");
        result.AssemblyName = assemblyName;

        Console.WriteLine($" -> LangVersion: {langVersion}");
        result.LangVersion = langVersion;

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
                result.Packages.Add(new PackageInfo()
                {
                    Name = pkg.Name,
                    Version = pkg.Version
                });
            }
        }

        var projectRefs = root?.Descendants(projectReferenceDescendants)
          .Select(p => p.Attribute("Include")?.Value);

        if (projectRefs != null && projectRefs.Any())
        {
            Console.WriteLine(" -> Project References:");
            foreach (var projRef in projectRefs)
            {
                var fullPath = _fs.Path.GetFullPath(
                    _fs.Path.Combine(_fs.Path.GetDirectoryName(csprojPath)!, projRef!));

                Console.WriteLine($"    - {fullPath}");
                result.ProjectReferences.Add(fullPath);
            }
        }

        ProjectsInfos.Add(result);
    }
}

public class ProjectInfo
{
    public string? Sdk { get; internal set; }
    public string? TargetFramework { get; internal set; }
    public string? OutputType { get; internal set; }
    public string? AssemblyName { get; internal set; }
    public string? LangVersion { get; internal set; }
    public List<PackageInfo> Packages { get; internal set; } = [];
    public List<string> ProjectReferences { get; internal set; } = [];
}

public class PackageInfo
{
    public string? Name { get; internal set; }
    public string? Version { get; internal set; }
}
