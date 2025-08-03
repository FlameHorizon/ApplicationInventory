using System.Xml.Linq;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

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

    // Instance of a logger which well... logs messages.
    private readonly ILogger<Inventory> _logger;


    /// <summary>
    /// Creates instance of a class using 'real' file system
    /// and logger which prints messages to console.
    /// </summary>
    public Inventory()
    {
        _fs = new FileSystem();

        using ILoggerFactory factory = LoggerFactory
            .Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Inventory>();
    }

    /// <summary>
    /// Creates instance of a class using given file system
    /// and logger which prints messages to console.
    /// </summary>
    public Inventory(IFileSystem fs)
    {
        _fs = fs;

        using ILoggerFactory factory = LoggerFactory
            .Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Inventory>();
    }

    public Inventory(IFileSystem fs, ILogger<Inventory> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    /// <summary>
    /// Starts process of analyzing solution and attached projects to it.
    /// </summary>
    public SolutionInfo Start(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));

        var result = new SolutionInfo();
        string? solutionPath = _fs.Directory
            .GetFiles(path, "*.sln")
            .FirstOrDefault();

        if (solutionPath is null)
        {
            _logger.LogWarning("No solution (.sln) file found.");
            return new SolutionInfo();
        }

        SolutionsFound.Add(solutionPath);
        // TODO: Might thing if I want to still return values 
        // using properties. It was useful for testing but now seem
        // not needed.

        result.SolutionPath = solutionPath;

        _logger.LogInformation($"Reading solution: {_fs.Path.GetFileName(solutionPath)}");

        List<string> projectPaths = GetProjectPathsFromSln(solutionPath);
        ProjectsFound.AddRange(projectPaths);

        foreach (string projectPath in projectPaths)
        {
            _logger.LogInformation($"\nProject: {_fs.Path.GetFileName(projectPath)}");
            if (_fs.File.Exists(projectPath))
            {
                var project = ReadProjectFile(projectPath);
                project.Path = projectPath;
                result.Projects.Add(project);

            }
            else
            {
                _logger.LogInformation(" -> Project file not found.");
            }
        }

        return result;
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

    private ProjectInfo ReadProjectFile(string csprojPath)
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

        result.Sdk = sdk;
        result.TargetFramework = targetFramework;
        result.OutputType = outputType;
        result.AssemblyName = assemblyName;
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
            foreach (var pkg in packages)
            {
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
            foreach (var projRef in projectRefs)
            {
                var fullPath = _fs.Path.GetFullPath(
                    _fs.Path.Combine(_fs.Path.GetDirectoryName(csprojPath)!, projRef!));

                result.ProjectReferences.Add(fullPath);
            }
        }

        ProjectsInfos.Add(result);
        return result;
    }
}

public class SolutionInfo
{
    public SolutionInfo()
    { }

    public string SolutionPath { get; internal set; } = "";
    public List<ProjectInfo> Projects { get; internal set; } = [];
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
    public string? Path { get; internal set; }
}

public class PackageInfo
{
    public string? Name { get; internal set; }
    public string? Version { get; internal set; }
}
