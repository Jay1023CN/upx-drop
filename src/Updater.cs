// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace UpxDrop {
 public sealed class PendingUpdate { public string Version, Path, Sha256; }
 public sealed class ReleaseAsset { public string Version, Url, ChecksumUrl, Digest; public long Size; }
 public static class Updater {
  public const string RepositoryUrl = "https://github.com/Jay1023CN/upx-drop";
  public const string CurrentVersion = "1.1.0";
  const string AssetName = "UpxDrop.exe";
  sealed class Client : WebClient {
   public Client() { Headers["User-Agent"] = "UpxDrop/" + CurrentVersion; Headers["Accept"] = "application/vnd.github+json"; }
   protected override WebRequest GetWebRequest(Uri address) { var request = base.GetWebRequest(address); request.Timeout = 20000; var http = request as HttpWebRequest; if (http != null) http.ReadWriteTimeout = 20000; return request; }
  }
  static string StringValue(IDictionary<string, object> value, string key) { object item; return value.TryGetValue(key, out item) ? item as string : null; }
  static string AssetUrl(string value) {
   Uri uri;
   if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith("/Jay1023CN/upx-drop/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("更新下载地址不属于项目的 GitHub Releases。");
   return value;
  }
  public static ReleaseAsset ParseRelease(string json) {
   var release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
   if (release == null) throw new InvalidDataException("无效的 GitHub Release 信息。");
   object flag;
   if ((release.TryGetValue("draft", out flag) && object.Equals(flag, true)) || (release.TryGetValue("prerelease", out flag) && object.Equals(flag, true))) return null;
   string tag = StringValue(release, "tag_name"); Version latest;
   if (string.IsNullOrEmpty(tag) || !Version.TryParse(tag.TrimStart('v', 'V'), out latest)) throw new InvalidDataException("Release 标签必须使用 v1.1.0 这样的版本号。");
   if (latest <= new Version(CurrentVersion)) return null;
   object raw;
   if (!release.TryGetValue("assets", out raw) || !(raw is IEnumerable)) throw new InvalidDataException("Release 缺少更新附件。");
   var result = new ReleaseAsset { Version = latest.ToString() };
   foreach (var value in (IEnumerable)raw) {
    var asset = value as IDictionary<string, object>; if (asset == null) continue;
    string name = StringValue(asset, "name");
    if (name == AssetName) {
     result.Url = AssetUrl(StringValue(asset, "browser_download_url"));
     object size; if (!asset.TryGetValue("size", out size)) throw new InvalidDataException("缺少更新包大小。");
     result.Size = Convert.ToInt64(size);
     if (result.Size < 2 || result.Size > 64 * 1024 * 1024) throw new InvalidDataException("更新包大小异常。");
     string digest = StringValue(asset, "digest");
     if (digest != null && Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) result.Digest = digest.Substring(7).ToLowerInvariant();
    } else if (name == AssetName + ".sha256") result.ChecksumUrl = AssetUrl(StringValue(asset, "browser_download_url"));
   }
   if (result.Url == null || (result.Digest == null && result.ChecksumUrl == null)) throw new InvalidDataException("最新版缺少 UpxDrop.exe 或 SHA-256 校验信息。");
   return result;
  }
  public static string HashFile(string path) { using (var input = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant(); }
  static async Task WithTimeout(Client client, Task task, int milliseconds) {
   if (await Task.WhenAny(task, Task.Delay(milliseconds)) != task) {
    client.CancelAsync(); try { await task; } catch { }
    throw new TimeoutException("GitHub 更新请求超时，当前版本未改变。");
   }
   await task;
  }
  static async Task<T> WithTimeout<T>(Client client, Task<T> task, int milliseconds) {
   await WithTimeout(client, (Task)task, milliseconds); return await task;
  }
  public static async Task<PendingUpdate> DownloadLatestAsync(CancellationToken token = default(CancellationToken)) {
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   using (var client = new Client()) using (token.Register(client.CancelAsync)) {
    token.ThrowIfCancellationRequested();
    string json;
    try { json = await WithTimeout(client, client.DownloadStringTaskAsync("https://api.github.com/repos/Jay1023CN/upx-drop/releases/latest"), 30000); }
    catch (WebException ex) { var response = ex.Response as HttpWebResponse; if (response != null && response.StatusCode == HttpStatusCode.NotFound) { response.Dispose(); return null; } throw; }
    var asset = ParseRelease(json);
    if (asset == null) return null;
    string expected = asset.Digest;
    if (expected == null) {
     string checksum = await WithTimeout(client, client.DownloadStringTaskAsync(asset.ChecksumUrl), 30000);
     var match = Regex.Match(checksum.Trim(), "^([a-fA-F0-9]{64})[ \\t]+\\*?UpxDrop\\.exe$");
     if (!match.Success) throw new InvalidDataException("更新校验文件格式无效。");
     expected = match.Groups[1].Value.ToLowerInvariant();
    }
    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpxDrop", "updates");
    Directory.CreateDirectory(folder); string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".exe");
    try {
     token.ThrowIfCancellationRequested();
     await WithTimeout(client, client.DownloadFileTaskAsync(asset.Url, path), 120000);
     token.ThrowIfCancellationRequested();
     if (new FileInfo(path).Length != asset.Size || HashFile(path) != expected) throw new InvalidDataException("更新包的大小或 SHA-256 校验失败，已取消更新。");
     using (var input = File.OpenRead(path)) if (input.ReadByte() != 'M' || input.ReadByte() != 'Z') throw new InvalidDataException("更新包不是 Windows EXE。");
     return new PendingUpdate { Version = asset.Version, Path = path, Sha256 = expected };
    } catch { if (File.Exists(path)) File.Delete(path); throw; }
   }
  }
  static string Quote(string value) { return "'" + value.Replace("'", "''") + "'"; }
  public static void ApplyAfterExit(PendingUpdate update) {
   if (HashFile(update.Path) != update.Sha256) throw new InvalidDataException("待安装的更新已改变，取消更新。");
   string target = System.Reflection.Assembly.GetEntryAssembly().Location;
   string replacement = target + "." + Guid.NewGuid().ToString("N") + ".new";
   string backup = replacement + ".bak";
   string log = Path.Combine(Path.GetDirectoryName(update.Path), "last-update.log");
   string previousHash = HashFile(target);
   File.Copy(update.Path, replacement, false);
   try {
    if (HashFile(replacement) != update.Sha256) throw new InvalidDataException("更新副本校验失败。");
    string script = "$ErrorActionPreference='Stop'; $target=" + Quote(target) + "; $new=" + Quote(replacement) + "; $backup=" + Quote(backup) + "; $stage=" + Quote(update.Path) + "; try { "
     + "Wait-Process -Id " + Process.GetCurrentProcess().Id + " -Timeout 120 -ErrorAction SilentlyContinue; "
     + "if (Get-Process -Id " + Process.GetCurrentProcess().Id + " -ErrorAction SilentlyContinue) { throw 'Application did not exit' }; "
     + "if ((Get-FileHash -LiteralPath $new -Algorithm SHA256).Hash.ToLowerInvariant() -ne " + Quote(update.Sha256) + ") { throw 'SHA-256 mismatch' }; "
     + "if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne " + Quote(previousHash) + ") { throw 'Application changed during update' }; "
     + "[IO.File]::Replace($new,$target,$backup); Start-Process -FilePath $target; Remove-Item -LiteralPath $backup,$stage -Force -ErrorAction SilentlyContinue "
     + "} catch { if (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $target -Force }; $_ | Out-String | Set-Content -LiteralPath " + Quote(log) + "; } finally { if (Test-Path -LiteralPath $new) { Remove-Item -LiteralPath $new -Force } }";
    string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
    Process.Start(new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) { UseShellExecute = false, CreateNoWindow = true });
   } catch { if (File.Exists(replacement)) File.Delete(replacement); throw; }
  }
 }
}
