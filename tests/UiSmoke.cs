// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class UiSmoke {
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
 [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
 delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
 static IntPtr FindWindowForProcess(int pid, string title) {
  IntPtr result = IntPtr.Zero;
  EnumWindows((hWnd, lParam) => {
   uint procId; GetWindowThreadProcessId(hWnd, out procId);
   if (procId == pid) {
    var sb = new StringBuilder(256); GetWindowText(hWnd, sb, 256);
    if (sb.ToString() == title) { result = hWnd; return false; }
   }
   return true;
  }, IntPtr.Zero);
  return result;
 }
 [STAThread] static int Main(string[] args) {
  try {
   var app=new Application(); Window window;
   using(var stream=File.OpenRead(args[0])) window=(Window)XamlReader.Load(stream);
   foreach(string name in new[]{"Files","Mode","Preset","DownloadButton","EngineButton","StartButton","StopButton","AutoStart","AdvancedOptions","Backup","AllMethods","CompressExports","StripRelocs","Force","Overlay","Icons","AutoUpdate","UpdateButton","UpdateStatus","ProjectButton","LicenseButton"}) if(window.FindName(name)==null) throw new Exception("Missing UI control: "+name);
   var content=(System.Windows.Controls.Grid)window.Content; content.Background=window.Background;
   foreach(int width in new[]{1180,1060}) {
    int height=width==1180 ? 780 : 700;
    content.Measure(new Size(width,height)); content.Arrange(new Rect(0,0,width,height)); content.UpdateLayout();
    foreach(string name in new[]{"Files","StartButton","ProjectButton","UpdateButton","UpdateStatus","FolderButton","RetryButton","RemoveButton","ExportButton","SearchBox","StatusFilter","QueueProgress"}) {
     var element=(FrameworkElement)window.FindName(name);
     Rect bounds=element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
     if(bounds.Left < -1 || bounds.Top < -1 || bounds.Right > width+1 || bounds.Bottom > height+1) throw new Exception("Clipped UI control: "+name+" at "+width);
    }
    var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); image.Render(content);
    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
    string path=width==1180 ? args[1] : Path.Combine(Path.GetDirectoryName(args[1]),"preview-compact.png");
    using(var output=File.Create(path)) encoder.Save(output);
   }
   Console.WriteLine("PASS XAML controls resolve and layout renders at normal and compact sizes");
   if(args.Length>2) {
    string root=Path.Combine(Path.GetTempPath(),"upxdrop-single-file-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    try {
     string exe=Path.Combine(root,"UpxDrop.exe"); File.Copy(args[2],exe);
     using(var process=Process.Start(new ProcessStartInfo(exe,"--no-update-check") { UseShellExecute=false, WorkingDirectory=root })) {
      try {
       var watch=Stopwatch.StartNew(); IntPtr targetWnd = IntPtr.Zero;
       while(watch.Elapsed.TotalSeconds<20 && !process.HasExited) {
        process.Refresh();
        targetWnd = FindWindowForProcess(process.Id, "UPX Drop");
        if(targetWnd != IntPtr.Zero) break;
        if(process.MainWindowHandle!=IntPtr.Zero && process.MainWindowTitle=="UPX Drop") { targetWnd = process.MainWindowHandle; break; }
        Thread.Sleep(100);
       }
       if(targetWnd == IntPtr.Zero) throw new Exception("Single-file application did not open its main window.");
       Console.WriteLine("PASS application starts offline from a directory containing only UpxDrop.exe");
       PostMessage(targetWnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
       process.CloseMainWindow();
       if(!process.WaitForExit(5000)) throw new Exception("Application did not close cleanly.");
      } finally { if(!process.HasExited) { process.Kill(); process.WaitForExit(); } }
     }
    } finally {
     for (int i = 0; i < 10; i++) {
      try { Directory.Delete(root, true); break; }
      catch(IOException) { if (i == 9) throw; Thread.Sleep(200); }
      catch(UnauthorizedAccessException) { if (i == 9) throw; Thread.Sleep(200); }
     }
    }
   }
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
