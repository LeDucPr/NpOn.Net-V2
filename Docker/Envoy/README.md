# Envoy Option 3B ServiceMesh for NpOn.Net-V2 (Docker)

This folder provides **Option 3B (Envoy Sidecar ServiceMesh)** with **TLS 1.3 mTLS + SPIFFE SVIDs** between services and local **`h2c` + `x-forwarded-client-cert` (XFCC)** between Envoy and the .NET 10 services.

## Architecture

- **Inbound (`:15443`)**: Envoy terminates TLS 1.3 mTLS, validates peer certificates against `/certs/ca.crt` and `spiffe://cluster.local/`, sets `x-forwarded-client-cert` (`SANITIZE_SET`, `URI` + `Subject` + `DNS`), and forwards HTTP/2 (`h2c`) to the local .NET service (`sso:14023`, `accountservice:40004`, `generalservice:40000`).
- **Outbound (`:15004` AccountService, `:15000` GeneralService, `:15023` SSO)**: .NET services call their local Envoy sidecar over plaintext `h2c` (`AccountServiceUrl`, `GeneralServiceUrl`), and Envoy upgrades to TLS 1.3 mTLS targeting the peer Envoy's `:15443` with exact SPIFFE SAN pinning.
- **.NET Services**: Run with `InterServiceAuthMode=ServiceMesh`, `InterServiceUseMTLS=false`, `InterServiceServiceMeshTrustDomain=cluster.local`, and `InterServiceTrustedProxyIps=127.0.0.1,::1,172.16.0.0/12,192.168.0.0/16` (zero per-RPC JWT generation or TLS handshake cost inside .NET).

## 1. Generate SPIFFE mTLS Certificates

Generated `.crt`/`.key` files in `Docker/Envoy/certs/` are ignored by `.gitignore`.

### Windows (PowerShell)
```powershell
.\Docker\Envoy\gen-certs.ps1
```

### Linux / macOS
```bash
sh ./Docker/Envoy/gen-certs.sh
```

## 2. Run Full Stack with Envoy ServiceMesh (`compose.yaml` + `compose.envoy.yaml`)

From repository root:
```bash
docker compose -f compose.yaml -f compose.envoy.yaml config
docker compose -f compose.yaml -f compose.envoy.yaml up -d
```

## 3. Standalone Envoy Mesh Proxy Demo (`Docker/Envoy/docker-compose.yml`)

To validate or run only the 3 Envoy proxies on an isolated `npon-envoy-mesh-net` bridge network:
```bash
docker compose -f Docker/Envoy/docker-compose.yml up -d
```

Validate individual Envoy configs offline:
```bash
docker run --rm -v "${PWD}/Docker/Envoy:/etc/envoy:ro" -v "${PWD}/Docker/Envoy/certs:/certs:ro" envoyproxy/envoy:v1.31-latest envoy --mode validate -c /etc/envoy/envoy.sso.yaml
```
