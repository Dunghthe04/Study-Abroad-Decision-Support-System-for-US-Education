"use client";

import { useId } from "react";
import {
  BookOpenFilledIcon,
  CreditCardFilledIcon,
  UsersFilledIcon,
  type AdminNavIconProps,
} from "@/components/admin/AdminNavIcons";

export { BookOpenFilledIcon, CreditCardFilledIcon, UsersFilledIcon };

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
      <circle cx="12" cy="7" r="4" />
      <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2Z" />
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

export function ReceiptTextFilledIcon({ className }: AdminNavIconProps) {
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
          <path d="M14 8H8M16 12H8M13 16H8" stroke="black" strokeWidth="1.9" strokeLinecap="round" />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M4 3a1 1 0 0 1 1-1 1.3 1.3 0 0 1 .7.2l.933.6a1.3 1.3 0 0 0 1.4 0l.934-.6a1.3 1.3 0 0 1 1.4 0l.933.6a1.3 1.3 0 0 0 1.4 0l.933-.6a1.3 1.3 0 0 1 1.4 0l.934.6a1.3 1.3 0 0 0 1.4 0l.933-.6A1.3 1.3 0 0 1 19 2a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1 1.3 1.3 0 0 1-.7-.2l-.933-.6a1.3 1.3 0 0 0-1.4 0l-.934.6a1.3 1.3 0 0 1-1.4 0l-.933-.6a1.3 1.3 0 0 0-1.4 0l-.933.6a1.3 1.3 0 0 1-1.4 0l-.934-.6a1.3 1.3 0 0 0-1.4 0l-.933.6a1.3 1.3 0 0 1-.7.2 1 1 0 0 1-1-1z"
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
            d="m21.854 2.147-10.94 10.939"
            stroke="black"
            strokeWidth="1.9"
            strokeLinecap="round"
          />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z"
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
          <circle cx="16.5" cy="7.5" r="1.6" fill="black" />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4z"
        fill="currentColor"
      />
    </svg>
  );
}

export function GaugeFilledIcon({ className }: AdminNavIconProps) {
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
          <path d="m12 13.5 3.5-3.5" stroke="black" strokeWidth="2" strokeLinecap="round" />
          <circle cx="12" cy="13.5" r="1.4" fill="black" />
        </mask>
      </defs>
      <circle cx="12" cy="12" r="10" mask={`url(#${maskId})`} fill="currentColor" />
    </svg>
  );
}
