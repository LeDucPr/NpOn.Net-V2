param(
    [string]$TrustDomain = "cluster.local",
    [string]$Namespace = "default",
    [int]$Days = 365
)

$ErrorActionPreference = "Stop"
$certsDir = Join-Path $PSScriptRoot "certs"
if (-not (Test-Path $certsDir)) {
    New-Item -ItemType Directory -Path $certsDir -Force | Out-Null
}

function Resolve-OpenSsl {
    $gitOpenSsl = Join-Path $env:ProgramFiles "Git\usr\bin\openssl.exe"
    if (Test-Path $gitOpenSsl) { return $gitOpenSsl }

    $cmd = Get-Command openssl -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

$opensslPath = Resolve-OpenSsl

if ($opensslPath) {
    Write-Host "[ENVOY CERTS] Generating SPIFFE mTLS certificates using OpenSSL: $opensslPath" -ForegroundColor Cyan
    $caKey = Join-Path $certsDir "ca.key"
    $caCrt = Join-Path $certsDir "ca.crt"
    $tmpCnf = Join-Path $certsDir "openssl_tmp.cnf"

    @"
[req]
distinguished_name = req_distinguished_name
prompt = no

[req_distinguished_name]
CN = NpOn-ServiceMesh
"@ | Set-Content -Path $tmpCnf -Encoding Ascii

    try {
        & $opensslPath req -x509 -newkey rsa:4096 -nodes -sha256 -days 3650 `
            -config $tmpCnf -keyout $caKey -out $caCrt -subj "/CN=NpOn-ServiceMesh-CA" `
            -addext "basicConstraints=critical,CA:TRUE" `
            -addext "keyUsage=critical,keyCertSign,cRLSign"
        if ($LASTEXITCODE -ne 0) { throw "Failed to generate CA certificate." }

        $services = @(
            @{ Name = "sso"; Envoy = "envoy-sso" },
            @{ Name = "accountservice"; Envoy = "envoy-account" },
            @{ Name = "generalservice"; Envoy = "envoy-general" }
        )

        foreach ($svc in $services) {
            $name = $svc.Name
            $envoy = $svc.Envoy
            $spiffe = "spiffe://$TrustDomain/ns/$Namespace/sa/$name"
            $extFile = Join-Path $certsDir "$name.ext"
            $csrFile = Join-Path $certsDir "$name.csr"
            $keyFile = Join-Path $certsDir "$name.key"
            $crtFile = Join-Path $certsDir "$name.crt"

            @"
basicConstraints=CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth,clientAuth
subjectAltName=URI:$spiffe,DNS:$name,DNS:$envoy,DNS:localhost,IP:127.0.0.1
"@ | Set-Content -Path $extFile -Encoding Ascii

            & $opensslPath req -new -newkey rsa:2048 -nodes -config $tmpCnf -keyout $keyFile -out $csrFile -subj "/CN=$name"
            if ($LASTEXITCODE -ne 0) { throw "Failed to create CSR for $name" }

            & $opensslPath x509 -req -in $csrFile -CA $caCrt -CAkey $caKey -CAcreateserial `
                -out $crtFile -days $Days -sha256 -extfile $extFile
            if ($LASTEXITCODE -ne 0) { throw "Failed to sign certificate for $name" }

            Remove-Item $extFile, $csrFile -Force -ErrorAction SilentlyContinue
            Write-Host "  -> Issued $name ($spiffe)" -ForegroundColor Green
        }
    }
    finally {
        Remove-Item $tmpCnf, (Join-Path $certsDir "ca.srl") -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[ENVOY CERTS] Local openssl not found; falling back to Docker alpine/openssl..." -ForegroundColor Yellow
    docker run --rm -v "${PSScriptRoot}:/work" -w /work -e TRUST_DOMAIN="$TrustDomain" -e NAMESPACE="$Namespace" -e DAYS="$Days" alpine/openssl sh /work/gen-certs.sh
    if ($LASTEXITCODE -ne 0) { throw "Docker alpine/openssl certificate generation failed." }
}

Write-Host "[ENVOY CERTS] Done. Certificates stored in $certsDir" -ForegroundColor Green
