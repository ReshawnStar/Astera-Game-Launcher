#Requires -Version 7.2
param(
    [string]$CatalogFile=(Join-Path (Split-Path $PSScriptRoot -Parent) 'content/discover-catalog.json'),
    [Parameter(Mandatory)][string]$OutputFile,
    [string]$PrivateKeyFile='D:/vale/StarsAllies-Storage/keys/Astera/publisher.key',
    [Parameter(Mandatory)][string]$Executable
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputFile){throw 'Choose a new output file; signed catalogs are not overwritten'}
$check=Start-Process -FilePath $Executable -ArgumentList @('--catalog-validate',('"'+[IO.Path]::GetFullPath($CatalogFile)+'"')) -WindowStyle Hidden -Wait -PassThru
if($check.ExitCode -ne 0){throw 'Catalog validation failed'}
Add-Type -AssemblyName System.Security
$protected=[IO.File]::ReadAllBytes($PrivateKeyFile)
$plain=[Security.Cryptography.ProtectedData]::Unprotect($protected,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(2048,[Security.Cryptography.CspParameters]::new(24));$rsa.PersistKeyInCsp=$false
try{$rsa.FromXmlString([Text.Encoding]::UTF8.GetString($plain));$payload=[IO.File]::ReadAllBytes($CatalogFile);$envelope=@{Payload=[Convert]::ToBase64String($payload);Signature=[Convert]::ToBase64String($rsa.SignData($payload,'SHA256'))};[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),($envelope|ConvertTo-Json),[Text.UTF8Encoding]::new($false))}finally{[Array]::Clear($plain,0,$plain.Length);$rsa.Dispose()}
$verify=Start-Process -FilePath $Executable -ArgumentList @('--catalog-verify',('"'+[IO.Path]::GetFullPath($OutputFile)+'"')) -WindowStyle Hidden -Wait -PassThru
if($verify.ExitCode -ne 0){throw 'The prepared catalog does not verify against the app publisher key'}
Write-Output 'Prepared signed catalog locally. Review it before publishing as channels/discover.json. No files were uploaded.'
