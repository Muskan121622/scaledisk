# Azure side of ScaleDesk's DevOps stack.
# WHY Azure for Kubernetes: AKS has a "Free" control-plane tier, so the cluster
# management itself costs nothing — you only pay for the worker VM underneath,
# which the Azure for Students $100 credit covers. (EKS on AWS charges ~Rs6,300/mo
# just for the control plane, which is exactly what we're avoiding.)
#
# This is a TEMPLATE for a learner. Read ops/deploy/README.md first and SET THE
# BUDGET GUARDRAIL BEFORE `terraform apply`.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }
  # Local state is fine for a personal demo (0 cost). Do NOT commit terraform.tfstate.
  backend "local" {}
}

provider "azurerm" {
  features {}
  # The subscription comes from your login: `az account set --subscription "<student sub>"`
  # or the ARM_SUBSCRIPTION_ID env var. No subscription id is hard-coded here.
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project}-${var.env}"
  location = var.location
  tags = {
    Project = var.project
    Env     = var.env
    Owner   = var.owner_tag
    Cost    = "student-demo"
  }
}
