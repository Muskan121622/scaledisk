# Handy values printed after `terraform apply`. The get-credentials command is the
# single thing you copy to point kubectl at your brand-new cluster.

output "resource_group" {
  value = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "kubeconfig_command" {
  description = "Run this to give kubectl access to the cluster."
  value       = "az aks get-credentials --resource-group ${azurerm_resource_group.main.name} --name ${azurerm_kubernetes_cluster.aks.name}"
}

output "k8s_namespace_dashboard_url" {
  description = "http_application_routing gives each Ingress host a free public URL once the app is deployed (Slice 2)."
  value       = "KubeDNS-based wildcard host assigned per Ingress by the add-on controller."
}
