using Xunit;

namespace MBBSEmu.Btrieve.Tests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {

    }

    public abstract class TestBase
    {
        private static string randomPath { get; }

        static TestBase()
        {
            randomPath = Path.Join(Path.GetTempPath(), $"mbbsemu{new Random().Next()}");
        }

        protected string GetModulePath() => randomPath;
    }
}
