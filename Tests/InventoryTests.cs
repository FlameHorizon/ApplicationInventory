using System.IO.Abstractions.TestingHelpers;

namespace Tests;

public class InvetoryTests
{
    private readonly Inventory _inventory;

    public InvetoryTests()
    {
        _inventory = new Inventory(new MockFileSystem());
    }

    [Fact]
    public void Throws_ArugmentExcpetion_When_PathToSolution_IsEmpty()
    {
        Action act = () => _inventory.Start(string.Empty);
        Assert.Throws<ArgumentException>(act);
    }
}
