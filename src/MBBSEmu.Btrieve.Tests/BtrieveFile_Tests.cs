using FluentAssertions;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Btrieve.Tests.Resources;
using Xunit;

namespace MBBSEmu.Btrieve.Tests
{
    [Collection("Non-Parallel")]
    public class BtrieveFile_Tests : TestBase, IDisposable
    {
        private readonly string[] _btrieveFiles = { "MBBSEMU.DAT" };

        protected readonly string _modulePath;

        public BtrieveFile_Tests()
        {
            _modulePath = GetModulePath();

            Directory.CreateDirectory(_modulePath);

            CopyFilesToTempPath(ResourceManager.GetTestResourceManager());
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, recursive: true);
        }

        private void CopyFilesToTempPath(IResourceManager resourceManager)
        {
            foreach (var file in _btrieveFiles)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Btrieve.Tests.Assets.{file}").ToArray());

                //Verify Files Copied
                if (!File.Exists(Path.Combine(_modulePath, file)))
                    throw new FileNotFoundException($"Test Case File Copy Failed: {Path.Combine(_modulePath, file)}");
            }
        }

        [Fact]
        public void LoadsFile()
        {

            var btrieve = new BtrieveFile();
            btrieve.LoadFile(null, _modulePath, "MBBSEMU.DAT");

            Assert.Equal(4, btrieve.KeyCount);
            Assert.Equal(4, btrieve.Keys.Count);
            Assert.Equal(74, btrieve.RecordLength);
            Assert.Equal(90, btrieve.PhysicalRecordLength);
            Assert.Equal(512, btrieve.PageLength);
            Assert.Equal(5, btrieve.PageCount);
            Assert.False(btrieve.LogKeyPresent);
            Assert.False(btrieve.VariableLengthRecords);

            Assert.Single(btrieve.Keys[0].Segments);
            Assert.Single(btrieve.Keys[1].Segments);
            Assert.Single(btrieve.Keys[2].Segments);
            Assert.Single(btrieve.Keys[3].Segments);


            btrieve.Keys[0].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[3].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 3,
                    Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.AutoInc,
                    Offset = 70,
                    Length = 4,
                    Segment = false,
                });
        }
    }
}
