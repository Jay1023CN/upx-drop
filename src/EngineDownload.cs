// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace UpxDrop {
 public static class EngineDownload {
  public const string Version = "5.2.1";
  public const string Url = "https://github.com/upx/upx/releases/download/v5.2.1/upx-5.2.1-win64.zip";
  public const string Sha256 = "eabc6792a347d45e945be7748423e7868fd01b0d2bcaa2f4b1031fd71ff69bda";
  public static async Task<string> InstallAsync(string folder) {
   Directory.CreateDirectory(folder);
   string archive = Path.Combine(folder, ".download-" + Guid.NewGuid().ToString("N") + ".zip");
   try {
    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    using (var client = new WebClient()) { client.Headers["User-Agent"] = "UpxDrop/1.0"; await client.DownloadFileTaskAsync(new Uri(Url), archive); }
    using (var file = File.OpenRead(archive)) using (var sha = SHA256.Create()) {
     if (BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() != Sha256) throw new InvalidDataException("�ٷ� UPX ����У��ʧ�ܣ����������ء�");
    }
    using (var zip = ZipFile.OpenRead(archive)) {
     foreach (string name in new[] { "upx.exe", "LICENSE", "COPYING" }) {
      var entry = zip.GetEntry("upx-" + Version + "-win64/" + name);
      if (entry == null) throw new InvalidDataException("UPX �ٷ�ѹ����ȱ�� " + name);
      entry.ExtractToFile(Path.Combine(folder, name == "LICENSE" ? "UPX-LICENSE" : name), true);
     }
    }
    return Path.Combine(folder, "upx.exe");
   } finally { if (File.Exists(archive)) File.Delete(archive); }
  }
 }
}
