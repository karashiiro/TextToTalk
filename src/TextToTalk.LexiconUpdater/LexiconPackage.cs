using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextToTalk.LexiconUpdater
{
    public class LexiconPackage
    {
        private const string RepoBase = "https://raw.githubusercontent.com/karashiiro/TextToTalk/main/lexicons/";

        private readonly CachedLexiconPackage _cached;
        private readonly string _cachePath;
        private readonly HttpClient _http;
        private readonly string _packageName;

        public LexiconPackage(HttpClient http, string packageName, string cachePath)
        {
            _cachePath = cachePath;
            _http = http;
            _packageName = packageName;

            _cached = GetCachedPackage();
        }

        public async Task<Stream> GetPackageFile(string fileName)
        {
            var url = new Uri(RepoBase + _packageName + fileName);
            var res = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            if (!res.Headers.TryGetValues("etag", out var values))
            {
                throw new InvalidOperationException("Response has no etag header.");
            }

            var etag = values.FirstOrDefault();
            if (string.IsNullOrEmpty(etag))
            {
                throw new InvalidOperationException("Response has no etag value.");
            }

            if (_cached.FileETags.TryGetValue(fileName, out var cachedETag) && etag == cachedETag)
            {
                // Return already-downloaded file stream
                return null;
            }

            var fileData = await _http.GetStreamAsync(url);
            SaveCachedPackage(fileData);
            _cached.FileETags[fileName] = etag;
            return fileData;
        }

        private CachedLexiconPackage GetCachedPackage()
        {
            return new CachedLexiconPackage();
        }

        private void SaveCachedPackage(Stream data)
        {
            // Save stream to cached file location
            data.Seek(0, SeekOrigin.Begin);
        }
    }
}