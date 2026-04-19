using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace ModBrowser
{
    [DataContract]
    internal sealed class ModCatalogDocument
    {
        [DataMember(Name = "title")]
        public string Title = null;

        [DataMember(Name = "mods")]
        public List<ModCatalogEntry> Mods = null;

        [DataMember(Name = "submissions")]
        public List<ModSubmissionEntry> Submissions = null;
    }

    [DataContract]
    internal sealed class ModCatalogEntry
    {
        [DataMember(Name = "id")]
        public string Id = null;

        [DataMember(Name = "name")]
        public string Name = null;

        [DataMember(Name = "author")]
        public string Author = null;

        [DataMember(Name = "version")]
        public string Version = null;

        [DataMember(Name = "description")]
        public string Description = null;

        [DataMember(Name = "dll_url")]
        public string DllUrl = null;

        [DataMember(Name = "page_url")]
        public string PageUrl = null;

        [DataMember(Name = "releases_url")]
        public string ReleasesUrl = null;

        [DataMember(Name = "preview_path")]
        public string PreviewPath = null;

        [DataMember(Name = "preview_url")]
        public string PreviewUrl = null;

        [DataMember(Name = "official")]
        public bool Official;

        [DataMember(Name = "dependencies")]
        public string[] Dependencies = null;

        [DataMember(Name = "requires")]
        public string[] Requires = null;
    }

    [DataContract]
    internal sealed class ModSubmissionEntry
    {
        [DataMember(Name = "id")]
        public string Id = null;

        [DataMember(Name = "name")]
        public string Name = null;

        [DataMember(Name = "author")]
        public string Author = null;

        [DataMember(Name = "version")]
        public string Version = null;

        [DataMember(Name = "description")]
        public string Description = null;

        [DataMember(Name = "dll_url")]
        public string DllUrl = null;

        [DataMember(Name = "page_url")]
        public string PageUrl = null;

        [DataMember(Name = "status")]
        public string Status = null;
    }

    [DataContract]
    internal sealed class CommunityIndexEntry
    {
        [DataMember(Name = "name")]
        public string Name = null;

        [DataMember(Name = "slug")]
        public string Slug = null;

        [DataMember(Name = "category")]
        public string Category = null;

        [DataMember(Name = "author")]
        public string Author = null;

        [DataMember(Name = "summary")]
        public string Summary = null;

        [DataMember(Name = "source_repo")]
        public string SourceRepo = null;

        [DataMember(Name = "releases_url")]
        public string ReleasesUrl = null;

        [DataMember(Name = "preview_path")]
        public string PreviewPath = null;

        [DataMember(Name = "dependencies")]
        public string[] Dependencies = null;

        [DataMember(Name = "requires")]
        public string[] Requires = null;
    }

    [DataContract]
    internal sealed class GitHubReleaseAsset
    {
        [DataMember(Name = "name")]
        public string Name = null;

        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl = null;
    }

    [DataContract]
    internal sealed class GitHubLatestRelease
    {
        [DataMember(Name = "tag_name")]
        public string TagName = null;

        [DataMember(Name = "assets")]
        public List<GitHubReleaseAsset> Assets = null;
    }

    [DataContract]
    internal sealed class GitHubReleaseInfo
    {
        [DataMember(Name = "tag_name")]
        public string TagName = null;

        [DataMember(Name = "assets")]
        public List<GitHubReleaseAsset> Assets = null;
    }

    [DataContract]
    internal sealed class GitHubTreeEntry
    {
        [DataMember(Name = "path")]
        public string Path = null;

        [DataMember(Name = "type")]
        public string Type = null;
    }

    [DataContract]
    internal sealed class GitHubTreeResponse
    {
        [DataMember(Name = "tree")]
        public List<GitHubTreeEntry> Tree = null;
    }

    internal static class GitHubCatalogService
    {
        private static bool _tlsInitialized;

        public static List<ModCatalogEntry> FetchCatalog(string catalogUrl, int timeoutSeconds, out string error)
        {
            error = null;
            try
            {
                EnsureModernTls();

                if (string.IsNullOrWhiteSpace(catalogUrl))
                {
                    error = "CatalogUrl is empty.";
                    return new List<ModCatalogEntry>();
                }

                string text;
                string effectiveUrl = catalogUrl;
                if (LooksLikeWebUrl(catalogUrl))
                {
                    string fetchError;
                    if (!TryFetchTextWithFallback(catalogUrl, timeoutSeconds, out text, out fetchError))
                    {
                        string fallbackUrl = TryGetCommunityBrowserFallbackUrl(catalogUrl);
                        if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
                            !string.Equals(fallbackUrl, catalogUrl, StringComparison.OrdinalIgnoreCase) &&
                            TryFetchTextWithFallback(fallbackUrl, timeoutSeconds, out text, out fetchError))
                        {
                            effectiveUrl = fallbackUrl;
                        }
                        else
                        {
                            error = fetchError;
                            return new List<ModCatalogEntry>();
                        }
                    }
                }
                else
                {
                    string path = catalogUrl;
                    if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        var u = new Uri(path);
                        path = u.LocalPath;
                    }

                    if (!File.Exists(path))
                    {
                        error = "Catalog file not found: " + path;
                        return new List<ModCatalogEntry>();
                    }

                    text = File.ReadAllText(path);
                }

                var parsedMods = ParseCatalogText(text, effectiveUrl);
                if (parsedMods.Count > 0)
                    return parsedMods;

                if (LooksLikeWebUrl(catalogUrl))
                {
                    string fallbackUrl = TryGetCommunityBrowserFallbackUrl(catalogUrl);
                    if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
                        !string.Equals(fallbackUrl, effectiveUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        string fallbackText;
                        string fallbackError;
                        if (TryFetchTextWithFallback(fallbackUrl, timeoutSeconds, out fallbackText, out fallbackError))
                        {
                            parsedMods = ParseCatalogText(fallbackText, fallbackUrl);
                            if (parsedMods.Count > 0)
                                return parsedMods;
                        }
                    }
                }

                if (LooksLikeHtmlDocument(text))
                    error = "Catalog loaded, but Index/mods.json returned no entries.";

                return new List<ModCatalogEntry>();
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return new List<ModCatalogEntry>();
            }
        }

        private static List<ModCatalogEntry> ParseCatalogText(string text, string sourceUrl)
        {
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(ModCatalogDocument));
                    var doc = ser.ReadObject(ms) as ModCatalogDocument;
                    var mods = doc != null ? BuildEffectiveMods(doc) : null;
                    if (mods != null && mods.Count > 0)
                        return mods;
                }
            }
            catch
            {
            }

            var indexMods = TryBuildFromCommunityIndex(text, sourceUrl);
            if (indexMods != null && indexMods.Count > 0)
                return indexMods;

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(ModCatalogDocument));
                    var doc = ser.ReadObject(ms) as ModCatalogDocument;
                    var mods = doc != null ? BuildEffectiveMods(doc) : null;
                    return mods ?? new List<ModCatalogEntry>();
                }
            }
            catch
            {
                return new List<ModCatalogEntry>();
            }
        }

        public static bool DownloadModDll(ModCatalogEntry entry, string modsRoot, int timeoutSeconds, out string detail)
        {
            detail = "";
            try
            {
                EnsureModernTls();

                if (entry == null)
                {
                    detail = "No mod selected.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.DllUrl))
                {
                    string resolved;
                    if (!TryResolveDllUrl(entry, timeoutSeconds, out resolved))
                    {
                        detail = "No direct DLL URL found for this entry. Add dll_url in catalog or publish a .dll in latest GitHub release.";
                        return false;
                    }
                    entry.DllUrl = resolved;
                }

                Directory.CreateDirectory(modsRoot);
                string fileName = Path.GetFileName(new Uri(entry.DllUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = SanitizeFileName((entry.Name ?? entry.Id ?? "Mod") + ".dll");
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    fileName += ".dll";

                string targetPath = Path.Combine(modsRoot, fileName);

                string dlError;
                if (!TryDownloadFileWithFallback(entry.DllUrl, targetPath, timeoutSeconds, out dlError))
                {
                    detail = dlError;
                    return false;
                }

                detail = "Installed to " + targetPath;
                return true;
            }
            catch (Exception ex)
            {
                detail = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool DownloadBinary(string url, int timeoutSeconds, out byte[] bytes, out string error)
        {
            bytes = null;
            error = null;

            try
            {
                EnsureModernTls();
                if (string.IsNullOrWhiteSpace(url))
                {
                    error = "URL is empty.";
                    return false;
                }

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = Math.Max(5000, timeoutSeconds * 1000);
                req.ReadWriteTimeout = req.Timeout;
                req.UserAgent = "CastleForge-ModBrowser/1.0";
                req.ProtocolVersion = HttpVersion.Version11;

                using (var res = (HttpWebResponse)req.GetResponse())
                using (var stream = res.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    bytes = ms.ToArray();
                    return bytes.Length > 0;
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Mod.dll";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            return value;
        }

        private static bool LooksLikeWebUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            Uri u;
            if (!Uri.TryCreate(value, UriKind.Absolute, out u))
                return false;

            return string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureModernTls()
        {
            if (_tlsInitialized)
                return;

            try
            {
                // .NET Framework runtime in older games can default to SSL3/TLS1.0.
                // Force TLS1.2 so HTTPS to GitHub works.
                const SecurityProtocolType tls12 = (SecurityProtocolType)3072;
                ServicePointManager.SecurityProtocol = tls12;
                ServicePointManager.Expect100Continue = false;
            }
            catch
            {
            }

            _tlsInitialized = true;
        }

        private static bool TryFetchTextWithFallback(string url, int timeoutSeconds, out string text, out string error)
        {
            text = "";
            error = null;

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = Math.Max(5000, timeoutSeconds * 1000);
                req.ReadWriteTimeout = req.Timeout;
                req.UserAgent = "CastleForge-ModBrowser/1.0";
                req.ProtocolVersion = HttpVersion.Version11;

                using (var res = (HttpWebResponse)req.GetResponse())
                using (var stream = res.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (!LooksLikeTlsChannelFailure(ex))
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }

                string psError;
                if (TryPowerShellFetchText(url, timeoutSeconds, out text, out psError))
                    return true;

                error = ex.GetType().Name + ": " + ex.Message + " | PS fallback failed: " + psError;
                return false;
            }
        }

        private static bool TryDownloadFileWithFallback(string url, string targetPath, int timeoutSeconds, out string error)
        {
            error = null;

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = Math.Max(5000, timeoutSeconds * 1000);
                req.ReadWriteTimeout = req.Timeout;
                req.UserAgent = "CastleForge-ModBrowser/1.0";
                req.ProtocolVersion = HttpVersion.Version11;

                using (var res = (HttpWebResponse)req.GetResponse())
                using (var input = res.GetResponseStream())
                using (var output = File.Create(targetPath))
                {
                    input.CopyTo(output);
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (!LooksLikeTlsChannelFailure(ex))
                {
                    error = ex.GetType().Name + ": " + ex.Message;
                    return false;
                }

                string psError;
                if (TryPowerShellDownloadFile(url, targetPath, timeoutSeconds, out psError))
                    return true;

                error = ex.GetType().Name + ": " + ex.Message + " | PS fallback failed: " + psError;
                return false;
            }
        }

        private static bool LooksLikeTlsChannelFailure(Exception ex)
        {
            if (ex == null)
                return false;

            string msg = ex.ToString();
            if (string.IsNullOrWhiteSpace(msg))
                return false;

            return msg.IndexOf("secure channel", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("SSL/TLS", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("TLS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryPowerShellFetchText(string url, int timeoutSeconds, out string text, out string error)
        {
            text = "";
            error = null;

            string u = EscapePowerShellSingleQuoted(url);
            string script =
                "$ProgressPreference='SilentlyContinue'; " +
                "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; " +
                "$r=Invoke-WebRequest -UseBasicParsing -Uri '" + u + "' -TimeoutSec " + Math.Max(5, timeoutSeconds) + "; " +
                "[Console]::OutputEncoding=[Text.Encoding]::UTF8; " +
                "[Console]::Write($r.Content)";

            return RunPowerShell(script, Math.Max(10000, timeoutSeconds * 1500), out text, out error);
        }

        private static bool TryPowerShellDownloadFile(string url, string targetPath, int timeoutSeconds, out string error)
        {
            error = null;

            string u = EscapePowerShellSingleQuoted(url);
            string p = EscapePowerShellSingleQuoted(targetPath);
            string script =
                "$ProgressPreference='SilentlyContinue'; " +
                "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; " +
                "Invoke-WebRequest -UseBasicParsing -Uri '" + u + "' -OutFile '" + p + "' -TimeoutSec " + Math.Max(5, timeoutSeconds) + "; " +
                "[Console]::Write('OK')";

            string stdout;
            if (!RunPowerShell(script, Math.Max(10000, timeoutSeconds * 1500), out stdout, out error))
                return false;

            return File.Exists(targetPath);
        }

        private static bool RunPowerShell(string script, int timeoutMs, out string stdout, out string error)
        {
            stdout = "";
            error = null;

            try
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script ?? ""));
                var psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        error = "Failed to start powershell.exe";
                        return false;
                    }

                    if (!p.WaitForExit(Math.Max(5000, timeoutMs)))
                    {
                        try { p.Kill(); } catch { }
                        error = "PowerShell timeout.";
                        return false;
                    }

                    stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();

                    if (p.ExitCode != 0)
                    {
                        error = "PowerShell exit " + p.ExitCode + ": " + (string.IsNullOrWhiteSpace(stderr) ? "unknown error" : stderr.Trim());
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string EscapePowerShellSingleQuoted(string value)
        {
            return (value ?? "").Replace("'", "''");
        }

        private static List<ModCatalogEntry> BuildEffectiveMods(ModCatalogDocument doc)
        {
            var result = new List<ModCatalogEntry>();

            if (doc != null && doc.Mods != null && doc.Mods.Count > 0)
            {
                for (int i = 0; i < doc.Mods.Count; i++)
                {
                    var m = doc.Mods[i];
                    if (m == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(m.DllUrl) && string.IsNullOrWhiteSpace(m.ReleasesUrl))
                        continue;
                    result.Add(m);
                }
                return result;
            }

            if (doc != null && doc.Submissions != null && doc.Submissions.Count > 0)
            {
                for (int i = 0; i < doc.Submissions.Count; i++)
                {
                    var s = doc.Submissions[i];
                    if (s == null)
                        continue;

                    string st = (s.Status ?? "").Trim();
                    bool approved =
                        string.Equals(st, "approved", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(st, "live", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(st, "published", StringComparison.OrdinalIgnoreCase);
                    if (!approved || string.IsNullOrWhiteSpace(s.DllUrl))
                        continue;

                    result.Add(new ModCatalogEntry
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Author = s.Author,
                        Version = s.Version,
                        Description = s.Description,
                        DllUrl = s.DllUrl,
                        PageUrl = s.PageUrl,
                        Official = false
                    });
                }
            }

            return result;
        }

        private static List<ModCatalogEntry> TryBuildFromCommunityIndex(string text, string catalogUrl)
        {
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(List<CommunityIndexEntry>));
                    var rows = ser.ReadObject(ms) as List<CommunityIndexEntry>;
                    if (rows == null || rows.Count == 0)
                        return new List<ModCatalogEntry>();

                    var result = new List<ModCatalogEntry>();
                    string baseRaw = TryGetRawBasePath(catalogUrl);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        if (r == null)
                            continue;
                        string cat = (r.Category ?? "").Trim();
                        string summary = r.Summary ?? "";
                        if (!string.IsNullOrWhiteSpace(cat))
                            summary = "[" + cat + "] " + summary;

                        result.Add(new ModCatalogEntry
                        {
                            Id = string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug,
                            Name = r.Name,
                            Author = r.Author,
                            Version = cat,
                            Description = summary,
                            DllUrl = "",
                            PageUrl = string.IsNullOrWhiteSpace(r.SourceRepo) ? r.ReleasesUrl : r.SourceRepo,
                            ReleasesUrl = r.ReleasesUrl,
                            PreviewPath = r.PreviewPath,
                            PreviewUrl = ResolveRelativeUrl(string.IsNullOrWhiteSpace(baseRaw) ? catalogUrl : baseRaw, r.PreviewPath),
                            Official = false,
                            Dependencies = r.Dependencies,
                            Requires = r.Requires
                        });
                    }

                    return result;
                }
            }
            catch
            {
                return new List<ModCatalogEntry>();
            }
        }

        private static string TryGetRawBasePath(string catalogUrl)
        {
            Uri u;
            if (!Uri.TryCreate(catalogUrl ?? "", UriKind.Absolute, out u))
                return null;

            if (!string.Equals(u.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
                return null;

            string path = (u.AbsolutePath ?? "").Trim('/');
            string[] parts = path.Split('/');
            if (parts.Length < 4)
                return null;

            // /owner/repo/branch/...
            return "https://raw.githubusercontent.com/" + parts[0] + "/" + parts[1] + "/" + parts[2] + "/";
        }

        private static string ResolvePreviewUrl(string rawBasePath, string previewPath)
        {
            string p = (previewPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p))
                return null;

            Uri absolute;
            if (Uri.TryCreate(p, UriKind.Absolute, out absolute))
                return p;

            if (string.IsNullOrWhiteSpace(rawBasePath))
                return null;

            return rawBasePath + p.TrimStart('/');
        }

        private static List<ModCatalogEntry> TryBuildFromCommunityRepoScan(string catalogUrl, int timeoutSeconds)
        {
            try
            {
                string ownerRepo;
                if (!TryGetGitHubOwnerRepoFromCatalogUrl(catalogUrl, out ownerRepo))
                    return new List<ModCatalogEntry>();

                string treeApi = "https://api.github.com/repos/" + ownerRepo + "/git/trees/main?recursive=1";
                string treeJson;
                string treeError;
                if (!TryFetchTextWithFallback(treeApi, timeoutSeconds, out treeJson, out treeError))
                    return new List<ModCatalogEntry>();

                GitHubTreeResponse tree;
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(treeJson ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(GitHubTreeResponse));
                    tree = ser.ReadObject(ms) as GitHubTreeResponse;
                }

                if (tree == null || tree.Tree == null || tree.Tree.Count == 0)
                    return new List<ModCatalogEntry>();

                var result = new List<ModCatalogEntry>();
                for (int i = 0; i < tree.Tree.Count; i++)
                {
                    var item = tree.Tree[i];
                    if (item == null || !string.Equals(item.Type, "blob", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string path = (item.Path ?? "").Trim();
                    if (!IsCommunityManifestPath(path))
                        continue;

                    string manifestUrl = BuildRawGitHubUrl(ownerRepo, path);
                    if (string.IsNullOrWhiteSpace(manifestUrl))
                        continue;

                    string manifestJson;
                    string manifestError;
                    if (!TryFetchTextWithFallback(manifestUrl, timeoutSeconds, out manifestJson, out manifestError))
                        continue;

                    CommunityIndexEntry row;
                    try
                    {
                        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(manifestJson ?? "")))
                        {
                            var ser = new DataContractJsonSerializer(typeof(CommunityIndexEntry));
                            row = ser.ReadObject(ms) as CommunityIndexEntry;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if (row == null || string.IsNullOrWhiteSpace(row.Name))
                        continue;

                    string category = NormalizeCategoryLabel(row.Category);
                    string summary = row.Summary ?? "";
                    if (!string.IsNullOrWhiteSpace(category))
                        summary = "[" + category + "] " + summary;

                    result.Add(new ModCatalogEntry
                    {
                        Id = string.IsNullOrWhiteSpace(row.Slug) ? SlugifyId(row.Name) : row.Slug,
                        Name = row.Name,
                        Author = row.Author,
                        Version = category,
                        Description = summary,
                        DllUrl = "",
                        PageUrl = string.IsNullOrWhiteSpace(row.SourceRepo) ? row.ReleasesUrl : row.SourceRepo,
                        ReleasesUrl = row.ReleasesUrl,
                        PreviewPath = row.PreviewPath,
                        PreviewUrl = BuildRawGitHubUrl(ownerRepo, row.PreviewPath),
                        Official = false
                    });
                }

                return result;
            }
            catch
            {
                return new List<ModCatalogEntry>();
            }
        }

        private static bool TryGetGitHubOwnerRepoFromCatalogUrl(string catalogUrl, out string ownerRepo)
        {
            ownerRepo = null;

            Uri u;
            if (!Uri.TryCreate(catalogUrl ?? "", UriKind.Absolute, out u))
                return false;

            if (string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = (u.AbsolutePath ?? "").Trim('/').Split('/');
                if (parts.Length < 2)
                    return false;

                ownerRepo = parts[0] + "/" + parts[1];
                return true;
            }

            if (u.Host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase))
            {
                string owner = u.Host.Substring(0, u.Host.Length - ".github.io".Length);
                string[] parts = (u.AbsolutePath ?? "").Trim('/').Split('/');
                if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
                    return false;

                ownerRepo = owner + "/" + parts[0];
                return true;
            }

            return false;
        }

        private static bool IsCommunityManifestPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/_template/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return Regex.IsMatch(
                normalized,
                @"^(Mods|TexturePacks|WeaponAddons)/[^/]+/mod\.json$",
                RegexOptions.IgnoreCase);
        }

        private static string BuildRawGitHubUrl(string ownerRepo, string repoPath)
        {
            if (string.IsNullOrWhiteSpace(ownerRepo) || string.IsNullOrWhiteSpace(repoPath))
                return null;

            string[] parts = ownerRepo.Split('/');
            if (parts.Length < 2)
                return null;

            return "https://raw.githubusercontent.com/" + parts[0] + "/" + parts[1] + "/main/" + repoPath.TrimStart('/');
        }

        private static List<ModCatalogEntry> TryBuildFromCommunityBrowserHtml(string text, string catalogUrl)
        {
            try
            {
                if (!LooksLikeHtmlDocument(text))
                    return new List<ModCatalogEntry>();

                var matches = Regex.Matches(
                    text ?? "",
                    "<article[^>]*class=\"card[^\"]*\"[^>]*data-category=\"(?<category>[^\"]+)\"[^>]*>(?<body>.*?)</article>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (matches.Count == 0)
                    return new List<ModCatalogEntry>();

                var result = new List<ModCatalogEntry>();
                for (int i = 0; i < matches.Count; i++)
                {
                    string category = HtmlDecode(matches[i].Groups["category"].Value);
                    string body = matches[i].Groups["body"].Value;
                    string name = ExtractHtmlValue(body, "<h2>(?<value>.*?)</h2>");
                    string author = ExtractHtmlValue(body, "<p[^>]*class=\"author\"[^>]*>\\s*By\\s*(?<value>.*?)\\s*</p>");
                    string summary = ExtractHtmlValue(body, "<p[^>]*class=\"author\"[^>]*>.*?</p>\\s*<p>(?<value>.*?)</p>");
                    if (string.IsNullOrWhiteSpace(summary))
                        summary = ExtractHtmlValue(body, "<p>(?<value>.*?)</p>");
                    string previewPath = ExtractHtmlValue(body, "<img[^>]*src=\"(?<value>[^\"]+)\"");

                    string readmeUrl = null;
                    string sourceUrl = null;
                    string releasesUrl = null;

                    var linkMatches = Regex.Matches(body, "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<label>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    for (int linkIndex = 0; linkIndex < linkMatches.Count; linkIndex++)
                    {
                        string href = HtmlDecode(linkMatches[linkIndex].Groups["href"].Value);
                        string label = StripHtml(linkMatches[linkIndex].Groups["label"].Value).Trim();
                        if (label.Equals("README", StringComparison.OrdinalIgnoreCase))
                            readmeUrl = ResolveRelativeUrl(catalogUrl, href);
                        else if (label.Equals("Source", StringComparison.OrdinalIgnoreCase))
                            sourceUrl = ResolveRelativeUrl(catalogUrl, href);
                        else if (label.Equals("Releases", StringComparison.OrdinalIgnoreCase))
                            releasesUrl = ResolveRelativeUrl(catalogUrl, href);
                    }

                    string displayCategory = NormalizeCategoryLabel(category);
                    string desc = string.IsNullOrWhiteSpace(summary) ? displayCategory : "[" + displayCategory + "] " + summary;

                    result.Add(new ModCatalogEntry
                    {
                        Id = SlugifyId(name),
                        Name = name,
                        Author = author,
                        Version = displayCategory,
                        Description = desc,
                        DllUrl = "",
                        PageUrl = string.IsNullOrWhiteSpace(sourceUrl) ? readmeUrl : sourceUrl,
                        ReleasesUrl = releasesUrl,
                        PreviewPath = previewPath,
                        PreviewUrl = ResolveRelativeUrl(catalogUrl, previewPath),
                        Official = false
                    });
                }

                return result;
            }
            catch
            {
                return new List<ModCatalogEntry>();
            }
        }

        private static bool LooksLikeHtmlDocument(string text)
        {
            string value = (text ?? "").TrimStart();
            return value.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        private static string TryGetCommunityBrowserFallbackUrl(string catalogUrl)
        {
            Uri u;
            if (!Uri.TryCreate(catalogUrl ?? "", UriKind.Absolute, out u))
                return null;

            if (string.Equals(u.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = (u.AbsolutePath ?? "").Trim('/').Split('/');
                if (parts.Length >= 2)
                    return "https://" + parts[0] + ".github.io/" + parts[1] + "/";
            }

            return null;
        }

        private static string ResolveRelativeUrl(string baseUrl, string relativeOrAbsolute)
        {
            string value = (relativeOrAbsolute ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return null;

            Uri absolute;
            if (Uri.TryCreate(value, UriKind.Absolute, out absolute))
                return value;

            Uri baseUri;
            if (!Uri.TryCreate(baseUrl ?? "", UriKind.Absolute, out baseUri))
                return null;

            return new Uri(baseUri, value).ToString();
        }

        private static string ExtractHtmlValue(string html, string pattern)
        {
            var match = Regex.Match(html ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                return "";

            return StripHtml(match.Groups["value"].Value).Trim();
        }

        private static string StripHtml(string value)
        {
            string decoded = HtmlDecode(value);
            decoded = Regex.Replace(decoded, "<.*?>", "");
            decoded = Regex.Replace(decoded, "\\s+", " ");
            return decoded.Trim();
        }

        private static string HtmlDecode(string value)
        {
            return WebUtility.HtmlDecode(value ?? "");
        }

        private static string NormalizeCategoryLabel(string category)
        {
            string value = (category ?? "").Trim().ToLowerInvariant();
            switch (value)
            {
                case "texture-pack":
                    return "texture-pack";
                case "weapon-addon":
                    return "weapon-addon";
                case "mod":
                    return "mod";
                default:
                    return string.IsNullOrWhiteSpace(value) ? "mod" : value;
            }
        }

        private static string SlugifyId(string value)
        {
            string raw = (value ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(raw))
                return Guid.NewGuid().ToString("N");

            var sb = new StringBuilder();
            bool lastDash = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                    lastDash = false;
                }
                else if (!lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }

            string slug = sb.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
        }

        private static bool TryResolveDllUrl(ModCatalogEntry entry, int timeoutSeconds, out string dllUrl)
        {
            dllUrl = null;

            if (entry == null)
                return false;

            // Fast path: releases_url/page_url may already be a direct downloadable asset.
            string direct = FirstDirectAssetUrl(entry.ReleasesUrl, entry.PageUrl, entry.DllUrl);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                dllUrl = direct;
                return true;
            }

            string repoUrl = ExtractRepoUrl(entry.ReleasesUrl);
            if (string.IsNullOrWhiteSpace(repoUrl))
                repoUrl = ExtractRepoUrl(entry.PageUrl);
            if (string.IsNullOrWhiteSpace(repoUrl))
                return false;

            string ownerRepo;
            if (!TryGetGitHubOwnerRepo(repoUrl, out ownerRepo))
                return false;

            string api = "https://api.github.com/repos/" + ownerRepo + "/releases/latest";
            string json;
            string err;
            if (!TryFetchTextWithFallback(api, timeoutSeconds, out json, out err))
                return false;

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(GitHubLatestRelease));
                    var rel = ser.ReadObject(ms) as GitHubLatestRelease;
                    if (rel == null || rel.Assets == null)
                        return false;

                    for (int i = 0; i < rel.Assets.Count; i++)
                    {
                        var a = rel.Assets[i];
                        if (a == null)
                            continue;
                        string name = (a.Name ?? "").Trim();
                        string url = (a.BrowserDownloadUrl ?? "").Trim();
                        if ((name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                             name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) &&
                            LooksLikeWebUrl(url))
                        {
                            dllUrl = url;
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string FirstDirectAssetUrl(params string[] candidates)
        {
            if (candidates == null)
                return null;

            for (int i = 0; i < candidates.Length; i++)
            {
                string raw = (candidates[i] ?? "").Trim();
                if (!LooksLikeWebUrl(raw))
                    continue;

                Uri uri;
                if (!Uri.TryCreate(raw, UriKind.Absolute, out uri))
                    continue;

                string path = (uri.AbsolutePath ?? "").Trim();
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return raw;
            }

            return null;
        }

        public static List<ModCatalogEntry> FetchOfficialReleaseCatalog(string officialReleasesUrl, int timeoutSeconds, out string error)
        {
            error = null;
            var result = new List<ModCatalogEntry>();
            try
            {
                EnsureModernTls();
                string ownerRepo;
                string tag;
                if (!TryParseGitHubReleaseUrl(officialReleasesUrl, out ownerRepo, out tag))
                {
                    error = "OfficialReleasesUrl is invalid.";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(tag))
                {
                    if (!TryResolveLatestModsTag(ownerRepo, timeoutSeconds, out tag, out error))
                        return result;
                }

                string api = "https://api.github.com/repos/" + ownerRepo + "/releases/tags/" + tag;
                string json;
                string fetchError;
                if (!TryFetchTextWithFallback(api, timeoutSeconds, out json, out fetchError))
                {
                    error = fetchError;
                    return result;
                }

                GitHubLatestRelease rel;
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(GitHubLatestRelease));
                    rel = ser.ReadObject(ms) as GitHubLatestRelease;
                }

                if (rel == null || rel.Assets == null)
                    return result;

                string version = string.IsNullOrWhiteSpace(rel.TagName) ? tag : rel.TagName;
                for (int i = 0; i < rel.Assets.Count; i++)
                {
                    var a = rel.Assets[i];
                    if (a == null)
                        continue;

                    string assetUrl = (a.BrowserDownloadUrl ?? "").Trim();
                    string assetName = (a.Name ?? "").Trim();
                    if (!LooksLikeWebUrl(assetUrl) || string.IsNullOrWhiteSpace(assetName))
                        continue;

                    if (!(assetName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                          assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string clean = Path.GetFileNameWithoutExtension(assetName);
                    result.Add(new ModCatalogEntry
                    {
                        Id = SlugifyId(clean),
                        Name = clean,
                        Author = "RussDev7",
                        Version = version,
                        Description = "Official CastleForge release asset",
                        DllUrl = assetUrl,
                        PageUrl = officialReleasesUrl,
                        ReleasesUrl = officialReleasesUrl,
                        Official = true
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return result;
            }
        }

        private static bool TryParseGitHubReleaseUrl(string url, out string ownerRepo, out string tag)
        {
            ownerRepo = null;
            tag = null;

            Uri u;
            if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out u))
                return false;
            if (!string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = (u.AbsolutePath ?? "").Trim('/').Split('/');
            if (parts.Length < 3)
                return false;
            if (!string.Equals(parts[2], "releases", StringComparison.OrdinalIgnoreCase))
                return false;

            ownerRepo = parts[0] + "/" + parts[1];
            if (parts.Length >= 5 &&
                string.Equals(parts[3], "tag", StringComparison.OrdinalIgnoreCase))
                tag = parts[4];

            return true;
        }

        private static bool TryResolveLatestModsTag(string ownerRepo, int timeoutSeconds, out string tag, out string error)
        {
            tag = null;
            error = null;
            try
            {
                string api = "https://api.github.com/repos/" + ownerRepo + "/releases";
                string json;
                string fetchError;
                if (!TryFetchTextWithFallback(api, timeoutSeconds, out json, out fetchError))
                {
                    error = fetchError;
                    return false;
                }

                List<GitHubReleaseInfo> releases;
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                {
                    var ser = new DataContractJsonSerializer(typeof(List<GitHubReleaseInfo>));
                    releases = ser.ReadObject(ms) as List<GitHubReleaseInfo>;
                }

                if (releases == null || releases.Count == 0)
                {
                    error = "No releases found.";
                    return false;
                }

                for (int i = 0; i < releases.Count; i++)
                {
                    string t = (releases[i] != null ? releases[i].TagName : null) ?? "";
                    t = t.Trim();
                    if (Regex.IsMatch(t, @"^mods-v\d+(\.\d+)*$", RegexOptions.IgnoreCase))
                    {
                        tag = t;
                        return true;
                    }
                }

                error = "No release tag matched mods-v###.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static string ExtractRepoUrl(string value)
        {
            string v = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v))
                return null;

            int idx = v.IndexOf("/releases", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                return v.Substring(0, idx);

            return v;
        }

        private static bool TryGetGitHubOwnerRepo(string repoUrl, out string ownerRepo)
        {
            ownerRepo = null;
            Uri u;
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out u))
                return false;
            if (!string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = (u.AbsolutePath ?? "").Trim('/').Split('/');
            if (parts.Length < 2)
                return false;

            ownerRepo = parts[0] + "/" + parts[1];
            return true;
        }
    }
}
