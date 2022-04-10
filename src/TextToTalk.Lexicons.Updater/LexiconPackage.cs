using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TextToTalk.Lexicons.Updater
{
    public class LexiconPackage
    {
        private const string RepoBase = "https://raw.githubusercontent.com/karashiiro/TextToTalk/main/lexicons/";

        private readonly string cachePath;
        private readonly HttpClient http;

        private CachedLexicon cache;

        public string PackageName { get; }

        /// <summary>
        /// Create a lexicon package instance. No data will actually be loaded until it is requested.
        /// </summary>
        /// <param name="http"></param>
        /// <param name="packageName">The name of the lexicon package's folder in the repo.</param>
        /// <param name="cachePath">The cache folder. Leave this blank to skip the cache.</param>
        internal LexiconPackage(HttpClient http, string packageName, string cachePath)
        {
            this.cachePath = cachePath;
            this.http = http;

            PackageName = packageName;
        }

        public async Task<LexiconPackageInfo> GetPackageInfo()
        {
            // This can be disposed once we've read its data
            await using var data = await GetPackageFile("package.yml");
            using var sr = new StreamReader(data);
            var infoRaw = await sr.ReadToEndAsync();
            return ParsePackageInfo(infoRaw);
        }

        public LexiconPackageInfo GetPackageInfoLocal()
        {
            var filePath = GetCacheFilePath("package.yml");
            if (!File.Exists(filePath)) return null;

            // This can be disposed once we've read its data
            using var data = File.OpenRead(filePath);
            using var sr = new StreamReader(data);
            var infoRaw = sr.ReadToEnd();
            return ParsePackageInfo(infoRaw);
        }

        private LexiconPackageInfo ParsePackageInfo(string data)
        {
            var info = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()
                .Deserialize<LexiconPackageInfo>(data);
            info.InternalName = PackageName;
            return info;
        }

        public bool IsInstalled()
        {
            // A package is considered installed if at least one of its lexicon files has been downloaded.
            var info = GetPackageInfoLocal();
            if (info == null) return false;

            foreach (var file in info.Files)
            {
                var localPath = GetCacheFilePath(file);
                if (File.Exists(localPath))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> HasUpdate()
        {
            if (!IsInstalled()) return false;

            var packageInfo = await GetPackageInfo();
            foreach (var file in packageInfo.Files)
            {
                if (await HasUpdate(file))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> HasUpdate(string filename)
        {
            // Get the package metadata
            this.cache ??= GetCacheInfo();

            // Get the file etag
            var url = new Uri(RepoBase + PackageName + "/" + filename);

            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = await this.http.SendAsync(req);
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
            if (this.cache.FileETags.TryGetValue(filename, out var cachedETag) && etag == cachedETag)
            {
                return false;
            }

            // Update our cache info
            this.cache.FileETags[filename] = etag;
            SaveCacheInfo();

            return true;
        }

        public async Task<Stream> GetPackageFile(string filename)
        {
            // Check if the remote file matches our local file
            if (!await HasUpdate(filename))
            {
                if (TryGetLocalPackageStream(filename, out var localData))
                {
                    return localData;
                }
            }

            // Download the updated lexicon file and cache the updated data
            var url = new Uri(RepoBase + PackageName + "/" + filename);
            await using var fileData = await this.http.GetStreamAsync(url);

            // We can't seek on an HTTP data stream, so we need to copy the data to a second stream
            var fileDataCopy = new MemoryStream();
            await fileData.CopyToAsync(fileDataCopy);
            fileDataCopy.Seek(0, SeekOrigin.Begin);

            // Save the stream data
            SaveLocalPackageStream(filename, fileDataCopy);
            fileDataCopy.Seek(0, SeekOrigin.Begin);

            return fileDataCopy;
        }

        public Stream GetPackageFileLocal(string filename)
        {
            return TryGetLocalPackageStream(filename, out var localData) ? localData : null;
        }

        public void Delete()
        {
            var dir = Path.Join(this.cachePath, PackageName);
            if (!Directory.Exists(dir)) return;
            Directory.Delete(dir, true);
        }

        private bool TryGetLocalPackageStream(string filename, out Stream stream)
        {
            if (!CanCache())
            {
                stream = Stream.Null;
                return false;
            }

            var data = new MemoryStream();
            var path = GetCacheFilePath(filename);
            if (!File.Exists(path))
            {
                stream = Stream.Null;
                return false;
            }

            using var localData = File.OpenRead(path);
            localData.CopyTo(data);
            data.Seek(0, SeekOrigin.Begin);
            stream = data;
            return true;
        }

        private void SaveLocalPackageStream(string filename, Stream data)
        {
            if (!CanCache()) return;

            var path = GetCacheFilePath(filename);
            var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Directory is null.");
            Directory.CreateDirectory(dir);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var file = File.OpenWrite(path);
            data.CopyTo(file);
        }

        private CachedLexicon GetCacheInfo()
        {
            if (!CanCache()) return new CachedLexicon();

            var path = GetCacheFilePath("update.json");
            if (!File.Exists(path))
            {
                return new CachedLexicon();
            }

            var data = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<CachedLexicon>(data);
        }

        private void SaveCacheInfo()
        {
            if (!CanCache()) return;

            var path = GetCacheFilePath("update.json");
            var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Directory is null.");
            Directory.CreateDirectory(dir);
            var data = JsonConvert.SerializeObject(this.cache);
            File.WriteAllText(path, data);
        }

        private string GetCacheFilePath(string filename)
        {
            return Path.Join(this.cachePath, PackageName, filename);
        }

        private bool CanCache()
        {
            return !string.IsNullOrEmpty(this.cachePath);
        }

        public static string GetInternalName(string metadataFilePath)
        {
            // ReSharper disable CommentTypo
            // lexicons/something/package.yml -> lmy.egakcap/gnihtemos/snocixel
            var reversed = metadataFilePath.Reverse();
            // lmy.egakcap/gnihtemos/snocixel -> gnihtemos/snocixel
            var skippedFile = reversed.SkipWhile(c => c != '/').Skip(1);
            // gnihtemos/snocixel -> gnihtemos
            var takenName = skippedFile.TakeWhile(c => c != '/');
            // gnihtemos -> something
            var internalName = takenName.Reverse();
            // ReSharper restore CommentTypo
            return string.Concat(internalName);
        }
    }
}