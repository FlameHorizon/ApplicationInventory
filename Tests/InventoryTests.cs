using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;

namespace Tests;

public class InvetoryTests
{
    private readonly Inventory _inventory;

    public InvetoryTests()
    {
        MockFileSystem fs = new MockFileSystem();
        // TODO: Maybe we don't need actually check for the op. system
        // when using Path.Combine?
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            fs.AddDirectory(fs.Path.Combine("projects"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotImplementedException();
        }

        _inventory = new Inventory(fs);
    }

    [Fact]
    public void Throws_ArugmentExcpetion_When_PathToSolution_IsEmpty()
    {
        Action act = () => _inventory.Start(string.Empty);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void SolustionsFound_Should_ListFoundSolutions()
    {

    }
}
