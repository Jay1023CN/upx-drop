// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace UpxDrop {
 public sealed class PackOptions {
  public string Preset = "balanced";
  public bool Backup, AllMethods, Force;
  public bool CompressExports = true;
  public bool StripRelocs;
  public string Overlay = "copy";
  public int CompressIcons = 2;
  public string Arguments(bool decompress) {
   if (decompress) return "-d";
   if (Overlay != "copy" && Overlay != "strip" && Overlay != "skip") throw new ArgumentException("无效的附加数据选项。");
   if (CompressIcons < 0 || CompressIcons > 3) throw new ArgumentException("无效的图标选项。");
   return (Preset == "best" ? "--best" : Preset == "fast" ? "-1" : "-7")
    + (AllMethods ? " --all-methods" : "") + (Force ? " --force" : "")
    + " --compress-exports=" + (CompressExports ? "1" : "0")
    + " --strip-relocs=" + (StripRelocs ? "1" : "0")
    + " --compress-icons=" + CompressIcons + " --overlay=" + Overlay;
  }
 }
 public sealed class PackResult { public string Status, Detail; public long Before, After; }
 public sealed class UpxRunner {
  readonly string engine;
  public UpxRunner(string enginePath) { engine = Path.GetFullPath(enginePath); }
  static string Hash(Stream stream) { stream.Position = 0; using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)); }
  public static bool IsSignedPE(Stream stream) {
   using (var r = new BinaryReader(stream, Encoding.UTF8, true)) {
    if (stream.Length < 64 || r.ReadUInt16() != 0x5a4d) throw new InvalidDataException("不是有效的 Windows PE 文件。");
    stream.Position = 0x3c; long offset = r.ReadUInt32();
    if (offset > stream.Length - 24) throw new InvalidDataException("PE 文件头越界。");
    stream.Position = offset;
    if (r.ReadUInt32() != 0x00004550) throw new InvalidDataException("不是有效的 Windows PE 文件。");
    stream.Position = offset + 20; int size = r.ReadUInt16(); long optional = offset + 24;
    if (size < 2 || optional + size > stream.Length) throw new InvalidDataException("PE optional header 越界。");
    stream.Position = optional; int magic = r.ReadUInt16();
    int table = magic == 0x10b ? 96 : magic == 0x20b ? 112 : -1;
    if (table < 0 || size < table) throw new InvalidDataException("不支持的 PE 文件头。");
    stream.Position = optional + table - 4;
    if (r.ReadUInt32() <= 4) return false;
    if (size < table + 40) throw new InvalidDataException("PE certificate table 越界。");
    stream.Position = optional + table + 32;
    return r.ReadUInt32() != 0 | r.ReadUInt32() != 0;
   }
  }
  async Task<string> Run(string options, string path, CancellationToken token) {
   // Native UPX argv uses the system code page. Keep its argument ASCII and
   // let CreateProcessW set the Unicode working directory containing the copy.
   var info = new ProcessStartInfo(engine, options + " \"" + Path.GetFileName(path) + "\"") {
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(path)
   };
   info.EnvironmentVariables.Remove("UPX");
   using (var process = Process.Start(info)) {
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); var watch = Stopwatch.StartNew();
    while (!process.HasExited) {
     if (token.IsCancellationRequested || watch.Elapsed.TotalMinutes > 15) {
      try { process.Kill(); } catch (InvalidOperationException) { }
      process.WaitForExit(); token.ThrowIfCancellationRequested(); throw new TimeoutException("UPX 超时 15 分钟，原文件未修改。");
     }
     await Task.Delay(80).ConfigureAwait(false);
    }
    string output = (await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false));
    token.ThrowIfCancellationRequested();
    if (process.ExitCode != 0) throw new InvalidOperationException(output.Trim().Length == 0 ? "UPX 退出码：" + process.ExitCode : output.Trim());
    return output.Trim();
   }
  }
  public Task<PackResult> ProcessAsync(string file, string preset, bool decompress, CancellationToken token) {
   return ProcessAsync(file, new PackOptions { Preset = preset }, decompress, token);
  }
  public async Task<PackResult> ProcessAsync(string file, PackOptions options, bool decompress, CancellationToken token) {
   if (options == null) throw new ArgumentNullException("options");
   string arguments = options.Arguments(decompress);
   string path = Path.GetFullPath(file); string ext = Path.GetExtension(path).ToLowerInvariant();
   if (ext != ".exe" && ext != ".dll") throw new InvalidDataException("仅支持 EXE 和 DLL。");
   if (!File.Exists(engine)) throw new FileNotFoundException("未找到官方 UPX 引擎。", engine);
   if (string.Equals(path, engine, StringComparison.OrdinalIgnoreCase) || string.Equals(path, System.Reflection.Assembly.GetEntryAssembly().Location, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("不能处理正在使用的工具或 UPX 引擎。");
   if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0) throw new InvalidOperationException("跳过只读文件或符号链接。");
   string temp = Path.Combine(Path.GetDirectoryName(path), ".upxdrop-" + Guid.NewGuid().ToString("N") + ext);
   var result = new PackResult(); string originalHash;
   try {
    token.ThrowIfCancellationRequested();
    using (var original = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
     result.Before = original.Length;
     if (IsSignedPE(original)) { result.Status = "已跳过"; result.Detail = "文件含有数字签名，压缩会破坏签名。"; result.After = result.Before; return result; }
     originalHash = Hash(original); original.Position = 0;
     using (var copy = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) original.CopyTo(copy);
    }
    result.Detail = await Run(arguments, temp, token).ConfigureAwait(false);
    if (!decompress) await Run("-t", temp, token).ConfigureAwait(false);
    result.After = new FileInfo(temp).Length;
    if (!decompress && result.After >= result.Before) { result.Status = "已跳过"; result.Detail = "压缩后没有变小，保留原文件。"; result.After = result.Before; return result; }
    using (var current = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
     if (Hash(current) != originalHash) throw new IOException("处理期间原文件已被修改，取消替换。");
     token.ThrowIfCancellationRequested();
     string backup = options.Backup ? path + ".upxdrop-" + Guid.NewGuid().ToString("N") + ".bak" : null;
     File.Replace(temp, path, backup);
     if (backup != null) result.Detail += Environment.NewLine + "备份：" + backup;
    }
    result.Status = decompress ? "已解压" : "已压缩"; return result;
   } finally { if (File.Exists(temp)) File.Delete(temp); }
  }
 }
}
