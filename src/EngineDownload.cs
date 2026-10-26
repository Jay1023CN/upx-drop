// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
namespace UpxDrop {
 public static class EngineDownload {
  public const string Version = "5.2.1";
  public const string Sha256 = "eabc6792a347d45e945be7748423e7868fd01b0d2bcaa2f4b1031fd71ff69bda";
  static string Hash(Stream stream) {
   using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
  }
  public static string InstallEmbedded() {
   return InstallEmbedded(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpxDrop", "engine"));
  }
  public static string InstallEmbedded(string cacheRoot) {
   using (var resource = typeof(EngineDownload).Assembly.GetManifestResourceStream("upx.zip")) {
    if (resource == null) throw new InvalidDataException("未找到内置 UPX，请重新下载软件。");
    using (var data = new MemoryStream()) {
     resource.CopyTo(data); data.Position = 0;
     if (Hash(data) != Sha256) throw new InvalidDataException("内置 UPX 校验失败，请重新下载软件。");
     data.Position = 0;
     using (var zip = new ZipArchive(data, ZipArchiveMode.Read)) {
      var entry = zip.GetEntry("upx-" + Version + "-win64/upx.exe");
      if (entry == null) throw new InvalidDataException("官方压缩包中缺少 upx.exe。");
      string expected; using (var input = entry.Open()) expected = Hash(input);
      string folder = Path.Combine(cacheRoot, Version, expected);
      Directory.CreateDirectory(folder); string path = Path.Combine(folder, "upx.exe");
      if (File.Exists(path)) using (var current = File.OpenRead(path)) { if (Hash(current) == expected) return path; }
      string temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
      try {
       entry.ExtractToFile(temp);
       using (var current = File.OpenRead(temp)) { if (Hash(current) != expected) throw new InvalidDataException("释放 UPX 后校验失败。"); }
       if (File.Exists(path)) File.Replace(temp, path, null);
       else {
        try { File.Move(temp, path); }
        catch (IOException) {
         if (!File.Exists(path)) throw;
         using (var current = File.OpenRead(path)) { if (Hash(current) != expected) throw; }
        }
       }
      } finally { if (File.Exists(temp)) File.Delete(temp); }
      return path;
     }
    }
   }
  }
 }
}
