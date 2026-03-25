import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountInvitePreviewDto } from "../api/accountsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { ApiError } from "../../../shared/lib/api/client";
import { formatDate } from "../../../shared/lib/format";

export function AccountInviteAcceptPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const { status, accessToken, user } = useAuth();
  const [preview, setPreview] = useState<AccountInvitePreviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [accepting, setAccepting] = useState(false);

  const redirectTarget = useMemo(() => `/account-invites/accept?token=${encodeURIComponent(token)}`, [token]);
  const statusLabel = preview?.canAccept ? "Ready" : preview?.status ?? "Review needed";

  useEffect(() => {
    if (!token) {
      setLoading(false);
      setErrorMessage("This invite link is incomplete.");
      return;
    }

    void loadPreview();
  }, [token, accessToken, status]);

  async function loadPreview() {
    setLoading(true);
    try {
      const response = await accountsApi.previewInvite(token, accessToken);
      setPreview(response);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load invite details.");
      setPreview(null);
    } finally {
      setLoading(false);
    }
  }

  async function acceptInvite() {
    if (!accessToken || !token) return;

    setAccepting(true);
    try {
      await accountsApi.acceptInvite(accessToken, token);
      setSuccessMessage("Shared-account access has been added to your workspace.");
      await loadPreview();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to accept this invite.");
    } finally {
      setAccepting(false);
    }
  }

  if (loading || status === "loading") return <PageLoader label="Loading invite details" />;

  return (
    <div className="page-stack">
      <SectionHeader
        title="Shared account invite"
        description="Review the invite details before access is added to your workspace."
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {successMessage ? <p className="form-status">{successMessage}</p> : null}

      {!token ? (
        <EmptyState title="Invite link unavailable" description="Open the full invite link from the email or notification to continue." />
      ) : preview ? (
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>{preview.accountName}</h3>
            <p>Owner: {preview.ownerDisplayName}</p>
          </div>
          <div className="stats-grid stats-grid--four">
            <article className="stat-card">
              <p>Invited email</p>
              <strong>{preview.email}</strong>
              <span>This email must match the account you use to accept.</span>
            </article>
            <article className="stat-card">
              <p>Role</p>
              <strong>{preview.role}</strong>
              <span>{preview.role === "Editor" ? "Can view and manage transactions on this shared account." : "Can view this shared account without changing it."}</span>
            </article>
            <article className="stat-card">
              <p>Expires</p>
              <strong>{formatDate(preview.expiresUtc)}</strong>
              <span>Pending invites expire automatically if unused.</span>
            </article>
            <article className="stat-card">
              <p>Status</p>
              <strong>{statusLabel}</strong>
              <span>{preview.statusMessage}</span>
            </article>
          </div>

          {status === "anonymous" ? (
            <div className="rules-preview">
              <strong>Sign in first.</strong>
              <p>{preview.statusMessage}</p>
              <div className="section-header__actions">
                <Link className="ghost-button" to={`/login?redirect=${encodeURIComponent(redirectTarget)}`}>Sign in</Link>
                <Link className="ghost-button" to={`/signup?redirect=${encodeURIComponent(redirectTarget)}`}>Create account</Link>
              </div>
            </div>
          ) : preview.canAccept ? (
            <div className="rules-preview">
              <strong>Signed in as {user?.email}</strong>
              <p>{preview.statusMessage}</p>
              <div className="section-header__actions">
                <Button type="button" onClick={acceptInvite} loading={accepting}>Accept invite</Button>
                <Link className="ghost-button" to={`/accounts/${preview.accountId}`}>Go to account</Link>
              </div>
            </div>
          ) : (
            <div className="rules-preview">
              <strong>Signed in as {user?.email}</strong>
              <p>{preview.statusMessage}</p>
              <div className="section-header__actions">
                {preview.requiresDifferentAccount ? <Link className="ghost-button" to={`/login?redirect=${encodeURIComponent(redirectTarget)}`}>Sign in with another account</Link> : null}
                <Link className="ghost-button" to={preview.accountId ? `/accounts/${preview.accountId}` : "/accounts"}>Back to accounts</Link>
              </div>
            </div>
          )}
        </section>
      ) : null}
    </div>
  );
}
