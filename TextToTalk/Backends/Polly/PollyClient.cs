using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Polly
{
    public class PollyClient : IDisposable
    {
        private readonly AmazonPollyClient client;

        private WaveOut waveOut;

        public PollyClient(string accessKey, string secretKey, RegionEndpoint region)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            this.client = new AmazonPollyClient(credentials, region);
        }

        public IList<Voice> GetVoicesForEngine(Engine engine)
        {
            var voicesReq = new DescribeVoicesRequest
            {
                Engine = engine,
            };

            var voicesRes = this.client.DescribeVoices(voicesReq);

            return voicesRes.Voices;
        }

        public async Task Say(VoiceId voice, string text)
        {
            var req = new SynthesizeSpeechRequest
            {
                Text = text,
                VoiceId = voice,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = "8000",
                TextType = TextType.Text,
            };

            var res = await this.client.SynthesizeSpeechAsync(req);
            using var responseStream = new MemoryStream();
            await res.AudioStream.CopyToAsync(responseStream);
            responseStream.Seek(0, SeekOrigin.Begin);

            using var mp3Reader = new Mp3FileReader(responseStream);
            using var waveStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
            using var blockAlignmentStream = new BlockAlignReductionStream(waveStream);
            this.waveOut = new WaveOut();

            waveOut.Init(blockAlignmentStream);
            waveOut.Play();

            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                await Task.Delay(100);
            }

            this.waveOut.Dispose();
            this.waveOut = null;
        }

        public Task Cancel()
        {
            try
            {
                this.waveOut?.Stop();
            }
            catch (ObjectDisposedException) { }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Cancel();

            this.client.Dispose();
            this.waveOut?.Dispose();
        }
    }
}