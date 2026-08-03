using System;

namespace Rracf
{
    /// <summary>
    /// The two 16-bit hashes Unreal stores after every name-map string.
    ///
    /// Both algorithms were identified empirically against the hand-built
    /// Zero's Jacket reference asset, which contains these known-good pairs:
    ///   "Camouf_63_asset"                            -> nonCase 0xEE79, case 0xAFDD
    ///   "/Game/Maps/AssetCamouflage/Camouf_63_asset" -> nonCase 0xFB2A, case 0x1109
    /// </summary>
    internal static class Crc
    {
        // Standard reflected CRC-32 table (poly 0xEDB88320). Used by FCrc::StrCrc32.
        private static readonly uint[] Reflected = BuildReflected();

        // Unreal's legacy MSB-first table (poly 0x04C11DB7). Used by the
        // *_DEPRECATED hashes, which is why the two functions disagree.
        private static readonly uint[] LegacyMsb = BuildLegacyMsb();

        private static uint[] BuildReflected()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                t[i] = c;
            }
            return t;
        }

        private static uint[] BuildLegacyMsb()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i << 24;
                for (int j = 0; j < 8; j++)
                    c = ((c & 0x80000000u) != 0) ? ((c << 1) ^ 0x04C11DB7u) : (c << 1);
                t[i] = c;
            }
            return t;
        }

        /// <summary>FCrc::StrCrc32 - four table rounds per UTF-16 character.</summary>
        public static ushort CasePreservingHash(string s)
        {
            uint crc = ~0u;
            foreach (char c in s)
            {
                uint ch = c;
                for (int r = 0; r < 4; r++)
                {
                    crc = (crc >> 8) ^ Reflected[(crc ^ ch) & 0xFF];
                    ch >>= 8;
                }
            }
            return (ushort)~crc;
        }

        /// <summary>FCrc::Strihash_DEPRECATED - uppercased, one legacy-table round per character.</summary>
        public static ushort NonCasePreservingHash(string s)
        {
            uint hash = 0;
            foreach (char c in s)
            {
                uint ch = char.ToUpperInvariant(c);
                hash = (hash >> 8) ^ LegacyMsb[(hash ^ ch) & 0xFF];
            }
            return (ushort)hash;
        }
    }
}
