import { createContext, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { SharedAccessView } from "../../shared/lib/sharedAccessView";

type WorkspaceScopeContextValue = {
  sharedAccessView: SharedAccessView;
  setSharedAccessView: (value: SharedAccessView) => void;
};

const STORAGE_KEY = "ledgernest.sharedAccessView";
const WorkspaceScopeContext = createContext<WorkspaceScopeContextValue | undefined>(undefined);

function readInitialValue(): SharedAccessView {
  if (typeof window === "undefined") {
    return "all";
  }

  const stored = window.localStorage.getItem(STORAGE_KEY);
  return stored === "mine" || stored === "shared" || stored === "all" ? stored : "all";
}

export function WorkspaceScopeProvider({ children }: { children: ReactNode }) {
  const [sharedAccessView, setSharedAccessViewState] = useState<SharedAccessView>(readInitialValue);

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, sharedAccessView);
  }, [sharedAccessView]);

  const value = useMemo<WorkspaceScopeContextValue>(() => ({
    sharedAccessView,
    setSharedAccessView: setSharedAccessViewState,
  }), [sharedAccessView]);

  return <WorkspaceScopeContext.Provider value={value}>{children}</WorkspaceScopeContext.Provider>;
}

export function useWorkspaceScope() {
  const context = useContext(WorkspaceScopeContext);
  if (!context) {
    throw new Error("useWorkspaceScope must be used within WorkspaceScopeProvider");
  }

  return context;
}
