using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.AudioReactive;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;

var output = Path.GetFullPath(args.Length == 0 ? "local/checks/audio-reactive" : args[0]);
if (Directory.Exists(output)) throw new IOException("Use a new output directory; existing results are never overwritten.");
Directory.CreateDirectory(output);
new Checks(output, args.Length > 1 ? Path.GetFullPath(args[1]) : null).Run();

sealed class Checks(string output, string? previewAssets)
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
    readonly List<string> passed = new();
    static readonly TempoMap Tempo = new(new[] { new TempoChange(0, 120) });
    void Assert(bool condition, string description) { if (!condition) throw new Exception(description); passed.Add(description); }
    void Near(double actual, double expected, double tolerance, string description) => Assert(Math.Abs(actual - expected) <= tolerance, $"{description}: {actual:G8} vs {expected:G8}");
    void Reject<T>(Action action, string description) where T : Exception
    {
        try { action(); } catch (T) { passed.Add(description); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + description);
    }
    void Write(string name, object document) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(document, Json), new UTF8Encoding(false));
    static AnalysisSettings Config() => new() { WindowSize = 1024, HopSize = 64, Bands = new[] {
        new FrequencyBand { Id = "low", LowHz = 80, HighHz = 200 }, new FrequencyBand { Id = "high", LowHz = 800, HighHz = 1400 } } };
    static AudioDriverRecipe Recipe(AudioAnalysis analysis) => new() { AnalysisCacheKey = analysis.CacheKey,
        Envelope = new EnvelopeSettings { FloorDb = -50, CeilingDb = -8, AttackSeconds = .015, ReleaseSeconds = .15 },
        Mapping = new ParameterMapping { Minimum = 0, Maximum = 1.4, Blend = AudioBlend.Add } };
    static ChannelDocument Base() => new() { Node = "background", Property = "_Brightness", Default = new[] { .25 }, Keys = new[] {
        new KeyDocument { Seconds = 0, Value = new[] { .25 }, Ease = Easing.SineInOut },
        new KeyDocument { Seconds = 5, Value = new[] { .7 }, Ease = Easing.Linear },
        new KeyDocument { Seconds = 12, Value = new[] { .25 } } } };

    public void Run()
    {
        Spectrum(); IdentityAndValidation(); EnvelopeAndMapping(); Baking();
        var demo = Demo();
        Write("checks.json", new { ok = true, count = passed.Count, checks = passed, demo,
            unityStarted = false, gameStarted = false, audioPlayed = false, visualCalls = 0, playerDspDependencyAdded = false });
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, checks = passed.Count, output, demo }, Json));
    }
    void Spectrum()
    {
        var source = MemoryPcm.Tone(8192, 2, 2, (t, c) => .5 * Math.Sin(2 * Math.PI * 128 * t) * (c == 0 ? 1 : -1));
        var analysis = BandAnalyzer.Analyze(source, Config());
        Near(analysis.SampleLevel("low", .5), .5 / Math.Sqrt(2), 1e-5, "Hann/Parseval RMS agrees with a calibrated sine");
        Assert(analysis.SampleLevel("high", .5) < 1e-6, "out-of-band sine is rejected");
        Near(analysis.DurationSeconds, 2, 0, "duration follows decoded frames");
        Near(analysis.StepSeconds, 1.0 / 128, 0, "sample clock follows hop/sample rate");
        Assert(source.LargestReadFrames <= 4096, "PCM decoder is consumed in bounded blocks");
        var mono = BandAnalyzer.Analyze(MemoryPcm.Tone(8192, 1, 2, (t, _) => .5 * Math.Sin(2 * Math.PI * 128 * t)), Config());
        Near(analysis.SampleLevel("low", .5), mono.SampleLevel("low", .5), 1e-7, "opposite-phase stereo energy survives without mono cancellation");
        var scaled = BandAnalyzer.Analyze(MemoryPcm.Tone(8192, 1, 2, (t, _) => .25 * Math.Sin(2 * Math.PI * 128 * t)), Config());
        Near(scaled.SampleLevel("low", .5) / mono.SampleLevel("low", .5), .5, 1e-5, "linear PCM amplitude gives linear RMS");
        var mixture = BandAnalyzer.Analyze(MemoryPcm.Tone(8192, 1, 2, (t, _) => .3 * Math.Sin(2 * Math.PI * 128 * t) + .6 * Math.Sin(2 * Math.PI * 1024 * t)), Config());
        Near(mixture.SampleLevel("low", .5), .3 / Math.Sqrt(2), 1e-5, "low band separates a simultaneous high tone");
        Near(mixture.SampleLevel("high", .5), .6 / Math.Sqrt(2), 1e-5, "high band separates a simultaneous low tone");
        var all = new AnalysisSettings { WindowSize = 128, HopSize = 64, Bands = new[] { new FrequencyBand { Id = "all", LowHz = 0, HighHz = 4096 } } };
        var complex = MemoryPcm.Tone(8192, 1, 1, (t, _) => .1 + .2 * Math.Sin(2 * Math.PI * 256 * t) + .3 * Math.Cos(2 * Math.PI * 4096 * t));
        var full = BandAnalyzer.Analyze(complex, all);
        double weighted = 0, windowEnergy = 0;
        for (int i = 0; i < 128; i++) { double w = .5 - .5 * Math.Cos(2 * Math.PI * i / 128); weighted += Math.Pow(complex.Samples[4096 - 64 + i] * w, 2); windowEnergy += w * w; }
        Near(full.SampleLevel("all", .5), Math.Sqrt(weighted / windowEnergy), 1e-6, "independent time-domain Parseval check includes DC and Nyquist");
        var shortSource = MemoryPcm.Tone(8192, 1, .001, (t, _) => .4);
        var shortAnalysis = BandAnalyzer.Analyze(shortSource, Config());
        Assert(shortAnalysis.SampleCount == 2 && double.IsFinite(shortAnalysis.SampleLevel("low", .0005)), "sub-window clips use finite zero-padded samples");
        Near(analysis.SampleLevel("low", -.1), 0, 0, "before audio start returns zero");
        Near(analysis.SampleLevel("low", 2), 0, 0, "audio end is exclusive");
        Near(analysis.SampleLevel("low", 20), 0, 0, "after audio end returns zero");
    }
    void IdentityAndValidation()
    {
        var source = MemoryPcm.Tone(8192, 1, .5, (t, _) => .3 * Math.Sin(2 * Math.PI * 128 * t));
        var config = Config(); var analysis = BandAnalyzer.Analyze(source, config);
        Assert(analysis.Matches(BandAnalyzer.Fingerprint(source), config), "cache matches decoded PCM identity and settings");
        var doc = analysis.ToDocument(); var restored = AudioAnalysis.FromDocument(JsonSerializer.Deserialize<AudioAnalysisDocument>(JsonSerializer.Serialize(doc, Json), Json)!);
        Assert(restored.CacheKey == analysis.CacheKey && restored.CopyLevels("low").SequenceEqual(analysis.CopyLevels("low")), "analysis cache JSON roundtrip is exact");
        config.Bands[0].LowHz = 100;
        Assert(!analysis.Matches(BandAnalyzer.Fingerprint(source), config), "band changes invalidate analysis identity");
        config = Config(); config.HopSize = 128;
        Assert(!analysis.Matches(BandAnalyzer.Fingerprint(source), config), "hop changes invalidate analysis identity");
        config = Config(); config.WindowSize = 2048;
        Assert(!analysis.Matches(BandAnalyzer.Fingerprint(source), config), "window changes invalidate analysis identity");
        source.Samples[0] += .01f;
        Assert(!analysis.Matches(BandAnalyzer.Fingerprint(source), Config()), "audio-content changes invalidate analysis identity");
        double original = analysis.SampleLevel("low", .25);
        doc.Levels[0][32] = 99; doc.Settings.Bands[0].Id = "changed";
        Near(analysis.SampleLevel("low", .25), original, 0, "cache owns a deep immutable copy");
        var levels = analysis.CopyLevels("low"); Array.Fill(levels, 99);
        Near(analysis.SampleLevel("low", .25), original, 0, "level views do not expose mutable cache storage");
        doc = analysis.ToDocument(); doc.FormatVersion = 2;
        Reject<ArgumentException>(() => AudioAnalysis.FromDocument(doc), "unknown cache version is rejected");
        doc = analysis.ToDocument(); doc.CacheKey = "stale";
        Reject<ArgumentException>(() => AudioAnalysis.FromDocument(doc), "stale cache key is rejected");
        doc = analysis.ToDocument(); doc.Levels[0][0] = float.NaN;
        Reject<ArgumentException>(() => AudioAnalysis.FromDocument(doc), "non-finite cached levels are rejected");
        doc = analysis.ToDocument(); doc.Levels[0] = new float[1];
        Reject<ArgumentException>(() => AudioAnalysis.FromDocument(doc), "truncated cache is rejected");
        config = Config(); config.Bands[0].HighHz = 5000;
        Reject<ArgumentException>(() => BandAnalyzer.Analyze(source, config), "frequencies above Nyquist are rejected");
        config = Config(); config.Bands[1].Id = "low";
        Reject<ArgumentException>(() => BandAnalyzer.Analyze(source, config), "duplicate band IDs are rejected");
        config = Config(); config.WindowSize = 1000;
        Reject<ArgumentException>(() => BandAnalyzer.Analyze(source, config), "non-power-of-two FFT is rejected");
        config = Config(); config.HopSize = 0;
        Reject<ArgumentException>(() => BandAnalyzer.Analyze(source, config), "zero analysis hop is rejected");
        config = Config(); config.Bands[0].LowHz = config.Bands[0].HighHz;
        Reject<ArgumentException>(() => BandAnalyzer.Analyze(source, config), "empty frequency band is rejected");
        source.Samples[0] = float.PositiveInfinity;
        Reject<InvalidDataException>(() => BandAnalyzer.Analyze(source, Config()), "non-finite PCM is rejected before FFT");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Reject<OperationCanceledException>(() => BandAnalyzer.Analyze(source, Config(), cancel.Token), "analysis supports cancellation");
        source.Samples[0] = 0;
        var progress = new List<double>();
        _ = BandAnalyzer.Analyze(source, Config(), progress: new InlineProgress(p => progress.Add(p)));
        Assert(progress.First() == 0 && progress.Last() == 1 && progress.SequenceEqual(progress.Order()), "analysis progress is monotonic from zero to completion");
        using var midway = new CancellationTokenSource();
        Reject<OperationCanceledException>(() => BandAnalyzer.Analyze(source, Config(), midway.Token,
            new InlineProgress(p => { if (p > .1) midway.Cancel(); })), "analysis cancels during spectral processing without returning a partial cache");
        var shortRead = new ShortReadPcm();
        Reject<InvalidDataException>(() => BandAnalyzer.Analyze(shortRead, Config()), "decoder short reads fail explicitly");
    }
    void EnvelopeAndMapping()
    {
        var analysis = BandAnalyzer.Analyze(MemoryPcm.Tone(8192, 1, 3, (t, _) => t is >= .5 and < 1.5 ? .5 * Math.Sin(2 * Math.PI * 128 * t) : 0), Config());
        var fast = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { FloorDb = -50, CeilingDb = -9, AttackSeconds = 0, ReleaseSeconds = 0 });
        var smooth = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { FloorDb = -50, CeilingDb = -9, AttackSeconds = .1, ReleaseSeconds = .3 });
        Assert(smooth.Sample(.55) < fast.Sample(.55), "attack smooths an energy rise");
        Assert(smooth.Sample(1.8) > fast.Sample(1.8), "release preserves a decaying tail");
        Assert(fast.CopyValues().All(v => v is >= 0 and <= 1), "dB mapping clamps envelope to [0,1]");
        var power = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { FloorDb = -50, CeilingDb = 0, AttackSeconds = 0, ReleaseSeconds = 0, Exponent = 2 });
        var linear = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { FloorDb = -50, CeilingDb = 0, AttackSeconds = 0, ReleaseSeconds = 0 });
        Near(power.Sample(1), Math.Pow(linear.Sample(1), 2), 1e-6, "response exponent is applied before time smoothing");
        var gain = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { GainDb = 6 });
        var noGain = AudioEnvelope.Create(analysis, "low", new EnvelopeSettings());
        Assert(gain.Sample(1) > noGain.Sample(1), "input gain changes a fixed-reference envelope");
        var recipe = Recipe(analysis); var driver = AudioParameterDriver.Create(analysis, recipe);
        double direct = driver.Sample(1, .25), env = driver.Envelope.Sample(1);
        Near(direct, .25 + env * 1.4, 1e-6, "add blend preserves the manual base value");
        recipe.Mapping.Blend = AudioBlend.Replace; recipe.Mapping.Minimum = 2; recipe.Mapping.Maximum = .5;
        var replace = AudioParameterDriver.Create(analysis, recipe);
        Near(replace.Sample(1, 100), 2 - 1.5 * env, 1e-6, "replacement supports reversed output ranges");
        recipe.Mapping.Blend = AudioBlend.Multiply;
        var multiply = AudioParameterDriver.Create(analysis, recipe);
        Near(multiply.Sample(1, .5), .5 * replace.Sample(1, 100), 1e-6, "multiplicative modulation uses the base animation");
        recipe = Recipe(analysis); recipe.AudioOriginSeconds = .125;
        var shifted = AudioParameterDriver.Create(analysis, recipe);
        Near(shifted.Sample(1.125, .25), direct, 0, "positive chart/audio origin offset applies exactly once");
        recipe.AudioOriginSeconds = -.125;
        Near(AudioParameterDriver.Create(analysis, recipe).Sample(.875, .25), direct, 0, "negative audio origin is supported");
        var random = new Random(7); var points = Enumerable.Range(0, 300).Select(_ => random.NextDouble() * 3).ToArray();
        var before = points.Select(t => driver.Sample(t, .2)).ToArray();
        foreach (double t in points.Reverse()) _ = driver.Sample(t, .8);
        Assert(points.Select(t => driver.Sample(t, .2)).SequenceEqual(before), "random seeks, reverse inspection and repeated sampling are deterministic");
        recipe = Recipe(analysis); var frozen = AudioParameterDriver.Create(analysis, recipe); recipe.Mapping.Maximum = 99; recipe.Envelope.GainDb = 99;
        Near(frozen.Sample(1, .25), direct, 0, "driver settings are snapshotted against caller mutation");
        recipe = frozen.ToRecipe(); recipe.AnalysisCacheKey = "other";
        Reject<ArgumentException>(() => AudioParameterDriver.Create(analysis, recipe), "recipe cannot silently attach to a different analysis");
        Reject<ArgumentException>(() => driver.Sample(double.NaN, 0), "non-finite sample time is rejected");
        Reject<ArgumentException>(() => AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { AttackSeconds = -1 }), "negative smoothing is rejected");
        Reject<ArgumentException>(() => AudioEnvelope.Create(analysis, "low", new EnvelopeSettings { FloorDb = -6, CeilingDb = -60 }), "inverted dB thresholds are rejected");
        var silence = BandAnalyzer.Analyze(MemoryPcm.Tone(8192, 1, .5, (_, _) => 0), Config());
        Assert(AudioEnvelope.Create(silence, "low", new EnvelopeSettings()).CopyValues().All(v => v == 0), "silence remains exactly zero");
    }
    void Baking()
    {
        var analysis = BandAnalyzer.Analyze(MemoryPcm.Demo(8192), Config());
        var recipe = Recipe(analysis); var driver = AudioParameterDriver.Create(analysis, recipe);
        var source = Base(); string before = JsonSerializer.Serialize(source, Json);
        var opts = new BakeSettings { EndSeconds = 12, MaximumError = .002 };
        var result = AudioCurveBaker.Bake(source, Tempo, driver, opts);
        var track = AudioCurveBaker.Compile(source, Tempo); var baked = AudioCurveBaker.Compile(result.Channel, Tempo);
        double error = Enumerable.Range(0, 24001).Max(i => Math.Abs(driver.Sample(i / 2000.0, track) - baked.Sample(i / 2000.0).Number.X));
        Assert(error <= .002, "independent 2 kHz validation agrees with direct creator evaluation");
        Assert(result.MeasuredMaximumError <= .002, "bake reports its validation error");
        Assert(result.Channel.Keys.Length < result.EvaluationSamples / 4, "vertical-error reduction removes redundant samples");
        Assert(JsonSerializer.Serialize(source, Json) == before, "baking never changes the source keyframe document");
        var restored = JsonSerializer.Deserialize<ChannelDocument>(JsonSerializer.Serialize(result.Channel, Json), Json)!;
        var reloaded = AudioCurveBaker.Compile(restored, Tempo);
        Assert(Enumerable.Range(0, 1001).All(i => reloaded.Sample(i / 100.0).Number.X == baked.Sample(i / 100.0).Number.X), "baked channel JSON roundtrip preserves player values");
        Assert(result.Channel.Keys.All(k => k.Seconds.HasValue && !k.Beat.HasValue && k.Ease is Easing.Linear or Easing.Hold), "player curve uses existing seconds/linear/hold semantics only");
        var hold = Base(); hold.Keys = new[] { new KeyDocument { Seconds = 0, Value = new[] { .2 }, Ease = Easing.Hold }, new KeyDocument { Seconds = 2, Value = new[] { .9 } } };
        var held = AudioCurveBaker.Bake(hold, Tempo, driver, new BakeSettings { EndSeconds = 3 });
        var holdTrack = AudioCurveBaker.Compile(held.Channel, Tempo); var originalHold = AudioCurveBaker.Compile(hold, Tempo);
        Near(holdTrack.Sample(2 - 1e-7).Number.X, driver.Sample(2 - 1e-7, originalHold), .001, "manual step stays on its old side until the jump");
        Near(holdTrack.Sample(2).Number.X, driver.Sample(2, originalHold), .001, "manual step jumps at its exact time");
        var delayed = Base(); delayed.Keys = new[] { new KeyDocument { Seconds = 2, Value = new[] { .9 } } };
        var delayedBake = AudioCurveBaker.Bake(delayed, Tempo, driver, new BakeSettings { EndSeconds = 3 });
        var delayedTrack = AudioCurveBaker.Compile(delayedBake.Channel, Tempo); var delayedSource = AudioCurveBaker.Compile(delayed, Tempo);
        Near(delayedTrack.Sample(2 - 1e-7).Number.X, driver.Sample(2 - 1e-7, delayedSource), .001, "first-key default discontinuity is not turned into a pre-ramp");
        Near(delayedTrack.Sample(2).Number.X, driver.Sample(2, delayedSource), .001, "first key replaces its old default at the exact timestamp");
        var beat = Base(); beat.Keys[1].Seconds = null; beat.Keys[1].Beat = 10;
        var beatResult = AudioCurveBaker.Bake(beat, Tempo, driver, opts);
        Assert(beatResult.Channel.Keys.Select(k => (k.Seconds, k.Value[0])).SequenceEqual(result.Channel.Keys.Select(k => (k.Seconds, k.Value[0]))), "beat keys resolve through the existing tempo map");
        recipe.AudioOriginSeconds = .333; var shifted = AudioParameterDriver.Create(analysis, recipe);
        var edge = AudioCurveBaker.Bake(Base(), Tempo, shifted, new BakeSettings { EndSeconds = 13, MaximumError = .002 });
        var edgeTrack = AudioCurveBaker.Compile(edge.Channel, Tempo);
        var directTrack = AudioCurveBaker.Compile(Base(), Tempo);
        foreach (double t in new[] { .3329999, .333, 12.3329999, 12.333, 12.4 })
            Near(edgeTrack.Sample(t).Number.X, shifted.Sample(t, directTrack), .002, "shifted audio boundaries remain sharp at " + t);
        Reject<ArgumentException>(() => AudioCurveBaker.Bake(source, Tempo, driver, new BakeSettings { EndSeconds = 12, MaximumSamples = 20, MaximumKeys = 20 }), "sample budget fails explicitly");
        Reject<ArgumentException>(() => AudioCurveBaker.Bake(source, Tempo, driver, new BakeSettings { EndSeconds = 12, MaximumKeys = 2 }), "key budget fails explicitly");
        var color = Base(); color.Kind = ParameterKind.Color;
        Reject<ArgumentException>(() => AudioCurveBaker.Bake(color, Tempo, driver, opts), "unsupported vector/color binding is rejected");
        var wrong = Base(); wrong.Property = "_Other";
        Reject<ArgumentException>(() => AudioCurveBaker.Bake(wrong, Tempo, driver, opts), "mismatched parameter binding is rejected");
        Reject<ArgumentException>(() => AudioCurveBaker.Bake(source, Tempo, driver, new BakeSettings { EndSeconds = 1, MaximumError = 0 }), "zero bake error is rejected");
        var subrange = AudioCurveBaker.Bake(source, Tempo, driver, new BakeSettings { StartSeconds = 5, EndSeconds = 7 });
        Assert(subrange.Channel.Keys.First().Seconds == 5 && subrange.Channel.Keys.Last().Seconds == 7, "explicit bake subrange retains exact endpoints");
        recipe.Mapping.Blend = AudioBlend.Multiply; recipe.Mapping.Minimum = .5; recipe.Mapping.Maximum = 1.3;
        var mult = AudioParameterDriver.Create(analysis, recipe);
        var multBake = AudioCurveBaker.Bake(source, Tempo, mult, opts); var multTrack = AudioCurveBaker.Compile(multBake.Channel, Tempo);
        Assert(Enumerable.Range(0, 6001).Max(i => Math.Abs(mult.Sample(i / 500.0, track) - multTrack.Sample(i / 500.0).Number.X)) <= .002, "multiplicative nonlinear composition also bakes within tolerance");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Reject<OperationCanceledException>(() => AudioCurveBaker.Bake(source, Tempo, driver, opts, cancel.Token), "bake supports cancellation");
    }
    object Demo()
    {
        var pcm = MemoryPcm.Demo(48000);
        var config = new AnalysisSettings { WindowSize = 2048, HopSize = 400, Bands = new[] {
            new FrequencyBand { Id = "low", LowHz = 40, HighHz = 180 }, new FrequencyBand { Id = "high", LowHz = 1600, HighHz = 2800 } } };
        long allocation = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        var analysis = BandAnalyzer.Analyze(pcm, config); watch.Stop(); allocation = GC.GetAllocatedBytesForCurrentThread() - allocation;
        var recipe = Recipe(analysis); var driver = AudioParameterDriver.Create(analysis, recipe);
        var source = Base(); var options = new BakeSettings { EndSeconds = 12, MaximumError = .002 };
        var baked = AudioCurveBaker.Bake(source, Tempo, driver, options);
        Write("analysis.json", analysis.ToDocument()); Write("driver.recipe.json", recipe); Write("manual-channel.json", source); Write("bake-settings.json", options);
        var package = new PackageDocument { Id = "audio-reactive-demo", TimeDomain = TimeDomain.Audio, UnityVersion = "6000.3.25f1", ColorSpace = "Linear",
            Requires = new[] { "clock.audio", "shader.bundle", "background.material" },
            Nodes = new[] { new MaterialNodeDocument { Id = "background", Bundle = "preview.bundle", Material = "vfx/parameter-preview" } },
            Channels = new[] { baked.Channel,
                new ChannelDocument { Property = "_Tint", Kind = ParameterKind.Color, Default = new[] { .3, .55, .95, 1.0 } },
                new ChannelDocument { Property = "_Distortion", Default = new[] { .1 } },
                new ChannelDocument { Property = "_Frequency", Default = new[] { 6.0 } },
                new ChannelDocument { Property = "_Speed", Default = new[] { .3 } },
                new ChannelDocument { Property = "_OffsetX" }, new ChannelDocument { Property = "_OffsetY" },
                new ChannelDocument { Property = "_PatternMix", Default = new[] { 1.0 } } } };
        Write("baked.runtime.json", package);
        var compiled = EffectPackage.FromDocument(package, previewAssets ?? output, package.Requires, requireResources: false);
        var authoring = AuthoringProjects.Import(compiled);
        Write("AudioReactive.vfx.json", new { FormatVersion = 1, Project = authoring, BackgroundPath = "", AudioPath = Path.Combine(output, "demo.wav") });
        var playerPackage = BackgroundExport.Compile(authoring, new AddonTarget { SongKey = "game:fixture-audio-reactive", Difficulty = 0 }, "Gamma");
        Write("game-runtime.contract.json", playerPackage);
        var playerContract = EffectPackage.FromDocument(playerPackage, output, AddonPackage.Capabilities, requireResources: false);
        var gameBrightness = AudioCurveBaker.Compile(playerContract.Document.Channels.Single(c => c.Property == "_Brightness"), playerContract.Tempo);
        var expectedGameBrightness = AudioCurveBaker.Compile(baked.Channel, Tempo);
        Assert(playerPackage.Channels.Single(c => c.Property == "_Brightness").Keys.Length == baked.Channel.Keys.Length,
            "existing addon exporter preserves the baked sequence without audio-specific runtime semantics");
        Assert(Enumerable.Range(0, 1201).All(i => gameBrightness.Sample(i / 100.0).Number.X == expectedGameBrightness.Sample(i / 100.0).Number.X),
            "existing game curve contract preserves baked values at every checked time");
        Assert(!File.ReadAllText(Path.Combine(output, "game-runtime.contract.json")).Contains("SourceFingerprint"), "player contract contains no PCM/cache/analysis recipe");
        var decoded = JsonSerializer.Deserialize<PackageDocument>(File.ReadAllText(Path.Combine(output, "baked.runtime.json")), Json)!;
        var loaded = EffectPackage.FromDocument(decoded, previewAssets ?? output, package.Requires, requireResources: previewAssets != null);
        var direct = AudioCurveBaker.Compile(source, Tempo);
        var player = AudioCurveBaker.Compile(loaded.Document.Channels.Single(c => c.Property == "_Brightness"), loaded.Tempo);
        var csv = new StringBuilder("seconds,low_rms,high_rms,envelope,manual,direct,baked\n"); double error = 0;
        for (int i = 0; i <= 2400; i++)
        {
            double t = i / 200.0, live = driver.Sample(t, direct), final = player.Sample(t).Number.X;
            error = Math.Max(error, Math.Abs(final - live));
            csv.AppendLine(string.Join(",", new[] { t, analysis.SampleLevel("low", t), analysis.SampleLevel("high", t), driver.Envelope.Sample(t), direct.Sample(t).Number.X, live, final }.Select(v => v.ToString("G9", CultureInfo.InvariantCulture))));
        }
        File.WriteAllText(Path.Combine(output, "comparison.csv"), csv.ToString(), new UTF8Encoding(false));
        pcm.WriteWav(Path.Combine(output, "demo.wav"));
        Assert(error <= .002, "48 kHz demo direct and serialized player evaluations agree");
        Assert(allocation < 8 * 1024 * 1024, "analysis allocations stay bounded for the demo independently of PCM buffering");
        long playerBefore = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); double checksum = 0;
        for (int i = 0; i < 100_000; i++) checksum += player.Sample((i % 12000) / 1000.0).Number.X;
        timer.Stop(); long playerAllocated = GC.GetAllocatedBytesForCurrentThread() - playerBefore;
        Assert(double.IsFinite(checksum) && playerAllocated < 1024, "baked player sampling allocates no per-frame analysis state");
        var result = new { sampleRate = pcm.SampleRate, channels = pcm.Channels, seconds = analysis.DurationSeconds, fft = config.WindowSize,
            hop = config.HopSize, bands = analysis.BandIds, analysisMs = watch.Elapsed.TotalMilliseconds, analysisAllocatedBytes = allocation,
            bakedKeys = baked.Channel.Keys.Length, evaluatedSamples = baked.EvaluationSamples, validationMaximumError = baked.MeasuredMaximumError,
            independentMaximumError = error, player100kMs = timer.Elapsed.TotalMilliseconds, playerAllocatedBytes = playerAllocated,
            runtimeBytes = new FileInfo(Path.Combine(output, "baked.runtime.json")).Length, previewAssetsAvailable = previewAssets != null,
            sourceFingerprint = analysis.SourceFingerprint, cacheKey = analysis.CacheKey };
        Write("demo-report.json", result);
        return result;
    }
}

sealed class MemoryPcm(int sampleRate, int channels, float[] samples) : IPcmSource
{
    public int SampleRate => sampleRate;
    public int Channels => channels;
    public long FrameCount => samples.Length / channels;
    public float[] Samples => samples;
    public int LargestReadFrames { get; private set; }
    public int ReadFrames(long firstFrame, Span<float> interleaved)
    {
        int frames = (int)Math.Min(interleaved.Length / channels, FrameCount - firstFrame);
        samples.AsSpan(checked((int)firstFrame * channels), frames * channels).CopyTo(interleaved);
        LargestReadFrames = Math.Max(LargestReadFrames, frames); return frames;
    }
    public static MemoryPcm Tone(int rate, int channels, double seconds, Func<double, int, double> sample)
    {
        var data = new float[(int)(rate * seconds) * channels];
        for (int frame = 0; frame < data.Length / channels; frame++) for (int c = 0; c < channels; c++) data[frame * channels + c] = (float)sample(frame / (double)rate, c);
        return new(rate, channels, data);
    }
    public static MemoryPcm Demo(int rate)
    {
        return Tone(rate, 2, 12, (t, channel) => {
            double low = t is >= 1 and < 2 ? .5 : t is >= 5 and < 9 ? .55 * Math.Exp(-((t - 5) % .5) * 14) : 0;
            double high = t is >= 3 and < 4 ? .35 : t is >= 7 and < 10 ? .25 * Math.Exp(-((t - 7) % .25) * 16) : 0;
            double value = low * Math.Sin(2 * Math.PI * 128 * t) * (channel == 0 ? 1 : -1) + high * Math.Sin(2 * Math.PI * 2200 * t);
            return (short)Math.Round(value * 32767) / 32768.0; // Match the exported PCM16 file exactly.
        });
    }
    public void WriteWav(string path)
    {
        using var writer = new BinaryWriter(File.Create(path)); int size = Samples.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(size + 36); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)Channels); writer.Write(SampleRate); writer.Write(SampleRate * Channels * 2);
        writer.Write((short)(Channels * 2)); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(size);
        foreach (float value in Samples) writer.Write((short)Math.Round(value * 32768));
    }
}
sealed class ShortReadPcm : IPcmSource
{
    public int SampleRate => 8192; public int Channels => 1; public long FrameCount => 8192;
    public int ReadFrames(long firstFrame, Span<float> interleaved) => 0;
}
sealed class InlineProgress(Action<double> report) : IProgress<double>
{
    public void Report(double value) => report(value);
}
