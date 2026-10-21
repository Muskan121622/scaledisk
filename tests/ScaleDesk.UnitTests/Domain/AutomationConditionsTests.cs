using ScaleDesk.Domain.Automation;
using ScaleDesk.Domain.Enums;

namespace ScaleDesk.UnitTests.Domain;

/// <summary>
/// Locks the automation rule-matching contract (spec §10): every populated condition must hold
/// (logical AND) and unset conditions act as wildcards, so a bare rule matches any ticket.
/// </summary>
public class AutomationConditionsTests
{
    [Fact]
    public void Empty_conditions_match_everything()
    {
        var conditions = new AutomationConditions();
        Assert.True(conditions.Matches(TicketPriority.Low, TicketCategory.General, TicketStatus.Open, "anything"));
    }

    [Fact]
    public void Priority_condition_matches_only_that_priority()
    {
        var conditions = new AutomationConditions { Priority = TicketPriority.High };
        Assert.True(conditions.Matches(TicketPriority.High, TicketCategory.Billing, TicketStatus.Open, "x"));
        Assert.False(conditions.Matches(TicketPriority.Medium, TicketCategory.Billing, TicketStatus.Open, "x"));
    }

    [Fact]
    public void All_populated_conditions_must_hold_together()
    {
        var conditions = new AutomationConditions
        {
            Priority = TicketPriority.Urgent,
            Category = TicketCategory.Bug,
            Status = TicketStatus.Open,
            SubjectContains = "crash"
        };

        Assert.True(conditions.Matches(TicketPriority.Urgent, TicketCategory.Bug, TicketStatus.Open, "App crashes on login"));
        // Subject keyword is the only mismatch -> no match.
        Assert.False(conditions.Matches(TicketPriority.Urgent, TicketCategory.Bug, TicketStatus.Open, "App freezes on login"));
    }

    [Fact]
    public void Subject_contains_is_case_insensitive_and_substring()
    {
        var conditions = new AutomationConditions { SubjectContains = "Refund" };
        Assert.True(conditions.Matches(TicketPriority.Low, TicketCategory.General, TicketStatus.Open, "Please ISSUE A REFUND now"));
        Assert.False(conditions.Matches(TicketPriority.Low, TicketCategory.General, TicketStatus.Open, "Please issue a chargeback"));
    }

    [Fact]
    public void Whitespace_only_subject_filter_is_treated_as_wildcard()
    {
        var conditions = new AutomationConditions { SubjectContains = "   " };
        Assert.True(conditions.Matches(TicketPriority.Low, TicketCategory.General, TicketStatus.Open, "whatever"));
    }
}
