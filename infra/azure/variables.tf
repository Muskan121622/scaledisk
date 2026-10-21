variable "project" {
  description = "Short name used to prefix resources."
  type        = string
  default     = "scaledesk"
}

variable "env" {
  description = "Environment label (keep it 'demo' to control cost)."
  type        = string
  default     = "demo"
}

variable "location" {
  description = "Azure region. centralindia = Pune (lowest latency for you, same price band)."
  type        = string
  default     = "centralindia"
}

variable "aks_vm_size" {
  description = "AKS worker VM SKU. Standard_B2ms is a cheap 2-vCPU/8GB box that fits the student credit. Bump to DS2_v2 only if you need more headroom."
  type        = string
  default     = "Standard_B2ms"
}

variable "monthly_budget_usd" {
  description = "Dollars/month you authorize. Azure emails you at 50% and 100% of this. Roughly 1 B2ms node + disks."
  type        = number
  default     = 15
}

variable "notify_email" {
  description = "Email that receives the budget alerts. REQUIRED — this is your money-safety net. Set it in terraform.tfvars."
  type        = string
}

variable "owner_tag" {
  description = "Your name/handle, stamped on every resource so you can see what costs what."
  type        = string
  default     = "me"
}
