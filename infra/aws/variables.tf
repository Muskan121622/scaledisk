variable "project" {
  type    = string
  default = "scaledesk"
}

variable "region" {
  description = "AWS region. ap-south-1 = Mumbai (closest to you)."
  type        = string
  default     = "ap-south-1"
}

variable "monthly_budget_usd" {
  description = "Monthly USD cap. Alerting via AWS Budgets / CLI covers the email side."
  type        = number
  default     = 15
}
