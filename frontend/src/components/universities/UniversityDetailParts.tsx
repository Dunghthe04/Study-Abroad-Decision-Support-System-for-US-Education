"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import { Building2Icon, ExternalLinkIcon } from "lucide-react";
import { universityHost, universityUrl } from "@/lib/university-display";

export function SchoolImage({ url, alt, className, priority = false, contain = false }: {
  url: string | null; alt: string; className: string; priority?: boolean; contain?: boolean;
}) {
  const [failed, setFailed] = useState<string | null>(null);
  const source = universityUrl(url);
  return (
    <div className={`relative overflow-hidden ${className}`}>
      {source && failed !== source ? (
        <Image src={source} alt={alt} fill unoptimized priority={priority}
          sizes={contain ? "96px" : "(max-width: 768px) 100vw, 1200px"}
          className={contain ? "object-contain p-2" : "object-cover"} onError={() => setFailed(source)} />
      ) : (
        <div className="absolute inset-0 flex items-center justify-center bg-brand-wash text-brand/50">
          <Building2Icon className={contain ? "size-9" : "size-16"} aria-label={alt} />
        </div>
      )}
    </div>
  );
}

export function DetailSection({ id, title, description, children }: { id?: string; title: string; description?: string; children: ReactNode }) {
  return (
    <section id={id} aria-label={title} className="scroll-mt-32 rounded-xl border border-border/70 bg-card p-5 shadow-2xs sm:p-6">
      <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
      {description && <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{description}</p>}
      <div className="mt-5">{children}</div>
    </section>
  );
}

export function DetailRows({ rows }: { rows: { label: string; value: ReactNode }[] }) {
  return (
    <dl className="divide-y divide-border/70">
      {rows.map((row) => (
        <div key={row.label} className="flex items-start justify-between gap-5 py-3 first:pt-0 last:pb-0">
          <dt className="text-sm leading-relaxed text-muted-foreground">{row.label}</dt>
          <dd className="text-right text-sm font-medium leading-relaxed">{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function SourceLink({ url, label }: { url: string | null; label?: string }) {
  const safe = universityUrl(url);
  if (!safe) return null;
  return (
    <a href={safe} target="_blank" rel="noopener noreferrer" className="inline-flex max-w-full items-center gap-1.5 break-all text-sm font-medium text-brand underline-offset-4 hover:underline">
      {label ?? universityHost(safe)}<ExternalLinkIcon className="size-3.5 shrink-0" />
    </a>
  );
}
