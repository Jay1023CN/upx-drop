// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UpxDrop;
class RealEngineTests {
 static int passed;
 static void Check(bool value,string message) { if(!value) throw new Exception(message); passed++; Console.WriteLine("PASS "+message); }
 static string Run(string exe) {
  using(var process=Process.Start(new ProcessStartInfo(exe) { UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true })) {
   Task<string> output=process.StandardOutput.ReadToEndAsync();
   if(!process.WaitForExit(10000)) { process.Kill(); process.WaitForExit(); throw new Exception("Native fixture timed out."); }
   if(process.ExitCode!=0) throw new Exception("Native fixture failed.");
   return output.GetAwaiter().GetResult();
  }
 }
 static async Task Tests(string source,string root) {
  string folder=Path.Combine(root,"中文 files"); Directory.CreateDirectory(folder);
  string exe=Path.Combine(folder,"sample & fixture.exe"); File.Copy(source,exe);
  byte[] original=File.ReadAllBytes(exe); string expected=Run(exe);
  Check(expected.Trim()=="UPX Drop native fixture: 6","uncompressed native fixture executes");
  string engine=EngineDownload.InstallEmbedded(Path.Combine(root,"engine"));
  var runner=new UpxRunner(engine); var options=new PackOptions { Backup=true };
  var packed=await runner.ProcessAsync(exe,options,false,CancellationToken.None);
  Check(packed.Status=="已压缩" && new FileInfo(exe).Length<original.Length,"official Windows UPX compresses native PE with default options");
  Check(Run(exe)==expected,"compressed native executable retains behavior");
  string[] backups=Directory.GetFiles(folder,"*.bak");
  Check(backups.Length==1 && Convert.ToBase64String(File.ReadAllBytes(backups[0]))==Convert.ToBase64String(original),"real compression backup preserves original bytes");
  var unpacked=await runner.ProcessAsync(exe,options,true,CancellationToken.None);
  Check(unpacked.Status=="已解压" && Run(exe)==expected,"official Windows UPX decompresses to working native executable");
  Check(Directory.GetFiles(folder,"*.bak").Length==2,"decompression keeps separate backup");
  Check(Directory.GetFiles(folder,".upxdrop-*").Length==0,"real engine processing cleans temporary files");
 }
 static int Main(string[] args) {
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT) { Console.Error.WriteLine("Windows is required; test was not run."); return 2; }
  string root=Path.Combine(Path.GetTempPath(),"upxdrop-real-engine-"+Guid.NewGuid().ToString("N"));
  try { Tests(args[0],root).GetAwaiter().GetResult(); Console.WriteLine(passed+" real Windows engine checks passed"); return 0; }
  catch(Exception ex) { Console.WriteLine("::error::"+ex.ToString().Replace("%","%25").Replace("\r","%0D").Replace("\n","%0A")); Console.Error.WriteLine(ex); return 1; }
  finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
 }
}
