import { Link } from 'react-router-dom';
import { Bell, LogOut, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/lib/auth';
import { useUnreadNotifications } from '@/hooks/useUnreadNotifications';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

export function TopBar() {
  const { user, logout } = useAuth();
  const { data: unread } = useUnreadNotifications();

  return (
    <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-slate-200 bg-white/80 backdrop-blur px-6">
      <div className="text-sm text-slate-500">
        <Link to="/app" className="hover:text-slate-900 transition-colors font-medium">ScaleDesk</Link>
        <span className="mx-2 text-slate-300">/</span>
        <span className="text-slate-900">Workspace</span>
      </div>

      <div className="flex items-center gap-3">
        {/* Notification bell */}
        <Link to="/app" className="relative inline-flex h-10 w-10 items-center justify-center rounded-xl hover:bg-slate-100 transition-all group">
          <Bell className="h-[18px] w-[18px] text-slate-500 group-hover:text-slate-900 transition-colors" />
          {typeof unread === 'number' && unread > 0 && (
            <span className="absolute right-2 top-2 flex h-2.5 w-2.5">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-violet-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-violet-500"></span>
            </span>
          )}
        </Link>

        {/* User menu */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" className="h-10 gap-2.5 px-3 rounded-xl hover:bg-slate-100">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-violet-600 to-indigo-500 text-[11px] font-bold text-white shadow-brand">
                {(user?.email[0] ?? 'U').toUpperCase()}
              </div>
              <span className="hidden text-sm text-slate-700 md:inline">{user?.email}</span>
              <ChevronsUpDown className="h-3 w-3 opacity-50" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64">
            <DropdownMenuLabel>
              <div className="text-sm text-slate-900 font-medium">{user?.role ?? '—'}</div>
              <div className="text-xs text-slate-500 mt-0.5">{user?.email}</div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => logout()} className="text-rose-600 focus:bg-rose-50">
              <LogOut className="h-3.5 w-3.5" /> Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
