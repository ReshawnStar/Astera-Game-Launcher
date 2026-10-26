#Requires -Version 7.2
param(
    [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key',
    [string]$PublicKeyFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'channel.public.xml')
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$private=[IO.Path]::GetFullPath($PrivateKeyFile)
if(Test-Path -LiteralPath $private){throw 'Signing key already exists. Preserve it; do not rotate it accidentally.'}
foreach($path in @($private,[IO.Path]::GetFullPath($PublicKeyFile))){$parent=Split-Path $path -Parent;New-Item -ItemType Directory -Path $parent -Force|Out-Null;while($parent){if((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Redirected key path refused'};$parent=Split-Path $parent -Parent}}
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider(3072,([Security.Cryptography.CspParameters]::new(24)))
$rsa.PersistKeyInCsp=$false
try{
    $raw=[Text.Encoding]::UTF8.GetBytes($rsa.ToXmlString($true))
    $protected=[Security.Cryptography.ProtectedData]::Protect($raw,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($private,$protected)
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($PublicKeyFile),$rsa.ToXmlString($false),[Text.UTF8Encoding]::new($false))
}finally{$rsa.Dispose();if($raw){[Array]::Clear($raw,0,$raw.Length)}}
Write-Output 'Publisher key created, encrypted for this Windows account. Only channel.public.xml belongs in the repository.'
