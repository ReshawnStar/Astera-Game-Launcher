#Requires -Version 7.2
param(
    [string]$FeedUrl='',
    [string]$PublicKeyFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'channel.public.xml'),
    [string]$IconFile='',
    [string]$OutputDirectory='D:/vale/StarsAllies-Storage/builds/Astera/0.1.2'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$FeedUrl -and (Test-Path -LiteralPath (Join-Path $root 'channel-settings.json'))){$FeedUrl=(Get-Content -Raw -LiteralPath (Join-Path $root 'channel-settings.json')|ConvertFrom-Json).FeedUrl}
if($FeedUrl){$uri=[Uri]$FeedUrl;if(!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment){throw 'Feed URL must be HTTPS without credentials or a fragment'}}
$public=Get-Content -Raw -LiteralPath $PublicKeyFile
if($public -match '<D>|<P>|<Q>|<DP>|<DQ>|<InverseQ>'){throw 'Refusing to embed a private key'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force|Out-Null
$channel=Join-Path $out 'channel.json'
@{FeedUrl=$FeedUrl;PublicKeyXml=$public}|ConvertTo-Json|Set-Content -LiteralPath $channel -Encoding utf8
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe=Join-Path $out 'Astera.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File|Sort-Object Name|ForEach-Object FullName)
if(!$IconFile -and (Test-Path -LiteralPath (Join-Path $root 'assets/astera.ico'))){$IconFile=Join-Path $root 'assets/astera.ico'}
$art=@();if($IconFile){if([IO.Path]::GetExtension($IconFile) -ne '.ico'){throw 'Use a Windows .ico for -IconFile'};$art+=('/win32icon:'+([IO.Path]::GetFullPath($IconFile)))}
$buttons=Join-Path $root 'assets/buttons'
if(Test-Path -LiteralPath $buttons){
    Add-Type -AssemblyName System.Drawing
    foreach($file in Get-ChildItem -LiteralPath $buttons -Filter '*.png' -File){
        if($file.BaseName -cnotmatch '^(primary|secondary|quiet)-(normal|hover|pressed|disabled)$'){throw ('Unknown button frame name: '+$file.Name)}
        if($file.Length -gt 8MB){throw ('Button frame exceeds 8 MB: '+$file.Name)}
        $frame=[Drawing.Image]::FromFile($file.FullName)
        try{if($frame.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid -or $frame.Width -lt 3 -or $frame.Height -lt 3 -or $frame.Width -gt 4096 -or $frame.Height -gt 4096){throw ('Unsupported PNG frame: '+$file.Name)}}finally{$frame.Dispose()}
        $art+=('/resource:'+$file.FullName+',Astera.Buttons.'+$file.BaseName)
    }
}
& $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$exe" "/resource:$channel,Astera.Channel" @art @sources
if($LASTEXITCODE -ne 0){throw 'Astera compilation failed'}
@{Version='0.1.2';Executable='Astera.exe';Sha256=(Get-FileHash -LiteralPath $exe).Hash;FeedConfigured=[bool]$FeedUrl;BuiltUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'build-receipt.json')
Write-Output ('Built '+$exe)
