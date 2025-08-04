using System.Xml.Linq;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace Logic;

public class Inventory {
  /// <summary>
  /// Contains the list of a solution after running Start method.
  /// Each element is a path to a solution.
  /// </summary>
  public readonly List<string> SolutionsFound = [];

  /// <summary>
  /// Contains the list of projects found that is referenced by the solution.
  /// Each element is a path to a project.
  /// </summary>
  public readonly List<string> ProjectsFound = [];

  /// <summary>
  /// Contains the list of project-related information.
  /// Things like SDK, target framework, language version and so on.
  /// </summary>
  public readonly List<ProjectInfo> ProjectsInfos = [];

  // Represents a file system used. Can be swapped during testing.
  private readonly IFileSystem _fs;

  // Instance of a logger which well... logs messages.
  private readonly ILogger<Inventory> _logger;

  /// <summary>
  /// Creates an instance of a class using 'real' file system
  /// and logger which prints messages to the console.
  /// </summary>
  public Inventory() {
    _fs = new FileSystem();

    using ILoggerFactory factory = LoggerFactory
      .Create(builder => builder.AddConsole());
    _logger = factory.CreateLogger<Inventory>();
  }

  /// <summary>
  /// Creates an instance of a class using a given file system
  /// and logger which prints messages to the console.
  /// </summary>
  public Inventory(IFileSystem fs) {
    _fs = fs;

    using ILoggerFactory factory = LoggerFactory
      .Create(builder => builder.AddConsole());
    _logger = factory.CreateLogger<Inventory>();
  }

  public Inventory(IFileSystem fs, ILogger<Inventory> logger) {
    _fs = fs;
    _logger = logger;
  }

  /// <summary>
  /// Starts the process of analyzing a solution and attached projects to it.
  /// </summary>
  public SolutionInfo Start(string path) {
    ArgumentException.ThrowIfNullOrEmpty(path, nameof(path));

    var result = new SolutionInfo();

    string fullPath = Environment.ExpandEnvironmentVariables(path);

    if (fullPath.StartsWith('~')) {
      string home = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile);
      fullPath = _fs.Path.Combine(home,
        fullPath.TrimStart('~')
          .TrimStart(Path.DirectorySeparatorChar));
    }

    string? solutionPath = _fs.Directory
      .GetFiles(fullPath, "*.sln")
      .FirstOrDefault();

    if (solutionPath is null) {
      _logger.LogWarning("No solution (.sln) file found.");
      return new SolutionInfo();
    }

    SolutionsFound.Add(solutionPath);
    // TODO: Might thing if I want to still return values 
    // using properties. It was useful for testing but now seem
    // not needed.

    result.SolutionPath = solutionPath;

    _logger.LogInformation("Reading solution: {GetFileName}", _fs.Path.GetFileName(solutionPath));

    List<string> projectPaths = GetProjectPathsFromSln(solutionPath);
    ProjectsFound.AddRange(projectPaths);

    foreach (string projectPath in projectPaths) {
      _logger.LogInformation("\nProject: {GetFileName}", _fs.Path.GetFileName(projectPath));
      if (_fs.File.Exists(projectPath)) {
        ProjectInfo project = ReadProjectFile(projectPath);
        project.Path = projectPath;
        result.Projects.Add(project);
      }
      else {
        _logger.LogInformation(" -> Project file not found.");
      }
    }

    return result;
  }

  private List<string> GetProjectPathsFromSln(string slnPath) {
    var result = new List<string>();
    string[] lines = _fs.File.ReadAllLines(slnPath);
    foreach (string line in lines) {
      if (!line.StartsWith("Project(")) continue;

      string[] parts = line.Split(',');
      if (parts.Length < 2) continue;

      string relativePath = parts[1].Trim().Trim('"');
      relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);

      string path = _fs.Path.Combine(_fs.Path.GetDirectoryName(slnPath)!, relativePath);
      string fullPath = _fs.Path.GetFullPath(path);
      result.Add(fullPath);
    }

    return result;
  }

  private ProjectInfo ReadProjectFile(string csprojPath) {
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
    XName? targetFrameworkVersion = null;

    if (ns is not null) {
      targetFrameworkName = ns + "TargetFramework";
      targetFrameworksName = ns + "TargetFrameworks";
      targetFrameworkVersion = ns + "TargetFrameworkVersion";
      outputTypeName = ns + "OutputType";
      assemblyNameName = ns + "AssemblyName";
      langVersionName = ns + "LangVersion";
    }

    string? targetFramework = root?.Descendants(targetFrameworkName).FirstOrDefault()?.Value
                              ?? root?.Descendants(targetFrameworksName).FirstOrDefault()?.Value
                              ?? root?.Descendants(targetFrameworkVersion).FirstOrDefault()?.Value;

    string? outputType = root?.Descendants(outputTypeName).FirstOrDefault()?.Value;
    string? assemblyName = root?.Descendants(assemblyNameName).FirstOrDefault()?.Value;
    string? langVersion = root?.Descendants(langVersionName).FirstOrDefault()?.Value;

    var result = new ProjectInfo {
      Sdk = sdk,
      TargetFramework = targetFramework,
      OutputType = outputType,
      AssemblyName = assemblyName,
      LangVersion = langVersion
    };

    XName? packageReferenceName = null;
    XName? projectReferenceDescendants = null;
    if (ns is not null) {
      packageReferenceName = ns + "PackageReference";
      projectReferenceDescendants = ns + "ProjectReference";
    }

    var packages = root?.Descendants(packageReferenceName)
      .Select(p => new {
        Name = p.Attribute("Include")?.Value,
        Version = p.Attribute("Version")?.Value
      })
      .ToList();

    if (packages != null && packages.Count != 0) {
      foreach (var pkg in packages) {
        result.Packages.Add(new PackageInfo {
          Name = pkg.Name,
          Version = pkg.Version
        });
      }
    }

    IEnumerable<string?>? projectRefs = root?
      .Descendants(projectReferenceDescendants)
      .Select(p => p.Attribute("Include")?.Value)
      .ToList();

    if (projectRefs != null && projectRefs.Any()) {
      foreach (string? projRef in projectRefs) {
        string relativePath = projRef ?? throw new InvalidOperationException();
        relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);

        string fullPath = "";
        if (relativePath.StartsWith("..")) {
          relativePath = relativePath.TrimStart("..".ToCharArray());
          IDirectoryInfo projectRoot = _fs.Directory.GetParent(csprojPath)!;
          IDirectoryInfo solutionRoot = projectRoot.Parent!;
          fullPath = solutionRoot.FullName + relativePath;
        }
        else {
          fullPath = _fs.Path.GetFullPath(
            _fs.Path.Combine(_fs.Path.GetDirectoryName(csprojPath)!, projRef!));
        }

        if (string.IsNullOrEmpty(fullPath) == false) {
          result.ProjectReferences.Add(fullPath);
        }
      }
    }

    ProjectsInfos.Add(result);
    return result;
  }
}

public class SolutionInfo {
  public string SolutionPath { get; internal set; } = "";
  public List<ProjectInfo> Projects { get; } = [];
}

public class ProjectInfo {
  public string? Sdk { get; internal init; }
  public string? TargetFramework { get; internal init; }
  public string? OutputType { get; internal init; }
  public string? AssemblyName { get; internal init; }
  public string? LangVersion { get; internal init; }
  public List<PackageInfo> Packages { get; } = [];
  public List<string> ProjectReferences { get; } = [];
  public string? Path { get; internal set; }
}

public class PackageInfo {
  public string? Name { get; internal init; }
  public string? Version { get; internal init; }
}