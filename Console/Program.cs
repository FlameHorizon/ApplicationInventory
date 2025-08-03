using Logic;

var path = "~/code/ApplicationInventory";
var inventory = new Inventory();
var result = inventory.Start(path);

foreach (var project in result.Projects)
{
    Console.WriteLine(project.Sdk);
    Console.WriteLine(project.TargetFramework);
    Console.WriteLine(project.OutputType);
    Console.WriteLine(project.AssemblyName);
    Console.WriteLine(project.LangVersion);
    Console.WriteLine(project.Path);
}

// At this point you can save the result to json, csv, table
// or whatever you want. Job here is completed.
