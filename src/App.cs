// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace UpxDrop {
 public sealed class Entry : INotifyPropertyChanged {
  public string Path { get; set; }
  public string Name { get { return System.IO.Path.GetFileName(Path); } }
  string status = "等待中", before = "—", after = "—", saving = "—";
  public string Detail = "";
  public string Status { get { return status; } set { status = value; Changed("Status"); } }
  public string Before { get { return before; } set { before = value; Changed("Before"); } }
  public string After { get { return after; } set { after = value; Changed("After"); } }
  public string Saving { get { return saving; } set { saving = value; Changed("Saving"); } }
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
 }
 public class Program {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
  static void UseDarkTitleBar(Window target) {
   try {
    int enabled = 1; IntPtr handle = new WindowInteropHelper(target).Handle;
    if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0) DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
   } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
  }
  Window window; ListView files; TextBox details; TextBlock summary, engineLabel;
  ComboBox preset, mode, overlay, icons; CheckBox auto, backup, allMethods, exports, relocs, force, autoUpdate; FrameworkElement advanced; Button start, stop, engineButton, downloadButton, updateButton, retry; ProgressBar queueProgress; TextBlock queueProgressText;
  ObservableCollection<Entry> entries = new ObservableCollection<Entry>();
  CancellationTokenSource cancellation, updateCancellation; bool busy, closeRequested, updateChecking; string engine, embeddedEngine; PendingUpdate pendingUpdate;
  static string SettingFile { get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpxDrop", "settings.txt"); } }
  T Find<T>(string name) where T : class { return window.FindName(name) as T; }
  [STAThread] public static void Main(string[] args) {
   var application = new Application(); var program = new Program();
   try { program.Initialize(); } catch (Exception ex) { MessageBox.Show(ex.Message, "UPX Drop 启动失败"); return; }
   program.window.Loaded += async delegate { program.Add(args.Where(a => a != "--no-update-check").ToArray()); if (program.autoUpdate.IsChecked == true && !args.Contains("--no-update-check")) await program.CheckUpdates(false); }; application.Run(program.window);
  }
  void Initialize() {
   using (var stream = typeof(Program).Assembly.GetManifestResourceStream("MainWindow.xaml")) window = (Window)XamlReader.Load(stream);
   window.SourceInitialized += delegate { UseDarkTitleBar(window); };
   files = Find<ListView>("Files"); details = Find<TextBox>("Details"); summary = Find<TextBlock>("Summary"); engineLabel = Find<TextBlock>("EngineLabel");
   preset = Find<ComboBox>("Preset"); mode = Find<ComboBox>("Mode"); auto = Find<CheckBox>("AutoStart");
   start = Find<Button>("StartButton"); stop = Find<Button>("StopButton"); engineButton = Find<Button>("EngineButton"); downloadButton = Find<Button>("DownloadButton");
   advanced = Find<FrameworkElement>("AdvancedOptions"); overlay = Find<ComboBox>("Overlay"); icons = Find<ComboBox>("Icons");
   backup = Find<CheckBox>("Backup"); allMethods = Find<CheckBox>("AllMethods"); exports = Find<CheckBox>("CompressExports"); relocs = Find<CheckBox>("StripRelocs"); force = Find<CheckBox>("Force"); autoUpdate = Find<CheckBox>("AutoUpdate"); updateButton = Find<Button>("UpdateButton");
   retry = Find<Button>("RetryButton"); queueProgress = Find<ProgressBar>("QueueProgress"); queueProgressText = Find<TextBlock>("QueueProgressText");
   Find<TextBlock>("VersionLabel").Text = "v" + Updater.CurrentVersion;
   engine = embeddedEngine = EngineDownload.InstallEmbedded();
   if (File.Exists(SettingFile)) {
    try { var settings = File.ReadAllLines(SettingFile); if (settings.Length >= 3) {
     if (File.Exists(settings[0])) engine = settings[0];

     int index; if (int.TryParse(settings[1], out index) && index >= 0 && index <= 2) preset.SelectedIndex = index;
     auto.IsChecked = settings[2] == "True";
     if (settings.Length >= 11) {
      backup.IsChecked = settings[3] == "True"; allMethods.IsChecked = settings[4] == "True";
      exports.IsChecked = settings[5] == "True"; relocs.IsChecked = settings[6] == "True"; force.IsChecked = settings[7] == "True";
      if (int.TryParse(settings[8], out index) && index >= 0 && index <= 2) overlay.SelectedIndex = index;
      if (int.TryParse(settings[9], out index) && index >= 0 && index <= 3) icons.SelectedIndex = index;
      autoUpdate.IsChecked = settings[10] == "True";
     }
    } } catch (IOException) { } catch (UnauthorizedAccessException) { }
   }
   entries.CollectionChanged += delegate(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
    if (e.OldItems != null) foreach (Entry item in e.OldItems) item.PropertyChanged -= EntryChanged;
    if (e.NewItems != null) foreach (Entry item in e.NewItems) item.PropertyChanged += EntryChanged;
    UpdateQueueState();
   };
   mode.SelectionChanged += delegate { UpdateModeControls(); };
   retry.Click += async delegate { if (PrepareRetry() > 0) await ProcessQueue(); };
   UpdateQueueState(); UpdateModeControls();
   UpdateEngine(); files.ItemsSource = entries; files.SelectionChanged += delegate { ShowDetail(); };
   Find<Button>("AddButton").Click += delegate { var d = new OpenFileDialog { Filter = "Windows 程序 (*.exe;*.dll)|*.exe;*.dll", Multiselect = true }; if (d.ShowDialog(window) == true) Add(d.FileNames); };
   engineButton.Click += delegate { var d = new OpenFileDialog { Filter = "官方 UPX (upx.exe)|upx.exe" }; if (d.ShowDialog(window) == true) { engine = d.FileName; UpdateEngine(); Save(); } };
   downloadButton.Click += delegate {
    try { engine = EngineDownload.InstallEmbedded(); UpdateEngine(); summary.Text = "内嵌 UPX 已重新校验并就绪。"; }
    catch (Exception ex) { details.Text = ex.Message; }
   };
   Find<Button>("ProjectButton").Click += delegate { OpenProject(); };
   Find<Button>("LicenseButton").Click += delegate { ShowLicenses(); };
   updateButton.Click += async delegate {
    if (pendingUpdate != null) { if (!busy) window.Close(); else summary.Text = "请等待队列完成后关闭软件，更新将在退出后安装。"; }
    else await CheckUpdates(true);
   };
   start.Click += async delegate { await ProcessQueue(); }; stop.Click += delegate { if (cancellation != null) cancellation.Cancel(); };
   Find<Button>("ClearButton").Click += delegate { foreach (var entry in entries.Where(e => e.Status != "等待中" && e.Status != "处理中").ToList()) entries.Remove(entry); ShowDetail(); };
   window.DragOver += delegate(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
   window.Drop += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) Add((string[])e.Data.GetData(DataFormats.FileDrop)); e.Handled = true; };
   window.Closing += delegate(object sender, CancelEventArgs e) {
    Save();
    if (busy || updateChecking) {
     e.Cancel = true; closeRequested = true;
     if (cancellation != null) cancellation.Cancel();
     if (updateCancellation != null) updateCancellation.Cancel();
     summary.Text = "正在停止并清理临时文件…";
    }
   };
   window.Closed += delegate { if (pendingUpdate != null) { try { Updater.ApplyAfterExit(pendingUpdate); } catch (Exception ex) { MessageBox.Show(ex.Message, "更新未安装，当前版本保留"); } } };
  }
  async Task CheckUpdates(bool manual) {
   if (updateChecking) return;
   updateChecking = true; updateCancellation = new CancellationTokenSource(); updateButton.IsEnabled = false;
   try {
    pendingUpdate = await Updater.DownloadLatestAsync(updateCancellation.Token, new Progress<string>(message => Find<TextBlock>("UpdateStatus").Text = message));
    if (pendingUpdate != null) {
     updateButton.Content = "退出并更新";
     Find<TextBlock>("UpdateStatus").Text = "新版 " + pendingUpdate.Version + " 已校验，退出后自动更新。";
    } else Find<TextBlock>("UpdateStatus").Text = "当前 v" + Updater.CurrentVersion + " · 暂无新版本";
   } catch (Exception ex) { if (!closeRequested) { Find<TextBlock>("UpdateStatus").Text = "更新不可用，当前版本可继续使用。"; if (manual) details.Text = ex.Message; } }
   finally { updateChecking = false; updateCancellation.Dispose(); updateCancellation = null; updateButton.IsEnabled = true; if (closeRequested && !busy) window.Close(); }
  }
  void OpenProject() { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Updater.RepositoryUrl) { UseShellExecute = true }); } catch (Exception ex) { details.Text = Updater.RepositoryUrl + Environment.NewLine + ex.Message; } }
  void ShowLicenses() {
   var text = new System.Text.StringBuilder();
   text.AppendLine("UPX Drop " + Updater.CurrentVersion + "\n" + Updater.RepositoryUrl + "\n\n运行时使用 Windows 自带的 .NET Framework 4.8。官方 UPX 会释放至用户缓存目录。\n");
   foreach (string name in new[] { "THIRD_PARTY_NOTICES.md", "LICENSE", "UPX-LICENSE" }) {
    using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name)) if (stream != null) using (var reader = new StreamReader(stream)) text.AppendLine(reader.ReadToEnd());
   }
   new Window { Title = "关于与开源许可", Owner = window, Width = 720, Height = 560, Content = new TextBox { Text = text.ToString(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16) }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
  }
  void EntryChanged(object sender, PropertyChangedEventArgs e) { if (e.PropertyName == "Status") UpdateQueueState(); }
  void UpdateQueueState() {
   int finished = entries.Count(e => e.Status == "已压缩" || e.Status == "已解压" || e.Status == "已跳过" || e.Status == "失败");
   queueProgress.Maximum = Math.Max(1, entries.Count); queueProgress.Value = finished;
   queueProgressText.Text = finished + "/" + entries.Count;
   retry.IsEnabled = !busy && entries.Any(e => e.Status == "失败" || e.Status == "已取消");
   start.IsEnabled = !busy && entries.Any(e => e.Status == "等待中");
  }
  int PrepareRetry() {
   if (busy) return 0;
   var retryEntries = entries.Where(e => e.Status == "失败" || e.Status == "已取消").ToList();
   foreach (var entry in retryEntries) { entry.Status = "等待中"; entry.After = entry.Saving = "—"; }
   return retryEntries.Count;
  }
  void UpdateModeControls() {
   bool compress = mode.SelectedIndex == 0;
   foreach (var control in new UIElement[] { preset, allMethods, exports, relocs, force, overlay, icons }) control.IsEnabled = !busy && compress;
   backup.IsEnabled = !busy;
   start.Content = compress ? "开始压缩  →" : "开始解压  →";
   Find<TextBlock>("SettingsTitle").Text = compress ? "压缩设置" : "解压设置";
  }
  void UpdateEngine() { engineLabel.Text = File.Exists(engine) ? (engine == embeddedEngine ? "UPX " + EngineDownload.Version : "外部 UPX") + " · 已就绪" : "请选择官方 upx.exe"; engineLabel.ToolTip = engine; }
  void Save() { try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingFile)); File.WriteAllLines(SettingFile, new[] { engine, preset.SelectedIndex.ToString(), auto.IsChecked.ToString(), backup.IsChecked.ToString(), allMethods.IsChecked.ToString(), exports.IsChecked.ToString(), relocs.IsChecked.ToString(), force.IsChecked.ToString(), overlay.SelectedIndex.ToString(), icons.SelectedIndex.ToString(), autoUpdate.IsChecked.ToString() }); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
  IEnumerable<string> Expand(string path) {
   if (File.Exists(path)) { yield return path; yield break; }
   if (!Directory.Exists(path)) yield break;
   var pending = new Stack<string>(); pending.Push(path);
   while (pending.Count > 0) {
    string dir = pending.Pop(); string[] childFiles, childDirs;
    try { if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue; childFiles = Directory.GetFiles(dir); childDirs = Directory.GetDirectories(dir); }
    catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
    foreach (string item in childFiles) yield return item;
    foreach (string item in childDirs) pending.Push(item);
   }
  }
  async void Add(string[] paths) {
   if (closeRequested || paths.Length == 0) return; int added = 0;
   try {
    foreach (string raw in paths) foreach (string path in Expand(raw)) {
     string ext = System.IO.Path.GetExtension(path);
     if (!(ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)) || System.IO.Path.GetFileName(path).StartsWith(".upxdrop-")) continue;
     if (entries.Any(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
     if (entries.Count >= 5000) break;
     var entry = new Entry { Path = System.IO.Path.GetFullPath(path) }; entry.Before = Size(new FileInfo(path).Length); entries.Add(entry); added++;
    }
    summary.Text = entries.Count >= 5000 ? "列表已达 5000 个文件，请分批处理。" : added > 0 ? "已加入 " + added + " 个文件" : "没有新的 EXE / DLL 文件";
    if (auto.IsChecked == true && !busy && added > 0) await ProcessQueue();
   } catch (Exception ex) { summary.Text = "添加失败：" + ex.Message; }
  }
  static string Size(long bytes) { return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.00") + " MB" : (bytes / 1024.0).ToString("0.0") + " KB"; }
  void ShowDetail() { var entry = files.SelectedItem as Entry; details.Text = entry == null ? "选择一行查看路径和 UPX 运行日志。" : entry.Path + Environment.NewLine + entry.Detail; }
  async Task ProcessQueue() {
   if (busy || !entries.Any(e => e.Status == "等待中")) return;
   if (!File.Exists(engine)) { summary.Text = "请先配置官方 UPX 引擎。"; return; }
   advanced.IsEnabled = false; retry.IsEnabled = false;
   busy = true; cancellation = new CancellationTokenSource(); start.IsEnabled = false; stop.IsEnabled = true; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = false;
   bool decompress = mode.SelectedIndex == 1; string level = preset.SelectedIndex == 2 ? "best" : preset.SelectedIndex == 1 ? "fast" : "balanced";
   var options = new PackOptions { Preset = level, Backup = backup.IsChecked == true, AllMethods = allMethods.IsChecked == true, CompressExports = exports.IsChecked == true, StripRelocs = relocs.IsChecked == true, Force = force.IsChecked == true, Overlay = overlay.SelectedIndex == 1 ? "strip" : overlay.SelectedIndex == 2 ? "skip" : "copy", CompressIcons = icons.SelectedIndex };
   int success = 0, failed = 0, skipped = 0; long saved = 0;
   try {
    while (!cancellation.IsCancellationRequested) {
     Entry entry = entries.FirstOrDefault(e => e.Status == "等待中"); if (entry == null) break;
     entry.Status = "处理中"; summary.Text = (decompress ? "正在解压：" : "正在压缩：") + entry.Name;
     try {
      var result = await Task.Run(() => new UpxRunner(engine).ProcessAsync(entry.Path, options, decompress, cancellation.Token));
      entry.Before = Size(result.Before); entry.After = Size(result.After); entry.Status = result.Status; entry.Detail = result.Detail;
      entry.Saving = result.Before == 0 ? "—" : ((result.Before - result.After) * 100.0 / result.Before).ToString("0.0") + "%";
      if (result.Status == "已跳过") skipped++; else { success++; saved += result.Before - result.After; }
     } catch (OperationCanceledException) { entry.Status = "已取消"; entry.Detail = "本文件已取消，原文件未修改；可点击“重试失败项”继续。"; }
     catch (Exception ex) {
      if (ex is InvalidOperationException && (ex.Message.Contains("AlreadyPackedException") || ex.Message.Contains("NotCompressibleException"))) { entry.Status = "已跳过"; skipped++; }
      else { entry.Status = "失败"; failed++; }
      entry.Detail = ex.Message;
     }
     ShowDetail();
    }
    bool cancelled = cancellation.IsCancellationRequested;
    if (cancelled) foreach (var entry in entries.Where(e => e.Status == "等待中")) { entry.Status = "已取消"; entry.Detail = "尚未处理，可点击“重试失败项”继续。"; }
    summary.Text = string.Format("{0} · 成功 {1} / 跳过 {2} / 失败 {3}{4}", cancelled ? "已停止" : "处理完成", success, skipped, failed, saved > 0 ? " · 节省 " + Size(saved) : "");
   } finally {
    advanced.IsEnabled = true;
    busy = false; cancellation.Dispose(); cancellation = null; start.IsEnabled = true; stop.IsEnabled = false; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = true; UpdateModeControls(); UpdateQueueState(); Save(); if (closeRequested) window.Close();
   }
  }
 }
}
