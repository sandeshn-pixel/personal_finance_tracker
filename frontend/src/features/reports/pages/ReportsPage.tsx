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
  const [isExporting, setIsExporting] = useState(false);

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

  async function exportOverview() {
    if (!accessToken) return;
    setIsExporting(true);
    setExportMessage(null);
    try {
      const fileName = await reportsApi.exportOverviewCsv(accessToken, buildQuery());
      setExportMessage(`Exported ${fileName}.`);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to export report data.");
    } finally {
      setIsExporting(false);
    }
  }

  const categorySpendMax = Math.max(...(overview?.categorySpend.map((item) => item.amount) ?? [0]));
  const balanceMax = Math.max(...(overview?.accountBalanceTrend.map((item) => Math.abs(item.balance)) ?? [0]));

  if (loading && !overview) {
    return <PageLoader label="Loading reports" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader title="Reports" description="Server-aggregated summaries with compact weekly or monthly trend buckets." />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {exportMessage ? <p className="form-status" role="status" aria-live="polite">{exportMessage}</p> : null}
      <form onSubmit={handleSubmit(loadReports)} noValidate>
        <FilterRow action={<div className="button-row"><button type="button" className="ghost-button" onClick={exportOverview} disabled={isExporting} aria-label="Export current report overview to CSV">{isExporting ? "Exporting..." : "Export CSV"}</button><button type="submit" className="primary-button" disabled={isSubmitting}>{isSubmitting ? "Loading..." : "Apply filters"}</button></div>}>
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
            <StatCard label="Total income" value={formatCurrency(overview.summary.totalIncome)} hint={`${overview.summary.incomeTransactionCount} income transactions`} tone="positive" />
            <StatCard label="Total expense" value={formatCurrency(overview.summary.totalExpense)} hint={`${overview.summary.expenseTransactionCount} expense transactions`} tone="negative" />
            <StatCard label="Net cash flow" value={formatCurrency(overview.summary.netCashFlow)} hint="Income minus expense in the selected range." tone={overview.summary.netCashFlow < 0 ? "negative" : "positive"} />
            <StatCard label="Coverage" value={`${overview.categorySpend.length} categories`} hint="Expense categories represented in this range." />
          </div>
          <div className="reports-grid">
            <ChartCard title="Category spend" description="Expense totals by category for the selected range.">
              {overview.categorySpend.length === 0 ? (
                <EmptyState title="No expense data" description="Category spend appears once expense transactions exist in the selected range." />
              ) : (
                <div className="chart-list">
                  {overview.categorySpend.map((item) => (
                    <div key={item.categoryId} className="chart-row">
                      <div className="chart-row__label">
                        <span>{item.categoryName}</span>
                        <strong>{formatCurrency(item.amount)}</strong>
                      </div>
                      <ProgressBar value={(item.amount / (categorySpendMax || 1)) * 100} />
                    </div>
                  ))}
                </div>
              )}
            </ChartCard>
            <ChartCard title="Income vs expense trend" description="Weekly or monthly buckets depending on the selected range.">
              {overview.incomeExpenseTrend.length === 0 ? (
                <EmptyState title="No trend data" description="The trend summary appears once transactions fall within the selected range." />
              ) : (
                <div className="trend-list">
                  {overview.incomeExpenseTrend.map((point) => {
                    const bucketMax = Math.max(point.income, point.expense, 1);
                    return (
                      <div key={point.periodStartUtc} className="trend-row">
                        <div className="trend-row__label">
                          <strong>{point.label}</strong>
                          <span>{formatCurrency(point.income - point.expense)} net</span>
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
          </div>
          <ChartCard title="Account balance trend" description="Compact closing-balance checkpoints across the selected period.">
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
        </>
      ) : null}
    </div>
  );
}
