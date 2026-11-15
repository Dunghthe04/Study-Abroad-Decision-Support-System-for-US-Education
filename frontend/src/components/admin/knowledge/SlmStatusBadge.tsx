import { Badge } from "@/components/ui/badge";
import type { LabeledOption } from "@/lib/admin-university-display";
import { statusBadgeVariant, statusLabel } from "@/lib/slm-sample-format";
import type { SlmReviewStatus } from "@/types/slm-sample";

export function SlmStatusBadge({ status }: { status: SlmReviewStatus }) {
  return <Badge variant={statusBadgeVariant(status)}>{statusLabel(status)}</Badge>;
}

export function renderStatusOption(option: LabeledOption) {
  return <SlmStatusBadge status={option.value as SlmReviewStatus} />;
}
