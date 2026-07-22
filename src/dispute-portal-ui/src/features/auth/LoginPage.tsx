import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { useAuth } from "@/auth/auth-context";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";
import type { AuthUser } from "@/types/api";

/**
 * Accessible login (TDP-FE-02 §2.1). On success, lands the user on their role home
 * (customers → /transactions, ops → /ops) or the originally requested redirect. Failures show a
 * single generic error — no credential enumeration (AC-AUTH-01).
 */
export default function LoginPage() {
  const { login } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (v: { email: string; password: string }) => login(v.email, v.password),
    onError: () => setError("Invalid email or password."),
    onSuccess: (user: AuthUser) => {
      const dest = user.role === "CUSTOMER" ? "/transactions" : "/ops";
      navigate(params.get("redirect") ?? dest, { replace: true });
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    mutation.mutate({ email, password });
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Sign in</CardTitle>
          <CardDescription>Transactions Dispute Portal</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} aria-describedby={error ? "login-error" : undefined} noValidate>
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div className="mt-4 space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
            {error && (
              <p id="login-error" role="alert" className="mt-4 text-sm text-destructive">
                {error}
              </p>
            )}
            <Button type="submit" className="mt-6 w-full" disabled={mutation.isPending} aria-busy={mutation.isPending}>
              {mutation.isPending && <Spinner />}
              {mutation.isPending ? "Signing in…" : "Sign in"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
