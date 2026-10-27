using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
namespace Astera {
sealed class WinHttpHandle:SafeHandleZeroOrMinusOneIsInvalid {public WinHttpHandle():base(true){}protected override bool ReleaseHandle(){return WindowsHttps.WinHttpCloseHandle(handle);}}
sealed class WindowsHttpsResponse {internal int Status;internal string Location;internal byte[] Body;}
static class WindowsHttps {
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern WinHttpHandle WinHttpOpen(string agent,uint access,string proxy,string bypass,uint flags);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern WinHttpHandle WinHttpConnect(WinHttpHandle session,string host,ushort port,uint reserved);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern WinHttpHandle WinHttpOpenRequest(WinHttpHandle connect,string verb,string path,string version,string referer,IntPtr accept,uint flags);
    [DllImport("winhttp.dll",SetLastError=true)]static extern bool WinHttpSetTimeouts(WinHttpHandle session,int resolve,int connect,int send,int receive);
    [DllImport("winhttp.dll",SetLastError=true)]static extern bool WinHttpSetOption(WinHttpHandle handle,uint option,ref uint value,uint size);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WinHttpSendRequest(WinHttpHandle request,string headers,uint length,IntPtr optional,uint optionalLength,uint total,IntPtr context);
    [DllImport("winhttp.dll",SetLastError=true)]static extern bool WinHttpReceiveResponse(WinHttpHandle request,IntPtr reserved);
    [DllImport("winhttp.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WinHttpQueryHeaders(WinHttpHandle request,uint query,string name,StringBuilder value,ref uint length,IntPtr index);
    [DllImport("winhttp.dll",SetLastError=true)]static extern bool WinHttpReadData(WinHttpHandle request,byte[] buffer,uint size,out uint read);
    [DllImport("winhttp.dll",SetLastError=true)]internal static extern bool WinHttpCloseHandle(IntPtr handle);
    static void Check(bool result,CancellationToken token){token.ThrowIfCancellationRequested();if(!result)throw new IOException("Windows HTTPS request failed",new Win32Exception(Marshal.GetLastWin32Error()));}
    static string Header(WinHttpHandle request,uint query){uint size=8192;var text=new StringBuilder(4096);return WinHttpQueryHeaders(request,query,null,text,ref size,IntPtr.Zero)?text.ToString():null;}
    internal static WindowsHttpsResponse Get(Uri uri,int limit,CancellationToken token){if(uri.Scheme!="https"||!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.UserInfo))throw new IOException("Use an HTTPS endpoint");token.ThrowIfCancellationRequested();using(var session=WinHttpOpen("Astera/0.1",4,null,null,0)){if(session.IsInvalid)throw new IOException("Windows HTTPS unavailable");Check(WinHttpSetTimeouts(session,10000,10000,10000,10000),token);uint protocols=0x2800;if(!WinHttpSetOption(session,84,ref protocols,4)){protocols=0x800;Check(WinHttpSetOption(session,84,ref protocols,4),token);}using(var connect=WinHttpConnect(session,uri.Host,443,0))using(var request=WinHttpOpenRequest(connect,"GET",uri.PathAndQuery,null,null,IntPtr.Zero,0x800000)){if(connect.IsInvalid||request.IsInvalid)throw new IOException("Windows HTTPS handle unavailable");uint disabled=2;Check(WinHttpSetOption(request,63,ref disabled,4),token);Check(WinHttpSendRequest(request,null,0,IntPtr.Zero,0,0,IntPtr.Zero),token);Check(WinHttpReceiveResponse(request,IntPtr.Zero),token);int status;if(!Int32.TryParse(Header(request,19),out status))throw new IOException("Invalid HTTP status");var result=new WindowsHttpsResponse{Status=status,Location=Header(request,33),Body=new byte[0]};if(status!=200)return result;long length;if(Int64.TryParse(Header(request,5),out length)&&length>limit)throw new IOException("Response too large");using(var output=new MemoryStream()){var buffer=new byte[16384];uint n;do{token.ThrowIfCancellationRequested();Check(WinHttpReadData(request,buffer,(uint)buffer.Length,out n),token);if(output.Length+n>limit)throw new IOException("Response too large");output.Write(buffer,0,(int)n);}while(n>0);result.Body=output.ToArray();return result;}}}}
}
}
