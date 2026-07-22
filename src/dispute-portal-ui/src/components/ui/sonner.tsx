import type { ComponentProps } from "react";
import { Toaster as Sonner } from "sonner";

/** App-wide toast host. `sonner`'s `toast()` is imported directly where needed. */
export function Toaster(props: ComponentProps<typeof Sonner>) {
  return <Sonner richColors position="top-right" {...props} />;
}

export { toast } from "sonner";
