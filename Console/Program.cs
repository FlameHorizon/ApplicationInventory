using Logic;

const string path = "/home/flm/code/Memoria";
var inventory = new Inventory();
SolutionInfo result = inventory.Start(path);

Console.WriteLine(result.SolutionPath);

foreach (ProjectInfo project in result.Projects)
{
    Console.WriteLine(project.Path);
    Console.WriteLine("SDK: " + project.Sdk);
    Console.WriteLine("Target framework: " + project.TargetFramework);
    Console.WriteLine("Output type: " + project.OutputType);
    Console.WriteLine("Assembly name: " + project.AssemblyName);
    Console.WriteLine("Language version: " + project.LangVersion);
    
    Console.WriteLine("Project References:");
    foreach (string projectReference in project.ProjectReferences) {
        Console.WriteLine("Reference: " + projectReference);
    }

    Console.WriteLine("Packages:");
    foreach (PackageInfo package in project.Packages) {
        Console.WriteLine("Name: " + package.Name + ", Version: " + package.Version);
    }

    Console.WriteLine("===");
}

// At this point you can save the result to JSON, csv, table
// or whatever you want. Job here is completed.
