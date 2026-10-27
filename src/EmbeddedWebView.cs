using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
namespace Astera {
static class EmbeddedWebView {
    static string folder;static bool registered;
    internal static void Register(){if(registered)return;registered=true;AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs args){string name=new AssemblyName(args.Name).Name;if(name!="Microsoft.Web.WebView2.Core"&&name!="Microsoft.Web.WebView2.WinForms")return null;if(folder==null)throw new IOException("Video runtime was not prepared");return Assembly.LoadFrom(Path.Combine(folder,name+".dll"));};}
    internal static string Prepare(string root){Register();string target=Path.Combine(root,"runtime","webview2","1.0.4314.35");Directory.CreateDirectory(target);foreach(string name in new[]{"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll","LICENSE.txt"}){byte[] bytes;using(var source=typeof(EmbeddedWebView).Assembly.GetManifestResourceStream("Astera.WebView2."+name))using(var memory=new MemoryStream()){if(source==null)throw new IOException("Embedded video component is missing");source.CopyTo(memory);bytes=memory.ToArray();}string path=Path.Combine(target,name);bool same=false;if(File.Exists(path))using(var sha=SHA256.Create()){var expected=sha.ComputeHash(bytes);var actual=sha.ComputeHash(File.ReadAllBytes(path));same=Convert.ToBase64String(expected)==Convert.ToBase64String(actual);}if(!same){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}folder=target;return target;}
}
}
