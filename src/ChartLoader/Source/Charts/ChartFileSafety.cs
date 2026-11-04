using System;
using System.Buffers.Binary;
using System.IO;

namespace InFalsusChartLoader
{
    /// <summary>Managed checks before touching the native decoder; no disk cache.</summary>
    internal static class ChartFileSafety
    {
        internal const int MaxBytes = 8 * 1024 * 1024;
        internal const int MaxTimeMs = 3_600_000;

        internal static bool ReadSnapshot(string path, out byte[] bytes, out string reason)
        {
            bytes = null;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 28 || stream.Length > 64*1024*1024)
                { reason = "chart file size is invalid"; return false; }
                var candidate = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < candidate.Length)
                {
                    int read = stream.Read(candidate, offset, candidate.Length-offset);
                    if (read == 0) { reason = "chart file is incomplete"; return false; }
                    offset += read;
                }
                if (!Validate(candidate, out reason)) return false;
                bytes = candidate;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { reason = "chart is missing or being replaced: " + e.GetType().Name; return false; }
        }

        internal static bool Validate(byte[] bytes, out string reason)
        {
            if(Spc2FileSafety.IsSpc2(bytes))return Spc2FileSafety.Validate(bytes,out reason);
            reason = "invalid or incomplete ICP1 chart";
            if (bytes == null || bytes.Length < 28 || bytes.Length > MaxBytes) return false;
            ReadOnlySpan<byte> data = bytes;
            if (BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x31504349 ||
                BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4)) != 1 ||
                BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6)) != 28 ||
                BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8)) != 80 ||
                BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(10)) != 32) return false;
            uint notes = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12));
            uint events = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16));
            if (notes == 0 || notes > 100000 || events > 100000 || 28L + notes*80L + events*32L != bytes.Length)
                return false;
            float bpm = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(20)));
            float beats = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(24)));
            if (!float.IsFinite(bpm) || bpm <= 0 || bpm > 10000 || !float.IsFinite(beats) || beats <= 0 || beats > 128)
                return false;
            for (int i = 0; i < notes; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int at = 28+i*80+48+k*8;
                    int n = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at));
                    int d = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at+4));
                    if (d <= 0 || Math.Abs(n/(double)d) > 2147 || (k >= 2 && (n <= 0 || n/(double)d > 1.5)))
                        return false;
                }
            }
            for (int i = 0, start = 28+(int)notes*80; i < events; i++)
            {
                int at = start+i*32;
                int time = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at+8));
                uint kind = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at+12));
                if (time < 0 || time > MaxTimeMs || kind > 3) return false;
                if (kind <= 2)
                {
                    double value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(data.Slice(at+16)));
                    if (!double.IsFinite(value) || (kind > 0 && value <= 0)) return false;
                }
            }
            reason = null;
            return true;
        }

        internal static bool ValidGeometry(int side, ReadOnlySpan<int> pairs)
        {
            for (int k = 0; k < 4; k++)
            {
                int n = pairs[k*2], d = pairs[k*2+1];
                if (d <= 0 || Math.Abs(n/(double)d) > 2147 || (k >= 2 && (n <= 0 || n/(double)d > 1.5))) return false;
            }
            if (pairs[1] != pairs[5] || pairs[3] != pairs[7]) return false;
            if (side == 4) return true;
            for (int i = 1; i < 8; i += 2) if (pairs[i] != 4) return false;
            if (side == 1)
                return pairs[0] >= 1 && pairs[2] >= 1 && pairs[4] >= 1 && pairs[6] >= 1 &&
                       pairs[0]+(long)pairs[4] <= 5 && pairs[2]+(long)pairs[6] <= 5;
            int lane = side == 2 ? 0 : 5;
            return (side == 2 || side == 3) && pairs[0] == lane && pairs[2] == lane;
        }
    }
}
