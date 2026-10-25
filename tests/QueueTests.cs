// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UpxDrop;
class QueueTests {
 static int passed;
 static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
 static int Main() {
  string root = Path.Combine(Path.GetTempPath(), "upx-queue-" + Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  try {
   string sub = Path.Combine(root, "中文 子目录"); Directory.CreateDirectory(sub);
   string first = Path.Combine(root, "sample.exe"); File.WriteAllText(first, "sample");
   File.WriteAllText(Path.Combine(sub, "helper.DLL"), "dll"); File.WriteAllText(Path.Combine(root, "ignore.txt"), "text");
   File.WriteAllText(Path.Combine(root, ".upxdrop-temp.exe"), "temp");
   var result = QueueTools.Scan(new[] { root, sub, first }, new string[0], 5000, CancellationToken.None);
   Check(result.Entries.Count == 2, "recursive scan deduplicates overlapping folders and ignores temporary/non-PE extensions");
   result = QueueTools.Scan(new[] { root }, new[] { first.ToUpperInvariant() }, 5000, CancellationToken.None);
   Check(result.Entries.Count == 1, "existing paths deduplicate without case sensitivity");
   result = QueueTools.Scan(new[] { root }, new string[0], 1, CancellationToken.None);
   Check(result.Entries.Count == 1 && result.LimitReached, "capacity limits imported files");
   result = QueueTools.Scan(new[] { Path.Combine(root, "gone.exe"), first }, new string[0], 5, CancellationToken.None);
   Check(result.Entries.Count == 1 && result.Unreadable == 1, "missing item does not discard subsequent files");
   using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); bool cancelled = false; try { QueueTools.Scan(new[] { root }, new string[0], 5, cancel.Token); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "scan observes cancellation"); }
   var e = new Entry { Path = first, BeforeBytes = 8192, AfterBytes = 1024, Status = "失败", Detail = "old", Operation = "压缩" };
   Check(QueueTools.Matches(e, "SAMPLE", 3) && !QueueTools.Matches(e, "missing", 3) && !QueueTools.Matches(e, "", 2), "search and status filters combine");
   e.Retry(); Check(e.Status == "等待中" && e.AfterBytes == null && e.Detail == "" && e.Operation == "—", "retry clears stale outcome and preserves original size");
   e.Status = "已压缩"; e.AfterBytes = 1024; e.Retry(); Check(e.Status == "已压缩" && e.AfterBytes == 1024, "retry excludes completed files");
   e.Path = Path.Combine(root, "=formula,.exe"); e.Detail = " =SUM(1,2)\r\nline \"two\"";
   var csv = QueueTools.Csv(new[] { e });
   Check(csv.Contains("\"'=formula,.exe\"") && csv.Contains("\"' =SUM(1,2)\r\nline \"\"two\"\"\""), "CSV quotes commas/newlines/quotes and neutralizes spreadsheet formulas");
   Check(csv.Contains(",8192,1024,"), "CSV exports exact byte counts");
   Console.WriteLine(passed + " queue checks passed"); return 0;
  } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally { Directory.Delete(root, true); }
 }
}
