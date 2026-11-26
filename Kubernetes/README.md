# NpOn.Net-V2 Kubernetes Migration Guide

Complete Kubernetes manifests migrated from `Docker/` and `manifests/`, configured for **Option 3B (`InterServiceAuthMode=ServiceMesh`)** with Envoy sidecars enforcing TLS 1.3 SPIFFE mTLS on the wire and `x-forwarded-client-cert` (`XFCC`) over local loopback `h2c`.

---

## 1. Docker to Kubernetes Mapping Table

| Source (`Docker/` or `manifests/`) | Target (`Kubernetes/`) | Kubernetes Resources | Ports |
|---|---|---|---|
| Shared namespace & config | `00-base/namespace.yaml` | `Namespace/npon-v2`, `ConfigMap/npon-common-config`, `Secret/npon-interservice-secret` | — |
| `Docker/Postgres/compose.yaml` | `01-databases/postgres.yaml` | `StatefulSet/postgres`, `Deployment/pgadmin`, `Secret/postgres-secret` | `5432`, `5050->80` |
| `Docker/ScyllaDb/compose.yaml`, `manager-compose.yaml`, `config/node1/scylla.yaml` | `01-databases/scylladb.yaml` | 3-node `StatefulSet/scylla`, `Service/scylla-headless`, `ConfigMap/scylla-config`, `StatefulSet/scylla-manager-db`, `Deployment/scylla-manager`, `StatefulSet/minio` | `9042`, `19042`, `7000`, `7001`, `9180`, `10000`, `5080`, `9000/9001` |
| `Docker/Clickhouse/compose.yaml` | `01-databases/clickhouse.yaml` | `StatefulSet/clickhouse`, `Deployment/clickhouse-grafana`, `Secret/clickhouse-secret` | `8123`, `9000`, `3001->3000` |
| `Docker/MongoDb/compose.yaml` | `01-databases/mongodb.yaml` | `StatefulSet/mongodb`, `Deployment/mongo-express`, `Secret/mongodb-secret` | `27017`, `8081` |
| `Docker/MySql/compose.yaml` | `01-databases/mysql.yaml` | `StatefulSet/mysql`, `Deployment/phpmyadmin`, `Secret/mysql-secret` | `3306`, `8080->80` |
| `Docker/docker-compose.yml` (CockroachDB) | `01-databases/cockroachdb.yaml` | 3-node `StatefulSet/cockroachdb`, `Service/cockroachdb-headless`, `Job/cockroachdb-init` | `26257`, `8080` |
| `Docker/Trino/compose.yml`, `trino.add.properties` | `01-databases/trino.yaml` | `Deployment/trino`, `ConfigMap/trino-config` | `8888->8080` |
| `Docker/Dragonfly/compose.yml`, `.env` | `02-cache-messaging/dragonfly.yaml` | `StatefulSet/dragonfly`, `Deployment/dragonfly-ui`, `Secret/dragonfly-secret` | `6379`, `8083->8081` |
| `Docker/Redis/docker-compose.yml` | `02-cache-messaging/redis.yaml` | `StatefulSet/redis`, `Secret/redis-secret` | `6379` |
| `Docker/RabbitMq/docker-compose.yml` | `02-cache-messaging/rabbitmq.yaml` | `StatefulSet/rabbitmq`, `Secret/rabbitmq-secret` | `5672`, `15672` |
| `Docker/RedPanda_Kafka/docker-compose.yml` | `02-cache-messaging/redpanda-kafka.yaml` | `StatefulSet/redpanda`, `Job/redpanda-init`, `Deployment/redpanda-console`, `ConfigMap/redpanda-console-config` | `9092`, `19092`, `9644`, `8080` |
| `Docker/elasticsearch/compose.yml`, `.env` | `03-search-observability/elasticsearch-kibana.yaml` | 2-node `StatefulSet/elasticsearch`, `Job/elasticsearch-cert-init`, `Job/elasticsearch-kibana-password-setup`, `Deployment/kibana` | `9200`, `9300`, `5601` |
| `Docker/ScyllaDb/monitoring-compose.yaml`, `RedPanda_Kafka` | `03-search-observability/monitoring-stack.yaml` | `Deployment/prometheus`, `Deployment/alertmanager`, `StatefulSet/loki`, `DaemonSet/promtail`, `Deployment/grafana` | `9090`, `9093`, `3100`, `9080`, `1514`, `3000` |
| `Docker/Clouflare/docker-compose.yml`, `nginx.conf` | `04-edge-platform/cloudflare-warp-nginx.yaml` | `Deployment/cf-node` (3 replicas), `Deployment/cloudflare-nginx-balancer`, `ConfigMap/cloudflare-nginx-config` | `1080`, `24000` |
| `Docker/Rancher/docker-compose.yml` | `04-edge-platform/rancher.yaml` | `Deployment/rancher`, `Secret/rancher-secret` | `80`, `443` |
| `manifests/envoy-3b.yaml` | `05-microservices/envoy-3b-configmap.yaml` | `ConfigMap/npon-envoy-3b-config` (`envoy-sso.yaml`, `envoy-account.yaml`, `envoy-general.yaml`) | `15443`, `15000`, `15004`, `9901` |
| `manifests/sso-controller-deployment.yaml` | `05-microservices/sso-controller.yaml` | `ServiceAccount/sso-controller`, `Deployment/sso-controller` (app + envoy), `Service/sso-controller` | `14023`, `15023`, `15443` |
| `manifests/account-service-deployment.yaml` | `05-microservices/account-service.yaml` | `ServiceAccount/account-service`, `Deployment/account-service` (app + envoy), `Service/account-service` | `15443`, `40004` |
| `manifests/general-service-deployment.yaml` | `05-microservices/general-service.yaml` | `ServiceAccount/general-service`, `Deployment/general-service` (app + envoy), `Service/general-service` | `15443`, `40000` |

---

## 2. Option 3B SPIFFE Certificates Setup (Before Starting Envoy Sidecars)

Each microservice pod mounts `/certs/{ca.crt, svid.crt, svid.key}` from Secrets:
- `npon-mesh-certs-sso` (SAN URI: `spiffe://cluster.local/ns/npon-v2/sa/sso-controller`)
- `npon-mesh-certs-account` (SAN URI: `spiffe://cluster.local/ns/npon-v2/sa/account-service`)
- `npon-mesh-certs-general` (SAN URI: `spiffe://cluster.local/ns/npon-v2/sa/general-service`)

Create them via cert-manager / SPIRE or OpenSSL for local testing:

```bash
kubectl apply -f Kubernetes/00-base/namespace.yaml

# Example Secret creation per service:
kubectl -n npon-v2 create secret generic npon-mesh-certs-sso \
  --from-file=ca.crt=./ca.crt --from-file=svid.crt=./sso.crt --from-file=svid.key=./sso.key
kubectl -n npon-v2 create secret generic npon-mesh-certs-account \
  --from-file=ca.crt=./ca.crt --from-file=svid.crt=./account.crt --from-file=svid.key=./account.key
kubectl -n npon-v2 create secret generic npon-mesh-certs-general \
  --from-file=ca.crt=./ca.crt --from-file=svid.crt=./general.crt --from-file=svid.key=./general.key
```

---

## 3. Deploy with Kustomize or Layered `kubectl apply`

### Full Stack (Kustomize)
```bash
kubectl apply -k Kubernetes/
```

### Layered Deployment
```bash
kubectl apply -f Kubernetes/00-base/
kubectl apply -f Kubernetes/01-databases/
kubectl apply -f Kubernetes/02-cache-messaging/
kubectl apply -f Kubernetes/03-search-observability/
kubectl apply -f Kubernetes/04-edge-platform/
kubectl apply -f Kubernetes/05-microservices/
```

### Verify Status
```bash
kubectl -n npon-v2 get pods,svc,pvc
kubectl -n npon-v2 logs deploy/account-service -c envoy
kubectl -n npon-v2 logs deploy/account-service -c account-service
```

> **Note on Multi-Node Storage**: `elasticsearch-certs-pvc` in `03-search-observability/elasticsearch-kibana.yaml` uses `ReadWriteOnce` by default for single-node clusters (Kind/Minikube/Docker Desktop). Switch to a `ReadWriteMany` StorageClass when scheduling `elasticsearch-0` and `elasticsearch-1` across separate nodes.
> **Note on Dragonfly vs Redis**: Both `dragonfly` and `redis` expose port `6379` on separate Services (`dragonfly.npon-v2.svc.cluster.local` vs `redis.npon-v2.svc.cluster.local`), and microservices point to `dragonfly` by default.
