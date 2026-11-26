#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
CERTS_DIR="${SCRIPT_DIR}/certs"
TRUST_DOMAIN="${TRUST_DOMAIN:-cluster.local}"
NAMESPACE="${NAMESPACE:-default}"
DAYS="${DAYS:-365}"

mkdir -p "${CERTS_DIR}"

echo "[ENVOY CERTS] Generating shared Root CA in ${CERTS_DIR}..."
openssl req -x509 -newkey rsa:4096 -nodes -sha256 -days 3650 \
  -keyout "${CERTS_DIR}/ca.key" \
  -out "${CERTS_DIR}/ca.crt" \
  -subj "/CN=NpOn-ServiceMesh-CA" \
  -addext "basicConstraints=critical,CA:TRUE" \
  -addext "keyUsage=critical,keyCertSign,cRLSign"

issue_svid() {
  svc="$1"
  envoy_host="$2"
  spiffe="spiffe://${TRUST_DOMAIN}/ns/${NAMESPACE}/sa/${svc}"
  ext_file="${CERTS_DIR}/${svc}.ext"
  csr_file="${CERTS_DIR}/${svc}.csr"

  cat > "${ext_file}" <<EOF
basicConstraints=CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth,clientAuth
subjectAltName=URI:${spiffe},DNS:${svc},DNS:${envoy_host},DNS:localhost,IP:127.0.0.1
EOF

  openssl req -new -newkey rsa:2048 -nodes \
    -keyout "${CERTS_DIR}/${svc}.key" \
    -out "${csr_file}" \
    -subj "/CN=${svc}"

  openssl x509 -req -in "${csr_file}" \
    -CA "${CERTS_DIR}/ca.crt" -CAkey "${CERTS_DIR}/ca.key" -CAcreateserial \
    -out "${CERTS_DIR}/${svc}.crt" -days "${DAYS}" -sha256 -extfile "${ext_file}"

  rm -f "${ext_file}" "${csr_file}"
  echo "  -> Issued ${svc} (${spiffe})"
}

issue_svid "sso" "envoy-sso"
issue_svid "accountservice" "envoy-account"
issue_svid "generalservice" "envoy-general"

rm -f "${CERTS_DIR}/ca.srl"
chmod 600 "${CERTS_DIR}"/*.key || true
echo "[ENVOY CERTS] Complete."
