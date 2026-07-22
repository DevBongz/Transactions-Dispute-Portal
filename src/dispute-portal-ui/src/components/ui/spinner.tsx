import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";

/** Inline loading spinner. Decorative — callers own the `aria-busy`/live-region semantics. */
export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={cn("h-4 w-4 animate-spin", className)} aria-hidden />;
}
