#Requires -Version 7.2
param(
    [string]$FeedUrl='',
    [string]$PublicKeyFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'channel.public.xml'),
    [string]$IconFile='',
    [string]$OutputDirectory=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$FeedUrl -and (Test-Path -LiteralPath (Join-Path $root 'channel-settings.json'))){$FeedUrl=(Get-Content -Raw -LiteralPath (Join-Path $root 'channel-settings.json')|ConvertFrom-Json).FeedUrl}
if($FeedUrl){$uri=[Uri]$FeedUrl;if(!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment){throw 'Feed URL must be HTTPS without credentials or a fragment'}}
$public=Get-Content -Raw -LiteralPath $PublicKeyFile
if($public -match '<D>|<P>|<Q>|<DP>|<DQ>|<InverseQ>'){throw 'Refusing to embed a private key'}
$version=(Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim();if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'VERSION must be major.minor.patch'};if(!$OutputDirectory){$OutputDirectory='D:/vale/StarsAllies-Storage/builds/Astera/'+$version};$out=[IO.Path]::GetFullPath($OutputDirectory)
$existing=Join-Path $out 'Astera.exe';if(Test-Path -LiteralPath $existing){try{$handle=[IO.File]::Open($existing,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$handle.Dispose()}catch{throw 'The output EXE is in use or blocked. Close that test process or choose a fresh output directory.'}}
New-Item -ItemType Directory -Path $out -Force|Out-Null
$channel=Join-Path $out 'channel.json'
@{FeedUrl=$FeedUrl;AppFeedUrl=(Get-Content -LiteralPath (Join-Path $root 'channel-settings.json') -Raw|ConvertFrom-Json).AppFeedUrl;PublicKeyXml=$public}|ConvertTo-Json|Set-Content -LiteralPath $channel -Encoding utf8
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe=Join-Path $out 'Astera.exe'; $versionSource=Join-Path $out 'version.g.cs';[IO.File]::WriteAllText($versionSource,('[assembly: System.Reflection.AssemblyVersion("'+$version+'.0")][assembly: System.Reflection.AssemblyFileVersion("'+$version+'.0")]'),[Text.UTF8Encoding]::new($false))
$sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File|Sort-Object Name|ForEach-Object FullName)
if(!$IconFile -and (Test-Path -LiteralPath (Join-Path $root 'assets/astera.ico'))){$IconFile=Join-Path $root 'assets/astera.ico'}
$art=@();if($IconFile){if([IO.Path]::GetExtension($IconFile) -ne '.ico'){throw 'Use a Windows .ico for -IconFile'};$art+=('/win32icon:'+([IO.Path]::GetFullPath($IconFile)))}
$art+=('/resource:'+(Join-Path $root 'assets/astera-icon-source.png')+',Astera.Brand.Mark')
$art+=('/resource:'+(Join-Path $root 'content/startup/astera_startup_lines.json')+',Astera.Startup.Lines')
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
$sheets=Join-Path $root 'assets/button-art'
foreach($item in @(@{File='button-atlas.png';Resource='Buttons'},@{File='button-mask.png';Resource='Mask'})){
    $sheet=Join-Path $sheets $item.File
    if(!(Test-Path -LiteralPath $sheet)){throw ('Missing artist button sheet: '+$sheet)}
    if((Get-Item -LiteralPath $sheet).Length -gt 8MB){throw 'Button sheet exceeds 8 MB'}
    Add-Type -AssemblyName System.Drawing
    $image=[Drawing.Image]::FromFile($sheet)
    try{if($image.Width -ne 2164 -or $image.Height -ne 727 -or $image.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid){throw 'Button sheets must be 2164x727 PNGs; update the source map if replacing their layout'}}finally{$image.Dispose()}
    $art+=('/resource:'+$sheet+',Astera.Art.'+$item.Resource)
}
$icons=Join-Path $root 'assets/ui-v1/icons/png_128'
foreach($icon in Get-ChildItem -LiteralPath $icons -Filter '*.png' -File){
    if($icon.BaseName -cnotmatch '^[a-z][a-z-]*$' -or $icon.Length -gt 1MB){throw 'Unsupported UI icon asset'}
    $image=[Drawing.Image]::FromFile($icon.FullName)
    try{if($image.Width -ne 128 -or $image.Height -ne 128){throw ('UI icons must be 128x128: '+$icon.Name)}}finally{$image.Dispose()}
    $art+=('/resource:'+$icon.FullName+',Astera.Icons.'+$icon.BaseName)
}
$background=Join-Path $root 'assets/backgrounds/minecraft-squared.png'
if(!(Test-Path -LiteralPath $background) -or (Get-Item -LiteralPath $background).Length -gt 25MB){throw 'Minecraft background is missing or too large'}
$art+=('/resource:'+$background+',Astera.Backgrounds.MinecraftSquared')
$art+=('/resource:'+(Join-Path $root 'assets/backgrounds/ftl-multiverse.png')+',Astera.Backgrounds.FtlMultiverse')
$art+=('/resource:'+(Join-Path $root 'assets/games/ftl-multiverse-icon.png')+',Astera.Games.FtlMultiverseIcon')
$art+=('/resource:'+(Join-Path $root 'assets/games/minecraft-squared-icon.png')+',Astera.Games.MinecraftSquaredIcon')
$art+=('/resource:'+(Join-Path $root 'content/minecraft-0.7.173-gf205.md')+',Astera.Notes.Minecraft205')
$art+=('/resource:'+(Join-Path $root 'app-release-notes.md')+',Astera.Notes.App')
foreach($item in @(@{File='assets/backgrounds/stardash.png';Resource='Astera.Backgrounds.StarDash'},@{File='assets/games/stardash-logo.png';Resource='Astera.Games.StarDashLogo'},@{File='assets/games/stardash-icon.png';Resource='Astera.Games.StarDashIcon'})){
    $file=Join-Path $root $item.File
    if(!(Test-Path -LiteralPath $file) -or (Get-Item -LiteralPath $file).Length -gt 25MB){throw ('Game artwork missing or too large: '+$item.File)}
    $art+=('/resource:'+$file+','+$item.Resource)
}
$webview=Join-Path $root 'assets/vendor/webview2'
$provenance=Get-Content -LiteralPath (Join-Path $webview 'provenance.json') -Raw|ConvertFrom-Json
foreach($file in $provenance.Files){$path=Join-Path $webview $file.Name;if((Get-FileHash -LiteralPath $path).Hash -ne $file.Sha256){throw 'WebView2 component differs from its pinned package'};$art+=('/resource:'+$path+',Astera.WebView2.'+$file.Name)}
$art+=('/resource:'+(Join-Path $webview 'LICENSE.txt')+',Astera.WebView2.LICENSE.txt');$art+=('/r:'+(Join-Path $webview 'Microsoft.Web.WebView2.Core.dll'));$art+=('/r:'+(Join-Path $webview 'Microsoft.Web.WebView2.WinForms.dll'));$art+=('/win32manifest:'+(Join-Path $root 'app.manifest'))
$yaml=Join-Path $root 'assets/vendor/yamldotnet'
$yamlInfo=Get-Content -Raw -LiteralPath (Join-Path $yaml 'provenance.json')|ConvertFrom-Json
$yamlDll=Join-Path $yaml 'YamlDotNet.dll'
if((Get-FileHash -LiteralPath $yamlDll).Hash -ne $yamlInfo.DllSha256){throw 'YAML dependency differs from pinned package'}
$yamlIdentity=Join-Path $out 'yaml-identity.g.cs'
[IO.File]::WriteAllText($yamlIdentity,('namespace Astera { static class ThunderYamlIdentity { internal const string Sha256="'+$yamlInfo.DllSha256+'"; } }'))
$sources+=$yamlIdentity
$art+=('/r:'+$yamlDll)
$art+=('/resource:'+$yamlDll+',Astera.YamlDotNet')
$art+=('/resource:'+(Join-Path $yaml 'LICENSE.txt')+',Astera.YamlDotNet.LICENSE')
$art+=('/resource:'+(Join-Path $root 'assets/thunderstore-games.json')+',Astera.Thunderstore.Games')
$helper=Join-Path $out 'Astera.Updater.exe'
& $compiler /nologo /codepage:65001 /define:ASTERA_UPDATER /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/out:$helper" "/resource:$channel,Astera.Channel" ('/win32manifest:'+(Join-Path $root 'app.manifest')) (Join-Path $root 'updater/UpdaterEntry.cs') (Join-Path $root 'src/SelfUpdate.cs') (Join-Path $root 'src/SelfUpdateFiles.cs') $versionSource
if($LASTEXITCODE -ne 0){throw 'Dedicated updater compilation failed'}
$helperHash=(Get-FileHash -LiteralPath $helper).Hash
$helperIdentity=Join-Path $out 'updater-identity.g.cs'
[IO.File]::WriteAllText($helperIdentity,('namespace Astera { static class UpdaterIdentity { internal const string Sha256="'+$helperHash+'"; } }'))
$sources+=$helperIdentity
$art+=('/resource:'+$helper+',Astera.Updater')
& $compiler /nologo /codepage:65001 /target:winexe /unsafe+ /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$exe" "/resource:$channel,Astera.Channel" @art @sources $versionSource
if($LASTEXITCODE -ne 0){throw 'Astera compilation failed'}
@{Version=$version;Executable='Astera.exe';UpdaterSha256=$helperHash;UpdaterBytes=(Get-Item -LiteralPath $helper).Length;Sha256=(Get-FileHash -LiteralPath $exe).Hash;FeedConfigured=[bool]$FeedUrl;BuiltUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'build-receipt.json')
Write-Output ('Built '+$exe)
