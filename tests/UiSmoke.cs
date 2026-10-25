using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class UiSmoke {
 [STAThread] static void Main(string[] args) {
  var app=new Application();
  Window window;
  using(var stream=File.OpenRead(args[0])) window=(Window)XamlReader.Load(stream);
  foreach(string name in new[]{"Files","Mode","Preset","DownloadButton","StartButton","StopButton","AutoStart"}) if(window.FindName(name)==null) throw new Exception("Missing UI control: "+name);
  var content=(System.Windows.Controls.Grid)window.Content; content.Background=window.Background; content.Measure(new Size(1000,740)); content.Arrange(new Rect(0,0,1000,740)); content.UpdateLayout();
  var image=new RenderTargetBitmap(1000,740,96,96,PixelFormats.Pbgra32); image.Render(content);
  var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(var output=File.Create(args[1])) encoder.Save(output);
  Console.WriteLine("PASS XAML loads, controls resolve, 1000x740 layout renders");
 }
}
