// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UpxDrop;
class WindowsUpdateTests {
 static int passed;
 static void Check(bool value,string message) { if(!value) throw new Exception(message); passed++; Console.WriteLine("PASS "+message); }
 static void WaitUntil(Func<bool> ready,string message) {
  var watch=Stopwatch.StartNew();
  while(!ready()) { if(watch.Elapsed.TotalSeconds>30) throw new Exception(message); Thread.Sleep(100); }
 }
 static bool TryReadPid(string path, out int pid) {
  pid = 0;
  try { return int.TryParse(File.ReadAllText(path), out pid) && pid > 0; }
  catch(IOException) { return false; }
  catch(UnauthorizedAccessException) { return false; }
 }
 static void StartUpdate(string target,string stage) {
  using(var process=Process.Start(new ProcessStartInfo(target,"\""+stage+"\" "+Updater.HashFile(stage)) { UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(target) })) {
   if(!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Update fixture did not exit."); }
   Check(process.ExitCode==0,"update helper launched and original process exited");
  }
 }
 static void Success(string folder,string oldExe,string newExe) {
  Directory.CreateDirectory(folder);
  string target=Path.Combine(folder,"UPX Drop.exe"),stage=Path.Combine(folder,"new version.exe");
  File.Copy(oldExe,target); File.Copy(newExe,stage); string expected=Updater.HashFile(stage);
  StartUpdate(target,stage);
  int updatedPid=0;
  WaitUntil(()=>TryReadPid(Path.Combine(folder,"updated.marker"),out updatedPid),"Updated executable did not restart.");
  try { using(var process=Process.GetProcessById(updatedPid)) { if(!process.WaitForExit(5000)) throw new Exception("Updated fixture did not exit."); } } catch(ArgumentException) { }
  Check(Updater.HashFile(target)==expected,"installed EXE exactly matches verified update");
  WaitUntil(()=>!File.Exists(stage) && Directory.GetFiles(folder,"*.new*").Length==0,"Successful update did not clean staging files.");
  Check(!File.Exists(Path.Combine(folder,"old.marker")),"new executable restarted from path with Unicode, spaces and apostrophe");
 }
 static void Failure(string folder,string oldExe) {
  Directory.CreateDirectory(folder);
  string target=Path.Combine(folder,"UPX Drop.exe"),stage=Path.Combine(folder,"invalid version.exe");
  File.Copy(oldExe,target); string original=Updater.HashFile(target);
  File.WriteAllText(stage,"MZ-invalid-executable");
  StartUpdate(target,stage);
  string log=Path.Combine(folder,"last-update.log");
  WaitUntil(()=>File.Exists(log) && new FileInfo(log).Length>0,"Failed update did not record failure.");
  Check(Updater.HashFile(target)==original,"failed replacement launch restores original EXE bytes");
  Check(!File.Exists(Path.Combine(folder,"updated.marker")),"invalid update never starts");
  WaitUntil(()=>Directory.GetFiles(folder,"*.new").Length==0,"Failed update left a replacement temporary file.");
 }
 static int Main(string[] args) {
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT) { Console.Error.WriteLine("Windows is required; test was not run."); return 2; }
  string root=Path.Combine(Path.GetTempPath(),"UPX 中文 O'Brien & "+Guid.NewGuid().ToString("N"));
  try {
   Success(Path.Combine(root,"success"),args[0],args[1]);
   Failure(Path.Combine(root,"rollback"),args[0]);
   Console.WriteLine(passed+" Windows update checks passed"); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally {
   if(Directory.Exists(root)) {
    for(int i=0;i<10;i++) {
     try { Directory.Delete(root,true); break; }
     catch(IOException) { if(i==9) throw; Thread.Sleep(200); }
     catch(UnauthorizedAccessException) { if(i==9) throw; Thread.Sleep(200); }
    }
   }
  }
 }
}
