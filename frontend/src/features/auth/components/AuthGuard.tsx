import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../../app/providers/AuthProvider";
import { PageLoader } from "../../../shared/components/PageLoader";

export function AuthGuard() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    return <PageLoader label="Restoring your secure session" />;
  }

  if (status === "anonymous") {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
