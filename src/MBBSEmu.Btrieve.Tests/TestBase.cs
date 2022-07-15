using Xunit;

namespace MBBSEmu.Btrieve.Tests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }

    public abstract class TestBase
    {
        protected static readonly Random RANDOM = new();

        static TestBase()
        {

        }

        protected string GetModulePath()
        {
            return Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");
        }
    }
}
