// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace UpxDrop {
 public sealed class EntryCollection : ObservableCollection<Entry> {
  public void AddRange(IEnumerable<Entry> entries) {
   CheckReentrancy();
   foreach (var entry in entries) Items.Add(entry);
   OnPropertyChanged(new PropertyChangedEventArgs("Count"));
   OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
   OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
  }
 }
 public sealed class Entry : INotifyPropertyChanged {
  public string Path { get; set; }
  public string Name { get { return System.IO.Path.GetFileName(Path); } }
  public string Folder { get { return System.IO.Path.GetDirectoryName(Path); } }
  string status = "等待中";
  public string Detail = "", Operation = "—";
  public long BeforeBytes;
  public long? AfterBytes;
  public string Status { get { return status; } set { status = value; Changed("Status"); } }
  public string Before { get { return QueueTools.Size(BeforeBytes); } }
  public string After { get { return AfterBytes.HasValue ? QueueTools.Size(AfterBytes.Value) : "—"; } }
  public string Saving { get { return !AfterBytes.HasValue || BeforeBytes == 0 ? "—" : ((BeforeBytes - AfterBytes.Value) * 100.0 / BeforeBytes).ToString("0.0") + "%"; } }
  public bool CanRetry { get { return Status == "失败" || Status == "已取消"; } }
  public bool Complete { get { return Status == "已压缩" || Status == "已解压" || Status == "已跳过"; } }
  public void Refresh() { Changed("Before"); Changed("After"); Changed("Saving"); }
  public void Retry() { if (!CanRetry) return; Detail = ""; AfterBytes = null; Operation = "—"; Status = "等待中"; Refresh(); }
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
 }
 public sealed class ScanResult {
  public readonly List<Entry> Entries = new List<Entry>();
  public int Unreadable;
  public bool LimitReached;
 }
 public static class QueueTools {
  public static string Size(long bytes) {
   if (bytes >= 1073741824) return (bytes / 1073741824.0).ToString("0.00") + " GB";
   if (bytes >= 1048576) return (bytes / 1048576.0).ToString("0.00") + " MB";
   return bytes < 1024 ? bytes + " B" : (bytes / 1024.0).ToString("0.0") + " KB";
  }
  public static bool Matches(Entry entry, string search, int filter) {
   bool status = filter == 0 || (filter == 1 && (entry.Status == "等待中" || entry.Status == "处理中")) || (filter == 2 && entry.Complete) || (filter == 3 && entry.CanRetry);
   return status && (string.IsNullOrWhiteSpace(search) || entry.Path.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
  }
  public static ScanResult Scan(IEnumerable<string> paths, IEnumerable<string> existing, int capacity, CancellationToken token) {
   var result = new ScanResult(); var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
   var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var pending = new Stack<string>(paths.Reverse());
   while (pending.Count > 0) {
    token.ThrowIfCancellationRequested();
    string path = pending.Pop();
    try {
     path = System.IO.Path.GetFullPath(path);
     var attributes = File.GetAttributes(path);
     if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
     if ((attributes & FileAttributes.Directory) != 0) {
      if (!visited.Add(path)) continue;
      foreach (var child in Directory.EnumerateFileSystemEntries(path)) { token.ThrowIfCancellationRequested(); pending.Push(child); }
      continue;
     }
     string ext = System.IO.Path.GetExtension(path);
     if ((!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)) || System.IO.Path.GetFileName(path).StartsWith(".upxdrop-", StringComparison.OrdinalIgnoreCase) || !known.Add(path)) continue;
     if (result.Entries.Count >= capacity) { result.LimitReached = true; break; }
     result.Entries.Add(new Entry { Path = path, BeforeBytes = new FileInfo(path).Length });
    } catch (IOException) { result.Unreadable++; }
    catch (UnauthorizedAccessException) { result.Unreadable++; }
    catch (ArgumentException) { result.Unreadable++; }
    catch (NotSupportedException) { result.Unreadable++; }
   }
   return result;
  }
  static string Cell(string value) {
   value = value ?? "";
   // Keep file names and engine output as text when opened by a spreadsheet.
   string trimmed = value.TrimStart();
   if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) value = "'" + value;
   return "\"" + value.Replace("\"", "\"\"") + "\"";
  }
  public static string Csv(IEnumerable<Entry> entries) {
   var output = new StringBuilder("文件名称,完整路径,操作,状态,原大小（字节）,处理后（字节）,体积变化,详情\r\n");
   foreach (var e in entries) output.AppendLine(string.Join(",", new[] { Cell(e.Name), Cell(e.Path), Cell(e.Operation), Cell(e.Status), e.BeforeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), e.AfterBytes.HasValue ? e.AfterBytes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "", Cell(e.Saving), Cell(e.Detail) }));
   return output.ToString();
  }
 }
}
