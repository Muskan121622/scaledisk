# COST GUARDRAIL — this is the FIRST thing that gets created and the reason you
# can safely let Terraform build the cluster. Azure emails you at 50% and 100% of
# your authorized monthly budget for THIS resource group. Set notify_email in tfvars.
#
# Note: a budget alert WARNS you; it does not auto-stop spend. The real protection
# is (1) this alert, (2) keeping node_count = 1, (3) `stop-cluster.ps1` when idle,
# (4) `teardown.ps1` (terraform destroy) when you're done. All four are in the runbook.

resource "azurerm_consumption_budget_resource_group" "guardrail" {
  name                = "budget-${var.project}-${var.env}"
  resource_group_name = azurerm_resource_group.main.name
  amount              = var.monthly_budget_usd
  time_grain          = "Monthly"

  threshold_amount {
    amount   = var.monthly_budget_usd * 0.5
    operator = "GreaterThan"
  }
  threshold_amount {
    amount   = var.monthly_budget_usd
    operator = "GreaterThan"
  }

  notification {
    enabled             = true
    operator            = "GreaterThan"
    threshold_amount    = var.monthly_budget_usd * 0.5
    threshold_type      = "Actual"
    notification_locale = "en"
    target              = [var.notify_email]
  }

  notification {
    enabled             = true
    operator            = "GreaterThan"
    threshold_amount    = var.monthly_budget_usd
    threshold_type      = "Actual"
    notification_locale = "en"
    target              = [var.notify_email]
  }
}
