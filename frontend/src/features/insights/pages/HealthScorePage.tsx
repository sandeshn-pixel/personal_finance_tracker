import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { insightsApi, type HealthScoreResponseDto } from "../api/insightsApi";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatDate } from "../../../shared/lib/format";
import { filterAccountsForView, getScopedAccountIdsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

const bandTone: Record<string, "default" | "warning" | "danger"> = {
  Strong: "default",
  Good: "default",
  Fair: "warning",
  Poor: "danger",
};

export function HealthScorePage() {
  const { accessToken } = useAuth();
  const { sharedAccessView } = useWorkspaceScope();
  const [healthScore, setHealthScore] = useState<HealthScoreResponseDto | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");

  useEffect(() => {
    void load();
  }, [accessToken, sharedAccessView]);

  async function load() {
    if (!accessToken) {
      return;
    }

    setLoading(true);
    try {
      const accountResponse = await accountsApi.list(accessToken);
      const activeAccounts = accountResponse.filter((account) => !account.isArchived);
      const scopedAccountIds = getScopedAccountIdsForView(activeAccounts, sharedAccessView);
      setAccounts(accountResponse);

      if (sharedAccessView !== "all" && activeAccounts.length > 0 && scopedAccountIds.length === 0) {
        setHealthScore(null);
        setErrorVariant("error");
        setErrorMessage(null);
        return;
      }

      const scopeQuery = scopedAccountIds.length > 0 ? { accountIds: scopedAccountIds } : undefined;
      const response = await insightsApi.healthScore(accessToken, scopeQuery);
      setHealthScore(response);
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Health score is refreshing too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load health score.");
      }
    } finally {
      setLoading(false);
    }
  }

  const weakestFactors = useMemo(() => healthScore?.factors.filter((factor) => factor.score < 60) ?? [], [healthScore]);
  const weakestFactor = weakestFactors[0] ?? null;
  const activeAccounts = useMemo(() => accounts.filter((account) => !account.isArchived), [accounts]);
  const scopedAccounts = useMemo(() => filterAccountsForView(activeAccounts, sharedAccessView), [activeAccounts, sharedAccessView]);
  const sharedGuestAccounts = useMemo(() => scopedAccounts.filter((account) => account.isShared && account.currentUserRole !== "Owner"), [scopedAccounts]);
  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(activeAccounts), [activeAccounts]);
  const isScopeEmpty = sharedAccessView !== "all" && activeAccounts.length > 0 && scopedAccounts.length === 0;
  const scopeMessage = useMemo(() => {
    if (sharedAccessView === "shared" && sharedGuestAccounts.length > 0) {
      return "This score is focused on accounts shared with you. Because budgets are still owner-managed in this phase, budget adherence stays neutral when shared activity is included.";
    }

    if (sharedAccessView === "mine" && showSharedViewToggle) {
      return "This score is focused on your own accounts only. Shared-account cashflow and balances are excluded from this view.";
    }

    if (sharedGuestAccounts.length > 0) {
      return "This score includes shared-account cashflow and balances. Budget adherence may stay neutral when shared activity is part of the current scope.";
    }

    return null;
  }, [sharedAccessView, sharedGuestAccounts.length, showSharedViewToggle]);

  if (loading) return <PageLoader label="Loading health score" />;

  if (isScopeEmpty) {
    return (
      <div className="page-stack">
        <SectionHeader
          title="Financial health score"
          description="A weighted view of savings rate, expense stability, budget adherence, and current cash buffer using the last three completed months of recorded data."
        />
        {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
        {scopeMessage ? <Alert message={scopeMessage} variant="info" /> : null}
        <section className="panel-card insights-summary-card">
          <EmptyState title="No accounts in this view" description={sharedAccessView === "mine" ? "Mine is focused on accounts you own. There are no owned accounts available for a personal-only health score." : "Shared with me is focused on guest access. There are no shared guest accounts available for this score."} />
        </section>
      </div>
    );
  }

  if (!healthScore) return <Alert message={errorMessage ?? "Financial health score is unavailable."} variant={errorVariant} />;

  return (
    <div className="page-stack">
      <SectionHeader
        title="Financial health score"
        description="A weighted view of savings rate, expense stability, budget adherence, and current cash buffer using the last three completed months of recorded data."
      />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {scopeMessage ? <Alert message={scopeMessage} variant="info" /> : null}

      <div className="stats-grid stats-grid--four">
        <StatCard label="Overall score" value={`${healthScore.score}/100`} hint={`${healthScore.band} range`} tone={healthScore.score < 40 ? "negative" : healthScore.score >= 80 ? "positive" : undefined} />
        <StatCard label="Band" value={healthScore.band} hint={healthScore.hasSparseData ? "Uses fallback where data is limited." : "Based on recorded balances and transactions."} />
        <StatCard label="Lookback window" value={formatDate(healthScore.lookbackStartUtc)} hint={`to ${formatDate(healthScore.lookbackEndUtc)}`} />
        <StatCard label="Weakest factor" value={weakestFactor?.title ?? "None"} hint={weakestFactor ? `${weakestFactors.length} factor${weakestFactors.length === 1 ? "" : "s"} under watch.` : "No factors currently need attention."} />
      </div>

      <section className="panel-card insights-summary-card">
        <div className="panel-card__header panel-card__header--inline">
          <div>
            <h3>Score summary</h3>
          </div>
          <span className={`status-badge status-badge--${bandTone[healthScore.band]}`}>{healthScore.band}</span>
        </div>
        <p className="insights-summary-card__copy">{healthScore.summary}</p>
        {healthScore.hasSparseData ? (
          <Alert message="This score is still useful, but some factors are staying neutral because recent data is limited." variant="info" />
        ) : null}
      </section>

      <section className="panel-card insights-factor-card">
        <div className="panel-card__header">
          <h3>Factor breakdown</h3>
          <p>Each factor contributes a fixed share of the final 0 to 100 score.</p>
        </div>
        {healthScore.factors.length === 0 ? (
          <EmptyState title="No score factors available" description="Factor breakdown will appear once the score can be computed from your recorded data." />
        ) : (
          <div className="insights-factor-list">
            {healthScore.factors.map((factor) => (
              <article key={factor.key} className="insights-factor-row">
                <div className="insights-factor-row__top">
                  <div>
                    <strong>{factor.title}</strong>
                    <p>{factor.explanation}</p>
                  </div>
                  <div className="insights-factor-row__score">
                    <strong>{factor.score}/100</strong>
                    <small>{factor.weightPercent}% weight • {factor.weightedPoints.toFixed(2)} pts</small>
                  </div>
                </div>
                <ProgressBar value={factor.score} tone={factor.score < 40 ? "danger" : factor.score < 60 ? "warning" : "default"} />
                <div className="insights-factor-row__meta">
                  <span>{factor.metricLabel}: {factor.metricValue.toFixed(2)}</span>
                  <span>{factor.isFallback ? "Neutral fallback used" : "Fully computed"}</span>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="panel-card insights-suggestions-card">
        <div className="panel-card__header">
          <h3>Suggestions</h3>
          <p>Short, practical actions derived from the weakest current factors.</p>
        </div>
        {healthScore.suggestions.length === 0 ? (
          <EmptyState title="No suggestions yet" description="Suggestions will appear when one or more factors need attention." />
        ) : (
          <div className="simple-list insights-suggestion-list">
            {healthScore.suggestions.map((suggestion) => (
              <div key={suggestion} className="list-row list-row--stacked insights-suggestion-row">
                <strong>{suggestion}</strong>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}



