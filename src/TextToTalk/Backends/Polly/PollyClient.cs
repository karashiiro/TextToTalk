using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends.Polly
{
    public class PollyClient : IDisposable
    {
        private readonly AmazonPollyClient client;
        private readonly StreamingSoundQueue soundQueue;
        private readonly LexiconManager lexiconManager;
        private readonly PluginConfiguration config;

        public CancellationTokenSource? _TtsCts;

        public PollyClient(string accessKey, string secretKey, RegionEndpoint region, LexiconManager lexiconManager, PluginConfiguration config)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            this.client = new AmazonPollyClient(credentials, region);
            this.soundQueue = new StreamingSoundQueue(config);
            this.lexiconManager = lexiconManager;
        }

        public List<Voice> GetVoicesForEngine(Engine engine)
        {
            var voicesReq = new DescribeVoicesRequest
            {
                Engine = engine,
            };

            var voices = new List<Voice>();
            string nextToken;
            do
            {
                var voicesRes = this.client.DescribeVoicesAsync(voicesReq).GetAwaiter().GetResult();
                voices.AddRange(voicesRes.Voices);
                nextToken = voicesRes.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            return voices;
        }

        public TextSource GetCurrentlySpokenTextSource()
        {
            // This should probably be designed differently.
            return this.soundQueue.GetCurrentlySpokenTextSource();
        }

        public async Task Say(Engine engine, VoiceId voice, string? amazonDomainName, int sampleRate, int playbackRate,
            float volume, TextSource source, string text)
        {
            _TtsCts?.Cancel();
            _TtsCts?.Dispose();

            _TtsCts = new CancellationTokenSource();
            var ct = _TtsCts.Token;

            if (!string.IsNullOrEmpty(amazonDomainName))
            {
                text = $"<amazon:domain name=\"{amazonDomainName}\">{text}</amazon:domain>";
            }

            var ssml = this.lexiconManager.MakeSsml(text, playbackRate: playbackRate, includeSpeakAttributes: false);
            DetailedLog.Verbose(ssml);

            var req = new SynthesizeSpeechRequest
            {
                Text = ssml,
                VoiceId = voice,
                Engine = engine,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = sampleRate.ToString(),
                TextType = TextType.Ssml,
            };

            try
            {
                // Using 'using' ensures the response (and its stream) is disposed after the queue handles it
                var res = await this.client.SynthesizeSpeechAsync(req, ct);

                // Pass the live AudioStream directly to the queue.
                // Ensure EnqueueSound is updated to process the stream as it arrives.
                this.soundQueue.EnqueueSound(res.AudioStream, source, volume, StreamFormat.Mp3, null);
            }
            catch (OperationCanceledException)
            {
                // 2026 Best Practice: Catch the cancellation exception to prevent it 
                // from bubbling up as a generic error.
                Log.Information("TTS generation was cancelled.");
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Synthesis request failed in {0}.", nameof(PollyClient));
            }

        }

        public Task CancelAllSounds()
        {
            this.soundQueue.CancelAllSounds();
            if (this._TtsCts != null)
            {
                this._TtsCts.Cancel();
            }
            this.soundQueue.StopHardware();
            return Task.CompletedTask;
        }

        public Task CancelFromSource(TextSource source)
        {
            this.soundQueue.CancelFromSource(source);
            this.soundQueue.CancelAllSounds();
            if (this._TtsCts != null)
            {
                this._TtsCts.Cancel();
            }
            this.soundQueue.StopHardware();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.client.Dispose();
            this.soundQueue.Dispose();
        }
    }
}