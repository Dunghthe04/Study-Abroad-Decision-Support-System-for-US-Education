"use client";

import { useId } from "react";

export interface AdminNavIconProps {
  className?: string;
}

export function LayoutDashboardFilledIcon({ className }: AdminNavIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="currentColor"
      aria-hidden="true"
      className={className}
    >
      <rect width="7" height="9" x="3" y="3" rx="1.2" />
      <rect width="7" height="5" x="14" y="3" rx="1.2" />
      <rect width="7" height="9" x="14" y="12" rx="1.2" />
      <rect width="7" height="5" x="3" y="16" rx="1.2" />
    </svg>
  );
}

export function UsersFilledIcon({ className }: AdminNavIconProps) {
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
          <circle cx="9" cy="7" r="5.2" fill="black" />
          <path d="M1 21v-2a5.2 5.2 0 0 1 5.2-5.2h5.6A5.2 5.2 0 0 1 17 19v2Z" fill="black" />
        </mask>
      </defs>
      <g mask={`url(#${maskId})`} fill="currentColor">
        <circle cx="16" cy="7.2" r="3.4" />
        <path d="M15 15.2a4.5 4.5 0 0 1 7 3.8v2h-7Z" />
      </g>
      <circle cx="9" cy="7" r="4" fill="currentColor" />
      <path d="M2 21v-2a4 4 0 0 1 4-4h6a4 4 0 0 1 4 4v2Z" fill="currentColor" />
    </svg>
  );
}

export function SchoolFilledIcon({ className }: AdminNavIconProps) {
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
          <circle cx="12" cy="9" r="1.8" fill="black" />
          <path d="M10 21v-3a2 2 0 0 1 4 0v3Z" fill="black" />
          <path d="M6 11v10M18 11v10" stroke="black" strokeWidth="1.2" />
        </mask>
      </defs>
      <g mask={`url(#${maskId})`} fill="currentColor">
        <path d="M12 2.2a2 2 0 0 1 .95.24L18 5.1V21H6V5.1l5.05-2.66a2 2 0 0 1 .95-.24Z" />
        <path d="M6 11 2.52 13.15A1 1 0 0 0 2 14v5a2 2 0 0 0 2 2h2Z" />
        <path d="M18 11l3.48 2.15A1 1 0 0 1 22 14v5a2 2 0 0 1-2 2h-2Z" />
      </g>
    </svg>
  );
}

export function GraduationCapFilledIcon({ className }: AdminNavIconProps) {
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
        d="M21.42 10.922a1 1 0 0 0-.019-1.838L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.832l8.57 3.908a2 2 0 0 0 1.66 0z"
        fill="currentColor"
      />
      <path
        d="M6 13.4V16a6 3 0 0 0 12 0v-2.6l-4.8 2.18a3 3 0 0 1-2.4 0Z"
        fill="currentColor"
      />
      <path
        d="M22 10v6"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function BookOpenFilledIcon({ className }: AdminNavIconProps) {
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
          <path d="M12 4.5v16.5" stroke="black" strokeWidth="1.8" strokeLinecap="round" />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M20.001 19A2 2 0 0 0 22 17V5a2 2 0 0 0-1.999-2L16 3.002A5 5 0 0 0 12 5a5 5 0 0 0-4-2H4a2 2 0 0 0-2 2v12a2 2 0 0 0 1.999 2H8a5 5 0 0 1 4 2 5 5 0 0 1 4-2Z"
        fill="currentColor"
      />
    </svg>
  );
}

export function LibraryBigFilledIcon({ className }: AdminNavIconProps) {
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
          <path d="M7 3v18" stroke="black" strokeWidth="1.6" />
        </mask>
      </defs>
      <rect
        mask={`url(#${maskId})`}
        width="8"
        height="18"
        x="3"
        y="3"
        rx="1"
        fill="currentColor"
      />
      <path
        d="M20.4 18.9c.2.5-.1 1.1-.6 1.3l-1.9.7c-.5.2-1.1-.1-1.3-.6L11.1 5.1c-.2-.5.1-1.1.6-1.3l1.9-.7c.5-.2 1.1.1 1.3.6Z"
        fill="currentColor"
      />
    </svg>
  );
}

export function BuildingFilledIcon({ className }: AdminNavIconProps) {
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
          <rect x="7.1" y="5.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="11.1" y="5.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="15.1" y="5.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="7.1" y="9.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="11.1" y="9.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="15.1" y="9.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="7.1" y="13.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="11.1" y="13.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <rect x="15.1" y="13.1" width="1.8" height="1.8" rx="0.4" fill="black" />
          <path d="M9.5 22v-3.5a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1V22Z" fill="black" />
        </mask>
      </defs>
      <rect
        mask={`url(#${maskId})`}
        x="4"
        y="2"
        width="16"
        height="20"
        rx="2"
        fill="currentColor"
      />
    </svg>
  );
}

export function ClipboardCheckFilledIcon({ className }: AdminNavIconProps) {
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
          <rect width="10" height="5.5" x="7" y="1.5" rx="1.5" fill="black" />
          <path
            d="m9 14 2 2 4-4"
            stroke="black"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </mask>
      </defs>
      <rect width="8" height="4" x="8" y="2" rx="1" fill="currentColor" />
      <rect
        mask={`url(#${maskId})`}
        width="16"
        height="18"
        x="4"
        y="4"
        rx="2"
        fill="currentColor"
      />
    </svg>
  );
}

export function CreditCardFilledIcon({ className }: AdminNavIconProps) {
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
          <rect width="20" height="2.2" x="2" y="9" fill="black" />
          <path d="M6 14.5h3" stroke="black" strokeWidth="2" strokeLinecap="round" />
        </mask>
      </defs>
      <rect
        mask={`url(#${maskId})`}
        width="20"
        height="14"
        x="2"
        y="5"
        rx="2"
        fill="currentColor"
      />
    </svg>
  );
}

export function SlidersHorizontalFilledIcon({ className }: AdminNavIconProps) {
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
        d="M21 5H3M21 12H3M21 19H3"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <circle cx="14" cy="5" r="2.75" fill="currentColor" />
      <circle cx="8" cy="12" r="2.75" fill="currentColor" />
      <circle cx="16" cy="19" r="2.75" fill="currentColor" />
    </svg>
  );
}

export function MessagesSquareFilledIcon({ className }: AdminNavIconProps) {
  const maskId = useId().replace(/:/g, "");
  const frontBubble =
    "M16 10a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 14.286V4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z";
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
          <path d={frontBubble} fill="black" stroke="black" strokeWidth="3" strokeLinejoin="round" />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M20 9a2 2 0 0 1 2 2v10.286a.71.71 0 0 1-1.212.502l-2.202-2.202A2 2 0 0 0 17.172 19H10a2 2 0 0 1-2-2V9Z"
        fill="currentColor"
      />
      <path d={frontBubble} fill="currentColor" />
    </svg>
  );
}

export function ArchiveXFilledIcon({ className }: AdminNavIconProps) {
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
            d="m9.5 17.5 5-5M9.5 12.5l5 5"
            stroke="black"
            strokeWidth="2.2"
            strokeLinecap="round"
          />
        </mask>
      </defs>
      <rect width="20" height="5" x="2" y="3" rx="1" fill="currentColor" />
      <path
        mask={`url(#${maskId})`}
        d="M4 9.5h16V19a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V9.5Z"
        fill="currentColor"
      />
    </svg>
  );
}

export function MailWarningFilledIcon({ className }: AdminNavIconProps) {
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
            d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"
            stroke="black"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <rect x="16.5" y="12" width="6.5" height="11" rx="2.5" fill="black" />
        </mask>
      </defs>
      <rect
        mask={`url(#${maskId})`}
        width="20"
        height="16"
        x="2"
        y="4"
        rx="2"
        fill="currentColor"
      />
      <path
        d="M20 14v4M20 21.5v.01"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function BarChart3FilledIcon({ className }: AdminNavIconProps) {
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
        d="M3 3v16a2 2 0 0 0 2 2h16"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <rect x="7" y="12" width="3" height="6" rx="0.75" fill="currentColor" />
      <rect x="12" y="5" width="3" height="13" rx="0.75" fill="currentColor" />
      <rect x="17" y="9" width="3" height="9" rx="0.75" fill="currentColor" />
    </svg>
  );
}

export function ShieldAlertFilledIcon({ className }: AdminNavIconProps) {
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
          <path d="M12 8v4" stroke="black" strokeWidth="2.2" strokeLinecap="round" />
          <circle cx="12" cy="16" r="1.2" fill="black" />
        </mask>
      </defs>
      <path
        mask={`url(#${maskId})`}
        d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"
        fill="currentColor"
      />
    </svg>
  );
}
