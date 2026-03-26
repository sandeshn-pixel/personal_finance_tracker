import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { reportsApi, type ReportsOverviewDto } from "../api/reportsApi";
import { Alert } from "../../../shared/components/Alert";
import { ChartCard } from "../../../shared/components/ChartCard";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { FilterRow } from "../../../shared/components/FilterRow";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, toDateInputValue } from "../../../shared/lib/format";
import { filterAccountsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

const reportSchema = z.object({
  startDateUtc: z.string().min(1, "Start date is required."),
  endDateUtc: z.string().min(1, "End date is required."),
  accountId: z.string().optional(),
});

type ReportFormValues = z.infer<typeof reportSchema>;

export function ReportsPage() {
  const { accessToken } = useAuth();
  const { sharedAccessView: reportView } = useWorkspaceScope();
  const now = new Date();
  const thirtyDaysAgo = new Date();
  thirtyDaysAgo.setDate(now.getDate() - 29);

  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [overview, setOverview] = useState<ReportsOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [exportMessage, setExportMessage] = useState<string | null>(null);
  const [isExportingCsv, setIsExportingCsv] = useState(false);
  const [isExportingPdf, setIsExportingPdf] = useState(false);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");

  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } = useForm<ReportFormValues>({
    resolver: zodResolver(reportSchema),
    defaultValues: {
      startDateUtc: toDateInputValue(thirtyDaysAgo.toISOString()),
      endDateUtc: toDateInputValue(now.toISOString()),
      accountId: "",
    },
  });

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => { void loadReports(); }, [accessToken, reportView, accounts]);

  async function bootstrap() {
    if (!accessToken) return;
    try {
      const accountResponse = await accountsApi.list(accessToken, true);
      setAccounts(accountResponse);
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Reports are refreshing too quickly for the current session limit. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load report filters.");
      }
    }
  }

  const activeAccounts = useMemo(() => accounts.filter((account) => !account.isArchived), [accounts]);
  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(activeAccounts), [activeAccounts]);
  const visibleAccounts = useMemo(() => filterAccountsForView(activeAccounts, reportView), [activeAccounts, reportView]);

  const selectedAccountId = watch("accountId") || "";
  const selectedAccount = useMemo(() => activeAccounts.find((account) => account.id === selectedAccountId) ?? null, [activeAccounts, selectedAccountId]);
  const isScopeEmpty = reportView !== "all" && visibleAccounts.length === 0;

  useEffect(() => {
    if (selectedAccountId && !visibleAccounts.some((account) => account.id === selectedAccountId)) {
      setValue("accountId", "");
    }
  }, [selectedAccountId, setValue, visibleAccounts]);

  function buildQuery(values?: ReportFormValues) {
    const activeValues = values ?? watch();
    const selectedId = activeValues.accountId || "";
    const scopedAccountIds = !selectedId && reportView !== "all" ? visibleAccounts.map((account) => account.id) : [];

    return {
      startDateUtc: new Date(activeValues.startDateUtc).toISOString(),
      endDateUtc: new Date(activeValues.endDateUtc).toISOString(),
      ...(selectedId ? { accountId: selectedId } : {}),
      ...(scopedAccountIds.length > 0 ? { accountIds: scopedAccountIds } : {}),
    };
  }

  async function loadReports(values?: ReportFormValues) {
    if (!accessToken) return;
    setLoading(true);
    try {
      const activeValues = values ?? watch();
      if (reportView !== "all" && !activeValues.accountId && visibleAccounts.length === 0) {
        setOverview(null);
        setErrorVariant("error");
        setErrorMessage(null);
        return;
      }

      const response = await reportsApi.overview(accessToken, buildQuery(activeValues));
      setOverview(response);
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Reports are being refreshed too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load reports.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function exportOverviewCsv() {
    if (!accessToken) return;
    if (reportView !== "all" && !selectedAccountId && visibleAccounts.length === 0) {
      setErrorVariant("info");
      setErrorMessage("There are no accounts in the current report scope to export.");
      return;
    }
    setIsExportingCsv(true);
    setExportMessage(null);
    try {
      const fileName = await reportsApi.exportOverviewCsv(accessToken, buildQuery());
      setExportMessage(`Exported ${fileName}.`);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to export report data.");
    } finally {
      setIsExportingCsv(false);
    }
  }

  async function exportOverviewPdf() {
    if (!accessToken) return;
    if (reportView !== "all" && !selectedAccountId && visibleAccounts.length === 0) {
      setErrorVariant("info");
      setErrorMessage("There are no accounts in the current report scope to export.");
      return;
    }
    setIsExportingPdf(true);
    setExportMessage(null);
    try {
      const fileName = await reportsApi.exportOverviewPdf(accessToken, buildQuery());
      setExportMessage(`Exported ${fileName}.`);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to export report PDF.");
    } finally {
      setIsExportingPdf(false);
    }
  }

  function formatDelta(current: number, previous: number, noun: string) {
    const delta = current - previous;
    if (previous === 0 && current === 0) {
      return "No change vs previous period.";
    }

    if (previous === 0) {
      return `${formatCurrency(delta)} more ${noun} than the previous period.`;
    }

    const direction = delta >= 0 ? "up" : "down";
    const percentage = Math.abs((delta / previous) * 100);
    return `${direction} ${percentage.toFixed(1)}% vs previous period (${formatCurrency(Math.abs(delta))}).`;
  }

  const sharedContextMessage = useMemo(() => {
    if (selectedAccount?.isShared) {
      return `This report is scoped to the shared account ${selectedAccount.name}. Category spend, merchants, and balances include activity visible through that shared account. Your role is ${selectedAccount.currentUserRole}.`;
    }

    if (reportView === "shared" && visibleAccounts.length > 0) {
      return `This report currently focuses only on ${visibleAccounts.length} shared account${visibleAccounts.length === 1 ? "" : "s"} that you can access.`;
    }

    if (reportView === "mine" && showSharedViewToggle) {
      return "This report currently excludes accounts shared with you and focuses only on your own account space.";
    }

    if (!selectedAccountId && reportView === "all" && activeAccounts.some((account) => account.isShared)) {
      const sharedAccounts = activeAccounts.filter((account) => account.isShared);
      return `All-accounts reporting currently includes ${sharedAccounts.length} shared account${sharedAccounts.length === 1 ? "" : "s"} alongside your own accounts.`;
    }

    return null;
  }, [activeAccounts, reportView, selectedAccount, selectedAccountId, showSharedViewToggle, visibleAccounts]);

  const categorySpendMax = Math.max(...(overview?.categorySpend.map((item) => item.amount) ?? [0]));
  const categorySpendTotal = overview?.categorySpend.reduce((sum, item) => sum + item.amount, 0) ?? 0;
  const topMerchantMax = Math.max(...(overview?.topMerchants.map((item) => item.amount) ?? [0]));

  if (loading && !overview) {
    return <PageLoader label="Loading reports" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader
        title="Reports"
        description="Structured summaries and export-ready breakdowns for the selected period. Use Insights for deeper visual analysis and trend interpretation."
        action={<Link to="/insights" className="ghost-button">Open insights</Link>}
      />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {sharedContextMessage ? (
        <div className="page-context-row">
          <div className="rules-preview"><strong>Reporting scope</strong><p>{sharedContextMessage}</p></div>
        </div>
      ) : null}
      {exportMessage ? <p className="form-status" role="status" aria-live="polite">{exportMessage}</p> : null}
      {isScopeEmpty ? (
        <section className="panel-card">
          <EmptyState title="No accounts in this view" description={reportView === "mine" ? "Mine is focused on accounts you own. There are no owned accounts available for reporting." : "Shared with me is focused on accounts owned by other people. There are no shared guest accounts available for reporting."} />
        </section>
      ) : null}
      <form onSubmit={handleSubmit(loadReports)} noValidate>
        <FilterRow action={(
          <div className="button-row">
            <button type="button" className="ghost-button" onClick={exportOverviewCsv} disabled={isExportingCsv || isScopeEmpty} aria-label="Export current report overview to CSV">
              {isExportingCsv ? "Exporting CSV..." : "Export CSV"}
            </button>
            <button type="button" className="ghost-button" onClick={exportOverviewPdf} disabled={isExportingPdf || isScopeEmpty} aria-label="Export current report overview to PDF">
              {isExportingPdf ? "Exporting PDF..." : "Export PDF"}
            </button>
            <button type="submit" className="primary-button" disabled={isSubmitting || isScopeEmpty}>
              {isSubmitting ? "Loading..." : "Apply filters"}
            </button>
          </div>
        )}>
          <Field label="Start date" error={errors.startDateUtc?.message}><input type="date" {...register("startDateUtc")} /></Field>
          <Field label="End date" error={errors.endDateUtc?.message}><input type="date" {...register("endDateUtc")} /></Field>
          <Field label="Account" error={errors.accountId?.message}>
            <SelectField {...register("accountId")}>
              <option value="">All accounts in this view</option>
              {visibleAccounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
            </SelectField>
          </Field>
        </FilterRow>
      </form>
      {overview ? (
        <>
          <div className="stats-grid stats-grid--four">
            <StatCard label="Total income" value={formatCurrency(overview.summary.totalIncome)} hint={formatDelta(overview.summary.totalIncome, overview.comparison.previousTotalIncome, "income")} tone="positive" />
            <StatCard label="Total expense" value={formatCurrency(overview.summary.totalExpense)} hint={formatDelta(overview.summary.totalExpense, overview.comparison.previousTotalExpense, "expense")} tone="negative" />
            <StatCard label="Net cash flow" value={formatCurrency(overview.summary.netCashFlow)} hint={formatDelta(overview.summary.netCashFlow, overview.comparison.previousNetCashFlow, "net cash flow")} tone={overview.summary.netCashFlow < 0 ? "negative" : "positive"} />
            <StatCard label="Coverage" value={`${overview.categorySpend.length} categories`} hint={`${overview.topMerchants.length} merchants in this range.`} />
          </div>

          <section className="panel-card">
            <div className="panel-card__header panel-card__header--inline">
              <div>
                <h3>Report snapshot</h3>
                <p>A practical overview for review, export, and month-over-month comparison.</p>
              </div>
              <Link to="/insights" className="ghost-button ghost-button--small">See advanced trends</Link>
            </div>
            <div className="dashboard-limits-grid">
              <div className="dashboard-mini-stat">
                <span>Previous income</span>
                <strong>{formatCurrency(overview.comparison.previousTotalIncome)}</strong>
              </div>
              <div className="dashboard-mini-stat">
                <span>Previous expense</span>
                <strong>{formatCurrency(overview.comparison.previousTotalExpense)}</strong>
              </div>
              <div className="dashboard-mini-stat">
                <span>Previous net</span>
                <strong>{formatCurrency(overview.comparison.previousNetCashFlow)}</strong>
              </div>
            </div>
          </section>

          <div className="reports-grid reports-grid--equal">
            <ChartCard title="Category spend" description="Expense share by category for the selected period.">
              {overview.categorySpend.length === 0 ? (
                <EmptyState title="No expense data" description="Category spend appears once expense transactions exist in the selected range." />
              ) : (
                <div className="report-bars">
                  {overview.categorySpend.map((item) => {
                    const share = categorySpendTotal > 0 ? (item.amount / categorySpendTotal) * 100 : 0;
                    return (
                      <div key={item.categoryId} className="report-bar-row">
                        <div className="report-bar-row__head">
                          <strong>{item.categoryName}</strong>
                          <span>{share.toFixed(1)}%</span>
                        </div>
                        <ProgressBar value={(item.amount / (categorySpendMax || 1)) * 100} />
                        <div className="report-bar-row__foot">
                          <span>{formatCurrency(item.amount)}</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </ChartCard>

            <ChartCard title="Top merchants" description="Highest-spend payees for the selected period.">
              {overview.topMerchants.length === 0 ? (
                <EmptyState title="No merchant insight yet" description="Merchant spend appears once expense transactions include merchant names." />
              ) : (
                <div className="merchant-list">
                  {overview.topMerchants.map((merchant) => (
                    <div key={merchant.merchantName} className="merchant-row">
                      <div className="merchant-row__copy">
                        <strong>{merchant.merchantName}</strong>
                        <span>{merchant.transactionCount} transaction{merchant.transactionCount === 1 ? "" : "s"}</span>
                      </div>
                      <div className="merchant-row__value">
                        <strong>{formatCurrency(merchant.amount)}</strong>
                        <ProgressBar value={(merchant.amount / (topMerchantMax || 1)) * 100} />
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </ChartCard>
          </div>
        </>
      ) : null}
    </div>
  );
}
