import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
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
  organizationName: z.string().min(2).max(60),
  organizationSlug: z.string().regex(/^[a-z][a-z0-9-]{1,38}[a-z0-9]$/, 'Use lowercase letters, digits, and dashes.'),
  firstName: z.string().min(1).max(40),
  lastName: z.string().min(1).max(40),
  email: z.string().email(),
  password: z.string().min(8, 'At least 8 characters'),
});
type FormValues = z.infer<typeof schema>;

export function Register() {
  const { register: registerFn } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, formState } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setError(null);
    try {
      await registerFn(values);
      navigate('/app', { replace: true });
    } catch (e) {
      if (e instanceof ApiError) {
        setError(e.body?.detail ?? 'Could not create workspace.');
      } else setError('Unable to reach the server.');
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
            <h1 className="text-2xl font-semibold tracking-tight">Create your workspace</h1>
            <p className="mt-1 text-sm text-muted-foreground">Spin up an isolated tenant with its own ticket queue.</p>

            <form className="mt-6 space-y-4" onSubmit={handleSubmit(onSubmit)}>
              <div>
                <Label htmlFor="orgName">Company name</Label>
                <Input id="orgName" placeholder="Acme Inc." className="mt-1.5" {...register('organizationName')} />
              </div>
              <div>
                <Label htmlFor="slug">Workspace URL</Label>
                <div className="mt-1.5 flex items-center rounded-md border border-input bg-transparent focus-within:ring-1 focus-within:ring-ring">
                  <span className="pl-3 text-xs text-muted-foreground">scaledesk.app/</span>
                  <input
                    id="slug"
                    className="flex-1 bg-transparent px-1 py-2 text-sm outline-none"
                    placeholder="acme"
                    {...register('organizationSlug')}
                  />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="first">First name</Label>
                  <Input id="first" className="mt-1.5" {...register('firstName')} />
                </div>
                <div>
                  <Label htmlFor="last">Last name</Label>
                  <Input id="last" className="mt-1.5" {...register('lastName')} />
                </div>
              </div>
              <div>
                <Label htmlFor="email">Work email</Label>
                <Input id="email" type="email" className="mt-1.5" {...register('email')} />
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
                {formState.isSubmitting ? 'Creating…' : 'Create workspace'}
              </Button>
            </form>

            <div className="mt-6 text-center text-xs text-muted-foreground">
              Already have one? <Link to="/login" className="text-primary hover:underline">Sign in</Link>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
