using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC2Pack;
namespace Astera {
static class AsteraDiagnostics {
    static readonly object Gate=new object();
    public static string Root,SessionLog;
    public static void Initialize(string applicationRoot){lock(Gate){Root=Path.Combine(applicationRoot,"logs");Directory.CreateDirectory(Root);SessionLog=Path.Combine(Root,"astera-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+System.Diagnostics.Process.GetCurrentProcess().Id+".log");Write("Session started",null);}}
    public static void Write(string context,Exception error){try{lock(Gate){if(SessionLog==null)return;File.AppendAllText(SessionLog,DateTime.UtcNow.ToString("o")+"  Astera "+Assembly.GetExecutingAssembly().GetName().Version+"  "+context+Environment.NewLine+(error==null?"":error.ToString()+Environment.NewLine)+Environment.NewLine,new UTF8Encoding(false));}}catch(IOException){}catch(UnauthorizedAccessException){}}
    public static void Report(string context,Exception error){Write(context,error);MessageBox.Show("Astera hit an error: "+error.Message+"\n\nDetails: "+(SessionLog??"The log folder could not be opened."),"Astera error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
}

static class GameDiagnostics {
    static readonly object Gate=new object();
    // Read only known diagnostic locations. Never move or remove a game's files.
    public static int Collect(string logRoot,string game,string profile,string godotRoot,bool includeRuntime){lock(Gate){
        var sources=new List<string>();
        if(game=="minecraft"&&!String.IsNullOrWhiteSpace(profile)&&Directory.Exists(profile)){
            string crash=Path.Combine(profile,"crash-reports");if(Directory.Exists(crash))sources.AddRange(Directory.GetFiles(crash,"crash-*.txt").OrderByDescending(File.GetLastWriteTimeUtc).Take(5));
            sources.AddRange(Directory.GetFiles(profile,"hs_err_pid*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(3));
            if(includeRuntime){string latest=Path.Combine(profile,"logs","latest.log");if(File.Exists(latest))sources.Add(latest);}
        }
        if(game=="stardash"&&includeRuntime&&Directory.Exists(godotRoot))sources.AddRange(Directory.GetFiles(godotRoot,"godot*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(3));
        string destination=Path.Combine(logRoot,"games",game);Directory.CreateDirectory(destination);var copied=new List<object>();
        foreach(string source in sources){
            try{
                const int cap=4*1024*1024;byte[] bytes;long original;bool tail;
                using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                    original=input.Length;tail=original>cap;if(tail)input.Seek(-cap,SeekOrigin.End);using(var output=new MemoryStream()){var buffer=new byte[65536];int read;while(output.Length<cap&&(read=input.Read(buffer,0,(int)Math.Min(buffer.Length,cap-output.Length)))>0)output.Write(buffer,0,read);bytes=output.ToArray();}
                }
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                string file=Path.GetFileNameWithoutExtension(source)+"-"+hash.Substring(0,16)+(tail?"-tail":"")+Path.GetExtension(source),target=Path.Combine(destination,file);
                if(File.Exists(target))continue;
                using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))output.Write(bytes,0,bytes.Length);
                copied.Add(new{Source=source,File=file,Sha256=hash,OriginalBytes=original,CopiedBytes=bytes.Length,TailOnly=tail});
            }catch(IOException error){AsteraDiagnostics.Write("Game diagnostic read skipped: "+source,error);}catch(UnauthorizedAccessException error){AsteraDiagnostics.Write("Game diagnostic access skipped: "+source,error);}
        }
        if(copied.Count>0)File.WriteAllText(Path.Combine(destination,"collection-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".json"),Engine.Json.Serialize(new{CollectedUtc=DateTime.UtcNow.ToString("o"),OriginalsPreserved=true,Files=copied}));
        return copied.Count;
    }}
}

sealed partial class LibraryForm {
    readonly System.Windows.Forms.Timer diagnosticsTimer=new System.Windows.Forms.Timer();bool collectingDiagnostics;
    string DiagnosticsRoot {get{return Path.Combine(store.Root,"logs");}}
    static string GodotLogRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Godot","app_userdata","StarDash","logs");}}
    void StartDiagnostics(){diagnosticsTimer.Interval=60000;diagnosticsTimer.Tick+=delegate{ImportCrashReports();};diagnosticsTimer.Start();Shown+=delegate{ImportCrashReports();};}
    async void ImportCrashReports(){if(collectingDiagnostics||IsDisposed||String.IsNullOrWhiteSpace(store.Settings.Profile))return;collectingDiagnostics=true;string profile=store.Settings.Profile;try{int count=await Task.Run(()=>GameDiagnostics.Collect(DiagnosticsRoot,"minecraft",profile,GodotLogRoot,false));if(count>0)Log("Saved "+count+" new Minecraft crash report(s) in Astera logs.");}catch(Exception error){AsteraDiagnostics.Write("Automatic crash report collection",error);}finally{collectingDiagnostics=false;}}
    async void CollectGameLogs(){if(collectingDiagnostics){Log("A diagnostic collection is already running.");return;}collectingDiagnostics=true;string game=games.SelectedIndex==0?"minecraft":"stardash",profile=store.Settings.Profile;try{int count=await Task.Run(()=>GameDiagnostics.Collect(DiagnosticsRoot,game,profile,GodotLogRoot,true));Log(count==0?"No new game reports found. Existing copies remain in Astera logs.":"Saved "+count+" game log(s). Original files were preserved.");if(!IsDisposed){SelectLibraryTab(1);OpenDiagnostics();}}catch(Exception error){AsteraDiagnostics.Report("Collect game logs",error);}finally{collectingDiagnostics=false;}}
    void OpenDiagnostics(){try{Directory.CreateDirectory(DiagnosticsRoot);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DiagnosticsRoot){UseShellExecute=true});}catch(Exception error){AsteraDiagnostics.Report("Open diagnostics folder",error);}}
    void AddDiagnosticButtons(){var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=96,WrapContents=true,Padding=new Padding(0,8,0,0)};var open=Button("Open logs",delegate{OpenDiagnostics();});var collect=Button("Collect game logs",delegate{CollectGameLogs();});row.Controls.Add(open);row.Controls.Add(collect);activityCard.Controls.Add(row);row.BringToFront();}
}
}
