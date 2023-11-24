using System;
using System.Collections.Generic;
using System.IO;
using MBBSEmu.Btrieve.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.ComponentModel;
using System.Net.Mail;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents an instance of a Btrieve File .DAT
    /// </summary>
    public class BtrieveFile
    {
        /// <summary>
        ///     Filename of Btrieve File
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        ///     Number of Pages within the Btrieve File
        /// </summary>
        public ushort PageCount => (ushort) (Data.Length / PageLength - 1);

        private uint _recordCount;
        /// <summary>
        ///     Total Number of Records in the specified Btrieve File
        /// </summary>
        public uint RecordCount
        {
            get
            {
                if (_fcr?.Length > 0)
                {
                    return (uint)(BitConverter.ToUInt16(_fcr, 0x1A) << 16) | BitConverter.ToUInt16(_fcr, 0x1C);
                }

                return _recordCount;
            }
            set
            {
                if (Data?.Length > 0)
                {
                    Array.Copy(BitConverter.GetBytes((ushort)(value >> 16)), 0, Data, 0x1A, sizeof(ushort));
                    Array.Copy(BitConverter.GetBytes((ushort)(value & 0xFFFF)), 0, Data, 0x1C, sizeof(ushort));
                }

                _recordCount = value;
            }
        }

        /// <summary>
        ///     Whether the records are variable length
        /// </summary>
        public bool VariableLengthRecords { get; set; }

        public bool VariableLengthTruncation { get; set; }

        private ushort _recordLength;
        /// <summary>
        ///     Defined Length of the records within the Btrieve File
        /// </summary>
        public ushort RecordLength
        {
            get
            {
                if (_fcr?.Length > 0)
                    return BitConverter.ToUInt16(_fcr, 0x16);

                return _recordLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x16, sizeof(ushort));

                _recordLength = value;
            }
        }

        private ushort _physicalRecordLength;
        /// <summary>
        ///     Actual Length of the records within the Btrieve File, including additional padding.
        /// </summary>
        public ushort PhysicalRecordLength
        {
            get
            {
                if (_fcr?.Length > 0)
                    return BitConverter.ToUInt16(_fcr, 0x18);

                return _physicalRecordLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x18, sizeof(ushort));

                _physicalRecordLength = value;
            }
        }

        private ushort _pageLength;
        /// <summary>
        ///     Defined length of each Page within the Btrieve File
        /// </summary>
        public ushort PageLength
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x08);

                return _pageLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x08, sizeof(ushort));

                _pageLength = value;
            }
        }


        private ushort _keyCount;
        /// <summary>
        ///     Number of Keys defined within the Btrieve File
        /// </summary>
        public ushort KeyCount
        {
            get
            {
                if (_fcr?.Length > 0)
                    return BitConverter.ToUInt16(_fcr, 0x14);

                return _keyCount;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x14, sizeof(ushort));

                _keyCount = value;
            }
        }

        public ushort DupOffset
        {
            get => BitConverter.ToUInt16(_fcr, 0x72);
        }

        public byte NumDupes
        {
            get => _fcr[0x74];
        }

        /// <summary>
        ///     The ACS table name used by the database. null if there is none
        /// </summary>
        /// <value></value>
        public string ACSName { get; set; }

        /// <summary>
        ///     The ACS table for the database. null if there is none
        /// </summary>
        public byte[] ACS { get; set; }

        /// <summary>
        ///     Raw contents of Btrieve File
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        ///     Btrieve Records
        /// </summary>
        public List<BtrieveRecord> Records { get; set; }

        /// <summary>
        ///     Btrieve Keys
        /// </summary>
        public Dictionary<ushort, BtrieveKey> Keys { get; set; }

        /// <summary>
        ///     Log Key is an internal value used by the Btrieve engine to track unique
        ///     records -- it adds 8 bytes to the end of the record that's not accounted for
        ///     in the RecordLength definition (but it is accounted for in PhysicalRecordLength).
        ///
        ///     <para/>This data is for completion purposes and not currently used.
        /// </summary>
        public bool LogKeyPresent { get; set; }

        /// <summary>
        ///     Set of absolute file position record offsets that are marked as deleted, and
        ///     therefore not loaded during initial load.
        /// </summary>
        public HashSet<uint> DeletedRecordOffsets { get; set; }

        private byte[] _fcr;

        public BtrieveFile()
        {
            Records = new List<BtrieveRecord>();
            Keys = new Dictionary<ushort, BtrieveKey>();
            DeletedRecordOffsets = new HashSet<uint>();
        }

        /// <summary>
        ///     Loads a Btrieve .DAT File
        /// </summary>
        public void LoadFile(ILogger logger, string path, string fileName, bool allowCorruptedRecords = false)
        {
            //Sanity Check if we're missing .DAT files and there are available .VIR files that can be used
            var virginFileName = fileName.ToUpper().Replace(".DAT", ".VIR");
            if (!File.Exists(Path.Combine(path, fileName)) && File.Exists(Path.Combine(path, virginFileName)))
            {
                File.Copy(Path.Combine(path, virginFileName), Path.Combine(path, fileName));
                logger?.LogWarning($"Created {fileName} by copying {virginFileName} for first use");
            }

            //If we're missing a DAT file, just bail. Because we don't know the file definition, we can't just create a "blank" one.
            if (!File.Exists(Path.Combine(path, fileName)))
            {
                logger?.LogError($"Unable to locate existing btrieve file {fileName}");
                throw new FileNotFoundException($"Unable to locate existing btrieve file {fileName}");
            }

            LoadFile(logger, Path.Combine(path, fileName));
        }

        public void LoadFile(ILogger logger, string fullPath, bool allowCorruptedRecords = false)
        {
            var fileName = Path.GetFileName(fullPath);
            var fileData = File.ReadAllBytes(fullPath);

            FileName = fullPath;
            Data = fileData;

            var (valid, v6, errorMessage) = ValidateDatabase(logger);
            if (!valid)
                throw new ArgumentException($"Failed to load database {FileName}: {errorMessage}");

#if DEBUG
            logger?.LogInformation($"Opened {fileName} and read {Data.Length} bytes");
#endif
            if (v6)
            {
                LoadPAT(logger);
            }
            else
            {
                DeletedRecordOffsets = GetRecordPointerList(GetRecordPointer(0x10));

                LoadACS(logger, v6, -1);
            }


            LoadBtrieveKeyDefinitions(logger, v6);

            //Only load records if there are any present
            if (RecordCount > 0)
                LoadBtrieveRecords(logger, v6, allowCorruptedRecords);
        }

        /// <summary>
        ///     Validates the Btrieve database being loaded
        /// </summary>
        /// <returns>True if valid. If false, the string is the error message.</returns>
        private (bool isValid, bool v6, string errorMessage) ValidateDatabase(ILogger logger)
        {
            if (Data.Length < 2)
                return (false, false, $"Btrieve File Is Empty/Invalid Length ({Data.Length} bytes)");
            bool v6 = (Data[0] == 'F' && Data[1] == 'C' && Data[2] == 0 && Data[3] == 0);
            if (Data[0] != 0 && Data[1] != 0 && Data[2] != 0 && Data[3] != 0)
                return (false, false, $"Doesn't appear to be a v5 Btrieve database {FileName}");

            logger?.LogInformation(v6 ? "v6" : "v5");

            if (PageLength < 512 || (PageLength & 0x1FF) != 0)
                return (false, v6, $"Invalid PageLength, must be multiple of 512 {FileName}");

            _fcr = new byte[PageLength];

            if (v6)
            {
                // check the usage count to find the active FCR
                var usageCount1 = Data[4] | Data[5] << 8 | Data[6] << 16 | Data[7] << 24;
                var usageCount2 = Data[PageLength + 4] | Data[PageLength + 5] << 8 | Data[PageLength + 6] << 16 | Data[PageLength + 7] << 24;
                // get the FCR
                if (usageCount1 > usageCount2)
                    Data.AsSpan().Slice(0, PageLength).CopyTo(_fcr.AsSpan());
                else
                    Data.AsSpan().Slice(PageLength, PageLength).CopyTo(_fcr.AsSpan());
            }
            else
            {
                Data.AsSpan().Slice(0, PageLength).CopyTo(_fcr.AsSpan());

                var versionCode = Data[6] << 16 | Data[7];
                switch (versionCode)
                {
                    case 3:
                    case 4:
                    case 5:
                        break;
                    default:
                        return (false, v6, $"Invalid version code [{versionCode}] in v5 Btrieve database {FileName}");
                }

                var needsRecovery = (_fcr[0x22] == 0xFF && _fcr[0x23] == 0xFF);
                if (needsRecovery)
                    return (false, v6, $"Cannot import Btrieve database {FileName} since it's marked inconsistent and needs recovery.");
            }

            var accelFlags = BitConverter.ToUInt16(_fcr.AsSpan().Slice(0xA, 2));
            if (accelFlags != 0)
                return (false, v6, $"Valid accel flags, expected 0, got {accelFlags}! {FileName}");

            var usrflgs = BitConverter.ToUInt16(_fcr.AsSpan().Slice(0x106, 2));
            if ((usrflgs & 0x8) != 0)
                return (false, v6, $"Data is compressed, cannot handle {FileName}");

            VariableLengthRecords = ((usrflgs & 0x1) != 0);
            VariableLengthTruncation = ((usrflgs & 0x2) != 0);
            var recordsContainVariableLength = (_fcr[0x38] == 0xFF);

            if (VariableLengthRecords ^ recordsContainVariableLength)
                return (false, v6, "Mismatched variable length fields");

            return (true, v6, "");
        }

        /// <summary>
        ///     Gets a record pointer offset at <paramref name="first"/> and then continues to walk
        ///     the chain of pointers until the end, returning all the offsets.
        /// </summary>
        /// <param name="first">Record pointer offset to start scanning from.</param>
        HashSet<uint> GetRecordPointerList(uint first)
        {
            var ret = new HashSet<uint>();
            while (first != 0xFFFFFFFF)
            {
                ret.Add(first);

                first = GetRecordPointer(first);
            }

            return ret;
        }

        /// <summary>
        ///     Returns the record pointer located at absolute file offset <paramref name="offset"/>.
        /// </summary>
        private uint GetRecordPointer(uint offset) =>
            GetRecordPointer(Data.AsSpan().Slice((int)offset, 4));

        /// <summary>
        ///     Returns the record pointer located within the span starting at offset 0
        /// </summary>
        private uint GetRecordPointer(ReadOnlySpan<byte> data)
        {
            // 2 byte high word -> 2 byte low word
            return (uint)BitConverter.ToUInt16(data.Slice(0, 2)) << 16 | (uint)BitConverter.ToUInt16(data.Slice(2, 2));
        }

        /// <summary>
        ///     Loads Btrieve Key Definitions from the Btrieve DAT File Header
        /// </summary>
        private void LoadBtrieveKeyDefinitions(ILogger logger, bool v6)
        {
            const ushort keyDefinitionLength = 0x1E;

            LogKeyPresent = (_fcr[0x10C] == 1);

            int[] keyOffsets = new int[KeyCount];
            if (v6)
            {
                if (_fcr[0x76] != KeyCount)
                    throw new ArgumentException($"Key number in KAT mismatches earlier key count: {FileName}");

                var katOffset = _fcr[0x78] | _fcr[0x79] << 8;

                for (int i = 0; i < KeyCount; ++i, katOffset += 2)
                    keyOffsets[i] = Data[katOffset] | Data[katOffset + 1] << 8;
            }
            else
            {
                const int keyDefinitionBase = 0x110;
                for (int i = 0; i < KeyCount; ++i)
                    keyOffsets[i] = keyDefinitionBase + (i * keyDefinitionLength);
            }

            var totalKeys = KeyCount;
            var currentKeyNumber = (ushort)0;
            var keyOffset = keyOffsets[currentKeyNumber];
            while (currentKeyNumber < totalKeys)
            {
                var data = Data.AsSpan().Slice(keyOffset, keyDefinitionLength).ToArray();

                EnumKeyDataType dataType;
                var attributes = (EnumKeyAttributeMask) BitConverter.ToUInt16(data, 0x8);
                if (attributes.HasFlag(EnumKeyAttributeMask.UseExtendedDataType))
                    dataType = (EnumKeyDataType) data[0x1C];
                else
                    dataType = attributes.HasFlag(EnumKeyAttributeMask.OldStyleBinary) ? EnumKeyDataType.OldBinary : EnumKeyDataType.OldAscii;

                var keyDefinition = new BtrieveKeyDefinition {
                    Number = currentKeyNumber,
                    Attributes = attributes,
                    DataType = dataType,
                    Offset = BitConverter.ToUInt16(data, 0x14),
                    Length = BitConverter.ToUInt16(data, 0x16),
                    Segment = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey),
                    SegmentOf = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey) ? currentKeyNumber : (ushort)0,
                    NullValue = data[0x1D],
                  };

                // TODO(paladine): support multiple ACS
                if (keyDefinition.RequiresACS)
                {
                    int acs = data[0x19] << 16 | data[0x1A] | data[0x1B] << 8;

                    if (ACS == null)
                        throw new ArgumentException($"Key {keyDefinition.Number} requires ACS, but none was read. This database is likely corrupt: {FileName}");

                    keyDefinition.ACS = ACS;
                }

                //If it's a segmented key, don't increment so the next key gets added to the same ordinal as an additional segment
                if (!keyDefinition.Segment)
                {
                    ++currentKeyNumber;
                    if (v6 && currentKeyNumber < totalKeys)
                        keyOffset = keyOffsets[currentKeyNumber];
                    else
                        keyOffset += keyDefinitionLength;
                }
                else
                {
                    keyOffset += keyDefinitionLength;
                }

#if DEBUG
                logger?.LogInformation("----------------");
                logger?.LogInformation("Loaded Key Definition:");
                logger?.LogInformation("----------------");
                logger?.LogInformation($"Number: {keyDefinition.Number}");
                logger?.LogInformation($"Data Type: {keyDefinition.DataType}");
                logger?.LogInformation($"Attributes: {keyDefinition.Attributes}");
                logger?.LogInformation($"Offset: {keyDefinition.Offset}");
                logger?.LogInformation($"Length: {keyDefinition.Length}");
                logger?.LogInformation($"Segment: {keyDefinition.Segment}");
                logger?.LogInformation($"SegmentOf: {keyDefinition.SegmentOf}");
                logger?.LogInformation("----------------");
#endif
                if (!Keys.TryGetValue(keyDefinition.Number, out var key))
                {
                    key = new BtrieveKey(keyDefinition);
                    Keys.Add(keyDefinition.Number, key);
                }
                else
                {
                    key.Segments.Add(keyDefinition);
                }
            }

            // update segment indices
            foreach (var key in Keys)
            {
                var i = 0;
                foreach (var segment in key.Value.Segments)
                {
                    segment.SegmentIndex = i++;
                }
            }
        }

        private readonly byte[] ACS_PAGE_HEADER = { 0, 0, 1, 0, 0, 0, 0xAC };

        private void LoadPAT(ILogger logger)
        {
            Span<Byte> pat1 = Data.AsSpan().Slice(PageLength * 2, PageLength);
            Span<Byte> pat2 = Data.AsSpan().Slice(PageLength * 3, PageLength);

            if (pat1[0] != 'P' || pat1[1] != 'P')
                throw new ArgumentException($"PAT1 table is invalid: {FileName}");
            if (pat2[0] != 'P' || pat2[1] != 'P')
                throw new ArgumentException($"PAT2 table is invalid: {FileName}");

            // check out the usage count to find active pat1/2
            var usageCount1 = BitConverter.ToUInt16(pat1.Slice(4));
            var usageCount2 = BitConverter.ToUInt16(pat2.Slice(4));
            // scan page type code to find ACS/Index/etc pages
            Span<Byte> activePat = (usageCount1 > usageCount2) ? pat1 : pat2;
            var sequenceNumber = activePat[2] << 8 | activePat[3];

            // enumerate all pages
            for (int i = 8; i < PageLength; i += 4)
            {
                byte type = activePat[i + 1];
                int pageNumber = activePat[i] << 16 | activePat[i + 2] | activePat[i + 3] << 8;
                // codes are 'A' for ACS, D for fixed-length data pages, E for extra pages and V for variable length pages
                // index have high bit set
                if ((type & 0x80) != 0)
                    continue;

                if (type == 'A')
                    LoadACS(logger, true, pageNumber);

                if (type != 0 && type != 'A' && type != 'D' && type != 'E' && type != 'V')
                    throw new ArgumentException($"Bad PAT entry: {FileName}");
            }
        }

        private bool LoadACS(ILogger logger, bool v6, int pageNumber)
        {
            if (!v6)
            {
                // ACS page immediately follows FCR (the first)
                pageNumber = 1;
            }
            var data = Data.AsSpan().Slice(pageNumber * PageLength, PageLength);

            if (v6)
            {
                if (data[1] != 'A' && data[6] != 0xAC)
                    throw new ArgumentException($"Bad v6 ACS header: {FileName}");
            }
            else
            {
                var pageHeader = data.Slice(0, ACS_PAGE_HEADER.Length);
                if (!pageHeader.SequenceEqual(ACS_PAGE_HEADER))
                    return false;
            }

            if (ACS != null)
                throw new ArgumentException($"Database has multiple ACS - NYI: {FileName}");

            ACSName = Encoding.ASCII.GetString(data.Slice(7, 9)).TrimEnd((char)0).TrimEnd(' ');
            ACS = data.Slice(0xF, 256).ToArray();
            return true;
        }

        /// <summary>
        ///     Loads Btrieve Records from Data Pages
        /// </summary>
        private void LoadBtrieveRecords(ILogger logger, bool v6, bool allowCorruptedRecords)
        {
            var recordsLoaded = 0;
            var recordsInPage = ((PageLength - 6) / PhysicalRecordLength);
            uint dataOffset = v6 ? 2u : 0u;

            //Starting at 1, since the first page is the header
            for (var i = 1; i <= PageCount; i++)
            {
                var pageOffset = (uint)(PageLength * i);

                //Verify Data Page, high bit set on byte 5 (usage count)
                if ((Data[pageOffset + 0x5] & 0x80) == 0)
                    continue;

                //Page data starts 6 bytes in
                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == RecordCount)
                        goto finished_loaded;

                    var recordOffset = (uint)pageOffset + (uint)(PhysicalRecordLength * j);
                    // Marked for deletion? Skip
                    if (DeletedRecordOffsets.Contains(recordOffset))
                        continue;

                    var record = Data.AsSpan().Slice((int)recordOffset, PhysicalRecordLength);
                    if (IsUnusedRecord(record, v6))
                        break;

                    recordOffset += dataOffset;
                    var recordArray = new byte[RecordLength];
                    Array.Copy(Data, recordOffset, recordArray, 0, RecordLength);

                    try
                    {
                        if (VariableLengthRecords)
                        {
                            using var stream = new MemoryStream();
                            stream.Write(recordArray);

                            Records.Add(new BtrieveRecord(recordOffset, GetVariableLengthData(recordOffset, stream, v6)));
                        }
                        else
                            Records.Add(new BtrieveRecord(recordOffset, recordArray));
                    }
                    catch (ArgumentException ex)
                    {
                        if (!allowCorruptedRecords)
                            throw ex;

                        logger?.LogWarning(ex, $"Detected a corrupted data record - skipping");
                    }
                    recordsLoaded++;
                }
            }

finished_loaded:
            if (recordsLoaded != RecordCount)
            {
                logger?.LogWarning($"Database {FileName} contains {RecordCount} records but only read {recordsLoaded}!");
            }
#if DEBUG
            logger?.LogInformation($"Loaded {recordsLoaded} records from {FileName}. Resetting cursor to 0");
#endif
        }

        /// <summary>
        ///     Returns true if the fixed record appears to be unused and should be skipped.
        ///
        ///     <para/>Fixed length records are contiguous in the page, and unused records are all zero except
        ///     for the first 4 bytes, which is a record pointer to the next free page.
        /// </summary>
        private bool IsUnusedRecord(ReadOnlySpan<byte> fixedRecordData, bool v6)
        {
            if (v6)
            {
                // first two bytes are usage count, which will be non-zero if used
                var usageCount = fixedRecordData[0] << 8 | fixedRecordData[1];
                return usageCount == 0;
            }

            if (BitConverter.ToUInt32(fixedRecordData.Slice(4)) == 0)
            {
                // additional validation, to ensure the record pointer is valid
                var offset = GetRecordPointer(fixedRecordData);
                if (offset < Data.Length)
                    return true;
            }

            return false;
        }

        private int LogicalPageToPhysicalOffset(uint logicalPage, bool v6)
        {
            if (!v6)
                return (int) logicalPage * PageLength;

            // go through the PAT
            uint ret = 2;
            uint pagesPerPAT = (PageLength / 4u) - 2u;

            // not on the current page? if so page up
            while (logicalPage > pagesPerPAT)
            {
                logicalPage -= pagesPerPAT;
                ret += (PageLength / 4u);
            }

            uint pat1 = ret * PageLength;
            uint pat2 = pat1 + PageLength;
            // pick the one with best usage count
            if (Data[pat1] != 'P' && Data[pat1 + 1] != 'P' && Data[pat2] != 'P' && Data[pat2 + 1] != 'P')
                throw new ArgumentException("Not a pat");

            var spanPat1 = Data.AsSpan().Slice((int) pat1 + 4, 4);
            var spanPat2 = Data.AsSpan().Slice((int) pat2 + 4, 4);
            var usageCount1 = BitConverter.ToUInt32(spanPat1);
            var usageCount2 = BitConverter.ToUInt32(spanPat2);
            var activePatOffset = (usageCount1 > usageCount2) ? pat1 : pat2;

            var spanPatTable = Data.AsSpan().Slice((int) activePatOffset + 8);
            var patTableEntry = ret + 8 + (logicalPage * 4);
            // now we have our pointer at ret
            var page = Data.AsSpan().Slice((int) patTableEntry, 4);
            ret = ((uint) page[0] << 16) | ((uint) page[3] << 8) | ((uint) page[2]);
            var typeCode = page[1];
            if (typeCode != 'V')
                throw new ArgumentException("Variable data page reference isn't a variable data page");
            if (ret * PageLength >= Data.Length)
                throw new ArgumentException("Variable page reference overflows max pages");

            return (int) ret * PageLength;
        }

        /// <summary>
        ///     Gets the complete variable length data from the specified <paramref name="recordOffset"/>,
        ///     walking through all data pages and returning the concatenated data.
        /// </summary>
        /// <param name="first">Fixed record pointer offset of the record from a data page</param>
        /// <param name="stream">MemoryStream containing the fixed record data already read.</param>
        private byte[] GetVariableLengthData(uint recordOffset, MemoryStream stream, bool v6) {
            var variableData = Data.AsSpan().Slice((int)recordOffset + RecordLength, PhysicalRecordLength - RecordLength);
            var vrecPage = GetPageFromVariableLengthRecordPointer(variableData);
            var vrecFragment = variableData[3];

            while (true) {
                // invalid page? abort and return what we have
                if (vrecPage == 0xFFFFFF && vrecFragment == 0xFF)
                    return stream.ToArray();

                // jump to that page
                var vpage = Data.AsSpan().Slice(LogicalPageToPhysicalOffset(vrecPage, v6), PageLength);
                var numFragmentsInPage = BitConverter.ToUInt16(vpage.Slice(0xA, 2));
                // grab the fragment pointer
                var (offset, length, nextPointerExists) = GetFragment(vpage, vrecFragment, numFragmentsInPage);
                // now finally read the data!
                variableData = vpage.Slice((int)offset, (int)length);
                if (!nextPointerExists)
                {
                    // read all the data and reached the end!
                    stream.Write(variableData);
                    return stream.ToArray();
                }

                // keep going through more pages!
                vrecPage = GetPageFromVariableLengthRecordPointer(variableData);
                vrecFragment = variableData[3];

                stream.Write(variableData.Slice(4));
            }
        }

        /// <summary>
        ///     Returns data about the specified fragment.
        /// </summary>
        /// <param name="page">The entire page's data, will be PageLength in size</param>
        /// <param name="fragment">The fragment to lookup, 0 based</param>
        /// <param name="numFragments">The maximum number of fragments in the page.</param>
        /// <returns>Three items: 1) the offset within the page where the fragment data resides, 2)
        ///     the length of data contained in the fragment, and 3) a boolean indicating the fragment
        ///     has a "next pointer", meaning the fragment data is prefixed with 4 bytes of another
        ///     data page to continue reading from.
        /// </returns>
        private (uint, uint, bool) GetFragment(ReadOnlySpan<byte> page, uint fragment, uint numFragments)
        {
            var offsetPointer = (uint)PageLength - 2u * (fragment + 1u);
            var (offset, nextPointerExists) = GetPageOffsetFromFragmentArray(page.Slice((int)offsetPointer, 2));

            // check offset for corruption now?
            if (offset < 0xC)
                return (0, 0, false);

            // to compute length, keep going until I read the next valid fragment and get its offset
            // then we subtract the two offsets to compute length
            var nextFragmentOffset = offsetPointer;
            var nextOffset = 0xFFFFFFFFu;
            for (var i = fragment + 1; i <= numFragments; ++i)
            {
                nextFragmentOffset -= 2; // fragment array is at end of page and grows downward
                (nextOffset, _) = GetPageOffsetFromFragmentArray(page.Slice((int)nextFragmentOffset, 2));
                if (nextOffset == 0xFFFF)
                    continue;
                // valid offset, break now
                break;
            }

            // some sanity checks
            if (nextOffset == 0xFFFFFFFFu)
                throw new ArgumentException($"Can't find next fragment offset {fragment} numFragments:{numFragments} {FileName}");

            var length = nextOffset - offset;
            // final sanity check
            if (offset < 0xC || (offset + length) > (PageLength - 2 * (numFragments + 1)))
                throw new ArgumentException($"Variable data overflows page {fragment} numFragments:{numFragments} {FileName}");

            return (offset, length, nextPointerExists);
        }

        /// <summary>
        ///     Reads the page offset from the fragment array
        /// </summary>
        /// <param name="arrayEntry">Fragment array entry, size of 2 bytes</param>
        /// <returns>The offset and a boolean indicating the offset contains a next pointer</returns>
        private static (uint, bool) GetPageOffsetFromFragmentArray(ReadOnlySpan<byte> arrayEntry)
        {
            if (BitConverter.ToUInt16(arrayEntry) == 0xFFFF)
                return (0xFFFFu, false);

            var offset = (uint)arrayEntry[0] | ((uint)arrayEntry[1] & 0x7F) << 8;
            var nextPointerExists = (arrayEntry[1] & 0x80) != 0;
            return (offset, nextPointerExists);
        }

        /// <summary>
        ///     Reads the variable length record pointer, which is contained in the first 4 bytes
        ///     of the footer after each fixed length record, and returns the page it points to.
        /// </summary>
        /// <param name="data">footer of the fixed record, at least 4 bytes in length</param>
        /// <returns>The page that this variable length record pointer points to</returns>
        private static uint GetPageFromVariableLengthRecordPointer(ReadOnlySpan<byte> data) {
            // high low mid, yep it's stupid
            return (uint)data[0] << 16 | (uint)data[1] | (uint)data[2] << 8;
        }
    }
}
