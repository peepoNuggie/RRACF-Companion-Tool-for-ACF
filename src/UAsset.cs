using System;
using System.Collections.Generic;
using System.Text;

namespace Rracf
{
    /// <summary>Thrown when the asset is not shaped the way we expect. Always carries a plain-English reason.</summary>
    internal class AssetFormatException : Exception
    {
        public AssetFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Reader/patcher for a legacy (.uasset) Unreal package header.
    ///
    /// Renaming Camouf_&lt;a&gt;_asset to Camouf_&lt;b&gt;_asset means rewriting the name in three places:
    /// the package name in the summary and two entries in the name table. When both IDs have the same
    /// number of digits that is a straight overwrite. When they do not - a single-digit camo such as
    /// Tiger_Stripe (ID 1) moving to slot 61 - the file grows, and every absolute offset in the header
    /// has to move with it. That is what the offset table below is for.
    /// </summary>
    internal class UAsset
    {
        private const uint PackageFileTag = 0x9E2A83C1;

        private byte[] _data;

        /// <summary>An absolute file offset stored in the summary, which must shift when the header resizes.</summary>
        private class OffsetField
        {
            public int Pos;
            public bool Is64;
            public OffsetField(int pos, bool is64) { Pos = pos; Is64 = is64; }
        }

        private List<OffsetField> _offsetFields;
        private int _packageNameLenPos, _packageNameStrPos, _packageNameRawLength;
        private int _summaryEnd;

        public int NameCount { get; private set; }
        public int NameOffset { get; private set; }
        public int NamesReferencedOffset { get; private set; }
        public int ExportCount { get; private set; }
        public int ExportOffset { get; private set; }
        public int DependsOffset { get; private set; }
        public int TotalHeaderSize { get; private set; }

        public byte[] Data { get { return _data; } }

        public UAsset(byte[] data)
        {
            _data = data;
            ParseSummary(true);
        }

        // ---- primitive reads/writes -------------------------------------------------

        private int ReadI32(int off)
        {
            if (off < 0 || off + 4 > _data.Length)
                throw new AssetFormatException("The asset header is truncated - it is not a valid .uasset.");
            return BitConverter.ToInt32(_data, off);
        }

        private long ReadI64(int off)
        {
            if (off < 0 || off + 8 > _data.Length)
                throw new AssetFormatException("The asset header is truncated - it is not a valid .uasset.");
            return BitConverter.ToInt64(_data, off);
        }

        private void WriteI32(int off, int v)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, _data, off, 4);
        }

        private void WriteI64(int off, long v)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, _data, off, 8);
        }

        private void WriteU16(int off, ushort v)
        {
            _data[off] = (byte)(v & 0xFF);
            _data[off + 1] = (byte)(v >> 8);
        }

        private static int ByteLengthOfString(int rawLength)
        {
            return rawLength >= 0 ? rawLength : (-rawLength) * 2;
        }

        /// <summary>Reads an FString and advances past it.</summary>
        private void SkipString(ref int pos)
        {
            int raw = ReadI32(pos);
            pos += 4 + ByteLengthOfString(raw);
        }

        // ---- summary ----------------------------------------------------------------

        /// <summary>
        /// Walks the package summary field by field, recording where every absolute offset lives.
        ///
        /// The walk is self-checking: the summary is immediately followed by the name table, so if the
        /// cursor does not land exactly on NameOffset then the layout is not what we think it is and we
        /// must not touch the file. That check is what makes the offset table below trustworthy.
        /// </summary>
        private void ParseSummary(bool verifyLanding)
        {
            if (_data.Length < 64 || (uint)ReadI32(0) != PackageFileTag)
                throw new AssetFormatException("This file does not start with the Unreal package tag, so it is not a .uasset.");

            _offsetFields = new List<OffsetField>();

            int legacyFileVersion = ReadI32(4);
            int pos = 8;
            pos += 4;                                   // LegacyUE3Version
            pos += 4;                                   // FileVersionUE4
            if (legacyFileVersion <= -8) pos += 4;      // FileVersionUE5
            pos += 4;                                   // FileVersionLicenseeUE

            int customVersionCount = ReadI32(pos);
            pos += 4;
            if (customVersionCount != 0)
                throw new AssetFormatException(
                    "This asset carries " + customVersionCount + " custom version entries. " +
                    "The camouflage assets this tool edits always have zero, so it refuses to guess at the layout.");

            _offsetFields.Add(new OffsetField(pos, false));  // TotalHeaderSize
            TotalHeaderSize = ReadI32(pos);
            pos += 4;

            _packageNameLenPos = pos;
            _packageNameRawLength = ReadI32(pos);
            pos += 4;
            _packageNameStrPos = pos;
            pos += ByteLengthOfString(_packageNameRawLength);

            pos += 4;                                   // PackageFlags
            NameCount = ReadI32(pos); pos += 4;
            NameOffset = ReadI32(pos);
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;

            pos += 4;                                                                   // SoftObjectPathsCount
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // SoftObjectPathsOffset
            pos += 4;                                                                   // GatherableTextDataCount
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // GatherableTextDataOffset
            ExportCount = ReadI32(pos); pos += 4;
            ExportOffset = ReadI32(pos);
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;
            pos += 4;                                                                   // ImportCount
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // ImportOffset
            DependsOffset = ReadI32(pos);
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;
            pos += 4;                                                                   // SoftPackageReferencesCount
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // SoftPackageReferencesOffset
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // SearchableNamesOffset
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // ThumbnailTableOffset

            pos += 16;                                                                  // Guid

            int generationCount = ReadI32(pos); pos += 4;
            pos += generationCount * 8;                                                 // ExportCount + NameCount each

            pos += 10; SkipString(ref pos);                                             // SavedByEngineVersion
            pos += 10; SkipString(ref pos);                                             // CompatibleWithEngineVersion

            pos += 4;                                                                   // CompressionFlags
            int compressedChunkCount = ReadI32(pos); pos += 4;
            if (compressedChunkCount != 0)
                throw new AssetFormatException("This asset uses package-level compression, which this tool does not handle.");
            pos += 4;                                                                   // PackageSource
            int additionalPackages = ReadI32(pos); pos += 4;
            pos += additionalPackages * 4;

            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // AssetRegistryDataOffset
            _offsetFields.Add(new OffsetField(pos, true)); pos += 8;                    // BulkDataStartOffset
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // WorldTileInfoDataOffset

            int chunkIdCount = ReadI32(pos); pos += 4;
            pos += chunkIdCount * 4;

            pos += 4;                                                                   // PreloadDependencyCount
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // PreloadDependencyOffset

            NamesReferencedOffset = pos; pos += 4;
            _offsetFields.Add(new OffsetField(pos, true)); pos += 8;                    // PayloadTocOffset
            _offsetFields.Add(new OffsetField(pos, false)); pos += 4;                   // DataResourceOffset

            _summaryEnd = pos;

            if (verifyLanding && pos != NameOffset)
                throw new AssetFormatException(
                    "The package header does not match the layout this tool understands (the name table was " +
                    "expected at byte " + pos + " but the header says " + NameOffset + "). Refusing to patch it.");

            if (NameCount <= 0 || NameOffset <= 0 || NameOffset > _data.Length)
                throw new AssetFormatException("The asset's name table location looks invalid (count=" +
                    NameCount + ", offset=" + NameOffset + ").");
        }

        public string GetPackageName()
        {
            int byteLen = ByteLengthOfString(_packageNameRawLength);
            if (byteLen == 0) return "";
            if (_packageNameRawLength >= 0)
                return Encoding.ASCII.GetString(_data, _packageNameStrPos, byteLen - 1);
            return Encoding.Unicode.GetString(_data, _packageNameStrPos, byteLen - 2);
        }

        // ---- name table -------------------------------------------------------------

        private struct NameEntry
        {
            public int LengthPos;      // the int32 length prefix
            public int StringOffset;   // first character byte
            public int RawLength;
            public string Value;       // without the null terminator
            public int HashOffset;
            public bool IsAnsi;
        }

        private List<NameEntry> ReadNameMap()
        {
            var entries = new List<NameEntry>(NameCount);
            int pos = NameOffset;
            for (int i = 0; i < NameCount; i++)
            {
                var e = new NameEntry();
                e.LengthPos = pos;
                e.RawLength = ReadI32(pos);
                pos += 4;
                int byteLen = ByteLengthOfString(e.RawLength);
                if (byteLen < 0 || pos + byteLen + 4 > _data.Length)
                    throw new AssetFormatException("Name table entry " + i + " runs past the end of the file.");
                e.StringOffset = pos;
                e.IsAnsi = e.RawLength >= 0;
                if (byteLen == 0) e.Value = "";
                else if (e.IsAnsi) e.Value = Encoding.ASCII.GetString(_data, pos, byteLen - 1);
                else e.Value = Encoding.Unicode.GetString(_data, pos, byteLen - 2);
                pos += byteLen;
                e.HashOffset = pos;
                pos += 4;
                entries.Add(e);
            }
            return entries;
        }

        // ---- the rename --------------------------------------------------------------

        /// <summary>One string to rewrite, described in original-file coordinates.</summary>
        private class Edit
        {
            public int LengthPos;
            public int StringPos;
            public int OldByteLength;   // characters plus terminator, as stored
            public string NewValue;
        }

        /// <summary>
        /// Replaces every occurrence of <paramref name="oldName"/> with <paramref name="newName"/> in the
        /// name table and in the summary package name, refreshes the stored hash of each entry it touches,
        /// fixes up every header offset if the file changed size, and sets
        /// NamesReferencedFromExportDataCount to the full name count.
        /// </summary>
        public PatchReport Replace(string oldName, string newName)
        {
            var report = new PatchReport();
            var edits = new List<Edit>();

            foreach (NameEntry e in ReadNameMap())
            {
                if (e.Value.IndexOf(oldName, StringComparison.Ordinal) < 0) continue;
                if (!e.IsAnsi)
                    throw new AssetFormatException(
                        "Name table entry \"" + e.Value + "\" is stored as UTF-16, which this tool cannot rewrite.");

                var edit = new Edit();
                edit.LengthPos = e.LengthPos;
                edit.StringPos = e.StringOffset;
                edit.OldByteLength = ByteLengthOfString(e.RawLength);
                edit.NewValue = e.Value.Replace(oldName, newName);
                edits.Add(edit);
                report.RenamedNames.Add(edit.NewValue);
            }

            string packageName = GetPackageName();
            if (packageName.IndexOf(oldName, StringComparison.Ordinal) >= 0)
            {
                if (_packageNameRawLength < 0)
                    throw new AssetFormatException("The summary package name is stored as UTF-16, which this tool cannot rewrite.");
                var edit = new Edit();
                edit.LengthPos = _packageNameLenPos;
                edit.StringPos = _packageNameStrPos;
                edit.OldByteLength = ByteLengthOfString(_packageNameRawLength);
                edit.NewValue = packageName.Replace(oldName, newName);
                edits.Add(edit);
                report.PackageNameUpdated = true;
            }

            if (edits.Count == 0)
                throw new AssetFormatException("The name \"" + oldName + "\" does not appear in this asset.");

            edits.Sort(delegate(Edit a, Edit b) { return a.StringPos.CompareTo(b.StringPos); });

            int oldNameOffset = NameOffset;
            ApplyEdits(edits, report);

            // Re-read the summary of the rewritten file. Offsets still hold their original values at
            // this point, so the landing check is skipped until they have been fixed up.
            ParseSummary(false);
            RemapOffsets(edits);
            ParseSummary(true);
            VerifyExportOffsets();

            // NamesReferencedFromExportDataCount describes the export data, which a rename does not
            // touch, so it is left exactly as found. Round-tripping a known-good mod pak through
            // to-legacy and back reproduces it byte for byte only when this value is preserved.
            report.NamesReferencedBefore = ReadI32(NamesReferencedOffset);
            report.NamesReferencedAfter = report.NamesReferencedBefore;

            // A renamed entry has to carry a real hash; the vanilla assets store zero for every name.
            foreach (NameEntry e in ReadNameMap())
            {
                if (e.Value.IndexOf(newName, StringComparison.Ordinal) < 0) continue;
                WriteU16(e.HashOffset, Crc.NonCasePreservingHash(e.Value));
                WriteU16(e.HashOffset + 2, Crc.CasePreservingHash(e.Value));
                report.NamesRehashed++;
            }

            report.SizeDelta = _data.Length - report.OriginalSize;
            if (oldNameOffset != NameOffset) report.NameOffsetMoved = true;

            int stray = IndexOfAscii(_data, oldName);
            if (stray >= 0)
                throw new AssetFormatException(
                    "After patching, the text \"" + oldName + "\" still appears at byte offset " + stray +
                    ". Refusing to ship an asset that would override the vanilla camo.");

            return report;
        }

        /// <summary>Rebuilds the file with the new strings spliced in.</summary>
        private void ApplyEdits(List<Edit> edits, PatchReport report)
        {
            report.OriginalSize = _data.Length;

            var output = new List<byte>(_data.Length + 16);
            int cursor = 0;

            foreach (Edit e in edits)
            {
                // everything up to and including this string's length prefix position
                for (int i = cursor; i < e.LengthPos; i++) output.Add(_data[i]);

                byte[] text = Encoding.ASCII.GetBytes(e.NewValue);
                int newByteLength = text.Length + 1;               // plus the null terminator
                output.AddRange(BitConverter.GetBytes(newByteLength));
                output.AddRange(text);
                output.Add(0);

                cursor = e.StringPos + e.OldByteLength;
            }
            for (int i = cursor; i < _data.Length; i++) output.Add(_data[i]);

            _data = output.ToArray();
        }

        /// <summary>
        /// Shifts every absolute offset by however many bytes were inserted before the place it points to.
        /// Zero and -1 mean "not present" and are left alone.
        /// </summary>
        private void RemapOffsets(List<Edit> edits)
        {
            foreach (OffsetField f in _offsetFields)
            {
                long value = f.Is64 ? ReadI64(f.Pos) : ReadI32(f.Pos);
                if (value <= 0) continue;
                long shifted = value + DeltaBefore(edits, value);
                if (f.Is64) WriteI64(f.Pos, shifted); else WriteI32(f.Pos, (int)shifted);
            }

            // Export data lives in the .uexp but its offset is measured from the start of the header,
            // so it moves too.
            if (ExportCount > 0 && ExportOffset > 0 && DependsOffset > ExportOffset)
            {
                int stride = (DependsOffset - ExportOffset) / ExportCount;
                if (stride >= 44 && stride * ExportCount == DependsOffset - ExportOffset)
                {
                    // ExportOffset still holds its ORIGINAL value here, but the export table has
                    // already shifted in the rewritten file - so walk it at its new position, or the
                    // write lands short and corrupts the preceding SerialSize.
                    int tableStart = ExportOffset + DeltaBefore(edits, ExportOffset);
                    for (int i = 0; i < ExportCount; i++)
                    {
                        int serialOffsetPos = tableStart + i * stride + 36;
                        if (serialOffsetPos + 8 > _data.Length) break;
                        long v = ReadI64(serialOffsetPos);
                        if (v > 0) WriteI64(serialOffsetPos, v + DeltaBefore(edits, v));
                    }
                }
            }
        }

        /// <summary>
        /// The first export's data begins immediately after the header, so its SerialOffset must equal
        /// TotalHeaderSize. Getting this wrong does not fail any packing step - the game crashes on load
        /// with "Serial size mismatch" - so it is checked here.
        /// </summary>
        private void VerifyExportOffsets()
        {
            if (ExportCount <= 0 || ExportOffset <= 0 || DependsOffset <= ExportOffset) return;
            int stride = (DependsOffset - ExportOffset) / ExportCount;
            if (stride < 44 || stride * ExportCount != DependsOffset - ExportOffset) return;

            long first = ReadI64(ExportOffset + 36);
            if (first != TotalHeaderSize)
                throw new AssetFormatException(
                    "Internal check failed: the first export starts at byte " + first +
                    " but the header ends at " + TotalHeaderSize +
                    ". The rewritten asset would crash the game on load, so it has not been written.");
        }

        /// <summary>How many bytes were inserted at or before <paramref name="originalOffset"/>.</summary>
        private static int DeltaBefore(List<Edit> edits, long originalOffset)
        {
            int delta = 0;
            foreach (Edit e in edits)
            {
                int insertionPoint = e.StringPos + e.OldByteLength;
                int grew = (Encoding.ASCII.GetByteCount(e.NewValue) + 1) - e.OldByteLength;
                if (insertionPoint <= originalOffset) delta += grew;
            }
            return delta;
        }

        public static int IndexOfAscii(byte[] haystack, string needle)
        {
            byte[] pat = Encoding.ASCII.GetBytes(needle);
            for (int i = 0; i + pat.Length <= haystack.Length; i++)
            {
                bool hit = true;
                for (int j = 0; j < pat.Length; j++)
                {
                    if (haystack[i + j] != pat[j]) { hit = false; break; }
                }
                if (hit) return i;
            }
            return -1;
        }
    }

    internal class PatchReport
    {
        public List<string> RenamedNames = new List<string>();
        public int NamesRehashed;
        public bool PackageNameUpdated;
        public int NamesReferencedBefore;
        public int NamesReferencedAfter;
        public int OriginalSize;
        public int SizeDelta;
        public bool NameOffsetMoved;
    }
}
