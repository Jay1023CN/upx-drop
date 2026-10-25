// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using UpxDrop;
class UpdaterTests {
 static int passed;
 static void Check(bool condition,string message) { if(!condition) throw new Exception(message); passed++; Console.WriteLine("PASS "+message); }
 static string Json(string tag, bool prerelease, string url, string digest, bool checksum, long size) {
  var assets=new List<object>();
  assets.Add(new { name="UpxDrop.exe", browser_download_url=url, digest=digest, size=size });
  if(checksum) assets.Add(new { name="UpxDrop.exe.sha256", browser_download_url="https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.0/UpxDrop.exe.sha256" });
  return new JavaScriptSerializer().Serialize(new { tag_name=tag, prerelease=prerelease, draft=false, assets=assets });
 }
 static void Rejected(string json,string message) { bool rejected=false; try { Updater.ParseRelease(json); } catch(Exception) { rejected=true; } Check(rejected,message); }
 static int Main() {
  string path=Path.GetTempFileName();
  try {
   string url="https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.0/UpxDrop.exe";
   string digest="sha256:"+new string('a',64);
   var asset=Updater.ParseRelease(Json("v1.2.0",false,url,digest,false,2048));
   Check(asset.Version=="1.2.0" && asset.Digest==new string('a',64),"new stable release selects exact EXE and GitHub digest");
   Check(Updater.ParseRelease(Json("v1.1.0",false,url,digest,false,2048))==null,"equal version does not update");
   Check(Updater.ParseRelease(Json("v1.0.0",false,url,digest,false,2048))==null,"older version does not downgrade");
   Check(Updater.ParseRelease(Json("v2.0.0",true,url,digest,false,2048))==null,"prereleases ignored");
   Check(Updater.ParseRelease(Json("v1.2.0",false,url,null,true,2048)).ChecksumUrl!=null,"checksum asset supported when API digest absent");
   Rejected(Json("v1.2.0",false,url,null,false,2048),"missing checksum rejected");
   Rejected(Json("v1.2.0",false,"https://example.com/UpxDrop.exe",digest,false,2048),"foreign download host rejected");
   Rejected(Json("v1.2.0",false,url.Replace("https:","http:"),digest,false,2048),"unencrypted download URL rejected");
   Rejected(Json("v1.2.0",false,url.Replace("Jay1023CN/upx-drop","other/project"),digest,false,2048),"different repository download rejected");
   Rejected(Json("v1.2.0",false,url,digest,false,128*1024*1024),"oversized update rejected");
   Rejected(Json("main",false,url,digest,false,2048),"non-version tag rejected");
   File.WriteAllText(path,"abc");
   Check(Updater.HashFile(path)=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","SHA-256 verified against known vector");
   bool tampered=false; try { Updater.ApplyAfterExit(new PendingUpdate { Path=path,Sha256=new string('0',64) }); } catch(InvalidDataException) { tampered=true; }
   Check(tampered,"tampered staged executable rejected before installation");
   Console.WriteLine(passed+" checks passed"); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
  finally { File.Delete(path); }
 }
}
