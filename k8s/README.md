# ScaleDesk on Kubernetes (Stage 7E)

The whole product — API, web SPA, Postgres, Redis, Kafka, Jaeger — runs in-cluster from
`k8s/base` (kustomize). The manifests are a 1:1 translation of `docker-compose.yml`:
same images, same env vars, same health endpoints; only hostnames become Service DNS
(`postgres`, `redis`, `kafka`, `jaeger`, `api`) and docker volumes become PVCs.

## Topology

```
browser ──► ingress (host: scaledesk.local, class: nginx)
              └─► web  (nginx :80)          ← static SPA + reverse proxy
      /api /hubs /health ─► web ─► api :8080 ← ASP.NET (1 replica, Recreate)
                                 ├─► postgres :5432   (StatefulSet, 1Gi PVC, RLS enforced)
                                 ├─► redis   :6379    (AOF, 1Gi PVC)
                                 ├─► kafka   :9092    (KRaft single node, 2Gi PVC)
                                 └─► jaeger  :4317    (OTLP gRPC)
```

| compose service | k8s object | notes |
|---|---|---|
| api | Deployment + Service `api:8080` | env from `configmap.yaml`; `Jwt__SecretKey` + postgres password from `secret.yaml`; startup probe allows ~5 min for first migration+seed |
| web | Deployment + Service `web:80` | built from `web/Dockerfile` (node:20 → nginx:1.27-alpine, `web/nginx.conf` proxies `/api` `/hubs` (ws) `/health`) |
| postgres | StatefulSet + Service | only the `scaledesk` owner is bootstrapped; the least-privilege `scaledesk_app` role is created by the app's own `LeastPrivilegeAppRole` migration — identical to compose |
| redis | Deployment + PVC + Service | `--appendonly yes` like compose |
| kafka | StatefulSet + headless Service | apache/kafka:3.8.1 KRaft; the compose EXTERNAL host listener is dropped (no host tooling in-cluster); controller voter via pod DNS `kafka-0.kafka:9093` |
| jaeger | Deployment + Service | UI (16686) is cluster-internal: `kubectl -n scaledesk port-forward svc/jaeger 16686:16686` |
| kafka-ui | **not deployed** | dev-only convenience; use port-forward to kafka or a local `docker compose up kafka-ui` when needed |

## Why the API is 1 replica (known limitation, honest note)

- `Database__AutoMigrate=true` runs EF migrations + `DbSeeder` at process start. Two pods
  racing the same migration set is a real failure mode, so the Deployment uses
  `strategy: Recreate` and `replicas: 1`.
- The outbox dispatcher and the Kafka notification consumer were built as single-process
  background services; a second replica would duplicate work until consumer-group rebalance
  semantics for the dispatcher are added.

`kubectl scale deployment api --replicas=2` will technically schedule, but is unsupported
until migrations move to an init Job. Cloud path: replace the in-pod migration with a
`Job`/`initContainer` (`dotnet ef migrations` or a migrator image), flip to
`RollingUpdate`, and the rest of the manifests (env, probes, service names) carries over
to EKS/GKE unchanged; swap minikube-ingress for the cloud LB ingress class.

## Deploy

```powershell
# prerequisites: docker desktop running, kubectl + minikube on PATH
# (minikube binary installs to %USERPROFILE%\.local\bin if not system-wide)
powershell -File ops\k8s\deploy.ps1     # start cluster -> build images in minikube daemon -> apply -> rollouts -> smoke
powershell -File ops\k8s\teardown.ps1   # delete stack (+ hosts entry)
```

`deploy.ps1` runs `minikube docker-env` first, so **both images build directly inside the
cluster's docker daemon** — no `image load` needed — then `kubectl apply -k k8s/base` and
waits on every rollout. It re-runs cleanly after code changes (docker layer cache makes
warm rebuilds fast; `imagePullPolicy: IfNotPresent` + tag `:local`).

First boot ≈ 4–8 min (K8s images + .NET/npm base pulls). Warm re-deploys ≈ 1–2 min.

## Access

- Ingress host `scaledesk.local` → deploy.ps1 appends `<minikube ip> scaledesk.local` to
  `C:\Windows\System32\drivers\etc\hosts` (needs one elevated run; otherwise browse via
  `http://<minikube ip>` with a `Host: scaledesk.local` header — nginx on the web pod does
  not check hostnames, only the ingress does).
- Staff workspace `/app`, customer portal `/portal`, super admin HQ `/platform` — same
  credentials as the compose demo (`demo@scaledesk.app`, `platform@scaledesk.app`,
  `DemoPass2026!`; portal: `priya@acme-retail.example`).

## Verification (what "working" means here)

1. `kubectl -n scaledesk get pods` — 6/6 Running/Ready.
2. `http://scaledesk.local/health/ready` → `Healthy` (ingress → web → api → postgres).
3. Login + ticket flow through the SPA in the browser (staff + portal).
4. Chaos: `kubectl -n scaledesk delete pod -l app=api` → pod self-heals, outbox backlog
   drains (events published while the pod was down appear on the Kafka topic within ~30 s).
5. Data survives: `kubectl -n scaledesk delete pod postgres-0` → StatefulSet recreates
   onto the same PVC; tickets/tokens intact.

## Secrets

`k8s/base/secret.yaml` carries the same well-known **dev-only** credentials as
docker-compose.yml so a clean clone deploys with zero setup. Do not reuse it anywhere
real — replace with a sealed secret / external secret store and rotate the JWT key.
