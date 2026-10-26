// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UpxDrop;
class UiFlowTests {
 static int passed;
 const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
 static object Call(Program p, string name, params object[] args) { return typeof(Program).GetMethod(name, Private).Invoke(p, args); }
 static T Field<T>(Program p, string name) { return (T)typeof(Program).GetField(name, Private).GetValue(p); }
 static T Control<T>(Program p, string name) where T : class { return Field<Window>(p, "window").FindName(name) as T; }
 static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
 static void PumpUntil(Func<bool> done) {
  var watch = Stopwatch.StartNew();
  while (!done()) {
   if (watch.Elapsed.TotalSeconds > 30) throw new TimeoutException("UI operation timed out");
   var frame = new DispatcherFrame();
   Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
   Dispatcher.PushFrame(frame); Thread.Sleep(10);
  }
 }
 static void Click(Program p, string name) { Control<Button>(p, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
 static void Idle(Program p) { PumpUntil(() => !Field<bool>(p, "scanning") && !Field<bool>(p, "busy")); }
 static Program Init(string settings) {
  var p = new Program(settings); Call(p, "Initialize");
  Control<CheckBox>(p, "AutoStart").IsChecked = false; Control<CheckBox>(p, "AutoUpdate").IsChecked = false;
  return p;
 }
 static byte[] Pe(byte behavior) {
  var data = new byte[8192];
  using (var stream = new MemoryStream(data)) using (var writer = new BinaryWriter(stream)) {
   writer.Write((ushort)0x5a4d); stream.Position = 0x3c; writer.Write(128); stream.Position = 128; writer.Write(0x4550);
   stream.Position = 148; writer.Write((ushort)240); stream.Position = 152; writer.Write((ushort)0x20b); stream.Position = 260; writer.Write(16);
  }
  data[700] = behavior; return data;
 }
 static Border Host(Program p, int width, int height) {
  var window = Field<Window>(p, "window");
  var host = window.Content as Border;
  if (host == null) { var content = (UIElement)window.Content; window.Content = null; host = new Border { Background = window.Background, Child = content }; window.Content = host; }
  host.Measure(new Size(width, height)); host.Arrange(new Rect(0, 0, width, height)); host.UpdateLayout();
  return host;
 }
 static void Capture(Program p, string path, int width, int height) {
  var host = Host(p, width, height);
  foreach (string name in new[] { "Files", "StartButton", "ExportButton", "RetryButton", "PackMode", "PresetSelector", "QueueProgress", "UpdateButton" }) {
   var e = Control<FrameworkElement>(p, name); var bounds = e.TransformToAncestor(host).TransformBounds(new Rect(e.RenderSize));
   Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= width && bounds.Bottom <= height, "layout contains " + name + " at " + width);
  }
  var drop = Control<Border>(p, "DropSurface");
  foreach (var name in new[] { "AddButton", "FolderButton" }) {
   var button = Control<Button>(p, name);
   var bounds = button.TransformToAncestor(drop).TransformBounds(new Rect(button.RenderSize));
   Check(bounds.Top >= 0 && bounds.Bottom <= drop.ActualHeight, name + " stays inside the drop area at " + width);
  }
  var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
  var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
  using (var output = File.Create(path)) encoder.Save(output);
 }
 [STAThread] static int Main(string[] args) {
  string root = Path.Combine(Path.GetTempPath(), "upx-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  try {
   var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
   var p = Init(Path.Combine(root, "settings.txt"));
   // Practice uses its own state and must never mutate the live queue or settings.
   int originalPreset = Control<ComboBox>(p, "Preset").SelectedIndex;
   Click(p, "TutorialButton");
   Check(Control<Grid>(p, "TutorialOverlay").Visibility == Visibility.Visible && !Control<FrameworkElement>(p, "Workspace").IsEnabled, "tutorial opens with background interaction blocked");
   Check(!Control<Button>(p, "TutorialNext").IsEnabled, "tutorial waits for the sample action");
   Capture(p, Path.Combine(Path.GetDirectoryName(args[0]), "preview-tutorial-start.png"), 1060, 700);
   var tutorialCard = Control<Border>(p, "TutorialCard"); var tutorialHost = Host(p, 1060, 700);
   var tutorialBounds = tutorialCard.TransformToAncestor(tutorialHost).TransformBounds(new Rect(tutorialCard.RenderSize));
   Check(tutorialBounds.Top >= 0 && tutorialBounds.Bottom <= 700 && tutorialBounds.Left >= 0 && tutorialBounds.Right <= 1060, "tutorial fits compact window");
   Click(p, "TutorialAction"); Click(p, "TutorialNext"); Click(p, "TutorialBest");
   Click(p, "TutorialAction");
   PumpUntil(() => Control<Button>(p, "TutorialNext").IsEnabled);
   Check(Control<TextBlock>(p, "TutorialFileStatus").Text.Contains("2.1 MB"), "practice compresses selected example without touching files");
   Click(p, "TutorialNext");
   Capture(p, Path.Combine(Path.GetDirectoryName(args[0]), "preview-tutorial-result.png"), 1060, 700);
   Check(Control<Button>(p, "TutorialNext").Content.ToString() == "开始使用", "tutorial reaches result page");
   Click(p, "TutorialNext");
   Check(Control<Grid>(p, "TutorialOverlay").Visibility == Visibility.Collapsed && Control<FrameworkElement>(p, "Workspace").IsEnabled, "finishing tutorial restores workspace");
   Check(Field<ObservableCollection<Entry>>(p, "entries").Count == 0 && Control<ComboBox>(p, "Preset").SelectedIndex == originalPreset, "tutorial leaves live queue and presets unchanged");
   Click(p, "TutorialButton"); Click(p, "TutorialAction"); Click(p, "TutorialNext"); Click(p, "TutorialAction");
   Click(p, "TutorialClose"); Click(p, "TutorialButton");
   var settling = Stopwatch.StartNew(); PumpUntil(() => settling.ElapsedMilliseconds > 850);
   Check(Control<TextBlock>(p, "TutorialStepLabel").Text == "1 / 3" && !Control<Button>(p, "TutorialNext").IsEnabled, "closing and reopening discards pending practice completion");
   Click(p, "TutorialClose");
   typeof(Program).GetField("engine", Private).SetValue(p, Path.GetFullPath(args[0]));
   var entries = Field<ObservableCollection<Entry>>(p, "entries");
   string folder = Path.Combine(root, "queue"); Directory.CreateDirectory(folder);
   string a = Path.Combine(folder, "alpha.exe"), b = Path.Combine(folder, "beta.dll"), bad = Path.Combine(folder, "invalid.dll");
   File.WriteAllBytes(a, Pe(3)); File.WriteAllBytes(b, Pe(0)); File.WriteAllText(bad, "invalid PE");
   Call(p, "Add", (object)new[] { folder, a }); Idle(p);
   Check(entries.Count == 3 && Control<Button>(p, "StartButton").IsEnabled, "folder import deduplicates and enables manual start: " + Control<TextBlock>(p, "Summary").Text);
   Control<TextBox>(p, "SearchBox").Text = "alpha";
   Check(Control<ListView>(p, "Files").Items.Count == 1, "search updates actual WPF view");
   Control<TextBox>(p, "SearchBox").Clear();
   Control<RadioButton>(p, "UnpackMode").IsChecked = true;
   Check(Control<Button>(p, "StartButton").Content.ToString() == "开始解压" && !Control<FrameworkElement>(p, "PresetSelector").IsEnabled, "mode segments update action and disable compression presets");
   Control<RadioButton>(p, "PackMode").IsChecked = true;
   var run = (Task)Call(p, "ProcessQueue");
   Check(!Control<Button>(p, "RemoveButton").IsEnabled && !Control<FrameworkElement>(p, "ModeSelector").IsEnabled, "running queue locks removal and mode changes");
   Click(p, "StopButton"); PumpUntil(() => run.IsCompleted); run.GetAwaiter().GetResult();
   Check(entries.Any(e => e.CanRetry) && !entries.Any(e => e.Status == "等待中"), "stop marks pending work retryable");
   File.WriteAllBytes(a, Pe(0));
   Control<ComboBox>(p, "StatusFilter").SelectedIndex = 3; Control<TextBox>(p, "SearchBox").Text = "alpha";
   Click(p, "RetryButton"); Idle(p);
   Check(entries.Count(e => e.Status == "已压缩") == 2 && entries.Count(e => e.Status == "失败") == 1, "retry processes cancelled work and surfaces invalid PE failure");
   Check(Control<ComboBox>(p, "StatusFilter").SelectedIndex == 0 && Control<TextBox>(p, "SearchBox").Text == "", "retry reveals queue hidden by earlier filters");
   Check(Control<TextBlock>(p, "SavedSize").Text == QueueTools.Size(14336), "aggregate saved size matches completed results");
   File.WriteAllBytes(bad, Pe(0)); Click(p, "RetryButton"); Idle(p);
   Check(entries.All(e => e.Status == "已压缩") && new FileInfo(bad).Length == 1024, "failed file can be fixed and retried without reimporting");
   Control<ListView>(p, "Files").SelectedItem = entries[0]; string removed = entries[0].Path; Click(p, "RemoveButton");
   Check(entries.Count == 2 && File.Exists(removed), "remove selection preserves disk file");
   Click(p, "ClearButton"); Check(entries.Count == 0 && !Control<Button>(p, "StartButton").IsEnabled && Control<ProgressBar>(p, "QueueProgress").Value == 0, "clear completed resets queue actions and progress");
   string slow = Path.Combine(root, "slow.exe"), extra = Path.Combine(root, "extra.exe");
   File.WriteAllBytes(slow, Pe(3)); File.WriteAllBytes(extra, Pe(0));
   Call(p, "Add", (object)new[] { slow }); Idle(p);
   var dynamicRun = (Task)Call(p, "ProcessQueue");
   Call(p, "Add", (object)new[] { extra }); Idle(p); dynamicRun.GetAwaiter().GetResult();
   Check(entries.Count == 2 && entries.All(e => e.Status == "已压缩") && Field<int>(p, "batchTotal") == 2, "files added during processing join the active batch and progress total");
   Click(p, "ClearButton");
   Call(p, "Add", (object)new[] { folder }); Click(p, "StopButton"); Idle(p);
   Check(entries.Count == 0 && !Control<ProgressBar>(p, "QueueProgress").IsIndeterminate, "stopping an import cancels results and resets scan progress");
   Capture(p, Path.Combine(Path.GetDirectoryName(args[0]), "preview-instrument.png"), 1180, 780);
   Capture(p, Path.Combine(Path.GetDirectoryName(args[0]), "preview-instrument-compact.png"), 1060, 700);
   if (args.Length > 1) {
    var demo = Init(Path.Combine(root, "demo-settings.txt"));
    string samples = Path.Combine(root, "native-fixtures"); Directory.CreateDirectory(samples);
    foreach (var name in new[] { "sample-x64.exe", "sample-copy.exe", "backup-test.exe" }) File.Copy(args[1], Path.Combine(samples, name));
    Control<CheckBox>(demo, "Backup").IsChecked = true;
    Call(demo, "Add", (object)new[] { samples }); Idle(demo);
    var work = (Task)Call(demo, "ProcessQueue"); PumpUntil(() => work.IsCompleted); work.GetAwaiter().GetResult();
    Check(Field<ObservableCollection<Entry>>(demo, "entries").All(e => e.Status == "已压缩"), "native engine completes the displayed UI queue");
    // Let the meter animation settle before the final product screenshot.
    var watch = Stopwatch.StartNew(); PumpUntil(() => watch.ElapsedMilliseconds > 250);
    string output = args.Length > 2 ? args[2] : Path.GetDirectoryName(args[0]); Directory.CreateDirectory(output);
    Capture(demo, Path.Combine(output, "workspace.png"), 1180, 780);
    Control<Expander>(demo, "AdvancedExpander").IsExpanded = true;
    Capture(demo, Path.Combine(output, "workspace-advanced.png"), 1180, 780);
    Capture(demo, Path.Combine(output, "workspace-compact.png"), 1060, 700);
   }
   Console.WriteLine(passed + " UI flow checks passed"); return 0;
  } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally { Directory.Delete(root, true); }
 }
}
