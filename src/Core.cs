// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace UpxDrop {
 public sealed class PackResult { public string Status, Detail; public long Before, After; }
 public sealed class UpxRunner {
  readonly string engine;
  public UpxRunner(string enginePath) { engine = Path.GetFullPath(enginePath); }
  static string Hash(Stream stream) { stream.Position = 0; using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)); }
  public static bool IsSignedPE(Stream stream) {
   using (var r = new BinaryReader(stream, Encoding.UTF8, true)) {
    if (stream.Length < 64 || r.ReadUInt16() != 0x5a4d) throw new InvalidDataException("������Ч�� Windows PE �ļ���");
    stream.Position = 0x3c; long offset = r.ReadUInt32();
    if (offset > stream.Length - 24) throw new InvalidDataException("PE �ļ�ͷ��������");
    stream.Position = offset;
    if (r.ReadUInt32() != 0x00004550) throw new InvalidDataException("������Ч�� Windows PE �ļ���");
    stream.Position = offset + 20; int size = r.ReadUInt16(); long optional = offset + 24;
    if (size < 2 || optional + size > stream.Length) throw new InvalidDataException("PE optional header ��������");
    stream.Position = optional; int magic = r.ReadUInt16();
    int table = magic == 0x10b ? 96 : magic == 0x20b ? 112 : -1;
    if (table < 0 || size < table) throw new InvalidDataException("��֧�ֵ� PE �ļ�ͷ��");
    stream.Position = optional + table - 4;
    if (r.ReadUInt32() <= 4) return false;
    if (size < table + 40) throw new InvalidDataException("PE certificate table ��������");
    stream.Position = optional + table + 32;
    return r.ReadUInt32() != 0 | r.ReadUInt32() != 0;
   }
  }
  async Task<string> Run(string options, string path, CancellationToken token) {
   var info = new ProcessStartInfo(engine, options + " \"" + path + "\"") {
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(path)
   };
   info.EnvironmentVariables.Remove("UPX");
   using (var process = Process.Start(info)) {
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); var watch = Stopwatch.StartNew();
    while (!process.HasExited) {
     if (token.IsCancellationRequested || watch.Elapsed.TotalMinutes > 15) {
      try { process.Kill(); } catch (InvalidOperationException) { }
      process.WaitForExit(); token.ThrowIfCancellationRequested(); throw new TimeoutException("UPX ���� 15 ���ӣ�ԭ�ļ�δ�޸ġ�");
     }
     await Task.Delay(80).ConfigureAwait(false);
    }
    string output = (await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false));
    token.ThrowIfCancellationRequested();
    if (process.ExitCode != 0) throw new InvalidOperationException(output.Trim().Length == 0 ? "UPX �˳��룺" + process.ExitCode : output.Trim());
    return output.Trim();
   }
  }
  public async Task<PackResult> ProcessAsync(string file, string preset, bool decompress, CancellationToken token) {
   string path = Path.GetFullPath(file); string ext = Path.GetExtension(path).ToLowerInvariant();
   if (ext != ".exe" && ext != ".dll") throw new InvalidDataException("��֧�� EXE �� DLL��");
   if (!File.Exists(engine)) throw new FileNotFoundException("δ�ҵ��ٷ� UPX��������ѡ�� UPX����λ upx.exe��", engine);
   if (string.Equals(path, engine, StringComparison.OrdinalIgnoreCase) || string.Equals(path, System.Reflection.Assembly.GetEntryAssembly().Location, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("���ܴ�������ʹ�õĹ��߻� UPX ���档");
   if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly)) != 0) throw new InvalidOperationException("����ֻ���ļ���������ӡ�");
   string temp = Path.Combine(Path.GetDirectoryName(path), ".upxdrop-" + Guid.NewGuid().ToString("N") + ext);
   var result = new PackResult(); string originalHash;
   try {
    token.ThrowIfCancellationRequested();
    using (var original = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
     result.Before = original.Length;
     if (IsSignedPE(original)) { result.Status = "������"; result.Detail = "�ļ���������ǩ����ѹ�����ƻ�ǩ����"; result.After = result.Before; return result; }
     originalHash = Hash(original); original.Position = 0;
     using (var copy = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) original.CopyTo(copy);
    }
    string options = decompress ? "-d" : preset == "best" ? "--best" : preset == "fast" ? "-1" : "-7";
    result.Detail = await Run(options, temp, token).ConfigureAwait(false);
    if (!decompress) await Run("-t", temp, token).ConfigureAwait(false);
    result.After = new FileInfo(temp).Length;
    if (!decompress && result.After >= result.Before) { result.Status = "������"; result.Detail = "ѹ����û�б�С������ԭ�ļ���"; result.After = result.Before; return result; }
    using (var current = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
     if (Hash(current) != originalHash) throw new IOException("�����ڼ�ԭ�ļ��ѱ��޸ģ�ȡ���滻�����������ӡ�");
     token.ThrowIfCancellationRequested();
     File.Replace(temp, path, null);
    }
    result.Status = decompress ? "�ѽ�ѹ" : "��ѹ��"; return result;
   } finally { if (File.Exists(temp)) File.Delete(temp); }
  }
 }
}
