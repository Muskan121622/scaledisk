// Mirrors the shape of the API contracts in ScaleDesk.Application/Contracts.
// Enums serialize as numbers on the wire (no JsonStringEnumConverter registered on the server).

export const TicketStatus = {
  Open: 0,
  InProgress: 1,
  WaitingForCustomer: 2,
  WaitingForInternal: 3,
  Resolved: 4,
  Closed: 5,
} as const;
export type TicketStatus = (typeof TicketStatus)[keyof typeof TicketStatus];

export const TicketPriority = {
  Low: 0,
  Medium: 1,
  High: 2,
  Urgent: 3,
} as const;
export type TicketPriority = (typeof TicketPriority)[keyof typeof TicketPriority];

export const TicketCategory = {
  General: 0,
  Billing: 1,
  Technical: 2,
  Account: 3,
  FeatureRequest: 4,
  Bug: 5,
} as const;
export type TicketCategory = (typeof TicketCategory)[keyof typeof TicketCategory];

export const StatusLabel: Record<TicketStatus, string> = {
  [TicketStatus.Open]: 'Open',
  [TicketStatus.InProgress]: 'In progress',
  [TicketStatus.WaitingForCustomer]: 'Waiting on customer',
  [TicketStatus.WaitingForInternal]: 'Waiting internal',
  [TicketStatus.Resolved]: 'Resolved',
  [TicketStatus.Closed]: 'Closed',
};

export const PriorityLabel: Record<TicketPriority, string> = {
  [TicketPriority.Low]: 'Low',
  [TicketPriority.Medium]: 'Medium',
  [TicketPriority.High]: 'High',
  [TicketPriority.Urgent]: 'Urgent',
};

export const CategoryLabel: Record<TicketCategory, string> = {
  [TicketCategory.General]: 'General',
  [TicketCategory.Billing]: 'Billing',
  [TicketCategory.Technical]: 'Technical',
  [TicketCategory.Account]: 'Account',
  [TicketCategory.FeatureRequest]: 'Feature request',
  [TicketCategory.Bug]: 'Bug',
};

export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  expiresInSeconds: number;
  tokenType: string;
};

export type CurrentUser = {
  userId: string;
  email: string;
  tenantId: string;
  tenantName: string;
  role: string;
  permissions: string[];
};

export type Ticket = {
  id: string;
  tenantId: string;
  customerId: string;
  subject: string;
  description: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: TicketCategory;
  assigneeUserId: string | null;
  teamId: string | null;
  createdAt: string;
  resolvedAt: string | null;
  rowVersion: number;
  slaPolicyId: string | null;
  responseDueAt: string | null;
  resolutionDueAt: string | null;
  slaBreached: boolean;
};

export type TicketComment = {
  id: string;
  authorUserId: string | null;
  customerId: string | null;
  body: string;
  isInternal: boolean;
  createdAt: string;
};

// One entry of the append-only ticket timeline (GET /tickets/{id}/events). Types are stable
// machine names raised by the domain: ticket.created / assigned / status_changed / commented,
// sla.breached ...
export type TicketActivity = {
  id: string;
  type: string;
  summary: string;
  actorUserId: string | null;
  createdAt: string;
};

export type Paged<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
};

export type Customer = {
  id: string;
  tenantId: string;
  name: string;
  email: string;
  company: string | null;
  tags: string[];
  createdAt: string;
};

export type Notification = {
  id: string;
  type: string;
  title: string;
  body: string | null;
  relatedTicketId: string | null;
  isRead: boolean;
  createdAt: string;
};

export type DailyCount = { date: string; count: number };

export type DashboardStats = {
  open: number;
  inProgress: number;
  waiting: number;
  resolved: number;
  closed: number;
  slaSuccessRate: number;
  unreadNotifications: number;
  ticketsPerDay: DailyCount[];
};

// --- Workspace admin surfaces (mirror Contracts/Teams, Automation, Sla, Invitations) ---------

export const UserRole = { Owner: 0, Admin: 1, Manager: 2, Agent: 3, Viewer: 4 } as const;
export type UserRole = (typeof UserRole)[keyof typeof UserRole];

export const RoleLabel: Record<UserRole, string> = {
  [UserRole.Owner]: 'Owner',
  [UserRole.Admin]: 'Admin',
  [UserRole.Manager]: 'Manager',
  [UserRole.Agent]: 'Agent',
  [UserRole.Viewer]: 'Viewer',
};

export const AutomationActionType = { SetPriority: 0, SetStatus: 1, AssignTeam: 2, AssignUser: 3 } as const;
export type AutomationActionType = (typeof AutomationActionType)[keyof typeof AutomationActionType];

export const ActionTypeLabel: Record<AutomationActionType, string> = {
  [AutomationActionType.SetPriority]: 'Set priority',
  [AutomationActionType.SetStatus]: 'Set status',
  [AutomationActionType.AssignTeam]: 'Assign team',
  [AutomationActionType.AssignUser]: 'Assign user',
};

export const InvitationStatus = { Pending: 0, Accepted: 1, Revoked: 2, Expired: 3 } as const;
export type InvitationStatus = (typeof InvitationStatus)[keyof typeof InvitationStatus];

// Stable machine names the automation engine matches on (Domain/Events + sla.breached).
export const TriggerEvents = ['ticket.created', 'ticket.assigned', 'ticket.status_changed', 'ticket.commented', 'sla.breached'] as const;

export type Team = {
  id: string;
  tenantId: string;
  name: string;
  description: string | null;
  createdAt: string;
};

export type AutomationConditions = {
  priority: TicketPriority | null;
  category: TicketCategory | null;
  status: TicketStatus | null;
  subjectContains: string | null;
};

export type AutomationAction = {
  type: AutomationActionType;
  priority: TicketPriority | null;
  status: TicketStatus | null;
  teamId: string | null;
  assigneeUserId: string | null;
};

export type AutomationRule = {
  id: string;
  name: string;
  description: string | null;
  triggerEvent: string;
  isEnabled: boolean;
  priority: number;
  conditions: AutomationConditions;
  actions: AutomationAction[];
};

export type SlaPolicy = {
  id: string;
  name: string;
  priority: TicketPriority;
  responseDueInMinutes: number;
  resolutionDueInMinutes: number;
  isDefault: boolean;
};

export type Invitation = {
  id: string;
  email: string;
  role: UserRole;
  status: InvitationStatus;
  expiresAt: string;
  createdAt: string;
};
