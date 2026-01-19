using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLayer;
using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class BossMusicUrlSystem : ModSystem
    {
        private ICoreClientAPI capi;

        private static readonly Dictionary<string, string> DefaultUrlByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> remoteUrlByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private string currentKey;
        private string currentUrl;
        private float currentStartAtSeconds;
        private volatile float desiredLoopStartAtSeconds;

        private int sourceId = -1;
        private CancellationTokenSource cts;
        private Task playTask;

        private readonly object preloadLock = new object();
        private readonly Dictionary<string, Task> preloadTasksByUrl = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

        private Task fadeTask;
        private CancellationTokenSource fadeCts;

        private readonly object alLock = new object();

        private float baseGain = 1f;
        private int lastSoundLevel = -1;

        private long suppressVanillaMusicListenerId;

        private const float BossMusicVolumeMul = 0.22f;

        private const float FadeOutSeconds = 2f;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            try
            {
                capi.Network.RegisterChannel("alegacyvsquestmusic")
                    .RegisterMessageType<BossMusicUrlMapMessage>()
                    .SetMessageHandler<BossMusicUrlMapMessage>(OnBossMusicUrlMapMessage);
            }
            catch
            {
            }

            try
            {
                api.Settings.Int.AddWatcher("soundLevel", _ =>
                {
                    UpdateGainFromSettings();
                });
            }
            catch
            {
            }
        }

        public void Preload(string url)
        {
            if (capi == null) return;
            if (string.IsNullOrWhiteSpace(url)) return;

            string resolvedUrl = url;
            if (string.IsNullOrWhiteSpace(resolvedUrl)) return;

            Task task;
            lock (preloadLock)
            {
                if (preloadTasksByUrl.TryGetValue(resolvedUrl, out task))
                {
                    return;
                }

                task = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource();
                        await EnsureCachedAsync(resolvedUrl, cts.Token);
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            capi?.Logger?.Warning("[alegacyvsquest] Boss music preload failed: {0}", e.Message);
                        }
                        catch
                        {
                        }
                    }
                    finally
                    {
                        lock (preloadLock)
                        {
                            preloadTasksByUrl.Remove(resolvedUrl);
                        }
                    }
                });

                preloadTasksByUrl[resolvedUrl] = task;
            }
        }

        public void Start(string key, string url)
        {
            Start(key, url, 0f);
        }

        public void Start(string key, string url, float startAtSeconds)
        {
            if (capi == null) return;

            if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(url)) return;

            string resolvedKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
            string resolvedUrl = NormalizeUrl(url);
            if (string.IsNullOrWhiteSpace(resolvedUrl) && !string.IsNullOrWhiteSpace(resolvedKey))
            {
                resolvedUrl = ResolveUrl(resolvedKey);
            }

            if (string.IsNullOrWhiteSpace(resolvedKey) && string.IsNullOrWhiteSpace(resolvedUrl)) return;

            if (startAtSeconds < 0f) startAtSeconds = 0f;

            bool sameKey = string.Equals(currentKey ?? "", resolvedKey ?? "", StringComparison.OrdinalIgnoreCase);
            bool sameUrl = string.Equals(currentUrl ?? "", resolvedUrl ?? "", StringComparison.OrdinalIgnoreCase);
            bool sameStart = Math.Abs(currentStartAtSeconds - startAtSeconds) < 0.01f;
            if (sameKey && sameUrl)
            {
                currentStartAtSeconds = startAtSeconds;
                desiredLoopStartAtSeconds = startAtSeconds;

                if (sameStart)
                {
                    return;
                }

                if (playTask != null && !playTask.IsCompleted)
                {
                    // Offset change while playing: apply on next loop without restarting.
                    return;
                }
            }

            currentKey = resolvedKey;
            currentUrl = resolvedUrl;
            currentStartAtSeconds = startAtSeconds;
            desiredLoopStartAtSeconds = startAtSeconds;

            try
            {
                capi?.Logger?.VerboseDebug("[alegacyvsquest] Boss music requested. key={0}, url={1}, startAtSeconds={2}", currentKey ?? "", currentUrl ?? "", currentStartAtSeconds);
            }
            catch
            {
            }

            RestartPlayback();
        }

        public void Stop()
        {
            if (capi == null) return;

            if (string.IsNullOrWhiteSpace(currentKey) && string.IsNullOrWhiteSpace(currentUrl)) return;

            try
            {
                capi?.Logger?.VerboseDebug("[alegacyvsquest] Boss music stop. key={0}", currentKey ?? "");
            }
            catch
            {
            }

            currentKey = null;
            currentUrl = null;
            currentStartAtSeconds = 0f;

            FadeOutAndStop();

            StopSuppressVanillaMusic();
        }

        public bool IsActive => !string.IsNullOrWhiteSpace(currentKey) || !string.IsNullOrWhiteSpace(currentUrl);

        public (string key, string url) Current => (currentKey, currentUrl);

        public string ResolveUrl(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            string trimmed = key.Trim();
            if (remoteUrlByKey.TryGetValue(trimmed, out var url))
            {
                return NormalizeUrl(url);
            }

            if (DefaultUrlByKey.TryGetValue(trimmed, out url))
            {
                return NormalizeUrl(url);
            }

            return null;
        }

        private void OnBossMusicUrlMapMessage(BossMusicUrlMapMessage message)
        {
            if (message?.Urls == null) return;

            try
            {
                remoteUrlByKey.Clear();
                foreach (var entry in message.Urls)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                    if (string.IsNullOrWhiteSpace(entry.Value)) continue;
                    remoteUrlByKey[entry.Key.Trim()] = NormalizeUrl(entry.Value);
                }
            }
            catch
            {
            }
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            // Guard against accidental spaces in JSON like "drive. google.com".
            string trimmed = url.Trim();
            trimmed = trimmed.Replace(" ", "");
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        public override void Dispose()
        {
            StopPlayback(immediate: true);
            base.Dispose();
        }

        private void RestartPlayback()
        {
            StopPlayback(immediate: true);

            if (string.IsNullOrWhiteSpace(currentUrl))
            {
                StopSuppressVanillaMusic();
                return;
            }

            // While boss music plays, suppress vanilla music engine without touching user settings
            StartSuppressVanillaMusic();

            cts = new CancellationTokenSource();
            var token = cts.Token;

            playTask = Task.Run(async () =>
            {
                try
                {
                    await PlayUrlWorker(currentUrl, currentStartAtSeconds, token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    try
                    {
                        capi?.Logger?.Warning("[alegacyvsquest] Boss music playback error: {0}", e.Message);
                    }
                    catch
                    {
                    }
                }
            }, token);
        }

        private void FadeOutAndStop()
        {
            try
            {
                fadeCts?.Cancel();
            }
            catch
            {
            }

            if (sourceId < 0)
            {
                StopPlayback(immediate: true);
                return;
            }

            fadeCts = new CancellationTokenSource();
            var token = fadeCts.Token;

            fadeTask = Task.Run(async () =>
            {
                try
                {
                    float fromGain = 0f;
                    try
                    {
                        lock (alLock)
                        {
                            if (sourceId >= 0)
                            {
                                fromGain = AL.GetSource(sourceId, ALSourcef.Gain);
                            }
                        }
                    }
                    catch
                    {
                        fromGain = 0.8f;
                    }

                    int steps = 20;
                    int delayMs = (int)Math.Max(10, (FadeOutSeconds * 1000f) / steps);

                    for (int i = 0; i < steps; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        float t = (i + 1) / (float)steps;
                        float g = Math.Clamp(fromGain * (1f - t), 0f, 1f);
                        try
                        {
                            lock (alLock)
                            {
                                if (sourceId >= 0)
                                {
                                    AL.Source(sourceId, ALSourcef.Gain, g);
                                }
                            }
                        }
                        catch
                        {
                        }

                        await Task.Delay(delayMs, token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                }
                finally
                {
                    StopPlayback(immediate: true);
                }
            }, token);
        }

        private void StopPlayback(bool immediate)
        {
            try
            {
                cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                lock (alLock)
                {
                    if (sourceId >= 0)
                    {
                        try
                        {
                            AL.SourceStop(sourceId);
                        }
                        catch
                        {
                        }

                        try
                        {
                            int queued;
                            AL.GetSource(sourceId, ALGetSourcei.BuffersQueued, out queued);
                            if (queued > 0)
                            {
                                var unqueued = AL.SourceUnqueueBuffers(sourceId, queued);
                                if (unqueued != null && unqueued.Length > 0)
                                {
                                    AL.DeleteBuffers(unqueued);
                                }
                            }
                        }
                        catch
                        {
                        }

                        try
                        {
                            AL.DeleteSource(sourceId);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                sourceId = -1;
            }
        }

        private void EnsureSource()
        {
            if (sourceId >= 0) return;

            lock (alLock)
            {
                if (sourceId >= 0) return;
                sourceId = AL.GenSource();
                AL.Source(sourceId, ALSourceb.Looping, false);
                AL.Source(sourceId, ALSourceb.SourceRelative, true);
                AL.Source(sourceId, ALSource3f.Position, 0f, 0f, 0f);
            }

            UpdateGainFromSettings(force: true);
        }

        private void UpdateGainFromSettings(bool force = false)
        {
            if (capi == null) return;

            int soundLevel = 100;
            try
            {
                soundLevel = capi.Settings.Int["soundLevel"];
            }
            catch
            {
                soundLevel = 100;
            }

            if (!force && soundLevel == lastSoundLevel) return;
            lastSoundLevel = soundLevel;

            // Boss music depends only on master sound volume
            float gain = Math.Clamp((soundLevel / 100f) * baseGain * BossMusicVolumeMul, 0f, 1f);
            try
            {
                lock (alLock)
                {
                    if (sourceId >= 0)
                    {
                        AL.Source(sourceId, ALSourcef.Gain, gain);
                    }
                }
            }
            catch
            {
            }
        }

        private void StartSuppressVanillaMusic()
        {
            if (capi == null) return;

            if (suppressVanillaMusicListenerId != 0) return;

            try
            {
                capi.CurrentMusicTrack?.FadeOut(0.5f);
            }
            catch
            {
            }

            try
            {
                suppressVanillaMusicListenerId = capi.Event.RegisterGameTickListener(_ =>
                {
                    try
                    {
                        // If vanilla music starts while boss music is active, fade it out.
                        capi.CurrentMusicTrack?.FadeOut(0.5f);
                    }
                    catch
                    {
                    }
                }, 500);
            }
            catch
            {
                suppressVanillaMusicListenerId = 0;
            }
        }

        private void StopSuppressVanillaMusic()
        {
            if (capi == null) return;
            try
            {
                if (suppressVanillaMusicListenerId != 0)
                {
                    capi.Event.UnregisterGameTickListener(suppressVanillaMusicListenerId);
                }
            }
            catch
            {
            }
            finally
            {
                suppressVanillaMusicListenerId = 0;
            }
        }

        private async Task PlayUrlWorker(string url, float startAtSeconds, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            EnsureSource();

            string cachedFile = null;
            try
            {
                cachedFile = await EnsureCachedAsync(url, token);

                token.ThrowIfCancellationRequested();

                // Boss music should loop
                float loopStartAtSeconds = startAtSeconds;

                while (!token.IsCancellationRequested)
                {
                    // If the requested track has changed, stop looping
                    if (!string.Equals(currentUrl ?? "", url ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await using var mp3fs = new FileStream(cachedFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var mpeg = new MpegFile(mp3fs);

                    int channels = Math.Clamp(mpeg.Channels, 1, 2);
                    int sampleRate = mpeg.SampleRate;
                    if (sampleRate <= 0) sampleRate = 44100;

                    var format = channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16;

                    const int samplesPerBufferPerChannel = 4096;
                    var floatBuf = new float[samplesPerBufferPerChannel * channels];
                    var pcmBuf = new short[samplesPerBufferPerChannel * channels];

                    if (loopStartAtSeconds > 0.01f)
                    {
                        try
                        {
                            long samplesToSkip = (long)(loopStartAtSeconds * sampleRate * channels);
                            while (samplesToSkip > 0 && !token.IsCancellationRequested)
                            {
                                int toRead = (int)Math.Min(floatBuf.Length, samplesToSkip);
                                int read = mpeg.ReadSamples(floatBuf, 0, toRead);
                                if (read <= 0) break;
                                samplesToSkip -= read;
                            }
                        }
                        catch
                        {
                        }
                    }

                    // Clear any queued buffers from a previous loop iteration
                    lock (alLock)
                    {
                        try
                        {
                            if (sourceId >= 0)
                            {
                                int queued;
                                AL.GetSource(sourceId, ALGetSourcei.BuffersQueued, out queued);
                                if (queued > 0)
                                {
                                    var unqueued = AL.SourceUnqueueBuffers(sourceId, queued);
                                    if (unqueued != null && unqueued.Length > 0)
                                    {
                                        AL.DeleteBuffers(unqueued);
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    // Prebuffer a few chunks
                    for (int i = 0; i < 6; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        int read = mpeg.ReadSamples(floatBuf, 0, floatBuf.Length);
                        if (read <= 0) break;

                        int shorts = FloatToPcm16(floatBuf, read, pcmBuf);
                        int buffer;
                        lock (alLock)
                        {
                            buffer = AL.GenBuffer();
                            unsafe
                            {
                                fixed (short* ptr = pcmBuf)
                                {
                                    AL.BufferData(buffer, format, (nint)ptr, shorts * sizeof(short), sampleRate);
                                }
                            }
                            if (sourceId >= 0)
                            {
                                AL.SourceQueueBuffer(sourceId, buffer);
                            }
                        }
                    }

                    lock (alLock)
                    {
                        if (sourceId >= 0)
                        {
                            AL.SourcePlay(sourceId);
                        }
                    }

                    bool reachedEnd = false;
                    while (!token.IsCancellationRequested)
                    {
                        // If the requested track has changed, stop looping
                        if (!string.Equals(currentUrl ?? "", url ?? "", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        int processed;
                        lock (alLock)
                        {
                            processed = 0;
                            if (sourceId >= 0)
                            {
                                AL.GetSource(sourceId, ALGetSourcei.BuffersProcessed, out processed);
                            }
                        }

                        if (processed > 0)
                        {
                            int[] unqueued;
                            lock (alLock)
                            {
                                unqueued = sourceId >= 0 ? AL.SourceUnqueueBuffers(sourceId, processed) : Array.Empty<int>();
                            }

                            foreach (var buf in unqueued)
                            {
                                token.ThrowIfCancellationRequested();

                                int read = mpeg.ReadSamples(floatBuf, 0, floatBuf.Length);
                                if (read <= 0)
                                {
                                    reachedEnd = true;
                                    lock (alLock)
                                    {
                                        try
                                        {
                                            AL.DeleteBuffer(buf);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                    continue;
                                }

                                int shorts = FloatToPcm16(floatBuf, read, pcmBuf);
                                lock (alLock)
                                {
                                    if (sourceId >= 0)
                                    {
                                        unsafe
                                        {
                                            fixed (short* ptr = pcmBuf)
                                            {
                                                AL.BufferData(buf, format, (nint)ptr, shorts * sizeof(short), sampleRate);
                                            }
                                        }
                                        AL.SourceQueueBuffer(sourceId, buf);
                                    }
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(50, token);
                        }

                        // keep playing if starved
                        int state;
                        lock (alLock)
                        {
                            state = (int)ALSourceState.Stopped;
                            if (sourceId >= 0)
                            {
                                AL.GetSource(sourceId, ALGetSourcei.SourceState, out state);
                            }
                        }

                        if ((ALSourceState)state != ALSourceState.Playing)
                        {
                            int queued;
                            lock (alLock)
                            {
                                queued = 0;
                                if (sourceId >= 0)
                                {
                                    AL.GetSource(sourceId, ALGetSourcei.BuffersQueued, out queued);
                                }
                            }

                            if (queued > 0)
                            {
                                lock (alLock)
                                {
                                    if (sourceId >= 0)
                                    {
                                        AL.SourcePlay(sourceId);
                                    }
                                }
                            }
                            else
                            {
                                // Nothing left queued
                                break;
                            }
                        }
                    }

                    // Next loop iteration uses the latest requested phase offset.
                    loopStartAtSeconds = Math.Max(0f, desiredLoopStartAtSeconds);

                    if (!reachedEnd)
                    {
                        // We stopped for some other reason (e.g. source invalid), don't tight-loop.
                        await Task.Delay(200, token);
                    }
                }
            }
            finally
            {
            }
        }

        private string GetCacheDirectory()
        {
            string dir;
            try
            {
                dir = Path.Combine(Path.GetTempPath(), "alegacyvsquest-bossmusic-cache");
            }
            catch
            {
                dir = Path.GetTempPath();
            }

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
            }

            return dir;
        }

        private string GetCachePathForUrl(string url)
        {
            string hash;
            try
            {
                using var sha = SHA256.Create();
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
                hash = Convert.ToHexString(bytes).ToLowerInvariant();
            }
            catch
            {
                hash = Guid.NewGuid().ToString("N");
            }

            return Path.Combine(GetCacheDirectory(), $"{hash}.mp3");
        }

        private async Task<string> EnsureCachedAsync(string url, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string path = GetCachePathForUrl(url);
            if (File.Exists(path))
            {
                return path;
            }

            string temp = path + ".tmp";

            try
            {
                using (var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }))
                using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var netStream = await resp.Content.ReadAsStreamAsync(token);
                    await using var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.Read);
                    await netStream.CopyToAsync(fs, 81920, token);
                }

                token.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(temp);
                    }
                    else
                    {
                        File.Move(temp, path);
                    }
                }
                catch
                {
                    try
                    {
                        if (!File.Exists(path) && File.Exists(temp))
                        {
                            File.Move(temp, path);
                        }
                    }
                    catch
                    {
                    }
                }

                return path;
            }
            finally
            {
                try
                {
                    if (File.Exists(temp) && !File.Exists(path))
                    {
                        File.Delete(temp);
                    }
                }
                catch
                {
                }
            }
        }

        private static int FloatToPcm16(float[] src, int srcCount, short[] dst)
        {
            int outCount = Math.Min(srcCount, dst.Length);
            for (int i = 0; i < outCount; i++)
            {
                float s = src[i];
                if (s > 1f) s = 1f;
                if (s < -1f) s = -1f;
                dst[i] = (short)(s * short.MaxValue);
            }
            return outCount;
        }
    }
}
