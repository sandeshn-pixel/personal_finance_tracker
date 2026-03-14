export function Alert({ message }: { message: string }) {
  return <div className="alert-banner" role="alert">{message}</div>;
}
