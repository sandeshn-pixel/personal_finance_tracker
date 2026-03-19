import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
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

const reportSchema = z.object({
  startDateUtc: z.string().min(1, "Start date is required."),
  endDateUtc: z.string().min(1, "End date is required."),
  accountId: z.string().optional(),
});

type ReportFormValues = z.infer<typeof reportSchema>;

export function ReportsPage() {
  const { accessToken } = useAuth();
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

  const { register, handleSubmit, watch, formState: { errors, isSubmitting } } = useForm<ReportFormValues>({
    resolver: zodResolver(reportSchema),
    defaultValues: {
      startDateUtc: toDateInputValue(thirtyDaysAgo.toISOString()),
      endDateUtc: toDateInputValue(now.toISOString()),
      accountId: "",
    },
  });

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => { void loadReports(); }, [accessToken]);

  async function bootstrap() {
    if (!accessToken) return;
    try {
      const accountResponse = await accountsApi.list(accessToken, true);
      setAccounts(accountResponse);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load report filters.");
    }
  }

  function buildQuery(values?: ReportFormValues) {
    const activeValues = values ?? watch();
    return {
      startDateUtc: new Date(activeValues.startDateUtc).toISOString(),
      endDateUtc: new Date(activeValues.endDateUtc).toISOString(),
      ...(activeValues.accountId ? { accountId: activeValues.accountId } : {}),
    };
  }

  async function loadReports(values?: ReportFormValues) {
    if (!accessToken) return;
    setLoading(true);
    try {
      const response = await reportsApi.overview(accessToken, buildQuery(values));
      setOverview(response);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load reports.");
    } finally {
      setLoading(false);
    }
  }

  async function exportOverviewCsv() {
    if (!accessToken) return;
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

  const categorySpendMax = Math.max(...(overview?.categorySpend.map((item) => item.amount) ?? [0]));
  const categorySpendTotal = overview?.categorySpend.reduce((sum, item) => sum + item.amount, 0) ?? 0;
  const topMerchantMax = Math.max(...(overview?.topMerchants.map((item) => item.amount) ?? [0]));
  const balanceMax = Math.max(...(overview?.accountBalanceTrend.map((item) => Math.abs(item.balance)) ?? [0]));

  if (loading && !overview) {
    return <PageLoader label="Loading reports" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader title="Reports" description="Server-aggregated summaries, period comparison, top merchants, and cleaner trend analysis." />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {exportMessage ? <p className="form-status" role="status" aria-live="polite">{exportMessage}</p> : null}
      <form onSubmit={handleSubmit(loadReports)} noValidate>
        <FilterRow action={(
          <div className="button-row">
            <button type="button" className="ghost-button" onClick={exportOverviewCsv} disabled={isExportingCsv} aria-label="Export current report overview to CSV">
              {isExportingCsv ? "Exporting CSV..." : "Export CSV"}
            </button>
            <button type="button" className="ghost-button" onClick={exportOverviewPdf} disabled={isExportingPdf} aria-label="Export current report overview to PDF">
              {isExportingPdf ? "Exporting PDF..." : "Export PDF"}
            </button>
            <button type="submit" className="primary-button" disabled={isSubmitting}>
              {isSubmitting ? "Loading..." : "Apply filters"}
            </button>
          </div>
        )}>
          <Field label="Start date" error={errors.startDateUtc?.message}><input type="date" {...register("startDateUtc")} /></Field>
          <Field label="End date" error={errors.endDateUtc?.message}><input type="date" {...register("endDateUtc")} /></Field>
          <Field label="Account" error={errors.accountId?.message}>
            <SelectField {...register("accountId")}>
              <option value="">All accounts</option>
              {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
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
            <StatCard label="Coverage" value={`${overview.categorySpend.length} categories`} hint={`${overview.topMerchants.length} top merchants highlighted in this range.`} />
          </div>
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
            <ChartCard title="Income vs expense trend" description="Bucketed cashflow comparison across the selected period.">
              {overview.incomeExpenseTrend.length === 0 ? (
                <EmptyState title="No trend data" description="The trend summary appears once transactions fall within the selected range." />
              ) : (
                <div className="trend-list">
                  {overview.incomeExpenseTrend.map((point) => {
                    const bucketMax = Math.max(point.income, point.expense, 1);
                    const net = point.income - point.expense;
                    return (
                      <div key={point.periodStartUtc} className="trend-row">
                        <div className="trend-row__label">
                          <strong>{point.label}</strong>
                          <span className={`trend-pill ${net < 0 ? "trend-pill--negative" : ""}`}>{formatCurrency(net)} net</span>
                        </div>
                        <div className="trend-bars">
                          <div>
                            <span>Income</span>
                            <ProgressBar value={(point.income / bucketMax) * 100} tone="default" />
                          </div>
                          <div>
                            <span>Expense</span>
                            <ProgressBar value={(point.expense / bucketMax) * 100} tone="danger" />
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </ChartCard>
            <ChartCard title="Account balance trend" description="Closing-balance checkpoints across the selected period.">
              {overview.accountBalanceTrend.length === 0 ? (
                <EmptyState title="No balance history" description="Balance trend points appear when an account exists for the selected filter." />
              ) : (
                <div className="trend-list">
                  {overview.accountBalanceTrend.map((point) => (
                    <div key={point.periodStartUtc} className="trend-row">
                      <div className="trend-row__label">
                        <strong>{point.label}</strong>
                        <span>{formatCurrency(point.balance)}</span>
                      </div>
                      <ProgressBar value={(Math.abs(point.balance) / (balanceMax || 1)) * 100} tone={point.balance < 0 ? "danger" : "default"} />
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
