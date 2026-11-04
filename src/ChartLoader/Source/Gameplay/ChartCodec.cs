using System;

namespace InFalsusChartLoader
{
    /// <summary>Decodes a chart with the game's own decoder.</summary>
    internal static unsafe class ChartCodec
    {
        /// <summary>The game's chart decoder.</summary>
        private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> _gab;

        /// <summary>The game's empty-chart constructor.</summary>
        private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> _hab;

        /// <summary>The byte stride of one note record.</summary>
        private static int _noteSize;

        /// <summary>The upper bound on a note time, in milliseconds.</summary>
        private const int MaxChartMs = 3_600_000;

        /// <summary>Finds the decoder and the empty-chart constructor.</summary>
        internal static bool Resolve()
        {
            IntPtr p = MethodResolver.ByName("_S", "_Gab");
            if (p == IntPtr.Zero) return false;

            _gab = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>)p;

#if DEBUG
            IntPtr reference = MethodResolver.MethodInfoOf("_S", "_Gab");
            if (reference != IntPtr.Zero)
                Diagnostics.Info($"reference MethodInfo _S._Gab at 0x{reference.ToInt64():X}");
            Diagnostics.Info($"reference _S._Gab words: {JacketFactory.Words(reference, 12)}");
#endif

            IntPtr hab = MethodResolver.ByName("_S", "_hab");
            if (hab == IntPtr.Zero) return false;

            _hab = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)hab;
            return true;
        }

        /// <summary>Fills the tuple with the game's empty chart.</summary>
        internal static bool Empty(IntPtr tuple32)
        {
            if (_hab == null)
            {
                Diagnostics.Error("the game's empty-chart constructor is not in this build; a chart " +
                                  "that fails to decode cannot be served");
                return false;
            }

            _hab(tuple32, IntPtr.Zero);
            return true;
        }

        /// <summary>The range of note planes.</summary>
        private const int SideMin = 1;
        private const int SideMax = 4;

        /// <summary>Decodes a chart into the game's 32-byte tuple.</summary>
        internal static bool TryDecode(byte[] bytes, string seed, IntPtr tuple32, out string reason)
        {
            reason = null;
            if (!ChartFileSafety.Validate(bytes, out reason)) return false;
            bool spc2=Spc2FileSafety.IsSpc2(bytes);
            if(spc2&&!Spc2FileSafety.Supported()){reason="SPC2 needs a supported community shell; official ICP1 decoder was not called";return false;}

            if (_gab == null)
            {
#if DEBUG
                reason = "the chart decoder could not be reached";
#endif
                return false;
            }

            IntPtr name = Il2CppInterop.Runtime.IL2CPP.ManagedStringToIl2Cpp(seed);
            if (name == IntPtr.Zero)
            {
#if DEBUG
                reason = $"the name '{seed}' could not be passed to the decoder";
#endif
                return false;
            }

            byte* span = stackalloc byte[Offsets.Runtime.SpanSize];
            fixed (byte* data = bytes)
            {
                *(IntPtr*)span = (IntPtr)data;
                *(int*)(span + Offsets.Runtime.SpanLength) = bytes.Length;
                _gab(tuple32, (IntPtr)span, name, IntPtr.Zero);
            }

            return Inspect(tuple32, seed, out reason,spc2);
        }

        /// <summary>Checks the decoded notes.</summary>
        private static bool Inspect(IntPtr tuple32, string seed, out string reason,bool spc2=false)
        {
            reason = null;

            IntPtr notes = Memory.Ptr(tuple32 + Offsets.Runtime.ChartTupleNotes);
            if (!Memory.TryList(notes, out IntPtr items, out int count))
            {
#if DEBUG
                reason = "the decoder returned no note list";
#endif
                return false;
            }
            if (count == 0)
            {
#if DEBUG
                reason = "the decoder found no notes (bad container, or encoded under another name)";
#endif
                return false;
            }

            if (!MeasureNoteSize(items, count))
            {
#if DEBUG
                reason = "the note records could not be read this build";
#endif
                return false;
            }

            Span<int> pairs = stackalloc int[8];
            for (int i = 0; i < count; i++)
            {
                IntPtr note = items + i * _noteSize;

                int side = Memory.U8(note + Offsets.Note.Side);
                int type = Memory.U8(note + Offsets.Note.Type);
                int start = Memory.I32(note + Offsets.Note.StartMs);
                int end = Memory.I32(note + Offsets.Note.EndMs);

                if (side < SideMin || side > SideMax)
                {
#if DEBUG
                    reason = $"note {i} is on plane {side} under the name '{seed}'; " +
                             "the file is probably not encoded under that name";
                    Dump(items, count);
#endif
                    return false;
                }
                if (!IsNoteType(type))
                {
#if DEBUG
                    reason = $"note {i} has type {type} under the name '{seed}'; " +
                             "the file is probably not encoded under that name";
                    Dump(items, count);
#endif
                    return false;
                }
                if (start < 0 || end < start || end > MaxChartMs)
                {
#if DEBUG
                    reason = $"note {i} runs {start}..{end}ms under the name '{seed}'; " +
                             "the file is probably not encoded under that name";
                    Dump(items, count);
#endif
                    return false;
                }
                for (int k = 0; k < 4; k++)
                {
                    int offset = _coordinateFields[k];
                    pairs[k*2] = Memory.I32(note+offset);
                    pairs[k*2+1] = Memory.I32(note+offset+4);
                    float value = BitConverter.Int32BitsToSingle(Memory.I32(note+offset+8));
                    if (!float.IsFinite(value)) { reason = "non-finite note coordinate"; return false; }
                }
                if ((!spc2||side!=4)&&!ChartFileSafety.ValidGeometry(side, pairs))
                { reason = "invalid note grid or width"; return false; }
            }

            Diagnostics.Info($"decoded '{seed}': {count} notes, {_noteSize}-byte records");
            return true;
        }

        /// <summary>Logs the first note records when a chart is refused.</summary>
        private static void Dump(IntPtr items, int count)
        {
            int show = count < 4 ? count : 4;
            for (int i = 0; i < show; i++)
            {
                IntPtr note = items + i * _noteSize;

                var text = new System.Text.StringBuilder(_noteSize * 2);
                for (int b = 0; b < _noteSize; b++)
                    text.Append(Memory.U8(note + b).ToString("X2"));

                Diagnostics.Info($"  note[{i}] {text}");
            }
        }

        /// <summary>The four note types the shipped charts contain: tap, hold, flick, sky area.</summary>
        private static bool IsNoteType(int type) => type == 1 || type == 2 || type == 4 || type == 5;

        /// <summary>Measures the note stride from the live array.</summary>
        private static bool MeasureNoteSize(IntPtr items, int count)
        {
            if (!ResolveNoteFields()) return false;
            if (_strideMissing) return false;

            try
            {
                IntPtr array = items - Offsets.Runtime.ArrayDataOffset;
                long byteLength = Il2CppInterop.Runtime.IL2CPP.il2cpp_array_get_byte_length(array);
                // SPC2 keeps owned metadata in spare List capacity, outside Count.
                // Array length, not the logical note count, determines the native stride.
                long capacity=(long)Il2CppInterop.Runtime.IL2CPP.il2cpp_array_length(array);
                if(capacity<count||capacity<=0)return false;
                int stride = (int)(byteLength / capacity);

                int required = Math.Max(Math.Max(Offsets.Note.Side, Offsets.Note.Type),
                    Math.Max(Offsets.Note.StartMs, Offsets.Note.EndMs)) + 4;
                foreach (int offset in _coordinateFields) required = Math.Max(required, offset+12);
                if (stride >= required && stride <= 4096 && byteLength % capacity == 0)
                {
                    if (_noteSize > 0 && stride != _noteSize)
                        Diagnostics.Warn($"note records measure {stride} bytes, not the {_noteSize} measured before");

                    _noteSize = stride;
                    return true;
                }
            }
            catch (Exception e)
            {
                Diagnostics.Warn($"could not measure the note stride: {Diagnostics.Describe(e)}");
            }

            _strideMissing = true;
            Diagnostics.Error("the note records' stride could not be measured in this build; " +
                              "no chart can be checked, so none will load");
            return false;
        }

        private static bool _offsetsResolved;

        private static bool _noteFieldsMissing;

        private static bool _strideMissing;
        private static readonly int[] _coordinateFields = new int[4];

        /// <summary>Resolves the four note field offsets.</summary>
        private static bool ResolveNoteFields()
        {
            if (_offsetsResolved) return true;
            if (_noteFieldsMissing) return false;

            if (FieldResolver.ClassPointer("_fA") == IntPtr.Zero) return false;

            Offsets.Note.Side = FieldResolver.Field("_fA", "_Ae");
            Offsets.Note.Type = FieldResolver.Field("_fA", "_be");
            Offsets.Note.StartMs = FieldResolver.Field("_fA", "_Be");
            Offsets.Note.EndMs = FieldResolver.Field("_fA", "_ce");
            string[] names = { "_Ce", "_de", "_De", "_ee" };
            for (int k = 0; k < 4; k++)
            {
                _coordinateFields[k] = FieldResolver.Field("_fA", names[k]);
                if (_coordinateFields[k] < 0) { _noteFieldsMissing = true; return false; }
            }

            if (Offsets.Note.Side < 0 || Offsets.Note.Type < 0 ||
                Offsets.Note.StartMs < 0 || Offsets.Note.EndMs < 0)
            {
                _noteFieldsMissing = true;
                Diagnostics.Error("the note record's fields could not be read by name in this build; " +
                                  "no chart can be checked, so none will load");
                return false;
            }

            _offsetsResolved = true;
            return true;
        }
    }
}
