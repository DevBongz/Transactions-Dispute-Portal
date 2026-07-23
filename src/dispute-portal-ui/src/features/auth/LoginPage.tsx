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
    <div className="login-atmosphere relative flex min-h-svh items-center justify-center overflow-hidden px-4 py-8 sm:px-6">
      <div className="login-motif" aria-hidden="true" />

      <div className="relative z-10 w-full max-w-md">
        <div className="mb-6 flex flex-col items-center text-center sm:mb-8">
          <img
            src="/brand/capitec-bank-removed-background.png"
            alt="Capitec Bank"
            className="h-auto w-[min(100%,18rem)] object-contain sm:w-[20rem]"
          />
          <p className="mt-4 text-xl font-semibold leading-none tracking-tight sm:text-2xl">
            Transactions Dispute Portal
          </p>
        </div>

        <Card className="border-border/80 shadow-brand backdrop-blur-sm">
          <CardHeader className="space-y-1 pb-4">
            <div className="mb-1 flex h-1 w-12 overflow-hidden rounded-full" aria-hidden="true">
              <span className="w-1/2 bg-brand-blue" />
              <span className="w-1/2 bg-brand-red" />
            </div>
            <CardTitle className="text-xl sm:text-2xl">Sign in</CardTitle>
            <CardDescription>Use your portal credentials to continue</CardDescription>
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
                  inputMode="email"
                  className="h-11"
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
                  className="h-11"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
              {error && (
                <p id="login-error" role="alert" className="mt-4 text-sm text-destructive">
                  {error}
                </p>
              )}
              <Button
                type="submit"
                className="mt-6 h-11 w-full text-base"
                disabled={mutation.isPending}
                aria-busy={mutation.isPending}
              >
                {mutation.isPending && <Spinner />}
                {mutation.isPending ? "Signing in…" : "Sign in"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
