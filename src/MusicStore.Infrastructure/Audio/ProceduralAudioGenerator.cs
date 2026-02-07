using MusicStore.Application.Abstractions;
using NAudio.Lame;
using NAudio.Wave;

namespace MusicStore.Infrastructure.Audio;

public sealed class ProceduralAudioGenerator : IAudioGenerator
{
    private const int SampleRate = 44100;
    private const int Channels = 1;

    private const float Master = 0.85f;
    private const float DrumsGain = 0.65f;
    private const float BassGain = 0.55f;
    private const float LeadGain = 0.45f;

    public byte[] GeneratePreviewMp3(ulong seed, TimeSpan duration)
    {
        var seconds = Math.Max(1.0, duration.TotalSeconds);
        var totalSamples = (int)(SampleRate * seconds);

        var rng = CreateRng(seed);

        var settings = CreateMusicSettings(rng);
        var grid = CreateStepGrid(settings.Bpm);

        var bars = ComputeBars(totalSamples, settings.Bpm);
        var progression = PickProgression(rng, settings.IsMinor);

        var motif = GenerateMotif(rng, settings.Scale, grid.StepsPerBar);

        var pcm = RenderTrack(totalSamples, rng, settings, grid, bars, progression, motif);

        ApplyFadeOut(pcm, fadeMs: 30);

        return EncodeMp3(pcm, SampleRate, bitrateKbps: 128);
    }

    private static float[] RenderTrack(
        int totalSamples,
        Random rng,
        MusicSettings settings,
        StepGrid grid,
        int bars,
        int[] progression,
        int[] motif)
    {
        var samples = new float[totalSamples];

        double bassPhase = 0;
        double leadPhase = 0;

        for (var bar = 0; bar < bars; bar++)
        {
            var chordDegree = progression[bar % progression.Length];

            var chordRootMidi = DegreeToMidi(settings.RootMidi, settings.Scale, chordDegree, octaveShift: -12);
            var chord = BuildTriad(settings.RootMidi, settings.Scale, chordDegree);

            RenderBar(
                samples,
                rng,
                settings,
                grid,
                bar,
                chordRootMidi,
                chord,
                motif,
                ref bassPhase,
                ref leadPhase);
        }

        return samples;
    }

    private static void RenderBar(
        float[] buffer,
        Random rng,
        MusicSettings settings,
        StepGrid grid,
        int barIndex,
        int chordRootMidi,
        int[] chord,
        int[] motif,
        ref double bassPhase,
        ref double leadPhase)
    {
        for (var step = 0; step < grid.StepsPerBar; step++)
        {
            var stepWindow = GetStepWindow(buffer.Length, barIndex, step, grid);
            if (!stepWindow.IsValid) 
                break;

            var pattern = ComputeStepPattern(rng, step, grid.StepPerBeat);
            var leadMidi = PickLeadNote(rng, settings, chord, motif[step]);

            RenderStep(
                buffer,
                rng,
                settings,
                grid,
                stepWindow,
                pattern,
                chordRootMidi,
                leadMidi,
                ref bassPhase,
                ref leadPhase);
        }
    }

    private static void RenderStep(
        float[] buffer,
        Random rng,
        MusicSettings settings,
        StepGrid grid,
        StepWindow window,
        StepPattern pattern,
        int chordRootMidi,
        int leadMidi,
        ref double bassPhase,
        ref double leadPhase)
    {
        var stepStartTime = window.StartSample / (double)SampleRate;

        var bassFreq = MidiToFreq(chordRootMidi);
        var leadFreq = MidiToFreq(leadMidi);

        var sub = window.StepIndex % grid.StepPerBeat;
        var gate = (rng.NextDouble() < 0.10 && sub != 0) ? 0.0 : 1.0;

        for (var n = window.StartSample; n < window.EndSample; n++)
        {
            var t = n / (double)SampleRate;

            var d = RenderDrumsSample(t, stepStartTime, rng, pattern) * DrumsGain;
            var b = pattern.BassOn ? RenderBassSample(n - window.StartSample, bassFreq, ref bassPhase) * BassGain : 0f;

            var l = (float)(gate * RenderLeadSample(n - window.StartSample, leadFreq, ref leadPhase));
            l *= EnvPerc(n - window.StartSample, SampleRate, 0.003, 0.12) * LeadGain;

            var mix = (d + b + l) * Master;
            buffer[n] = SoftClip(buffer[n] + mix);
        }
    }

    private static StepWindow GetStepWindow(int totalSamples, int barIndex, int step, StepGrid grid)
    {
        var globalStep = barIndex * grid.StepsPerBar + step;
        var startSample = globalStep * grid.SamplesPerStep;
        if (startSample >= totalSamples) return StepWindow.Invalid;

        var endSample = Math.Min(totalSamples, startSample + grid.SamplesPerStep);
       
        return new StepWindow(step, startSample, endSample);
    }

    private static StepPattern ComputeStepPattern(Random rng, int step, int stepPerBeat)
    {
        var beatIndex = step / stepPerBeat;
        var sub = step % stepPerBeat; 

        var kick = (sub == 0) && (beatIndex == 0 || beatIndex == 2);
        var snare = (sub == 0) && (beatIndex == 1 || beatIndex == 3);

        var hat = (sub == 0 || sub == 2);
        if (hat && rng.NextDouble() < 0.08) hat = false;

        var bassOn = (sub == 0 || sub == 2);

        return new StepPattern(kick, snare, hat, bassOn);
    }

    private static MusicSettings CreateMusicSettings(Random rng)
    {
        var bpm = 90 + rng.Next(0, 61);     
        var rootMidi = 57 + rng.Next(0, 6); 
        var isMinor = rng.NextDouble() < 0.65;

        int[] scale = isMinor
            ? new[] { 0, 2, 3, 5, 7, 8, 10 } 
            : new[] { 0, 2, 4, 5, 7, 9, 11 };

        return new MusicSettings(bpm, rootMidi, isMinor, scale);
    }

    private static StepGrid CreateStepGrid(int bpm)
    {
        var secondsPerBeat = 60.0 / bpm;

        const int beatsPerBar = 4;
        const int stepPerBeat = 4;

        var stepsPerBar = beatsPerBar * stepPerBeat;
        var secondsPerStep = secondsPerBeat / stepPerBeat;

        var samplesPerStep = (int)Math.Round(SampleRate * secondsPerStep);
        if (samplesPerStep <= 0) samplesPerStep = 1;

        return new StepGrid(beatsPerBar, stepPerBeat, stepsPerBar, samplesPerStep);
    }

    private static int ComputeBars(int totalSamples, int bpm)
    {
        var secondsPerBeat = 60.0 / bpm;
        var totalSeconds = totalSamples / (double)SampleRate;

        const int beatsPerBar = 4;
        var barSeconds = beatsPerBar * secondsPerBeat;

        return Math.Max(1, (int)Math.Round(totalSeconds / barSeconds));
    }

    private static int[] PickProgression(Random rng, bool isMinor)
    {
        int[][] minor =
        {
            new[] { 1, 6, 3, 7 },
            new[] { 1, 4, 6, 5 },
            new[] { 1, 7, 6, 7 }
        };

        int[][] major =
        {
            new[] { 1, 5, 6, 4 },
            new[] { 1, 6, 4, 5 },
            new[] { 1, 4, 5, 4 }
        };

        var list = isMinor ? minor : major;

        return list[rng.Next(list.Length)];
    }

    private static int[] GenerateMotif(Random rng, int[] scale, int lengthSteps)
    {
        var motif = new int[lengthSteps];
        var cur = rng.Next(0, scale.Length);

        for (var i = 0; i < lengthSteps; i++)
        {
            if (rng.NextDouble() < 0.55)
            {
                var move = rng.Next(-1, 2);
                cur = Math.Clamp(cur + move, 0, scale.Length - 1);
            }

            if (rng.NextDouble() < 0.08)
                cur = rng.Next(0, scale.Length);

            motif[i] = cur;
        }

        if (lengthSteps >= 12 && rng.NextDouble() < 0.7)
        {
            for (var i = 0; i < 4; i++)
            {
                motif[8 + i] = motif[i];
            }
        }

        return motif;
    }

    private static int DegreeToMidi(int rootMidi, int[] scale, int degree1to7, int octaveShift = 0)
    {
        var idx = Math.Clamp(degree1to7 - 1, 0, 6);

        return rootMidi + scale[idx] + octaveShift;
    }

    private static int[] BuildTriad(int rootMidi, int[] scale, int degree1to7)
    {
        var d = Math.Clamp(degree1to7 - 1, 0, 6);

        var i1 = d;
        var i3 = (d + 2) % 7;
        var i5 = (d + 4) % 7;

        var r = rootMidi + scale[i1];
        var t = rootMidi + scale[i3];
        var f = rootMidi + scale[i5];

        return new[] { r, t, f };
    }

    private static int PickLeadNote(Random rng, MusicSettings settings, int[] chord, int motifScaleIndex)
    {
        var scale = settings.Scale;
        var midi = settings.RootMidi + 12 + scale[Math.Clamp(motifScaleIndex, 0, scale.Length - 1)];

        if (rng.NextDouble() < 0.35)
        {
            var pick = chord[rng.Next(chord.Length)] + 12;
            if (Math.Abs(pick - midi) <= 7) return pick;

            return midi;
        }

        while (midi < 60) midi += 12;
        while (midi > 81) midi -= 12;

        return midi;
    }

    private static double MidiToFreq(int midi)
    {
      return 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
    }

    private static float RenderDrumsSample(double t, double t0, Random rng, StepPattern p)
    {
        var d = 0f;
        if (p.Kick) d += Kick(t, t0, rng);
        if (p.Snare) d += Snare(t, t0, rng);
        if (p.Hat) d += Hat(t, t0, rng) * 0.55f;
        
        return d;
    }

    private static float RenderBassSample(int localSampleIndex, double freq, ref double phase)
    {
        var env = EnvPerc(localSampleIndex, SampleRate, 0.002, 0.08);
        
        return BassOsc(freq, ref phase) * env;
    }

    private static double RenderLeadSample(int localSampleIndex, double freq, ref double phase)
    {
        return LeadOsc(freq, ref phase);
    }

    private static float EnvPerc(int sampleIndex, int sampleRate, double attackSec, double decaySec)
    {
        var t = sampleIndex / (double)sampleRate;
        if (t < attackSec)
        {
            return (float)(t / attackSec);
        }

        var d = t - attackSec;
        var v = Math.Exp(-d / Math.Max(0.0001, decaySec));
        
        return (float)v;
    }

    private static float Kick(double t, double t0, Random rng)
    {
        var dt = t - t0;
        if (dt < 0)
        {
            return 0;
        }

        var env = EnvPerc((int)(dt * SampleRate), SampleRate, 0.002, 0.18);
        var f0 = 90 + rng.NextDouble() * 20;
        var f = f0 * Math.Exp(-dt * 10.0);

        var s = Math.Sin(2 * Math.PI * f * dt);
        
        return (float)(s * env * 0.9);
    }

    private static float Snare(double t, double t0, Random rng)
    {
        var dt = t - t0;
        if (dt < 0)
        {
            return 0;
        }

        var env = EnvPerc((int)(dt * SampleRate), SampleRate, 0.001, 0.12);
        var noise = (rng.NextDouble() * 2.0 - 1.0);
        var body = Math.Sin(2 * Math.PI * 180 * dt) * 0.3;

        return (float)((noise * 0.8 + body) * env * 0.6);
    }

    private static float Hat(double t, double t0, Random rng)
    {
        var dt = t - t0;
        if (dt < 0)
        {
            return 0;
        }

        var env = EnvPerc((int)(dt * SampleRate), SampleRate, 0.001, 0.04);
        var noise = (rng.NextDouble() * 2.0 - 1.0);

        return (float)(noise * env * 0.25);
    }

    private static float BassOsc(double freq, ref double phase)
    {
        phase += freq / SampleRate;
        if (phase >= 1.0) phase -= 1.0;

        var s1 = Math.Sin(2 * Math.PI * phase);
        var s2 = Math.Sin(2 * Math.PI * 2.0 * phase) * 0.15;

        return (float)(s1 + s2);
    }

    private static double LeadOsc(double freq, ref double phase)
    {
        phase += freq / SampleRate;
        if (phase >= 1.0) phase -= 1.0;

        var x = phase < 0.5 ? (phase * 4.0 - 1.0) : (3.0 - phase * 4.0);
        
        return x * 0.9;
    }

    private static float SoftClip(float x)
    {
     return (float)Math.Tanh(x * 1.2);
    }

    private static void ApplyFadeOut(float[] samples, int fadeMs)
    {
        var fadeSamples = (int)(SampleRate * (fadeMs / 1000.0));
        fadeSamples = Math.Clamp(fadeSamples, 1, samples.Length);

        for (var i = 0; i < fadeSamples; i++)
        {
            var k = 1.0f - (i / (float)fadeSamples);
            var idx = samples.Length - 1 - i;
            samples[idx] *= k;
        }
    }

    private static byte[] EncodeMp3(float[] mono, int sampleRate, int bitrateKbps)
    {
        var bytes = new byte[mono.Length * 2];
        var o = 0;

        for (var i = 0; i < mono.Length; i++)
        {
            var v = Math.Clamp(mono[i], -1f, 1f);
            short s = (short)Math.Round(v * short.MaxValue);

            bytes[o++] = (byte)(s & 0xFF);
            bytes[o++] = (byte)((s >> 8) & 0xFF);
        }

        using var pcmStream = new MemoryStream(bytes);
        using var raw = new RawSourceWaveStream(pcmStream, new WaveFormat(sampleRate, 16, Channels));
        using var outMs = new MemoryStream();

        using (var mp3 = new LameMP3FileWriter(outMs, raw.WaveFormat, bitrateKbps))
        {
            raw.CopyTo(mp3);
        }

        return outMs.ToArray();
    }

    private static Random CreateRng(ulong seed)
    {
      return new Random(unchecked((int)(seed ^ (seed >> 32))));
    }
    private readonly record struct MusicSettings(int Bpm, int RootMidi, bool IsMinor, int[] Scale);

    private readonly record struct StepGrid(int BeatsPerBar, int StepPerBeat, int StepsPerBar, int SamplesPerStep);

    private readonly record struct StepPattern(bool Kick, bool Snare, bool Hat, bool BassOn);

    private readonly record struct StepWindow(int StepIndex, int StartSample, int EndSample)
    {
        public bool IsValid => StartSample >= 0 && EndSample > StartSample;
        public static StepWindow Invalid => new StepWindow(-1, -1, -1);
    }
}
