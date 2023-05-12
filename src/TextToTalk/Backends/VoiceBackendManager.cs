using System;
using System.Net.Http;
using System.Numerics;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.ElevenLabs;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;

namespace TextToTalk.Backends
{
    public class VoiceBackendManager : VoiceBackend
    {
        private readonly HttpClient http;
        private readonly PluginConfiguration config;
        private readonly Subject<int> onFailedToBindWsPort;

        private IDisposable? handleFailedToBindWsPort;

        public VoiceBackend? Backend { get; private set; }
        public bool BackendLoading { get; private set; }

        public VoiceBackendManager(PluginConfiguration config, HttpClient http)
        {
            this.config = config;
            this.http = http;
            this.onFailedToBindWsPort = new Subject<int>();

            SetBackend(this.config.Backend);
        }

        public override void Say(TextSource source, VoicePreset voice, string speaker, string text)
        {
            Backend?.Say(source, voice, speaker, text);
        }

        public override void CancelAllSpeech()
        {
            Backend?.CancelAllSpeech();
        }

        public override void CancelSay(TextSource source)
        {
            Backend?.CancelSay(source);
        }

        public override void DrawSettings(IConfigUIDelegates helpers)
        {
            Backend?.DrawSettings(helpers);
        }

        public override TextSource GetCurrentlySpokenTextSource()
        {
            return Backend?.GetCurrentlySpokenTextSource() ?? TextSource.None;
        }

        public IObservable<int> OnFailedToBindWSPort()
        {
            return this.onFailedToBindWsPort;
        }

        public void SetBackend(TTSBackend backendKind)
        {
            _ = Task.Run(() =>
            {
                BackendLoading = true;
                var newBackend = CreateBackendFor(backendKind);
                var oldBackend = Backend;
                if (backendKind == TTSBackend.Websocket)
                {
                    var wsBackend = newBackend as WebsocketBackend;
                    this.handleFailedToBindWsPort =
                        wsBackend?.OnFailedToBindPort().Subscribe(this.onFailedToBindWsPort);
                }

                Backend = newBackend;
                BackendLoading = false;
                oldBackend?.Dispose();
            });
        }

        public Vector4 GetBackendTitleBarColor()
        {
            return Backend?.TitleBarColor ?? TitleBarColor;
        }

        private VoiceBackend CreateBackendFor(TTSBackend backendKind)
        {
            return backendKind switch
            {
                TTSBackend.System => new SystemBackend(this.config, this.http),
                TTSBackend.Websocket => new WebsocketBackend(this.config),
                TTSBackend.AmazonPolly => new PollyBackend(this.config, this.http),
                TTSBackend.Uberduck => new UberduckBackend(this.config, this.http),
                TTSBackend.Azure => new AzureBackend(this.config, this.http),
                TTSBackend.ElevenLabs => new ElevenLabsBackend(this.config, this.http),
                _ => throw new NotImplementedException(),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.handleFailedToBindWsPort?.Dispose();
                Backend?.Dispose();
            }
        }
    }
}