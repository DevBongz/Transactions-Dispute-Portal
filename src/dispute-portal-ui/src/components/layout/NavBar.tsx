import { NavLink } from "react-router-dom";
import { useAuth } from "@/auth/auth-context";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { Role } from "@/types/api";

interface NavItem {
  to: string;
  label: string;
  roles: Role[];
}

const NAV: NavItem[] = [
  { to: "/transactions", label: "Transactions", roles: ["CUSTOMER"] },
  { to: "/my-disputes", label: "My Disputes", roles: ["CUSTOMER"] },
  { to: "/ops", label: "Operations", roles: ["OPS_ANALYST", "OPS_MANAGER"] },
];

export function NavBar() {
  const { user, logout } = useAuth();
  const items = NAV.filter((n) => (user ? n.roles.includes(user.role) : false));

  return (
    <header className="border-b bg-background">
      <nav className="container flex h-14 items-center gap-6" aria-label="Primary">
        <span className="font-semibold">Dispute Portal</span>
        <ul className="flex items-center gap-1">
          {items.map((n) => (
            <li key={n.to}>
              <NavLink
                to={n.to}
                className={({ isActive }) =>
                  cn(
                    "rounded-md px-3 py-2 text-sm font-medium transition-colors hover:bg-accent",
                    isActive ? "bg-accent text-accent-foreground" : "text-muted-foreground",
                  )
                }
              >
                {n.label}
              </NavLink>
            </li>
          ))}
        </ul>
        <div className="ml-auto flex items-center gap-3">
          {user && <span className="text-sm text-muted-foreground">{user.fullName}</span>}
          <Button variant="outline" size="sm" onClick={logout}>
            Sign out
          </Button>
        </div>
      </nav>
    </header>
  );
}
