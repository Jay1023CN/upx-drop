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
namespace UpxDrop {
 public sealed class Entry : INotifyPropertyChanged {
  public string Path { get; set; }
  public string Name { get { return System.IO.Path.GetFileName(Path); } }
  string status = "�ȴ���", before = "��", after = "��", saving = "��";
  public string Detail = "";
  public string Status { get { return status; } set { status = value; Changed("Status"); } }
  public string Before { get { return before; } set { before = value; Changed("Before"); } }
  public string After { get { return after; } set { after = value; Changed("After"); } }
  public string Saving { get { return saving; } set { saving = value; Changed("Saving"); } }
  public event PropertyChangedEventHandler PropertyChanged;
  void Changed(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
 }
 public class Program {
  Window window; ListView files; TextBox details; TextBlock summary, engineLabel;
  ComboBox preset, mode; CheckBox auto; Button start, stop, engineButton, downloadButton;
  ObservableCollection<Entry> entries = new ObservableCollection<Entry>();
  CancellationTokenSource cancellation; bool busy, closeRequested, downloading; string engine;
  static string SettingFile { get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpxDrop", "settings.txt"); } }
  T Find<T>(string name) where T : class { return window.FindName(name) as T; }
  [STAThread] public static void Main(string[] args) {
   var application = new Application(); var program = new Program();
   try { program.Initialize(); } catch (Exception ex) { MessageBox.Show(ex.Message, "UPX Drop ����ʧ��"); return; }
   program.window.Loaded += delegate { program.Add(args); }; application.Run(program.window);
  }
  void Initialize() {
   using (var stream = typeof(Program).Assembly.GetManifestResourceStream("MainWindow.xaml")) window = (Window)XamlReader.Load(stream);
   files = Find<ListView>("Files"); details = Find<TextBox>("Details"); summary = Find<TextBlock>("Summary"); engineLabel = Find<TextBlock>("EngineLabel");
   preset = Find<ComboBox>("Preset"); mode = Find<ComboBox>("Mode"); auto = Find<CheckBox>("AutoStart");
   start = Find<Button>("StartButton"); stop = Find<Button>("StopButton"); engineButton = Find<Button>("EngineButton"); downloadButton = Find<Button>("DownloadButton");
   engine = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "upx.exe");
   if (File.Exists(SettingFile)) {
    try { var settings = File.ReadAllLines(SettingFile); if (settings.Length >= 3) {
     if (!File.Exists(engine) && File.Exists(settings[0])) engine = settings[0];
     int index; if (int.TryParse(settings[1], out index) && index >= 0 && index <= 2) preset.SelectedIndex = index;
     auto.IsChecked = settings[2] == "True";
    } } catch (IOException) { } catch (UnauthorizedAccessException) { }
   }
   UpdateEngine(); files.ItemsSource = entries; files.SelectionChanged += delegate { ShowDetail(); };
   Find<Button>("AddButton").Click += delegate { var d = new OpenFileDialog { Filter = "Windows ���� (*.exe;*.dll)|*.exe;*.dll", Multiselect = true }; if (d.ShowDialog(window) == true) Add(d.FileNames); };
   engineButton.Click += delegate { var d = new OpenFileDialog { Filter = "�ٷ� UPX (upx.exe)|upx.exe" }; if (d.ShowDialog(window) == true) { engine = d.FileName; UpdateEngine(); Save(); } };
   downloadButton.Click += async delegate {
    downloading = true; downloadButton.IsEnabled = engineButton.IsEnabled = start.IsEnabled = false;
    summary.Text = "�������ز�У��ٷ� UPX 5.2.1��";
    try {
     engine = await EngineDownload.InstallAsync(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools"));
     UpdateEngine(); Save(); summary.Text = "�ٷ������Ѿ����������ļ����ɿ�ʼ��";
    } catch (Exception ex) { summary.Text = "����ʧ�ܣ����ֶ�ѡ��ٷ� UPX��"; details.Text = ex.Message; }
    finally { downloading = false; downloadButton.IsEnabled = engineButton.IsEnabled = start.IsEnabled = true; }
    if (auto.IsChecked == true && File.Exists(engine)) await ProcessQueue();
   };
   start.Click += async delegate { await ProcessQueue(); }; stop.Click += delegate { if (cancellation != null) cancellation.Cancel(); };
   Find<Button>("ClearButton").Click += delegate { foreach (var entry in entries.Where(e => e.Status != "�ȴ���" && e.Status != "������").ToList()) entries.Remove(entry); ShowDetail(); };
   window.DragOver += delegate(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
   window.Drop += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) Add((string[])e.Data.GetData(DataFormats.FileDrop)); e.Handled = true; };
   window.Closing += delegate(object sender, CancelEventArgs e) { Save(); if (busy) { e.Cancel = true; closeRequested = true; cancellation.Cancel(); summary.Text = "����ֹͣ��������ʱ�ļ���"; } };
  }
  void UpdateEngine() { engineLabel.Text = File.Exists(engine) ? "�ٷ� UPX �� �Ѿ���" : "����ѡ��ٷ� upx.exe"; engineLabel.ToolTip = engine; }
  void Save() { try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingFile)); File.WriteAllLines(SettingFile, new[] { engine, preset.SelectedIndex.ToString(), auto.IsChecked.ToString() }); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
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
    summary.Text = entries.Count >= 5000 ? "�����Ѵ� 5000 ���ļ��������������" : added > 0 ? "������ " + added + " ���ļ�" : "û���µ� EXE / DLL �ļ�";
    if (auto.IsChecked == true && !busy && added > 0) await ProcessQueue();
   } catch (Exception ex) { summary.Text = "����ʧ�ܣ�" + ex.Message; }
  }
  static string Size(long bytes) { return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.00") + " MB" : (bytes / 1024.0).ToString("0.0") + " KB"; }
  void ShowDetail() { var entry = files.SelectedItem as Entry; details.Text = entry == null ? "ѡ��һ��鿴·���� UPX ������־��" : entry.Path + Environment.NewLine + entry.Detail; }
  async Task ProcessQueue() {
   if (busy || downloading || !entries.Any(e => e.Status == "�ȴ���")) return;
   if (!File.Exists(engine)) { summary.Text = "���ȵ����ѡ�� UPX�����ùٷ����棬Ȼ��ʼ������"; return; }
   busy = true; cancellation = new CancellationTokenSource(); start.IsEnabled = false; stop.IsEnabled = true; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = false;
   bool decompress = mode.SelectedIndex == 1; string level = preset.SelectedIndex == 2 ? "best" : preset.SelectedIndex == 1 ? "fast" : "balanced";
   int success = 0, failed = 0, skipped = 0; long saved = 0;
   try {
    while (!cancellation.IsCancellationRequested) {
     Entry entry = entries.FirstOrDefault(e => e.Status == "�ȴ���"); if (entry == null) break;
     entry.Status = "������"; summary.Text = (decompress ? "���ڽ�ѹ��" : "����ѹ����") + entry.Name;
     try {
      var result = await Task.Run(() => new UpxRunner(engine).ProcessAsync(entry.Path, level, decompress, cancellation.Token));
      entry.Before = Size(result.Before); entry.After = Size(result.After); entry.Status = result.Status; entry.Detail = result.Detail;
      entry.Saving = result.Before == 0 ? "��" : ((result.Before - result.After) * 100.0 / result.Before).ToString("0.0") + "%";
      if (result.Status == "������") skipped++; else { success++; saved += result.Before - result.After; }
     } catch (OperationCanceledException) { entry.Status = "��ȡ��"; entry.Detail = "������ȡ����ԭ�ļ�δ�޸ġ�"; }
     catch (Exception ex) {
      if (ex is InvalidOperationException && (ex.Message.Contains("AlreadyPackedException") || ex.Message.Contains("NotCompressibleException"))) { entry.Status = "������"; skipped++; }
      else { entry.Status = "ʧ��"; failed++; }
      entry.Detail = ex.Message;
     }
     ShowDetail();
    }
    bool cancelled = cancellation.IsCancellationRequested;
    if (cancelled) foreach (var entry in entries.Where(e => e.Status == "�ȴ���")) { entry.Status = "��ȡ��"; entry.Detail = "��δ������������������Ӽ��ɡ�"; }
    summary.Text = string.Format("{0} �� �ɹ� {1} / ���� {2} / ʧ�� {3}{4}", cancelled ? "��ֹͣ" : "�������", success, skipped, failed, saved > 0 ? " �� ��ʡ " + Size(saved) : "");
   } finally {
    busy = false; cancellation.Dispose(); cancellation = null; start.IsEnabled = true; stop.IsEnabled = false; preset.IsEnabled = mode.IsEnabled = engineButton.IsEnabled = downloadButton.IsEnabled = true; Save(); if (closeRequested) window.Close();
   }
  }
 }
}
