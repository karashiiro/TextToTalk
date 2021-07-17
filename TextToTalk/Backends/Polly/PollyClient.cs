using Amazon;
using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TextToTalk.Backends.Polly
{
    public class PollyClient : IDisposable
    {
        private readonly AmazonPollyClient client;
        private readonly SoundQueue soundQueue;

        public PollyClient(string accessKey, string secretKey, RegionEndpoint region)
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            this.client = new AmazonPollyClient(credentials, region);
            this.soundQueue = new SoundQueue();
        }

        public IList<LexiconDescription> GetLexicons()
        {
            var lexiconsReq = new ListLexiconsRequest();

            var lexicons = new List<LexiconDescription>();
            string nextToken;
            do
            {
                var lexiconsRes = this.client.ListLexicons(lexiconsReq);
                lexicons.AddRange(lexiconsRes.Lexicons);
                nextToken = lexiconsRes.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            return lexicons;
        }

        public Lexicon GetLexicon(string lexiconName)
        {
            var lexicon = this.client.GetLexicon(new GetLexiconRequest
            {
                Name = lexiconName,
            });

            return lexicon.Lexicon;
        }

        public void UploadLexicon(string lexiconFilePath)
        {
            var content = File.ReadAllText(lexiconFilePath);
            var name = Path.GetFileNameWithoutExtension(lexiconFilePath);

            this.client.PutLexicon(new PutLexiconRequest
            {
                Content = content,
                Name = name,
            });
        }

        public void DeleteLexicon(string lexiconName)
        {
            this.client.DeleteLexicon(new DeleteLexiconRequest
            {
                Name = lexiconName,
            });
        }

        public IList<Voice> GetVoicesForEngine(Engine engine)
        {
            var voicesReq = new DescribeVoicesRequest
            {
                Engine = engine,
            };

            var voices = new List<Voice>();
            string nextToken;
            do
            {
                var voicesRes = this.client.DescribeVoices(voicesReq);
                voices.AddRange(voicesRes.Voices);
                nextToken = voicesRes.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            return voices;
        }

        public async Task Say(Engine engine, VoiceId voice, int sampleRate, float volume, IEnumerable<string> lexicons, string text)
        {
            var req = new SynthesizeSpeechRequest
            {
                Text = text,
                VoiceId = voice,
                Engine = engine,
                OutputFormat = OutputFormat.Mp3,
                SampleRate = sampleRate.ToString(),
                LexiconNames = lexicons.Where(l => !string.IsNullOrEmpty(l)).ToList(),
                TextType = TextType.Ssml,
            };

            SynthesizeSpeechResponse res;
            try
            {
                res = await this.client.SynthesizeSpeechAsync(req);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Synthesis request failed in {0}.", nameof(PollyClient));
                return;
            }

            var responseStream = new MemoryStream();
            await res.AudioStream.CopyToAsync(responseStream);
            responseStream.Seek(0, SeekOrigin.Begin);

            this.soundQueue.EnqueueSound(responseStream, volume);
        }

        public Task Cancel()
        {
            this.soundQueue.CancelAllSounds();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.client.Dispose();
            this.soundQueue.Dispose();
        }
    }
}