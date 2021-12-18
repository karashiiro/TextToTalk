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
            
            // Check the file etag
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

            // Check if the remote etag matches our local etag
            if (_cached.FileETags.TryGetValue(fileName, out var cachedETag) && etag == cachedETag)
            {
                return GetLocalPackageStream();
            }

            // Download the updated lexicon file and cache the updated data
            var fileData = await _http.GetStreamAsync(url);
            SaveLocalPackageStream(fileData);
            fileData.Seek(0, SeekOrigin.Begin);

            _cached.FileETags[fileName] = etag;
            SaveCachedPackage();

            return fileData;
        }

        private CachedLexiconPackage GetCachedPackage()
        {
            return new CachedLexiconPackage();
        }

        private void SaveCachedPackage()
        {
        }

        private Stream GetLocalPackageStream()
        {
            return null;
        }

        private void SaveLocalPackageStream(Stream data)
        {
            // Save stream to cached file location
        }
    }
}