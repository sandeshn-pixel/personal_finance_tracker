import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../../../app/providers/AuthProvider";
import { insightsApi, type HealthScoreResponseDto } from "../api/insightsApi";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatDate } from "../../../shared/lib/format";

const bandTone: Record<string, "default" | "warning" | "danger"> = {
  Strong: "default",
  Good: "default",
  Fair: "warning",
  Poor: "danger",
};

export function HealthScorePage() {
  const { accessToken } = useAuth();
  const [healthScore, setHealthScore] = useState<HealthScoreResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    void load();
  }, [accessToken]);

  async function load() {
    if (!accessToken) {
      return;
    }

    setLoading(true);
    try {
      const response = await insightsApi.healthScore(accessToken);
      setHealthScore(response);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load health score.");
    } finally {
      setLoading(false);
    }
  }

  const weakestFactors = useMemo(() => healthScore?.factors.filter((factor) => factor.score < 60) ?? [], [healthScore]);

  if (loading) return <PageLoader label="Loading health score" />;
  if (!healthScore) return <Alert message={errorMessage ?? "Financial health score is unavailable."} />;

  return (
    <div className="page-stack">
      <SectionHeader
        title="Financial health score"
        description="A weighted view of savings rate, expense stability, budget adherence, and current cash buffer using the last three completed months of recorded data."
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}

      <div className="stats-grid stats-grid--four">
        <StatCard label="Overall score" value={`${healthScore.score}/100`} hint={healthScore.summary} tone={healthScore.score < 40 ? "negative" : healthScore.score >= 80 ? "positive" : undefined} />
        <StatCard label="Band" value={healthScore.band} hint={healthScore.hasSparseData ? "Some factors use neutral fallback logic because recent data is limited." : "All factors are based on recorded balances, budgets, and transactions."} />
        <StatCard label="Lookback start" value={formatDate(healthScore.lookbackStartUtc)} hint="The score uses the last three completed months for stability." />
        <StatCard label="Lookback end" value={formatDate(healthScore.lookbackEndUtc)} hint={`${weakestFactors.length} factor${weakestFactors.length === 1 ? "" : "s"} currently need the most attention.`} />
      </div>

      <section className="panel-card insights-summary-card">
        <div className="panel-card__header panel-card__header--inline">
          <div>
            <h3>Score summary</h3>
            <p>{healthScore.summary}</p>
          </div>
          <span className={`status-badge status-badge--${bandTone[healthScore.band]}`}>{healthScore.band}</span>
        </div>
        {healthScore.hasSparseData ? (
          <Alert message="This score is still useful, but some factors are staying neutral because recent data is limited." />
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
