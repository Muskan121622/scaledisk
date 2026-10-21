# The Kubernetes cluster itself. Deliberately minimal & cheap:
#   - Free-tier control plane (no cluster management fee)
#   - ONE small node, autoscaling OFF (autoscaling can silently add nodes = money)
#   - System-assigned identity (no manual service-principal setup — easier for a learner)
#   - http_application_routing -> gives you a free Ingress + a *.region.aksapp.io URL + DNS
# For state (Postgres/Redis/Kafka) we keep the app talking to self-hosted/external stores,
# which is also the professionally-correct split: stateless workloads on the cluster.

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "${var.project}-aks-${var.env}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "${var.project}${var.env}"

  # Free control-plane tier (vs Paid/Standard which adds an SLA fee).
  sku_tier = "Free"

  default_node_pool {
    name                = "agentpool"
    node_count          = 1 # keep at 1; never let it grow on its own
    vm_size             = var.aks_vm_size
    enable_auto_scaling = false
    os_disk_size_gb     = 50
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin = "kubenet" # simplest, no paid CNI add-ons
    network_policy = "calico"
  }

  # Free add-on: an Ingress controller + public DNS per host -> real HTTPS URL to demo.
  http_application_routing_enabled = true

  tags = {
    Project = var.project
    Env     = var.env
    Owner   = var.owner_tag
  }
}
