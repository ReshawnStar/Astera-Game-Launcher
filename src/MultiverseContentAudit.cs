using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
namespace Astera {
static class MultiverseContentAudit {
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Reject(Action operation,string message){bool rejected=false;try{operation();}catch(IOException){rejected=true;}if(!rejected)throw new Exception(message);}
    static void Big(BinaryWriter writer,uint value){writer.Write(new[]{(byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value});}
    static void Archive(string path,string version,byte[] image){
        var files=new Dictionary<string,byte[]>{{"data/text_misc.xml",Encoding.UTF8.GetBytes("<text name=\"version\">Version "+version+" [Mod]</text>")}};
        if(image!=null)files["img/ship/test_gib1.png"]=image;
        byte[] names=Encoding.ASCII.GetBytes(String.Join("\0",files.Keys)+"\0");
        using(var stream=File.Create(path))using(var writer=new BinaryWriter(stream)){
            writer.Write(Encoding.ASCII.GetBytes("PKG\n"));writer.Write(new byte[]{0,16,0,20});Big(writer,(uint)files.Count);Big(writer,(uint)names.Length);int nameOffset=0,offset=16+20*files.Count+names.Length;
            foreach(var file in files){Big(writer,0);Big(writer,(uint)nameOffset);Big(writer,(uint)offset);Big(writer,(uint)file.Value.Length);Big(writer,(uint)file.Value.Length);nameOffset+=file.Key.Length+1;offset+=file.Value.Length;}
            writer.Write(names);foreach(var bytes in files.Values)writer.Write(bytes);
        }
    }
    static void Zip(string path,string version,bool image){
        using(var file=File.Create(path))using(var zip=new ZipArchive(file,ZipArchiveMode.Create)){
            using(var writer=new StreamWriter(zip.CreateEntry("mod-appendix/metadata.xml").Open()))writer.Write("<metadata><version><![CDATA[ "+version+" ]]></version></metadata>");
            if(image)using(var stream=zip.CreateEntry("img/ship/test_gib1.png").Open())stream.Write(new byte[]{1,2,3},0,3);
        }
    }
    internal static IEnumerable<string> Run(string root){
        Directory.CreateDirectory(root);string data=Path.Combine(root,"Data.zip"),assets=Path.Combine(root,"Assets.zip"),dat=Path.Combine(root,"ftl.dat");
        Zip(data,"5.5.1",false);Zip(assets,"5.5",true);Archive(dat,"5.5.1",new byte[]{1,2,3});
        var plan=new FtlPlan{Stage=root,DataVersion="5.5.1",AssetsVersion="5.5",HyperspaceVersion="1.23.2",Data=new FtlAsset{Name="Data.zip"},Assets=new FtlAsset{Name="Assets.zip"},Tool=new FtlAsset{Name="tool.zip"}};
        plan.Downloads["Data.zip"]=data;plan.Downloads["Assets.zip"]=assets;plan.Downloads["tool.zip"]="tool-cache";
        Multiverse.VerifyContent(plan,Multiverse.ArchiveNames(dat));
        Require(Multiverse.InstalledDataVersion(dat)=="5.5.1","Did not read the actual installed version");
        var checks=new List<string>{"Actual SIL data version and distinct Data5.5.1/Assets5.5 manifests are validated"};
        Archive(dat,"5.5",new byte[]{1,2,3});Reject(()=>Multiverse.VerifyContent(plan,Multiverse.ArchiveNames(dat)),"Stale patched Data accepted");
        Archive(dat,"5.5.1",null);Reject(()=>Multiverse.VerifyContent(plan,Multiverse.ArchiveNames(dat)),"Missing ship debris accepted");
        Archive(dat,"5.5.1",new byte[]{9,9,9});Reject(()=>Multiverse.VerifyContent(plan,Multiverse.ArchiveNames(dat)),"Wrong debris bytes accepted");
        Archive(dat,"5.5.1",new byte[]{1,2,3});Zip(data,"5.5",false);Reject(()=>Multiverse.VerifyContent(plan,Multiverse.ArchiveNames(dat)),"Mislabeled Data archive accepted");
        checks.Add("Stale game data, missing debris, wrong debris bytes and mismatched download metadata are rejected before live installation");
        Multiverse.SelectGibs(plan,true);plan.Downloads[plan.Gibs.Name]="gibs-cache";
        Require(Multiverse.PatchArguments(plan).Skip(3).SequenceEqual(new[]{assets,data,"gibs-cache"}),"Wrong addon patch order");
        Require(Multiverse.Payloads(plan).Count()==4,"Addon missing from download/progress budget");
        var installed=new FtlState{DataVersion=plan.DataVersion,AssetsVersion=plan.AssetsVersion,HyperspaceVersion=plan.HyperspaceVersion};
        Require(Multiverse.NeedsUpdate(installed,plan),"Missing enabled addon did not offer update");installed.GibsVersion=plan.GibsVersion;
        Require(!Multiverse.NeedsUpdate(installed,plan),"Identical installed addon repeatedly offers updates");
        Multiverse.SelectGibs(plan,false);Require(Multiverse.NeedsUpdate(installed,plan)&&Multiverse.Payloads(plan).Count()==3,"Turning off addon did not request a clean base rebuild");
        checks.Add("Optional GenGibs is patched after Assets/Data, participates in downloads and updates exactly when its selected state changes");
        plan.DataVersion="5.6";Reject(()=>Multiverse.SelectGibs(plan,true),"Unverified future Multiverse compatibility accepted");
        checks.Add("Pinned GenGibs for5.5 is never silently applied to an unsupported future Multiverse release");
        return checks;
    }
}
}
