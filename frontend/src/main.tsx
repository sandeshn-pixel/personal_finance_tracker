import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "react-router-dom";
import { AuthProvider } from "./app/providers/AuthProvider";
import { ThemeProvider } from "./app/providers/ThemeProvider";
import { router } from "./app/router/router";
import { WorkspaceScopeProvider } from "./app/providers/WorkspaceScopeProvider";
import "./shared/theme/tokens.css";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeProvider>
      <AuthProvider>
        <WorkspaceScopeProvider>
          <RouterProvider router={router} />
        </WorkspaceScopeProvider>
      </AuthProvider>
    </ThemeProvider>
  </React.StrictMode>,
);
