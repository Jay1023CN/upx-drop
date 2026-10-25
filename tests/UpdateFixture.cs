// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using UpxDrop;
class UpdateFixture {
 static int Main(string[] args) {
  try {
   if(args.Length==2) {
    Updater.ApplyAfterExit(new PendingUpdate { Path=args[0], Sha256=args[1], Version="1.2.0" });
   } else {
#if UPDATED
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"updated.marker"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
#else
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"old.marker"),"old version started");
#endif
   }
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
