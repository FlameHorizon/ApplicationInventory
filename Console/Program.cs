using Logic;

const string path = "~/code/ApplicationInventory";
var inventory = new Inventory();
SolutionInfo result = inventory.Start(path);

foreach (ProjectInfo project in result.Projects) {
  Console.WriteLine(project.Sdk);
  Console.WriteLine(project.TargetFramework);
  Console.WriteLine(project.OutputType);
  Console.WriteLine(project.AssemblyName);
  Console.WriteLine(project.LangVersion);
  Console.WriteLine(project.Path);
}

// At this point you can save the result to JSON, csv, table
// or whatever you want. Job here is completed.