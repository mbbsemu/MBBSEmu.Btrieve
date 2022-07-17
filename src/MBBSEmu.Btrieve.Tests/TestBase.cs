using Xunit;
using Xunit.Sdk;

namespace MBBSEmu.Btrieve.Tests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }

    public abstract class TestBase
    {
        static TestBase()
        {

        }

        protected string GetModulePath()
        {
            return Path.Join(Path.GetTempPath(), $"mbbsemu{new Random().Next()}");
        }
    }
}
