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
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
namespace UpxDrop {
 public partial class Program {
  Window window; ListView files; TextBox details; TextBlock summary, engineLabel;
  ICollectionView view; TextBox search; ComboBox filter; ProgressBar progress;
  CancellationTokenSource scanCancellation; bool scanning; int batchDone, batchTotal;
  ComboBox preset, mode, overlay, icons; CheckBox auto, backup, allMethods, exports, relocs, force, autoUpdate; FrameworkElement advanced; Button start, stop, engineButton, downloadButton, updateButton;
  EntryCollection entries = new EntryCollection();
  CancellationTokenSource cancellation, updateCancellation; bool busy, closeRequested, updateChecking; string engine, embeddedEngine; PendingUpdate pendingUpdate;
  readonly string SettingFile;
  public Program(string settingsPath = null) { SettingFile = settingsPath ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpxDrop", "settings.txt"); }
  T Find<T>(string name) where T : class { return window.FindName(name) as T; }
  [STAThread] public static void Main(string[] args) {
   var application = new Application(); var program = new Program();
   try { program.Initialize(); } catch (Exception ex) { MessageBox.Show(ex.Message, "UPX Drop 启动失败"); return; }
   program.window.Loaded += async delegate { program.Add(args.Where(a => a != "--no-update-check").ToArray()); if (program.autoUpdate.IsChecked == true && !args.Contains("--no-update-check")) await program.CheckUpdates(false); }; application.Run(program.window);
  }
  void Initialize() {
   using (var stream = typeof(Program).Assembly.GetManifestResourceStream("MainWindow.xaml")) window = (Window)XamlReader.Load(stream);
   LoadBrandIcon();
   files = Find<ListView>("Files"); details = Find<TextBox>("Details"); summary = Find<TextBlock>("Summary"); engineLabel = Find<TextBlock>("EngineLabel");
   preset = Find<ComboBox>("Preset"); mode = Find<ComboBox>("Mode"); auto = Find<CheckBox>("AutoStart");
   start = Find<Button>("StartButton"); stop = Find<Button>("StopButton"); engineButton = Find<Button>("EngineButton"); downloadButton = Find<Button>("DownloadButton");
   advanced = Find<FrameworkElement>("AdvancedOptions"); overlay = Find<ComboBox>("Overlay"); icons = Find<ComboBox>("Icons");
   backup = Find<CheckBox>("Backup"); allMethods = Find<CheckBox>("AllMethods"); exports = Find<CheckBox>("CompressExports"); relocs = Find<CheckBox>("StripRelocs"); force = Find<CheckBox>("Force"); autoUpdate = Find<CheckBox>("AutoUpdate"); updateButton = Find<Button>("UpdateButton");
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
   UpdateEngine(); files.ItemsSource = entries; view = CollectionViewSource.GetDefaultView(entries);
   search = Find<TextBox>("SearchBox"); filter = Find<ComboBox>("StatusFilter"); progress = Find<ProgressBar>("QueueProgress");
   view.Filter = item => QueueTools.Matches((Entry)item, search.Text, filter.SelectedIndex);
   search.TextChanged += delegate { RefreshQueue(); }; filter.SelectionChanged += delegate { RefreshQueue(); };
   files.SelectionChanged += delegate { ShowDetail(); UpdateActions(); };
   files.SizeChanged += delegate {
    var columns = ((GridView)files.View).Columns;
    columns[0].Width = Math.Max(190, files.ActualWidth - 376);
   };
   mode.SelectionChanged += delegate { UpdateMode(); };
   BindModeSelectors();
   Find<Button>("FolderButton").Click += delegate {
    using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "选择要扫描的文件夹", ShowNewFolderButton = false })
     if (dialog.ShowDialog(new WindowOwner(new System.Windows.Interop.WindowInteropHelper(window).Handle)) == System.Windows.Forms.DialogResult.OK) Add(new[] { dialog.SelectedPath });
   };
   Find<Button>("RetryButton").Click += async delegate {
    if (busy || scanning) return;
    var retry = entries.Where(e => e.CanRetry).ToList(); foreach (var entry in retry) entry.Retry();
    filter.SelectedIndex = 0; search.Clear(); RefreshQueue();
    summary.Text = "已重新加入 " + retry.Count + " 个文件"; await ProcessQueue();
   };
   Find<Button>("RemoveButton").Click += delegate { RemoveSelected(); };
   Find<Button>("ExportButton").Click += delegate { ExportResults(); };
   Find<Button>("RevealButton").Click += delegate { RevealSelected(); };
   files.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e) {
    if (ItemsControl.ContainerFromElement(files, e.OriginalSource as DependencyObject) is ListViewItem) RevealSelected();
   };
   files.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Delete) { RemoveSelected(); e.Handled = true; } };
   window.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
    if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control && !scanning) { Find<Button>("AddButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
    if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { search.Focus(); search.SelectAll(); e.Handled = true; }
   };
   window.Loaded += delegate { AnimateEntrance(); }; InstallMotion(); UpdateMode(); RefreshQueue();
   Find<Button>("AddButton").Click += delegate { var d = new OpenFileDialog { Filter = "Windows 程序 (*.exe;*.dll)|*.exe;*.dll", Multiselect = true }; if (d.ShowDialog(window) == true) Add(d.FileNames); };
   engineButton.Click += delegate { var d = new OpenFileDialog { Filter = "官方 UPX (upx.exe)|upx.exe" }; if (d.ShowDialog(window) == true) { engine = d.FileName; UpdateEngine(); Save(); } };
   downloadButton.Click += delegate {
    try { engine = EngineDownload.InstallEmbedded(); UpdateEngine(); summary.Text = "内嵌 UPX 已重新校验并就绪。"; }
    catch (Exception ex) { details.Text = ex.Message; }
   };
   Find<Button>("ProjectButton").Click += delegate { OpenProject(); };
   Find<Button>("LicenseButton").Click += delegate { ShowLicenses(); };
   updateButton.Click += async delegate {
    if (pendingUpdate != null) { if (!busy && !scanning) window.Close(); else summary.Text = "请等待队列完成后关闭软件，更新将在退出后安装。"; }
    else await CheckUpdates(true);
   };
   start.Click += async delegate { await ProcessQueue(); }; stop.Click += delegate { if (cancellation != null) cancellation.Cancel(); if (scanCancellation != null) scanCancellation.Cancel(); };
   Find<Button>("ClearButton").Click += delegate { if (busy || scanning) return; foreach (var entry in entries.Where(e => e.Complete).ToList()) entries.Remove(entry); ShowDetail(); RefreshQueue(); };
   window.PreviewDragOver += delegate(object sender, DragEventArgs e) { e.Effects = !scanning && !closeRequested && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; HighlightDrop(); e.Handled = true; };
   window.DragLeave += delegate { ResetDrop(); };
   window.PreviewDrop += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) Add((string[])e.Data.GetData(DataFormats.FileDrop)); ResetDrop(); e.Handled = true; };
   window.Closing += delegate(object sender, CancelEventArgs e) {
    Save();
    if (busy || updateChecking || scanning) {
     e.Cancel = true; closeRequested = true;
     if (cancellation != null) cancellation.Cancel();
     if (scanCancellation != null) scanCancellation.Cancel();
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
    pendingUpdate = await Updater.DownloadLatestAsync(updateCancellation.Token);
    if (pendingUpdate != null) {
     updateButton.Content = "退出并更新";
     Find<TextBlock>("UpdateStatus").Text = "新版 " + pendingUpdate.Version + " 已校验，退出后自动更新。";
    } else if (manual) Find<TextBlock>("UpdateStatus").Text = "未发现更新的正式版本。";
   } catch (Exception ex) { if (!closeRequested) { Find<TextBlock>("UpdateStatus").Text = "更新不可用，当前版本可继续使用。"; if (manual) details.Text = ex.Message; } }
   finally { updateChecking = false; updateCancellation.Dispose(); updateCancellation = null; updateButton.IsEnabled = true; TryClose(); }
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
  void UpdateEngine() { engineLabel.Text = File.Exists(engine) ? (engine == embeddedEngine ? "UPX " + EngineDownload.Version : "外部 UPX") + " · 已就绪" : "请选择官方 upx.exe"; engineLabel.ToolTip = engine; }
  void Save() { try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingFile)); File.WriteAllLines(SettingFile, new[] { engine, preset.SelectedIndex.ToString(), auto.IsChecked.ToString(), backup.IsChecked.ToString(), allMethods.IsChecked.ToString(), exports.IsChecked.ToString(), relocs.IsChecked.ToString(), force.IsChecked.ToString(), overlay.SelectedIndex.ToString(), icons.SelectedIndex.ToString(), autoUpdate.IsChecked.ToString() }); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
  sealed class WindowOwner : System.Windows.Forms.IWin32Window {
   public IntPtr Handle { get; private set; }
   public WindowOwner(IntPtr handle) { Handle = handle; }
  }
  void TryClose() { if (closeRequested && !busy && !scanning && !updateChecking) window.Close(); }
  void UpdateMode() {
   bool packing = mode.SelectedIndex == 0;
   preset.IsEnabled = !busy && packing;
   SyncModePresentation(packing);
   start.Content = packing ? "开始压缩" : "开始解压";
  }
  void UpdateActions() {
   start.IsEnabled = !busy && !scanning && entries.Any(e => e.Status == "等待中");
   stop.IsEnabled = busy || scanning;
   UpdateActivityIndicator();
   Find<Button>("RetryButton").IsEnabled = !busy && !scanning && entries.Any(e => e.CanRetry);
   Find<Button>("RemoveButton").IsEnabled = !busy && !scanning && files.SelectedItems.Count > 0;
   Find<Button>("ClearButton").IsEnabled = !busy && !scanning && entries.Any(e => e.Complete);
   Find<Button>("ExportButton").IsEnabled = !busy && !scanning && entries.Count > 0;
   Find<Button>("RevealButton").IsEnabled = files.SelectedItem != null;
   Find<Button>("AddButton").IsEnabled = Find<Button>("FolderButton").IsEnabled = !scanning && !closeRequested;
  }
  void RefreshQueue() {
   view.Refresh();
   if (!busy && !scanning && entries.Count == 0) {
    SetProgress(0); Find<TextBlock>("ProgressText").Text = "等待添加文件";
   }
   Find<TextBlock>("QueueCount").Text = entries.Count.ToString();
   Find<TextBlock>("VisibleCount").Text = "显示 " + view.Cast<Entry>().Count() + " / " + entries.Count;
   Find<TextBlock>("TotalSize").Text = QueueTools.Size(entries.Sum(e => e.BeforeBytes));
   long saved = entries.Where(e => e.AfterBytes.HasValue).Sum(e => e.BeforeBytes - e.AfterBytes.Value);
   Find<TextBlock>("SavedLabel").Text = saved < 0 ? "净增加" : "净节省";
   Find<TextBlock>("SavedSize").Text = QueueTools.Size(Math.Abs(saved));
   Find<TextBlock>("DoneCount").Text = entries.Count(e => e.Complete).ToString();
   Find<TextBlock>("EmptyTitle").Text = entries.Count == 0 ? "还没有文件" : "没有匹配的文件";
   Find<TextBlock>("EmptyHint").Text = entries.Count == 0 ? "拖入 EXE、DLL 或整个文件夹" : "换个关键词，或选择全部状态";
   long input = entries.Where(e => e.AfterBytes.HasValue).Sum(e => e.BeforeBytes);
   long output = entries.Where(e => e.AfterBytes.HasValue).Sum(e => e.AfterBytes.Value);
   UpdateSizeMeters(input, output);
   UpdateActions();
  }
  void RemoveSelected() {
   if (busy || scanning) return;
   var selected = files.SelectedItems.Cast<Entry>().ToList(); foreach (var e in selected) entries.Remove(e);
   ShowDetail(); RefreshQueue(); summary.Text = "已从队列移除 " + selected.Count + " 项，磁盘文件保留。";
  }
  void RevealSelected() {
   var entry = files.SelectedItem as Entry; if (entry == null) return;
   try {
    if (!File.Exists(entry.Path)) throw new FileNotFoundException("文件已移动或删除。", entry.Path);
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + entry.Path + "\"") { UseShellExecute = true });
   } catch (Exception ex) { summary.Text = "无法定位文件：" + ex.Message; }
  }
  void ExportResults() {
   if (busy || scanning || entries.Count == 0) return;
   var dialog = new SaveFileDialog { Filter = "CSV 表格 (*.csv)|*.csv", FileName = "UPX处理结果-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv" };
   if (dialog.ShowDialog(window) != true) return;
   try { File.WriteAllText(dialog.FileName, QueueTools.Csv(entries), new System.Text.UTF8Encoding(true)); summary.Text = "已导出全部 " + entries.Count + " 项结果。"; }
   catch (Exception ex) { summary.Text = "导出失败：" + ex.Message; }
  }
  async void Add(string[] paths) {
   if (closeRequested || scanning || paths.Length == 0) return;
   scanning = true; scanCancellation = new CancellationTokenSource(); UpdateActions();
   summary.Text = "正在扫描文件…"; progress.IsIndeterminate = true;
   int added = 0;
   try {
    string[] existing = entries.Select(e => e.Path).ToArray(); int capacity = 5000 - entries.Count;
    var result = await Task.Run(() => QueueTools.Scan(paths, existing, capacity, scanCancellation.Token));
    scanCancellation.Token.ThrowIfCancellationRequested();
    entries.AddRange(result.Entries); added = result.Entries.Count;
    if (busy) { batchTotal += added; SetProgress(batchTotal == 0 ? 0 : batchDone * 100.0 / batchTotal); }
    else { SetProgress(0); Find<TextBlock>("ProgressText").Text = "等待处理 " + entries.Count(e => e.Status == "等待中") + " 项"; }
    summary.Text = result.LimitReached ? "列表已达 5000 个文件，请分批处理。" : added > 0 ? "已加入 " + added + " 个文件" : "没有新的 EXE / DLL 文件";
    if (result.Unreadable > 0) summary.Text += "，另有 " + result.Unreadable + " 项无法读取";
   } catch (OperationCanceledException) { summary.Text = "已停止扫描。"; }
   catch (Exception ex) { summary.Text = "添加失败：" + ex.Message; }
   finally { scanning = false; scanCancellation.Dispose(); scanCancellation = null; progress.IsIndeterminate = false; RefreshQueue(); TryClose(); }
   if (!closeRequested && auto.IsChecked == true && !busy && added > 0) await ProcessQueue();
  }
  void ShowDetail() { var entry = files.SelectedItem as Entry; details.Text = entry == null ? "选择一行查看路径和 UPX 运行日志。" : entry.Path + Environment.NewLine + entry.Detail; }
  async Task ProcessQueue() {
   if (busy || scanning || closeRequested || !entries.Any(e => e.Status == "等待中")) return;
   if (!File.Exists(engine)) { summary.Text = "请先配置官方 UPX 引擎。"; return; }
   advanced.IsEnabled = false;
   busy = true; cancellation = new CancellationTokenSource(); start.IsEnabled = false; stop.IsEnabled = true; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = false;
   bool decompress = mode.SelectedIndex == 1; string level = preset.SelectedIndex == 2 ? "best" : preset.SelectedIndex == 1 ? "fast" : "balanced";
   var options = new PackOptions { Preset = level, Backup = backup.IsChecked == true, AllMethods = allMethods.IsChecked == true, CompressExports = exports.IsChecked == true, StripRelocs = relocs.IsChecked == true, Force = force.IsChecked == true, Overlay = overlay.SelectedIndex == 1 ? "strip" : overlay.SelectedIndex == 2 ? "skip" : "copy", CompressIcons = icons.SelectedIndex };
   int success = 0, failed = 0, skipped = 0; long saved = 0;
   Find<TextBlock>("ProgressText").Text = "正在准备…";
   batchDone = 0; batchTotal = entries.Count(e => e.Status == "等待中"); SetProgress(0); UpdateMode(); UpdateActions();
   try {
    while (!cancellation.IsCancellationRequested) {
     Entry entry = entries.FirstOrDefault(e => e.Status == "等待中"); if (entry == null) break;
     entry.AfterBytes = null; entry.Operation = decompress ? "解压" : "压缩"; entry.Refresh(); entry.Status = "处理中"; RefreshQueue(); summary.Text = (decompress ? "正在解压：" : "正在压缩：") + entry.Name;
     try {
      var result = await Task.Run(() => new UpxRunner(engine).ProcessAsync(entry.Path, options, decompress, cancellation.Token));
      entry.BeforeBytes = result.Before; entry.AfterBytes = result.After; entry.Status = result.Status; entry.Detail = result.Detail;
      entry.Refresh();
      if (result.Status == "已跳过") skipped++; else { success++; saved += result.Before - result.After; }
     } catch (OperationCanceledException) { entry.Status = "已取消"; entry.Detail = "本文件已取消，原文件未修改。"; }
     catch (Exception ex) {
      if (ex is InvalidOperationException && (ex.Message.Contains("AlreadyPackedException") || ex.Message.Contains("NotCompressibleException"))) { entry.Status = "已跳过"; skipped++; }
      else { entry.Status = "失败"; failed++; }
      entry.Detail = ex.Message;
     }
     batchDone++; SetProgress(batchTotal == 0 ? 0 : batchDone * 100.0 / batchTotal);
     Find<TextBlock>("ProgressText").Text = batchDone + " / " + batchTotal;
     RefreshQueue(); ShowDetail();
    }
    bool cancelled = cancellation.IsCancellationRequested;
    if (cancelled) foreach (var entry in entries.Where(e => e.Status == "等待中")) { entry.Status = "已取消"; entry.Detail = "尚未处理，点击“重试未完成”即可继续。"; }
    summary.Text = string.Format("{0} · 成功 {1} / 跳过 {2} / 失败 {3}{4}", cancelled ? "已停止" : "处理完成", success, skipped, failed, saved > 0 ? " · 节省 " + QueueTools.Size(saved) : "");
   } finally {
    advanced.IsEnabled = true;
    busy = false; cancellation.Dispose(); cancellation = null; start.IsEnabled = true; stop.IsEnabled = false; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = true; UpdateMode(); RefreshQueue(); Save(); TryClose();
   }
  }
 }
}
