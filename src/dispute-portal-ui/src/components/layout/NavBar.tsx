import { useEffect, useId, useState } from "react";
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
  const [open, setOpen] = useState(false);
  const menuId = useId();

  // Close the mobile drawer when the viewport grows past the md breakpoint.
  useEffect(() => {
    const mq = window.matchMedia("(min-width: 768px)");
    const onChange = () => {
      if (mq.matches) setOpen(false);
    };
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);

  // Prevent background scroll while the mobile menu is open.
  useEffect(() => {
    document.body.style.overflow = open ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [open]);

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    cn(
      "block rounded-md px-3 py-2.5 text-sm font-medium transition-colors hover:bg-accent hover:text-accent-foreground md:py-2",
      isActive ? "bg-accent text-accent-foreground" : "text-muted-foreground",
    );

  return (
    <header className="sticky top-0 z-40 border-b border-border/80 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      <div className="h-0.5 w-full bg-gradient-to-r from-brand-blue via-brand-gem to-brand-red" aria-hidden="true" />
      <nav className="container flex h-14 items-center gap-3 md:h-16 md:gap-6" aria-label="Primary">
        <div className="flex min-w-0 items-center gap-2.5">
          <img
            src="/brand/capitec-logo-svg.png"
            alt=""
            className="h-8 w-8 shrink-0 object-contain md:h-9 md:w-9"
            aria-hidden="true"
          />
          <span className="truncate text-sm font-semibold tracking-tight text-brand-neutral sm:text-base">
            Dispute Portal
          </span>
        </div>

        <ul className="ml-2 hidden items-center gap-1 md:flex">
          {items.map((n) => (
            <li key={n.to}>
              <NavLink to={n.to} className={linkClass}>
                {n.label}
              </NavLink>
            </li>
          ))}
        </ul>

        <div className="ml-auto flex items-center gap-2 sm:gap-3">
          {user && (
            <span className="hidden max-w-[10rem] truncate text-sm text-muted-foreground sm:inline lg:max-w-xs">
              {user.fullName}
            </span>
          )}
          <Button variant="outline" size="sm" className="hidden sm:inline-flex" onClick={logout}>
            Sign out
          </Button>

          <Button
            type="button"
            variant="outline"
            size="icon"
            className="md:hidden"
            aria-expanded={open}
            aria-controls={menuId}
            aria-label={open ? "Close menu" : "Open menu"}
            onClick={() => setOpen((v) => !v)}
          >
            <MenuIcon open={open} />
          </Button>
        </div>
      </nav>

      {/* Mobile menu */}
      <div
        id={menuId}
        className={cn(
          "border-t bg-background md:hidden",
          open ? "block" : "hidden",
        )}
      >
        <ul className="container flex flex-col gap-1 py-3">
          {items.map((n) => (
            <li key={n.to}>
              <NavLink to={n.to} className={linkClass} onClick={() => setOpen(false)}>
                {n.label}
              </NavLink>
            </li>
          ))}
        </ul>
        <div className="container flex items-center justify-between gap-3 border-t py-3">
          {user && <span className="truncate text-sm text-muted-foreground">{user.fullName}</span>}
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setOpen(false);
              logout();
            }}
          >
            Sign out
          </Button>
        </div>
      </div>
    </header>
  );
}

function MenuIcon({ open }: { open: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      {open ? (
        <path strokeLinecap="round" d="M6 6l12 12M18 6L6 18" />
      ) : (
        <>
          <path strokeLinecap="round" d="M4 7h16" />
          <path strokeLinecap="round" d="M4 12h16" />
          <path strokeLinecap="round" d="M4 17h16" />
        </>
      )}
    </svg>
  );
}
