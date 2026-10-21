import { useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { TopBar } from './TopBar';
import { CursorGlow } from '@/components/ui/CursorGlow';
import { ensureConnected } from '@/lib/signalr';
import { useAuth } from '@/lib/auth';

export function AppShell() {
  const { status } = useAuth();

  useEffect(() => {
    if (status === 'authenticated') ensureConnected().catch(() => { /* auto-reconnect will retry */ });
  }, [status]);

  return (
    <div className="flex min-h-screen bg-background text-foreground relative">
      <CursorGlow />
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6 lg:p-8 relative z-10">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
