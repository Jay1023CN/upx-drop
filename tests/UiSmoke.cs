// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class UiSmoke {
 [STAThread] static int Main(string[] args) {
  try {
   var app=new Application(); Window window;
   using(var stream=File.OpenRead(args[0])) window=(Window)XamlReader.Load(stream);
   foreach(string name in new[]{"Files","Mode","Preset","DownloadButton","EngineButton","StartButton","StopButton","AutoStart","AdvancedOptions","Backup","AllMethods","CompressExports","StripRelocs","Force","Overlay","Icons","AutoUpdate","UpdateButton","UpdateStatus","ProjectButton","LicenseButton","VersionLabel","RetryButton","QueueProgress","QueueProgressText","SettingsTitle"}) if(window.FindName(name)==null) throw new Exception("Missing UI control: "+name);
   var content=(System.Windows.Controls.Grid)window.Content;
   window.Content=null;
   var surface=new System.Windows.Controls.Border { Background=window.Background,Child=content };
   foreach(int width in new[]{1140,1000}) {
    int height=width==1140 ? 780 : 700;
    surface.Measure(new Size(width,height)); surface.Arrange(new Rect(0,0,width,height)); surface.UpdateLayout();
    foreach(string name in new[]{"Files","StartButton","ProjectButton","UpdateButton","UpdateStatus","RetryButton","QueueProgressText"}) {
     var element=(FrameworkElement)window.FindName(name);
     Rect bounds=element.TransformToAncestor(surface).TransformBounds(new Rect(element.RenderSize));
     if(bounds.Left < -1 || bounds.Top < -1 || bounds.Right > width+1 || bounds.Bottom > height+1) throw new Exception("Clipped UI control: "+name+" at "+width);
    }
    var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); image.Render(surface);
    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
    string path=width==1140 ? args[1] : Path.Combine(Path.GetDirectoryName(args[1]),"preview-compact.png");
    using(var output=File.Create(path)) encoder.Save(output);
   }
   var list=(System.Windows.Controls.ListView)window.FindName("Files");
   list.Items.Add(new { Name="my-tool.exe",Status="已压缩",Before="2.4 MB",After="980 KB",Saving="60.1%" });
   list.Items.Add(new { Name="library.dll",Status="等待中",Before="780 KB",After="—",Saving="—" });
   list.Items.Add(new { Name="signed-app.exe",Status="已跳过",Before="1.2 MB",After="1.2 MB",Saving="0.0%" });
   surface.Measure(new Size(1140,780)); surface.Arrange(new Rect(0,0,1140,780)); surface.UpdateLayout();
   var populated=new RenderTargetBitmap(1140,780,96,96,PixelFormats.Pbgra32); populated.Render(surface);
   var populatedEncoder=new PngBitmapEncoder(); populatedEncoder.Frames.Add(BitmapFrame.Create(populated));
   using(var output=File.Create(Path.Combine(Path.GetDirectoryName(args[1]),"preview-queue.png"))) populatedEncoder.Save(output);
   Console.WriteLine("PASS XAML controls resolve and empty/populated layouts render at normal and compact sizes");
   if(args.Length>2) {
    string root=Path.Combine(Path.GetTempPath(),"upxdrop-single-file-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    try {
     string exe=Path.Combine(root,"UpxDrop.exe"); File.Copy(args[2],exe);
     using(var process=Process.Start(new ProcessStartInfo(exe,"--no-update-check") { UseShellExecute=false, WorkingDirectory=root })) {
      try {
       var watch=Stopwatch.StartNew(); bool ready=false;
       while(watch.Elapsed.TotalSeconds<20 && !process.HasExited) {
        process.Refresh(); if(process.MainWindowHandle!=IntPtr.Zero && process.MainWindowTitle=="UPX Drop") { ready=true; break; }
        Thread.Sleep(100);
       }
       if(!ready) throw new Exception("Single-file application did not open its main window.");
       Console.WriteLine("PASS application starts offline from a directory containing only UpxDrop.exe");
       process.CloseMainWindow(); if(!process.WaitForExit(5000)) throw new Exception("Application did not close cleanly.");
      } finally { if(!process.HasExited) { process.Kill(); process.WaitForExit(); } }
     }
    } finally { Directory.Delete(root,true); }
   }
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
