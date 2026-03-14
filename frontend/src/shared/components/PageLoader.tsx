export function PageLoader({ label }: { label: string }) {
  return (
    <div className="page-loader" role="status" aria-live="polite">
      <div className="page-loader__dot" />
      <span>{label}</span>
    </div>
  );
}
