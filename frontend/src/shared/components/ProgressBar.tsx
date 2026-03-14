export function ProgressBar({ value, tone = "default" }: { value: number; tone?: "default" | "warning" | "danger" }) {
  const safeValue = Math.max(0, Math.min(100, value));
  return (
    <div className="progress-bar">
      <div className={`progress-bar__fill progress-bar__fill--${tone}`} style={{ width: `${safeValue}%` }} />
    </div>
  );
}
