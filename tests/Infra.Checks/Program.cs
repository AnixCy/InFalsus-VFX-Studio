using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Presentation.AudioReactive;
using InFalsus.Vfx.Addon;

var output = Path.GetFullPath(args.Length == 0 ? "local/checks/infra" : args[0]);
if (Directory.Exists(output)) throw new IOException("Use a new check directory.");
Directory.CreateDirectory(output);
new Checks(output).Run();

sealed class Checks(string output)
{
    readonly List<string> passed = new();
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
    static readonly TempoMap Tempo = new(new[] { new TempoChange(0, 120) });
    static string Encode(object value) => JsonSerializer.Serialize(value, value.GetType(), Json);
    static T Decode<T>(string text) => JsonSerializer.Deserialize<T>(text, Json)!;
    void Assert(bool ok, string text) { if (!ok) throw new Exception(text); passed.Add(text); }
    void Reject<T>(Action action, string text) where T : Exception
    { try { action(); } catch (T) { passed.Add(text); return; } throw new Exception("Expected " + typeof(T).Name + ": " + text); }
    static AnalysisSettings Settings() => new() { WindowSize = 1024, HopSize = 64, Bands = new[] { new FrequencyBand { LowHz = 80, HighHz = 200 } } };
    static AudioParameterDriver Driver(AudioAnalysis analysis, string property) => AudioParameterDriver.Create(analysis,
        new AudioDriverRecipe { AnalysisCacheKey = analysis.CacheKey, Property = property,
            Mapping = new ParameterMapping { Minimum = 0, Maximum = .4 }, Envelope = new EnvelopeSettings { AttackSeconds = 0, ReleaseSeconds = 0 } });
    AudioAnalysisStore Store(string name, Func<StoredAudioAnalysis, string>? encoder = null, int limit = 128 * 1024 * 1024)
        => new(Path.Combine(output, name), encoder ?? (d => Encode(d)), Decode<StoredAudioAnalysis>, limit);
    public void Run()
    {
        var analysis = BandAnalyzer.Analyze(new Pcm(), Settings());
        Cache(analysis); Batch(analysis); ExportBudget();
        File.WriteAllText(Path.Combine(output, "checks.json"), Encode(new { ok = true, count = passed.Count, checks = passed,
            studioStarted = false, gameStarted = false, audioPlayed = false, visualCalls = 0, screenshots = 0 }));
        Console.WriteLine(Encode(new { ok = true, count = passed.Count, output }));
    }
    void Cache(AudioAnalysis analysis)
    {
        string lookup = BandAnalyzer.GetCacheKey(BandAnalyzer.Fingerprint(new Pcm()), 8192, 2, 16384, Settings());
        Assert(lookup == analysis.CacheKey, "cache address is available before FFT analysis");
        var store = Store("cache"); var read = store.Read(lookup);
        Assert(read.Status == AudioCacheStatus.Missing && read.Analysis == null, "missing cache is an explicit miss");
        Assert(!Directory.Exists(Path.Combine(output, "cache")), "cache reads create no directories");
        Assert(store.Save(analysis), "new cache publishes atomically");
        string path = store.PathFor(lookup), original = File.ReadAllText(path);
        read = store.Read(lookup);
        Assert(read.Status == AudioCacheStatus.Hit && read.Analysis!.CopyLevels("low").SequenceEqual(analysis.CopyLevels("low")), "stored cache preserves every RMS float");
        Assert(!store.Save(analysis) && File.ReadAllText(path) == original, "valid cache is reused byte-for-byte");
        Assert(!Directory.EnumerateFiles(Path.Combine(output, "cache")).Any(p => p.EndsWith(".tmp") || p.EndsWith(".lock")), "successful cache save releases its owned lock/temp files");
        var modified = Decode<StoredAudioAnalysis>(original); modified.Payload.Levels[0][20] += .01f;
        File.WriteAllText(path, Encode(modified));
        Assert(store.Read(lookup).Status == AudioCacheStatus.Invalid, "checksum detects a valid-number RMS corruption");
        Assert(store.Save(analysis) && store.Read(lookup).Status == AudioCacheStatus.Hit, "invalid derived cache is repaired without touching artwork");
        File.WriteAllText(path, "{ broken json");
        Assert(store.Read(lookup).Diagnostic == "cache.invalid-payload", "malformed cache is a diagnosed miss rather than a project failure");
        Assert(store.Save(analysis), "malformed cache can be replaced by freshly computed data");
        modified = Decode<StoredAudioAnalysis>(original); modified.FormatVersion = 2; File.WriteAllText(path, Encode(modified));
        Assert(store.Read(lookup).Status == AudioCacheStatus.Invalid, "unknown cache envelope is rejected");
        store.Save(analysis);
        Reject<ArgumentException>(() => store.PathFor("../project.vfx.json"), "cache key cannot escape its root");
        Reject<ArgumentException>(() => BandAnalyzer.GetCacheKey("file-name", 8192, 2, 16384, Settings()), "cache lookup requires decoded PCM identity");
        using (var held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose))
            Reject<IOException>(() => store.Save(analysis), "concurrent cache writer contention is surfaced as I/O failure");
        Assert(File.ReadAllText(path) == original, "writer contention keeps the last valid cache");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Reject<OperationCanceledException>(() => store.Save(analysis, cancelled.Token), "cancelled save keeps existing data");
        Reject<OperationCanceledException>(() => store.Read(lookup, cancelled.Token), "cache reads support cancellation");
        var badCodec = Store("bad-codec", d => { d.Payload.Levels[0][0] += .1f; return Encode(d); });
        Reject<InvalidDataException>(() => badCodec.Save(analysis), "lossy cache codec is rejected before publication");
        Assert(!Directory.Exists(Path.Combine(output, "bad-codec")), "invalid codec creates no cache directory");
        var tiny = Store("tiny", limit: 1024);
        Reject<InvalidDataException>(() => tiny.Save(analysis), "cache write budget is measured before file creation");
        Directory.CreateDirectory(Path.Combine(output, "tiny")); File.WriteAllText(tiny.PathFor(lookup), new string(' ', 1025));
        Assert(tiny.Read(lookup).Diagnostic == "cache.too-large", "oversized cache is rejected before decoding");
        var wrong = Store("wrong-key"); string otherKey = new string('a', 64);
        Directory.CreateDirectory(Path.Combine(output, "wrong-key")); File.WriteAllText(wrong.PathFor(otherKey), original);
        Assert(wrong.Read(otherKey).Status == AudioCacheStatus.Invalid, "cache content must match the requested address");
        using var duringSave = new CancellationTokenSource();
        var interrupted = Store("cancel-codec", d => { string text = Encode(d); duringSave.Cancel(); return text; });
        Reject<OperationCanceledException>(() => interrupted.Save(analysis, duringSave.Token), "cancellation after encoding prevents publication");
        Assert(!Directory.Exists(Path.Combine(output, "cancel-codec")), "cancelled encoding leaves no partial output");
    }
    void Batch(AudioAnalysis analysis)
    {
        var channels = new[] {
            new ChannelDocument { Property = "_Brightness", Default = new[] { .2 }, Keys = new[] { new KeyDocument { Seconds = 0, Value = new[] { .2 } }, new KeyDocument { Seconds = 2, Value = new[] { .6 } } } },
            new ChannelDocument { Property = "_Distortion", Default = new[] { .1 } },
            new ChannelDocument { Property = "_Tint", Kind = ParameterKind.Color, Default = new[] { .2, .5, .8, 1.0 } },
            new ChannelDocument { Property = "_MainTex", Kind = ParameterKind.Choice, DefaultChoice = "source-image", Keys = new[] { new KeyDocument { Seconds = 1, Choice = "other-image", Ease = Easing.Hold } } } };
        string original = Encode(channels);
        var drivers = new[] { Driver(analysis, "_Brightness"), Driver(analysis, "_Distortion") };
        var settings = new BakeSettings { EndSeconds = 2 };
        var result = AudioBakeBatch.Bake(channels, Tempo, drivers, settings);
        var baked = result.ToChannels();
        Assert(Encode(channels) == original, "batch baking preserves the original manual source");
        Assert(baked.Select(c => c.Property).SequenceEqual(channels.Select(c => c.Property)), "batch output preserves original channel order");
        Assert(Encode(baked[2]) == Encode(channels[2]) && Encode(baked[3]) == Encode(channels[3]), "undriven color and texture-choice channels survive exactly");
        Assert(result.BakedChannels.Count == 2 && result.TotalKeys == baked.Sum(c => c.Keys.Length), "batch reports changed bindings and total keys including untouched channels");
        double error = 0;
        for (int c = 0; c < 2; c++)
        {
            var direct = AudioCurveBaker.Compile(channels[c], Tempo); var final = AudioCurveBaker.Compile(baked[c], Tempo);
            for (int i = 0; i <= 4000; i++) error = Math.Max(error, Math.Abs(drivers[c].Sample(i / 2000.0, direct) - final.Sample(i / 2000.0).Number.X));
        }
        Assert(error <= settings.MaximumError, "both batch targets agree with independent dense direct evaluation");
        baked[0].Keys[0].Value[0] = 99;
        Assert(result.ToChannels()[0].Keys[0].Value[0] != 99, "batch result snapshots resist caller mutation");
        Reject<ArgumentException>(() => AudioBakeBatch.Bake(channels, Tempo, new[] { drivers[0], drivers[0] }, settings), "duplicate parameter drivers require explicit future ordering semantics");
        Reject<ArgumentException>(() => AudioBakeBatch.Bake(channels, Tempo, new[] { Driver(analysis, "_Missing") }, settings), "undeclared target is rejected before baking");
        Reject<ArgumentException>(() => AudioBakeBatch.Bake(channels, Tempo, new[] { Driver(analysis, "_Tint") }, settings), "batch cannot silently drive color as a scalar");
        Reject<ArgumentException>(() => AudioBakeBatch.Bake(channels.Append(channels[0]).ToArray(), Tempo, drivers, settings), "duplicate source bindings are rejected");
        Reject<ArgumentException>(() => AudioBakeBatch.Bake(channels, Tempo, drivers, settings, maximumTotalKeys: 2), "aggregate key budget includes unchanged channels and all driven outputs");
        Assert(Encode(channels) == original, "failed batch never partially applies a result");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Reject<OperationCanceledException>(() => AudioBakeBatch.Bake(channels, Tempo, drivers, settings, cancellation: cancelled.Token), "batch cancellation returns no partial replacement");
        var unchanged = AudioBakeBatch.Bake(channels, Tempo, Array.Empty<AudioParameterDriver>(), settings);
        Assert(Encode(unchanged.ToChannels()) == original, "an empty plan returns an independent unchanged snapshot");
    }
    void ExportBudget()
    {
        var project = new AuthoringProjectDocument { ProjectId = "infra-fixture", ContentId = "source", Revision = 1,
            Runtime = new PackageDocument { Id = "source", TimeDomain = TimeDomain.Audio,
                Nodes = new[] { new MaterialNodeDocument { Bundle = "preview.bundle", Material = "vfx/parameter-preview" } },
                Channels = new[] { new ChannelDocument { Property = "_Brightness", Default = new[] { .8 } } } } };
        var target = new AddonTarget { SongKey = "game:infra-fixture", Difficulty = 0 };
        string bundle = Path.Combine(output, "fixture.bundle"); File.WriteAllText(bundle, "numerical contract only; not a GPU resource");
        string snapshot = Encode(project);
        string oversized = Path.Combine(output, "oversized-runtime");
        Reject<InvalidDataException>(() => BackgroundExport.Publish(project, target, "", bundle, "Gamma", oversized,
            v => v is PackageDocument ? Encode(v) + new string(' ', AddonPackage.MaximumDocumentBytes) : Encode(v)), "oversized runtime is rejected before addon publication");
        Assert(!Directory.Exists(oversized), "oversized runtime produces no final package");
        string nonAscii = Path.Combine(output, "non-ascii-runtime"); int utf16Count = 0, utf8Count = 0;
        string MultiByte(object value)
        {
            if (value is PackageDocument p) p.Id = new string('界', 1_400_000);
            string text = JsonSerializer.Serialize(value, value.GetType(), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            if (value is PackageDocument) { utf16Count = text.Length; utf8Count = Encoding.UTF8.GetByteCount(text); }
            return text;
        }
        Reject<InvalidDataException>(() => BackgroundExport.Publish(project, target, "", bundle, "Gamma", nonAscii, MultiByte), "UTF-8 bytes rather than string length determine runtime budget");
        Assert(utf16Count < AddonPackage.MaximumDocumentBytes && utf8Count > AddonPackage.MaximumDocumentBytes, "multibyte fixture crosses only the byte limit");
        string oversizedManifest = Path.Combine(output, "oversized-manifest");
        Reject<InvalidDataException>(() => BackgroundExport.Publish(project, target, "", bundle, "Gamma", oversizedManifest,
            v => v is AddonManifest ? Encode(v) + new string(' ', AddonPackage.MaximumDocumentBytes) : Encode(v)), "oversized addon manifest is also rejected");
        string empty = Path.Combine(output, "empty-codec");
        Reject<InvalidDataException>(() => BackgroundExport.Publish(project, target, "", bundle, "Gamma", empty, _ => ""), "empty codec output cannot publish an addon");
        string exact = Path.Combine(output, "exact-limit");
        string Pad(object value) { string text = Encode(value); return value is PackageDocument ? text + new string(' ', AddonPackage.MaximumDocumentBytes - Encoding.UTF8.GetByteCount(text)) : text; }
        var manifest = BackgroundExport.Publish(project, target, "", bundle, "Gamma", exact, Pad);
        var loaded = AddonPackage.Load(manifest, Decode<AddonManifest>, Decode<PackageDocument>, AddonPackage.Capabilities);
        Assert(new FileInfo(Path.Combine(exact, "runtime.json")).Length == AddonPackage.MaximumDocumentBytes && loaded.Document.TimeDomain == TimeDomain.Audio,
            "exactly 4 MiB remains loadable by the existing player contract");
        Assert(!Directory.EnumerateDirectories(output, ".vfx-export-*").Any(), "failed exports remove only their owned staging directories");
        Assert(Encode(project) == snapshot, "all export preflights preserve the source project");
    }
}

sealed class Pcm : IPcmSource
{
    public int SampleRate => 8192; public int Channels => 2; public long FrameCount => 16384;
    public int ReadFrames(long firstFrame, Span<float> interleaved)
    {
        int count = (int)Math.Min(FrameCount - firstFrame, interleaved.Length / 2);
        for (int i = 0; i < count; i++)
        {
            double t = (firstFrame + i) / 8192.0;
            float sample = (float)((.2 + .1 * Math.Sin(t * 4)) * Math.Sin(2 * Math.PI * 128 * t));
            interleaved[2 * i] = sample; interleaved[2 * i + 1] = -sample;
        }
        return count;
    }
}
