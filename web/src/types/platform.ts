// ScaleDesk HQ (platform) API shapes. Kept out of types/index.ts because this surface
// is a separate session space (see lib/platform-auth.tsx) and must never mix with staff types.

export type PlatformSession = {
  email: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  expiresInSeconds: number;
  tokenType: string;
};

export type PlatformStats = {
  totalTenants: number;
  activeTenants: number;
  suspendedTenants: number;
  totalUsers: number;
  totalTickets: number;
  openTickets: number;
};

// TenantStatus on the wire is the raw enum number: 0 Active, 1 Suspended, 2 Cancelled.
export type PlatformTenant = {
  id: string;
  name: string;
  slug: string;
  status: number;
  planTier: string | null;
  createdAt: string;
  userCount: number;
  ticketCount: number;
  openTicketCount: number;
};

export type PagedPlatformTenants = {
  items: PlatformTenant[];
  totalCount: number;
  page: number;
  pageSize: number;
};
