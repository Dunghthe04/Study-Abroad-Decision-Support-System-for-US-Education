"use client";

import { useId } from "react";
import { BookOpenFilledIcon, UsersFilledIcon, type AdminNavIconProps } from "@/components/admin/AdminNavIcons";

export { BookOpenFilledIcon, UsersFilledIcon };

export function UserFilledIcon({ className }: AdminNavIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="currentColor"
      aria-hidden="true"
      className={className}
    >
      <circle cx="12" cy="7.5" r="4.25" />
      <path d="M4.5 20.25A6.75 6.75 0 0 1 11.25 13.5h1.5a6.75 6.75 0 0 1 6.75 6.75.75.75 0 0 1-.75.75H5.25a.75.75 0 0 1-.75-.75Z" />
    </svg>
  );
}

export function CrownFilledIcon({ className }: AdminNavIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="none"
      aria-hidden="true"
      className={className}
    >
      <path
        d="M11.562 3.266a.5.5 0 0 1 .876 0L15.39 8.87a1 1 0 0 0 1.516.294L21.183 5.5a.5.5 0 0 1 .798.519l-2.834 10.246a1 1 0 0 1-.956.734H5.81a1 1 0 0 1-.957-.734L2.02 6.02a.5.5 0 0 1 .798-.519l4.276 3.664a1 1 0 0 0 1.516-.294z"
        fill="currentColor"
      />
      <rect x="5" y="19" width="14" height="2.2" rx="1.1" fill="currentColor" />
    </svg>
  );
}

export function HeartHandshakeFilledIcon({ className }: AdminNavIconProps) {
  const maskId = useId().replace(/:/g, "");
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="none"
      aria-hidden="true"
      className={className}
    >
      <defs>
        <mask id={maskId}>
          <rect width="24" height="24" fill="white" />
          <path
            d="M14 5.5 11.1 8.4a2.2 2.2 0 0 0 3.1 3.1l2.2-2.2a.8.8 0 0 1 1.1 0l2.8 2.8"
            stroke="black"
            strokeWidth="1.9"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <path
            d="m16.5 14-2.5 2.5m-1.5-1.5-2 2"
            stroke="black"
            strokeWidth="1.7"
            strokeLinecap="round"
          />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M19.5 12.572 12 20l-7.5-7.428A5 5 0 1 1 12 6.006a5 5 0 1 1 7.5 6.572Z"
        fill="currentColor"
      />
    </svg>
  );
}

export function SendFilledIcon({ className }: AdminNavIconProps) {
  const maskId = useId().replace(/:/g, "");
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="none"
      aria-hidden="true"
      className={className}
    >
      <defs>
        <mask id={maskId}>
          <rect width="24" height="24" fill="white" />
          <path
            d="M21.5 2.5 10.5 13.5"
            stroke="black"
            strokeWidth="1.9"
            strokeLinecap="round"
          />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M21.42 2.58a.9.9 0 0 1 .23.95l-5.74 17.5a.9.9 0 0 1-1.67.1l-3.67-6.77a.9.9 0 0 0-.36-.36L3.44 10.33a.9.9 0 0 1 .1-1.67l17.5-5.74a.9.9 0 0 1 .38-.34Z"
        fill="currentColor"
      />
    </svg>
  );
}

export function KeyRoundFilledIcon({ className }: AdminNavIconProps) {
  const maskId = useId().replace(/:/g, "");
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="none"
      aria-hidden="true"
      className={className}
    >
      <defs>
        <mask id={maskId}>
          <rect width="24" height="24" fill="white" />
          <circle cx="16.5" cy="7.5" r="1.75" fill="black" />
        </mask>
      </defs>
      <g mask={`url(#${maskId})`} fill="currentColor">
        <path d="M16.5 2A5.5 5.5 0 0 0 11 7.5c0 .7.14 1.38.39 2l-8.1 8.1a1 1 0 0 0-.29.7V21a1 1 0 0 0 1 1h2.5a1 1 0 0 0 1-1v-1.5H9a1 1 0 0 0 1-1V17h1.5a1 1 0 0 0 .7-.29l2.3-2.3c.62.25 1.3.39 2 .39a5.5 5.5 0 0 0 0-11Z" />
      </g>
    </svg>
  );
}
