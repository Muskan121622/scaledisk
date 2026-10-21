import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/lib/auth';
import { Landing } from '@/routes/Landing';
import { Login } from '@/routes/Login';
import { Register } from '@/routes/Register';
import { AppShell } from '@/components/layout/AppShell';
import { Dashboard } from '@/routes/Dashboard';
import { Tickets } from '@/routes/Tickets';
import { TicketDetail } from '@/routes/TicketDetail';
import { Customers } from '@/routes/Customers';
import { Teams } from '@/routes/Teams';
import { Automation } from '@/routes/Automation';
import { SlaPolicies } from '@/routes/SlaPolicies';
import { Analytics } from '@/routes/Analytics';
import { Settings } from '@/routes/Settings';
import { PlatformAuthProvider, usePlatformAuth } from '@/lib/platform-auth';
import { PlatformLogin } from '@/routes/platform/PlatformLogin';
import { PlatformShell } from '@/routes/platform/PlatformShell';
import { PlatformOverview } from '@/routes/platform/PlatformOverview';
import { PlatformTenants } from '@/routes/platform/PlatformTenants';
import { PlatformTenantDetail } from '@/routes/platform/PlatformTenantDetail';
import { PortalAuthProvider, usePortalAuth } from '@/lib/portal-auth';
import { PortalLogin } from '@/routes/portal/PortalLogin';
import { PortalShell } from '@/routes/portal/PortalShell';
import { PortalTickets } from '@/routes/portal/PortalTickets';
import { PortalNewTicket } from '@/routes/portal/PortalNewTicket';
import { PortalTicketDetail } from '@/routes/portal/PortalTicketDetail';
import type { ReactNode } from 'react';

function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const location = useLocation();
  if (status === 'loading') return <FullScreenSpinner />;
  if (status === 'anonymous') return <Navigate to="/login" state={{ from: location }} replace />;
  return <>{children}</>;
}

function RedirectIfAuthed({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  if (status === 'authenticated') return <Navigate to="/app" replace />;
  return <>{children}</>;
}

function FullScreenSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
    </div>
  );
}

// HQ gate: its own session space — a staff login says nothing about platform auth and vice versa.
function RequirePlatform({ children }: { children: ReactNode }) {
  const { status } = usePlatformAuth();
  if (status === 'loading') return <FullScreenSpinner />;
  if (status === 'anonymous') return <Navigate to="/platform/login" replace />;
  return <>{children}</>;
}

// Customer gate: third session space (scaledesk.portal.*) — a customer tab and an agent tab
// can run in the same browser, which is exactly what the J3 two-window demo needs.
function RequirePortal({ children }: { children: ReactNode }) {
  const { status } = usePortalAuth();
  if (status === 'loading') return <FullScreenSpinner />;
  if (status === 'anonymous') return <Navigate to="/portal/login" replace />;
  return <>{children}</>;
}

export function AppRoutes() {
  return (
    <PortalAuthProvider>
      <PlatformAuthProvider>
        <Routes>
      <Route path="/" element={<Landing />} />
      <Route path="/login" element={<RedirectIfAuthed><Login /></RedirectIfAuthed>} />
      <Route path="/register" element={<RedirectIfAuthed><Register /></RedirectIfAuthed>} />

      <Route path="/app" element={<RequireAuth><AppShell /></RequireAuth>}>
        <Route index element={<Dashboard />} />
        <Route path="tickets" element={<Tickets />} />
        <Route path="tickets/:id" element={<TicketDetail />} />
        <Route path="customers" element={<Customers />} />
        <Route path="teams" element={<Teams />} />
        <Route path="automation" element={<Automation />} />
        <Route path="sla" element={<SlaPolicies />} />
        <Route path="analytics" element={<Analytics />} />
        <Route path="settings" element={<Settings />} />
        <Route path="*" element={<Navigate to="/app" replace />} />
      </Route>

      {/* ScaleDesk HQ — platform admins only (platform-scoped tokens). */}
      <Route path="/platform/login" element={<PlatformLogin />} />
      <Route path="/platform" element={<RequirePlatform><PlatformShell /></RequirePlatform>}>
        <Route index element={<PlatformOverview />} />
        <Route path="tenants" element={<PlatformTenants />} />
        <Route path="tenants/:id" element={<PlatformTenantDetail />} />
        <Route path="*" element={<Navigate to="/platform" replace />} />
      </Route>

      {/* Customer portal — portal-scoped tokens, own session space, own layout. */}
      <Route path="/portal/login" element={<PortalLogin />} />
      <Route path="/portal" element={<RequirePortal><PortalShell /></RequirePortal>}>
        <Route index element={<Navigate to="/portal/tickets" replace />} />
        <Route path="tickets" element={<PortalTickets />} />
        <Route path="tickets/new" element={<PortalNewTicket />} />
        <Route path="tickets/:id" element={<PortalTicketDetail />} />
        <Route path="*" element={<Navigate to="/portal/tickets" replace />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </PlatformAuthProvider>
    </PortalAuthProvider>
  );
}
