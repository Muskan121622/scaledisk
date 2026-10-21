# ScaleDesk — DevOps Deployment Runbook (Azure AKS + AWS)

Read this top-to-bottom once before running anything. It is written for someone
learning DevOps: every command has a *why*. **The golden rule: guardrails before
compute — you set a budget alert BEFORE any billable resource exists.**

Cost target: **~Rs0 out of pocket** by using the Azure for Students $100 credit and
tearing down / stopping anything not actively being demoed. See the cost table at the end.

---

## 0 · Install the tools (one time)
| Tool | Why | Windows install |
|---|---|---|
| **Azure CLI** (`az`) | talk to Azure, create/read the cluster | `winget install Microsoft.AzureCLI` |
| **AWS CLI** (`aws`) | the AWS budget + Slice-3 EC2 | `winget install Amazon.AWSCLI` then `aws configure` |
| **Terraform** (`terraform`) | infrastructure as code | `winget install Hashicorp.Terraform` |
| **kubectl** + **Helm** | drive Kubernetes | `winget install Kubernetes.kubectl`; `choco install kubernetes-helm` |
| **Docker Desktop** | build images locally (CI builds them too) | you already have it |

Verify: `az --version; aws --version; terraform version; kubectl version --client`.

---

## 1 · MONEY GUARDRAILS FIRST (do this before apply) — Rs0
These only create alerts, not resources. Nothing bills from this step.

**Azure** — log in and select your *student* subscription:
```powershell
az login
az account list --output table          # find your student subscription name
az account set --subscription "<the-student-subscription-id>"
```
Set a hard cost alert on the subscription (emails you at 50% & 100%):
```powershell
az consumption budget create `
  --budget-name scaledesk-demo `
  --amount 15 `
  --time-grain Monthly `
  --start-date 2026-10-01 `
  --filtered `[ `[] `]
```
(Also flip on **Cost Management → Alerts** in the portal for good measure.)

**AWS** — enable Budgets (Billing console once), then:
```powershell
aws budgets create-budget `
  --account-id "<your-12-digit-account-id>" `
  --budget "{`"BudgetName`":`"scaledesk-monthly-cap`",`"BudgetType`":`"COST`",`"TimeUnit`":`"MONTHLY`",`"LimitAmount`":15,`"LimitUnit`":`"USD`",`"CostType`":`"UNAMORTIZED`"}" `
  --notifications-with-subscribers "`[{`"Notification`":{`"NotificationType`":`"ACTUAL`",`"Threshold`":80,`"ThresholdType`":`"PERCENTAGE`"},`"Subscribers`":[`"you@example.com`"]}]`"
```

> A budget **warns** you; it does not auto-stop spend. Your real safety net is
> these alerts **+ node_count=1 + stop-when-idle (§4) + teardown (§5)**.

---

## 2 · Create the Kubernetes cluster on Azure (AKS)
```powershell
cd infra/azure
Copy-Item terraform.tfvars.example terraform.tfvars   # then EDIT it: set notify_email!
terraform init
terraform plan     # READ this — it shows exactly what will be created & why
terraform apply    # type yes; ~5-8 min; creates RG + Free-tier AKS + budget
```
Point kubectl at your new cluster (printed as an output after apply):
```powershell
az aks get-credentials --resource-group rg-scaledesk-demo --name scaledesk-aks-demo --overwrite-existing
kubectl get nodes -o wide        # you should see ONE Ready node
```
✅ You now have real, managed Kubernetes. That is the **"AKS (Azure)"** resume line — earned.

---

## 3 · Deploy the app to the cluster  → *Slice 2*
This runbook is Slice 1 (guardrails + cluster + CI/CD). Slice 2 adds: pushing images to
GHCR from CI, the kustomize/Helm overlay for AKS (private-registry pull secret, Ingress +
HTTPS from the free `http_application_routing` DNS), and **Prometheus + Grafana** monitoring.
Come back here after Slice 2 and run `kubectl apply -k k8s/azure` (path from Slice 2).

---

## 4 · STOP when you're not demoing (this is how you stay near Rs0)
Deallocate the nodes — the cluster config stays, you pay ~only storage while stopped:
```powershell
./ops/deploy/stop-cluster.ps1
```
Start again with `start-cluster.ps1` (or `az aks start`) only when you need it live.

---

## 5 · TEARDOWN when done (reclaims everything, back to Rs0)
```powershell
./ops/deploy/teardown.ps1        # terraform destroy on Azure (and AWS if applied)
```
`terraform destroy` deletes the AKS cluster + resource group + budget. The student
credit stops being consumed. Re-create anytime with `terraform apply`.

---

## 6 · Cost table (rough, ~Rs85/USD)
| Item | Always-on | Stopped (`az aks stop`) | After `terraform destroy` |
|---|---|---|---|
| AKS control plane (Free tier) | Rs0 | Rs0 | Rs0 |
| 1 × Standard_B2ms node | ~Rs1,300–1,500/mo | ~Rs0 (only ~Rs50 disk) | Rs0 |
| Managed disks / IP | ~Rs150/mo | ~Rs150/mo | Rs0 |
| GitHub Actions + GHCR | Rs0 (free tier) | Rs0 | Rs0 |
| **Total** | **~Rs1,500/mo** | **~Rs200/mo** | **Rs0** |

All of the "always-on" figure is covered by the Rs8,500 ($100) student credit for
several months — and drops to Rs0 the moment you stop or destroy. **Never leave it
running between demo sessions.**

---

## 7 · The AWS "always-on small box" → *Slice 3*
To also carry an **AWS** tag cheaply, Slice 3 provisions one tiny EC2 (t3.micro/small,
~Rs500–1,300/mo, stoppable) running **k3s** (real Kubernetes, no EKS control-plane fee)
with the same app. Budget guardrail already in `infra/aws/main.tf`. Terraform in Slice 3.

---

### Decision recap (what we chose and why)
- **Kubernetes on Azure AKS** (Free control plane) instead of AWS EKS (Rs6,300/mo tax) → managed-K8s skill for ~Rs0 from pocket.
- **Self-hosted Postgres/Redis/Kafka** (no RDS/ElastiCache/MSK bills), stateless API+web scheduled on the cluster.
- **Ephemeral**: apply → prove → stop/destroy. Resume lines are 100% true *and* your bill stays ~Rs0.
