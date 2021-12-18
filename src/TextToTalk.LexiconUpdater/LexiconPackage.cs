using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

        public async Task<Stream> GetPackageFile(string filename)
        {
            var url = new Uri(RepoBase + _packageName + filename);
            
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
            if (_cached.FileETags.TryGetValue(filename, out var cachedETag) && etag == cachedETag)
            {
                return GetLocalPackageStream(filename);
            }

            // Download the updated lexicon file and cache the updated data
            var fileData = await _http.GetStreamAsync(url);
            SaveLocalPackageStream(filename, fileData);
            fileData.Seek(0, SeekOrigin.Begin);

            _cached.FileETags[filename] = etag;
            SaveCachedPackage();

            return fileData;
        }

        private CachedLexiconPackage GetCachedPackage()
        {
            var path = Path.Join(_cachePath, _packageName, "package.yml");
            var raw = File.ReadAllText(path);
            return new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Deserialize<CachedLexiconPackage>(raw);
        }

        private void SaveCachedPackage()
        {
            var path = Path.Join(_cachePath, _packageName, "package.yml");
            var raw = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Serialize(_cached);
            File.WriteAllText(path, raw);
        }

        private Stream GetLocalPackageStream(string filename)
        {
            var data = new MemoryStream();
            var path = Path.Join(_cachePath, _packageName, filename);
            var localData = File.OpenRead(path);
            localData.CopyTo(data);
            data.Seek(0, SeekOrigin.Begin);
            return data;
        }

        private void SaveLocalPackageStream(string filename, Stream data)
        {
            var path = Path.Join(_cachePath, _packageName, filename);
            File.Delete(path);
            using var file = File.OpenWrite(path);
            data.CopyTo(file);
        }
    }
}