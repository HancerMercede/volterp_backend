# Runs the circuit against the DGII test environment (testecf) with the
# issuer's own certificate: seed -> signature -> token, and document ->
# verdict. The certificate is copied out of the repository to the temp
# folder and the password is read from its sidecar file; neither the
# certificate nor the password is ever printed.
#
# Usage (from the repository root):
#   .\t48-dgii-test.ps1

param(
    # The e-NCF and the RNC of the taxpayer: they travel in the environment
    # because they are the taxpayer's own. The number of the range is not
    # reusable (the DGII answers 1209), so each attempt takes the next one.
    [string]$Encf = 'E310000000001',
    [string]$Rnc = '00110205168'
)

$ErrorActionPreference = 'Stop'

$repo = $PSScriptRoot
$source = Join-Path $repo 'certificado\certificado arq.p12'
$sidecar = Join-Path $repo 'certificado\certificado arq.p12.password'
$work = Join-Path $env:TEMP 'volterp-dgii'
$log = Join-Path $repo 't48-certificado.log'

if (-not (Test-Path $source)) { throw "The issuer certificate is not there: $source" }

New-Item -ItemType Directory -Force -Path $work | Out-Null
Copy-Item $source (Join-Path $work 'cert.p12') -Force
Write-Host "copiado: $work"

$env:VOLTERP_DGII_BASE_URL = 'https://ecf.dgii.gov.do'
$env:VOLTERP_DGII_ENVIRONMENT = 'PreCertification'
$env:VOLTERP_DGII_CERTIFICATE = Join-Path $work 'cert.p12'
$env:VOLTERP_DGII_CERTIFICATE_PASSWORD = (Get-Content $sidecar -Raw).Trim()
$env:VOLTERP_DGII_ISSUER_RNC = $Rnc
$env:VOLTERP_DGII_ENCF = $Encf

Set-Location $repo

# The dotnet SDK is not always in the PATH of the shell that runs the
# script, so it is looked up where it is installed.
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path $dotnet)) { throw 'The dotnet SDK was not found.' }

& $dotnet test "$repo\Volterp.ElectronicInvoicing.Tests\Volterp.ElectronicInvoicing.Tests.csproj" `
    -c Release --no-build --disable-build-servers `
    --filter 'FullyQualifiedName~AgainstTheDgii' `
    --logger 'console;verbosity=detailed' *> $log

Write-Host "dotnet: $dotnet"
Write-Host "codigo de salida: $LASTEXITCODE"
Write-Host "log: $((Get-Item $log).Length) bytes"

Get-Content $log -Tail 60

# The copy of the certificate does not outlive the run: the repository keeps
# the original and the copy in the temp folder is deleted here.
Remove-Item (Join-Path $work 'cert.p12') -Force -ErrorAction SilentlyContinue
