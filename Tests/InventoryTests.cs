using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Logic;

namespace Tests;

public class InvetoryTests
{
    private readonly Inventory _inventory;
    private readonly MockFileSystem _fs;

    public InvetoryTests()
    {
        _fs = new MockFileSystem();
        // TODO: Maybe we don't need actually check for the op. system
        // when using Path.Combine?
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _fs.AddDirectory(_fs.Path.Combine("projects"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotImplementedException();
        }

        _inventory = new Inventory(_fs);
    }

    [Fact]
    public void Throws_ArugmentExcpetion_When_PathToSolution_IsEmpty()
    {
        Action act = () => _inventory.Start(string.Empty);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void SolutionsFound_Should_BeEmpty_When_PathDoesntContainsAnySolutions()
    {
        Assert.Empty(_inventory.SolutionsFound);
        _inventory.Start("projects");
        Assert.Empty(_inventory.SolutionsFound);
    }

    [Fact]
    public void SolutionsFound_Should_ListFoundSolutions()
    {
        string pathToSolution = _fs.Path.Combine("/projects", "Sol1.sln");
        _fs.AddEmptyFile(pathToSolution);

        _inventory.Start("projects");
        Assert.NotEmpty(_inventory.SolutionsFound);

        Assert.Equal(pathToSolution, _inventory.SolutionsFound.First());
    }

    [Fact]
    public void ProjectFound_Should_ListFoundProjects()
    {
        string projectPath = _fs.Path.Combine("/projects", "Console", "Console.csproj");
        string solutionContent =
"""
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Console", <projectPath>, "{A6DDB6F0-7584-4727-BC74-F22CAE829AA3}" 
EndProject
""";
        solutionContent = solutionContent.Replace("<projectPath>", projectPath);

        _fs.AddFile(
            _fs.Path.Combine("projects", "Sol1.sln"),
            new MockFileData(solutionContent));

        _inventory.Start("projects");
        Assert.NotEmpty(_inventory.ProjectsFound);
        Assert.Equal(
            _fs.Path.Combine("/projects", "Console", "Console.csproj"),
            _inventory.ProjectsFound.First());
    }


    // This test uses data for solution and project which are already stored
    // in the .sln and .csproj file.
    [Fact]
    public void ProjectInfo_Should_ListProjectInformation()
    {
        string solutionContent = File.ReadAllText(
            Path.Combine("TestData", "Sol1.sln"));

        _fs.AddFile(
            _fs.Path.Combine("projects", "Sol1.sln"),
            new MockFileData(solutionContent));

        string projectContent = File.ReadAllText(
            Path.Combine("TestData", "Proj1.csproj"));

        _fs.AddFile(
            _fs.Path.Combine("projects", "Console", "Proj1.csproj"),
            new MockFileData(projectContent));

        _inventory.Start("projects");

        Assert.Single(_inventory.ProjectsInfos);
        ProjectInfo pi = _inventory.ProjectsInfos.First();

        Assert.Equal("Microsoft.NET.Sdk", pi.Sdk);
        Assert.Equal("net9.0", pi.TargetFramework);
        Assert.Equal("Exe", pi.OutputType);
        Assert.True(string.IsNullOrEmpty(pi.LangVersion));

        Assert.Single(pi.Packages);
        PackageInfo pki = pi.Packages.First();
        Assert.Equal("Microsoft.NET.Test.Sdk", pki.Name);
        Assert.Equal("17.12.0", pki.Version);

        Assert.Single(pi.ProjectReferences);
        string pp = pi.ProjectReferences.First();
        Assert.Equal("/projects/Proj2/Proj2.csproj", pp);
    }

    [Fact]
    public void ProjectInfo_Should_ReturnProjectInformation()
    {
        string solutionContent = File.ReadAllText(
            Path.Combine("TestData", "Sol1.sln"));

        _fs.AddFile(
            _fs.Path.Combine("projects", "Sol1.sln"),
            new MockFileData(solutionContent));

        string projectContent = File.ReadAllText(
            Path.Combine("TestData", "Proj1.csproj"));

        _fs.AddFile(
            _fs.Path.Combine("projects", "Console", "Proj1.csproj"),
            new MockFileData(projectContent));

        SolutionInfo result = _inventory.Start("projects");

        Assert.Single(result.Projects);
        ProjectInfo pi = result.Projects.First();

        Assert.Equal("Microsoft.NET.Sdk", pi.Sdk);
        Assert.Equal("net9.0", pi.TargetFramework);
        Assert.Equal("Exe", pi.OutputType);
        Assert.True(string.IsNullOrEmpty(pi.LangVersion));

        Assert.Single(pi.Packages);
        PackageInfo pki = pi.Packages.First();
        Assert.Equal("Microsoft.NET.Test.Sdk", pki.Name);
        Assert.Equal("17.12.0", pki.Version);

        Assert.Single(pi.ProjectReferences);
        string pp = pi.ProjectReferences.First();
        Assert.Equal("/projects/Proj2/Proj2.csproj", pp);
    }
}
