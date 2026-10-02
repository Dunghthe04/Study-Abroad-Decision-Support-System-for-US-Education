import Link from "next/link";

const links = [
  { href: "/advisor", label: "Tư vấn AI" },
  { href: "/centers", label: "Trung tâm" },
];

export function SiteHeader() {
  return (
    <header className="border-b border-slate-200 bg-white">
      <nav className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <Link href="/" className="font-semibold text-slate-900">
          USAS
        </Link>
        <ul className="flex gap-6 text-sm text-slate-600">
          {links.map((l) => (
            <li key={l.href}>
              <Link href={l.href} className="hover:text-slate-900">
                {l.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </header>
  );
}
