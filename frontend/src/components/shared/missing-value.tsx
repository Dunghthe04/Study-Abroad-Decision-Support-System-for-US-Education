export function MissingValue({ className }: { className?: string }) {
  return (
    <span
      title="Chưa có dữ liệu"
      aria-label="Chưa có dữ liệu"
      className={className ? `text-muted-foreground ${className}` : "text-muted-foreground"}
    >
      —
    </span>
  );
}
