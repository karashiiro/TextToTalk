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

        private readonly CachedLexiconPackage cached;
        private readonly string cachePath;
        private readonly HttpClient http;
        private readonly string packageName;

        public LexiconPackage(HttpClient http, string packageName, string cachePath)
        {
            this.cachePath = cachePath;
            this.http = http;
            this.packageName = packageName;

            this.cached = GetCachedPackage();
        }

        public async Task<Stream> GetPackageFile(string filename)
        {
            var url = new Uri(RepoBase + this.packageName + filename);
            
            // Check the file etag
            var res = await this.http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
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
            if (this.cached.FileETags.TryGetValue(filename, out var cachedETag) && etag == cachedETag)
            {
                return GetLocalPackageStream(filename);
            }

            // Download the updated lexicon file and cache the updated data
            var fileData = await this.http.GetStreamAsync(url);
            SaveLocalPackageStream(filename, fileData);
            fileData.Seek(0, SeekOrigin.Begin);

            this.cached.FileETags[filename] = etag;
            SaveCachedPackage();

            return fileData;
        }

        private CachedLexiconPackage GetCachedPackage()
        {
            var path = GetCachedFilePath("package.yml");
            var raw = File.ReadAllText(path);
            return new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Deserialize<CachedLexiconPackage>(raw);
        }

        private void SaveCachedPackage()
        {
            var path = GetCachedFilePath("package.yml");
            var raw = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Serialize(this.cached);
            File.WriteAllText(path, raw);
        }

        private Stream GetLocalPackageStream(string filename)
        {
            var data = new MemoryStream();
            var path = GetCachedFilePath(filename);
            var localData = File.OpenRead(path);
            localData.CopyTo(data);
            data.Seek(0, SeekOrigin.Begin);
            return data;
        }

        private void SaveLocalPackageStream(string filename, Stream data)
        {
            var path = GetCachedFilePath(filename);
            File.Delete(path);
            using var file = File.OpenWrite(path);
            data.CopyTo(file);
        }

        private string GetCachedFilePath(string filename)
        {
            return Path.Join(this.cachePath, this.packageName, filename);
        }
    }
}