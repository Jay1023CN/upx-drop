// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using UpxDrop;
class AppUiTests {
 static int passed;
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool value,string message) { if(!value) throw new Exception(message); passed++; Console.WriteLine("PASS "+message); }
 static object Call(Program program,string name) { return typeof(Program).GetMethod(name,Flags).Invoke(program,null); }
 static T Field<T>(Program program,string name) { return (T)typeof(Program).GetField(name,Flags).GetValue(program); }
 [STAThread] static int Main() {
  try {
   var app=new Application(); var program=new Program(); Call(program,"Initialize");
   var window=Field<Window>(program,"window"); var entries=Field<ObservableCollection<Entry>>(program,"entries");
   var start=(Button)window.FindName("StartButton"); var retry=(Button)window.FindName("RetryButton");
   var progress=(ProgressBar)window.FindName("QueueProgress"); var label=(TextBlock)window.FindName("QueueProgressText");
   Check(!start.IsEnabled && !retry.IsEnabled && label.Text=="0/0","empty queue disables processing and retry");
   var completed=new Entry { Path="completed.exe", Status="已压缩" };
   var failed=new Entry { Path="failed.exe", Status="失败",After="old",Saving="old" };
   var cancelled=new Entry { Path="cancelled.dll",Status="已取消" };
   entries.Add(completed); entries.Add(failed); entries.Add(cancelled); entries.Add(new Entry { Path="waiting.exe" });
   Check(start.IsEnabled && retry.IsEnabled,"queue enables start and failed/cancelled retry");
   Check(progress.Value==2 && progress.Maximum==4 && label.Text=="2/4","queue progress distinguishes processed and cancelled entries");
   Check((int)Call(program,"PrepareRetry")==2,"retry prepares only failed and cancelled entries");
   Check(completed.Status=="已压缩" && failed.Status=="等待中" && cancelled.Status=="等待中","retry preserves successful files and requeues unfinished work");
   Check(failed.After=="—" && failed.Saving=="—" && !retry.IsEnabled,"retry clears stale result values and refreshes controls");
   failed.Status="失败";
   Check(retry.IsEnabled,"status changes refresh retry availability");
   typeof(Program).GetField("busy",Flags).SetValue(program,true); Call(program,"UpdateQueueState");
   Check(!start.IsEnabled && !retry.IsEnabled && (int)Call(program,"PrepareRetry")==0,"active processing prevents concurrent retry");
   typeof(Program).GetField("busy",Flags).SetValue(program,false);
   var mode=(ComboBox)window.FindName("Mode"); mode.SelectedIndex=1;
   Check(!((ComboBox)window.FindName("Preset")).IsEnabled && !((ComboBox)window.FindName("Icons")).IsEnabled,"decompression disables compression-only controls");
   Check(((CheckBox)window.FindName("Backup")).IsEnabled && start.Content.ToString().Contains("解压"),"decompression retains optional backup and labels its action");
   mode.SelectedIndex=0;
   Check(((ComboBox)window.FindName("Preset")).IsEnabled && ((ComboBox)window.FindName("Icons")).IsEnabled,"switching back restores compression controls");
   Console.WriteLine(passed+" application UI checks passed"); return 0;
  } catch(Exception ex) { Console.WriteLine("::error::"+ex.ToString().Replace("%","%25").Replace("\r","%0D").Replace("\n","%0A")); Console.Error.WriteLine(ex); return 1; }
 }
}
