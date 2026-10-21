import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label-badge';
import { Card, CardContent } from '@/components/ui/card';
import { useAuth } from '@/lib/auth';
import { ApiError } from '@/lib/api';

const schema = z.object({
  organizationSlug: z.string().min(2).max(40),
  email: z.string().email(),
  password: z.string().min(8),
});
type FormValues = z.infer<typeof schema>;

export function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { organizationSlug: 'demo', email: 'demo@scaledesk.app', password: 'DemoPass2026!' },
  });

  async function onSubmit(values: FormValues) {
    setError(null);
    try {
      await login(values.email, values.password, values.organizationSlug);
      const from = (location.state as { from?: { pathname: string } })?.from?.pathname ?? '/app';
      navigate(from, { replace: true });
    } catch (e) {
      setError(e instanceof ApiError ? (e.body?.detail ?? 'Invalid credentials.') : 'Unable to reach the server.');
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="w-full max-w-md">
        <Link to="/" className="mb-6 inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
          <ArrowLeft className="h-3 w-3" /> Back to home
        </Link>
        <Card>
          <CardContent className="pt-8">
            <h1 className="text-2xl font-semibold tracking-tight">Welcome back 👋</h1>
            <p className="mt-1 text-sm text-muted-foreground">Sign in to your ScaleDesk workspace.</p>

            <form className="mt-6 space-y-4" onSubmit={handleSubmit(onSubmit)}>
              <div>
                <Label htmlFor="slug">Workspace URL</Label>
                <Input id="slug" placeholder="acme" className="mt-1.5" {...register('organizationSlug')} />
              </div>
              <div>
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" placeholder="you@company.com" className="mt-1.5" {...register('email')} />
              </div>
              <div>
                <Label htmlFor="password">Password</Label>
                <Input id="password" type="password" className="mt-1.5" {...register('password')} />
              </div>

              {error && (
                <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive-foreground">
                  {error}
                </div>
              )}

              <Button type="submit" className="w-full" disabled={formState.isSubmitting}>
                {formState.isSubmitting ? 'Signing in…' : 'Sign in'}
              </Button>
            </form>

            <div className="mt-6 text-center text-xs text-muted-foreground">
              New here? <Link to="/register" className="text-primary hover:underline">Create a workspace</Link>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
