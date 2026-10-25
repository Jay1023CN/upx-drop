// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UpxDrop;
class CoreTests {
 static int passed;
 static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
 static byte[] Pe(bool signed, byte behavior) {
  var data = new byte[8192];
  using (var s = new MemoryStream(data)) using (var w = new BinaryWriter(s)) {
   w.Write((ushort)0x5a4d); s.Position=0x3c; w.Write(128); s.Position=128; w.Write(0x4550); s.Position=148; w.Write((ushort)240);
   s.Position=152; w.Write((ushort)0x20b); s.Position=260; w.Write(16); s.Position=296; w.Write(signed ? 4096 : 0); w.Write(signed ? 256 : 0);
  }
  data[700]=behavior; return data;
 }
 static void Main(string[] args) {
  if (args.Length > 0) { Fake(args); return; }
  try { Tests().GetAwaiter().GetResult(); Console.WriteLine(passed + " checks passed"); }
  catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
 }
 static void Fake(string[] args) {
  string path=args[args.Length-1]; byte[] data=File.ReadAllBytes(path); byte mode=data[700];
  if (mode==1) { Console.Error.WriteLine("NotCompressibleException"); Environment.ExitCode=1; return; }
  if (mode==2 && args[0]=="-t") { Console.Error.WriteLine("test failed"); Environment.ExitCode=2; return; }
  if (mode==3) Thread.Sleep(3000);
  if (args[0]=="-t") return;
  Array.Resize(ref data, args[0]=="-d" ? 8192 : mode==4 ? 16384 : 1024); File.WriteAllBytes(path,data);
  Console.WriteLine("fixture OK " + string.Join(" ", args));
 }
 static async Task Tests() {
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  string engine=System.Reflection.Assembly.GetExecutingAssembly().Location;
  var runner=new UpxRunner(engine);
  try {
   string path=Path.Combine(root,"中文 空格 & sample.exe"); File.WriteAllBytes(path,Pe(false,0));
   var result=await runner.ProcessAsync(path,"balanced",false,CancellationToken.None);
   Check(result.Status=="已压缩" && new FileInfo(path).Length==1024,"compress + Unicode/space path + atomic overwrite");
   Check(Directory.GetFiles(root).Length==1,"no backup or temporary leftovers");
   result=await runner.ProcessAsync(path,"balanced",true,CancellationToken.None);
   Check(result.Status=="已解压" && new FileInfo(path).Length==8192,"decompression path");
   File.WriteAllBytes(path,Pe(true,0)); result=await runner.ProcessAsync(path,"balanced",false,CancellationToken.None);
   Check(result.Status=="已跳过" && new FileInfo(path).Length==8192,"signed PE skipped");
   foreach(byte behavior in new byte[]{1,2,4}) {
    byte[] original=Pe(false,behavior); File.WriteAllBytes(path,original); bool threw=false;
    try { result=await runner.ProcessAsync(path,"balanced",false,CancellationToken.None); } catch(InvalidOperationException) { threw=true; }
    Check(behavior==4 ? result.Status=="已跳过" : threw,"UPX failure/test failure/no saving "+behavior);
    Check(Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(original),"original preserved "+behavior);
   }
   File.WriteAllBytes(path,Pe(false,3)); using(var cancel=new CancellationTokenSource(200)) {
    bool cancelled=false; try { await runner.ProcessAsync(path,"fast",false,cancel.Token); } catch(OperationCanceledException) { cancelled=true; }
    Check(cancelled && new FileInfo(path).Length==8192,"cancel running process without replacing original");
   }
   File.WriteAllBytes(path,Pe(false,3)); var running=runner.ProcessAsync(path,"balanced",false,CancellationToken.None);
   await Task.Delay(200); var edited=Pe(false,0); edited[701]=123; File.WriteAllBytes(path,edited); bool changed=false;
   try { await running; } catch(IOException) { changed=true; }
   Check(changed && File.ReadAllBytes(path)[701]==123,"concurrent original edit detected");
   File.WriteAllText(path,"invalid"); bool invalid=false;
   try { await runner.ProcessAsync(path,"balanced",false,CancellationToken.None); } catch(InvalidDataException) { invalid=true; }
   Check(invalid,"invalid PE rejected");
   File.WriteAllBytes(path,Pe(false,0)); File.SetAttributes(path,FileAttributes.ReadOnly); bool readOnly=false;
   try { await runner.ProcessAsync(path,"balanced",false,CancellationToken.None); } catch(InvalidOperationException) { readOnly=true; }
   finally { File.SetAttributes(path,FileAttributes.Normal); }
   Check(readOnly,"read-only rejected");
   var options = new PackOptions { Preset="best", Backup=true, AllMethods=true, Force=true, CompressExports=false, StripRelocs=false, CompressIcons=3, Overlay="strip" };
   byte[] backedUp=Pe(false,0); File.WriteAllBytes(path,backedUp);
   result=await runner.ProcessAsync(path,options,false,CancellationToken.None);
   var backups=Directory.GetFiles(root,"*.bak");
   Check(backups.Length==1 && Convert.ToBase64String(File.ReadAllBytes(backups[0]))==Convert.ToBase64String(backedUp),"backup preserves exact original bytes");
   Check(result.Detail.Contains("--best --all-methods --force --compress-exports=0 --strip-relocs=0 --compress-icons=3 --overlay=strip"),"all advanced options reach child process");
   result=await runner.ProcessAsync(path,options,true,CancellationToken.None);
   Check(Directory.GetFiles(root,"*.bak").Length==2 && new FileInfo(backups[0]).Length==8192,"decompression backup does not overwrite existing backup");
   Check(result.Detail.Contains("fixture OK -d ") && !result.Detail.Contains("--force"),"decompression excludes compression options");
   options.Overlay="invalid"; bool invalidOptions=false;
   try { options.Arguments(false); } catch(ArgumentException) { invalidOptions=true; }
   Check(invalidOptions,"unknown overlay cannot become a command argument");
   options.Overlay="copy"; options.CompressIcons=4; invalidOptions=false;
   try { options.Arguments(false); } catch(ArgumentException) { invalidOptions=true; }
   Check(invalidOptions,"out of range icon option rejected");
   Check(Directory.GetFiles(root,".upxdrop-*").Length==0,"all error/cancel temporary files cleaned");
  } finally { Directory.Delete(root,true); }
 }
}
