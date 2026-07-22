import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { TxnFilters } from "./api";

/**
 * Filter bar: an inclusive date range (native date inputs — keyboard-accessible, no extra deps)
 * and a debounced merchant search. Changing any filter resets pagination to page 1.
 */
export function TransactionFilters({
  value,
  onChange,
}: {
  value: TxnFilters;
  onChange: (next: TxnFilters) => void;
}) {
  const [merchant, setMerchant] = useState(value.merchant ?? "");

  // Debounce the merchant text so we don't fire a request per keystroke.
  useEffect(() => {
    const handle = setTimeout(() => {
      if ((value.merchant ?? "") !== merchant) {
        onChange({ ...value, page: 1, merchant: merchant || undefined });
      }
    }, 300);
    return () => clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [merchant]);

  return (
    <div className="flex flex-wrap items-end gap-4 rounded-lg border p-4">
      <div className="space-y-2">
        <Label htmlFor="from">From</Label>
        <Input
          id="from"
          type="date"
          className="w-[10rem]"
          value={value.from ?? ""}
          onChange={(e) => onChange({ ...value, page: 1, from: e.target.value || undefined })}
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="to">To</Label>
        <Input
          id="to"
          type="date"
          className="w-[10rem]"
          value={value.to ?? ""}
          onChange={(e) => onChange({ ...value, page: 1, to: e.target.value || undefined })}
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="merchant">Merchant</Label>
        <Input
          id="merchant"
          type="search"
          placeholder="Search merchant…"
          className="w-[14rem]"
          value={merchant}
          onChange={(e) => setMerchant(e.target.value)}
        />
      </div>
      {(value.from || value.to || value.merchant) && (
        <Button
          variant="ghost"
          onClick={() => {
            setMerchant("");
            onChange({ page: 1, pageSize: value.pageSize });
          }}
        >
          Clear filters
        </Button>
      )}
    </div>
  );
}
