# AWS side of the stack. Right now this creates ONLY the cost guardrail (budget).
# The small always-on EC2 that earns the "AWS" resume tag comes in Slice 3 of
# ops/deploy/README.md — so no billable compute is defined yet.
#
# Credentials: `aws configure` (or env vars). Region ap-south-1 = Mumbai.
# NOTE: an email alert on a budget is simplest via `aws budgets create-budget`
# (shown in the runbook) or the console; Terraform's aws_budgets_budget creates
# the cap itself. Together they are your money-safety net before compute exists.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
  }
  backend "local" {}
}

provider "aws" {
  region = var.region
}

# Account-wide monthly cost cap. Requires AWS Budgets enabled once in Billing.
resource "aws_budgets_budget" "guardrail" {
  name         = "${var.project}-monthly-cap"
  budget_type  = "COST"
  time_unit    = "MONTHLY"
  limit_amount = tostring(var.monthly_budget_usd)
  limit_unit   = "USD"
  cost_type    = "UNAMORTIZED"
}
