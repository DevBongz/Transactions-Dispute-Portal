import { Outlet } from "react-router-dom";
import { NavBar } from "./NavBar";

/** Authenticated shell: skip link, role-aware nav, and the routed content region. */
export function AppShell() {
  return (
    <div className="min-h-svh bg-background">
      <a href="#main-content" className="skip-link">
        Skip to content
      </a>
      <NavBar />
      <main id="main-content" className="container py-5 sm:py-8">
        <Outlet />
      </main>
    </div>
  );
}
