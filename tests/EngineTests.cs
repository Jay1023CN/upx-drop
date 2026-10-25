// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using UpxDrop;
class EngineTests {
 static int passed;
 static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
 static int Main() {
  string root=Path.Combine(Path.GetTempPath(),"upxdrop-engine-test-"+Guid.NewGuid().ToString("N"));
  try {
   string path=EngineDownload.InstallEmbedded(root);
   using(var input=File.OpenRead(path)) Check(input.ReadByte()=='M' && input.ReadByte()=='Z',"verified embedded archive extracts Windows engine");
   Check(path==EngineDownload.InstallEmbedded(root),"repeat installation reuses verified cache");
   long length=new FileInfo(path).Length; File.WriteAllText(path,"corrupted");
   Check(EngineDownload.InstallEmbedded(root)==path && new FileInfo(path).Length==length,"corrupt cache is replaced from verified embedded archive");
   Check(Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp").Length==0,"no extraction temporary files remain");
   Console.WriteLine(passed+" checks passed"); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
 }
}
