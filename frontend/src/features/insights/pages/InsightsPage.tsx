import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import {
  reportsApi,
  type CategoryTrendSeriesDto,
  type IncomeExpenseTrendPointDto,
  type NetWorthReportDto,
  type NetWorthTrendPointDto,
  type ReportsTrendResponseDto,
  type SavingsRateTrendPointDto,
} from "../../reports/api/reportsApi";
import { insightsApi, type InsightItemDto, type InsightsResponseDto } from "../api/insightsApi";
import { Alert } from "../../../shared/components/Alert";
import { ChartCard } from "../../../shared/components/ChartCard";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { FilterRow } from "../../../shared/components/FilterRow";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, toDateInputValue } from "../../../shared/lib/format";
import { filterAccountsForView, getScopedAccountIdsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

const insightsSchema = z.object({
  startDateUtc: z.string().min(1, "Start date is required."),
  endDateUtc: z.string().min(1, "End date is required."),
  accountId: z.string().optional(),
  categoryId: z.string().optional(),
  bucket: z.enum(["Auto", "Week", "Month"]),
});

type InsightsFormValues = z.infer<typeof insightsSchema>;

type CashflowChartPoint = {
  label: string;
  income: number;
  expense: number;
  net: number;
};

type SavingsRateChartPoint = {
  label: string;
  savingsRatePercent: number;
  hasIncomeData: boolean;
};

type NetWorthChartPoint = {
  label: string;
  netWorth: number;
  assetBalance: number;
  liabilityBalance: number;
};

type CategoryHeatmapPoint = {
  label: string;
  amount: number;
  categoryName: string;
  color: string;
};

const CATEGORY_COLORS = ["#8C5A3C", "#C08552", "#4B2E2B", "#A96F48", "#6E625B"];

function formatPercent(value: number | null) {
  return value === null ? "No income" : `${value.toFixed(1)}%`;
}

function getInsightTone(level: InsightItemDto["level"]): "warning" | "default" | "info" {
  if (level === "Attention") {
    return "warning";
  }

  if (level === "Positive") {
    return "default";
  }

  return "info";
}

function buildTopCategorySeries(trends: ReportsTrendResponseDto | null) {
  if (!trends) {
    return [] as CategoryTrendSeriesDto[];
  }

  return [...trends.categoryTrends]
    .sort((left, right) => right.totalAmount - left.totalAmount)
    .slice(0, 4);
}

function buildCategoryHeatmapData(seriesList: CategoryTrendSeriesDto[]) {
  return seriesList.flatMap((series, seriesIndex) =>
    series.points.map((point) => ({
      label: point.label,
      amount: point.amount,
      categoryName: series.categoryName,
      color: CATEGORY_COLORS[seriesIndex % CATEGORY_COLORS.length],
    })),
  );
}

export function InsightsPage() {
  const { accessToken } = useAuth();
  const { sharedAccessView } = useWorkspaceScope();
  const now = new Date();
  const oneHundredEightyDaysAgo = new Date();
  oneHundredEightyDaysAgo.setDate(now.getDate() - 179);

  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [insights, setInsights] = useState<InsightsResponseDto | null>(null);
  const [trends, setTrends] = useState<ReportsTrendResponseDto | null>(null);
  const [netWorth, setNetWorth] = useState<NetWorthReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");
  const [filtersReady, setFiltersReady] = useState(false);

  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } = useForm<InsightsFormValues>({
    resolver: zodResolver(insightsSchema),
    defaultValues: {
      startDateUtc: toDateInputValue(oneHundredEightyDaysAgo.toISOString()),
      endDateUtc: toDateInputValue(now.toISOString()),
      accountId: "",
      categoryId: "",
      bucket: "Auto",
    },
  });

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => { if (filtersReady) { void loadInsights(); } }, [accessToken, sharedAccessView, filtersReady]);

  async function bootstrap() {
    if (!accessToken) return;
    setFiltersReady(false);
    try {
      const [accountResponse, categoryResponse] = await Promise.all([
        accountsApi.list(accessToken),
        categoriesApi.list(accessToken, false),
      ]);
      setAccounts(accountResponse);
      setCategories(categoryResponse.filter((category) => category.type === "Expense"));
      setErrorVariant("error");
      setErrorMessage(null);
      setFiltersReady(true);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Insights filters are refreshing too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load insights filters.");
      }
    }
  }

  const activeAccounts = useMemo(() => accounts.filter((account) => !account.isArchived), [accounts]);
  const visibleAccounts = useMemo(() => filterAccountsForView(activeAccounts, sharedAccessView), [activeAccounts, sharedAccessView]);
  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(activeAccounts), [activeAccounts]);
  const isScopeEmpty = sharedAccessView !== "all" && activeAccounts.length > 0 && visibleAccounts.length === 0;
  const selectedAccountId = watch("accountId") || "";

  useEffect(() => {
    if (selectedAccountId && !visibleAccounts.some((account) => account.id === selectedAccountId)) {
      setValue("accountId", "");
    }
  }, [selectedAccountId, setValue, visibleAccounts]);

  function buildQuery(values?: InsightsFormValues) {
    const activeValues = values ?? watch();
    const scopedAccountIds = !activeValues.accountId && sharedAccessView !== "all" ? getScopedAccountIdsForView(activeAccounts, sharedAccessView) : [];
    return {
      startDateUtc: new Date(activeValues.startDateUtc).toISOString(),
      endDateUtc: new Date(activeValues.endDateUtc).toISOString(),
      bucket: activeValues.bucket,
      ...(activeValues.accountId ? { accountId: activeValues.accountId } : {}),
      ...(scopedAccountIds.length > 0 ? { accountIds: scopedAccountIds } : {}),
      ...(activeValues.categoryId ? { categoryId: activeValues.categoryId } : {}),
    };
  }

  async function loadInsights(values?: InsightsFormValues) {
    if (!accessToken) return;
    setLoading(true);
    try {
      const activeValues = values ?? watch();
      if (!filtersReady) {
        return;
      }

      if (sharedAccessView !== "all" && !activeValues.accountId && visibleAccounts.length === 0) {
        setInsights(null);
        setTrends(null);
        setNetWorth(null);
        setErrorVariant("error");
        setErrorMessage(null);
        return;
      }

      const query = buildQuery(activeValues);
      const [insightsResponse, trendResponse, netWorthResponse] = await Promise.all([
        insightsApi.list(accessToken, query),
        reportsApi.trends(accessToken, query),
        reportsApi.netWorth(accessToken, query),
      ]);
      setInsights(insightsResponse);
      setTrends(trendResponse);
      setNetWorth(netWorthResponse);
      setErrorVariant("error");
      setErrorMessage(null);
      setFiltersReady(true);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Insights are being refreshed too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load advanced insights.");
      }
    } finally {
      setLoading(false);
    }
  }

  const scopeMessage = useMemo(() => {
    if (sharedAccessView === "shared" && visibleAccounts.length > 0) {
      return `This analysis is focused on ${visibleAccounts.length} shared account${visibleAccounts.length === 1 ? "" : "s"} available to you.`;
    }

    if (sharedAccessView === "mine" && showSharedViewToggle) {
      return "This analysis is focused on your own accounts only. Shared-account balances and trends are excluded from this view.";
    }

    if (sharedAccessView === "all" && activeAccounts.some((account) => account.isShared)) {
      return "This analysis may include both your own accounts and any shared accounts currently visible to you.";
    }

    return null;
  }, [activeAccounts, sharedAccessView, showSharedViewToggle, visibleAccounts]);

  const validSavingsRatePoints = trends?.savingsRateTrend.filter((point) => point.savingsRatePercent !== null) ?? [];
  const averageSavingsRate = validSavingsRatePoints.length > 0
    ? validSavingsRatePoints.reduce((sum, point) => sum + (point.savingsRatePercent ?? 0), 0) / validSavingsRatePoints.length
    : null;
  const selectedCategoryName = categories.find((category) => category.id === (watch("categoryId") || ""))?.name ?? null;

  const cashflowChartData: CashflowChartPoint[] = useMemo(() => (
    trends?.incomeExpenseTrend.map((point: IncomeExpenseTrendPointDto) => ({
      label: point.label,
      income: point.income,
      expense: point.expense,
      net: point.income - point.expense,
    })) ?? []
  ), [trends]);

  const savingsRateChartData: SavingsRateChartPoint[] = useMemo(() => (
    trends?.savingsRateTrend.map((point: SavingsRateTrendPointDto) => ({
      label: point.label,
      savingsRatePercent: point.savingsRatePercent ?? 0,
      hasIncomeData: point.hasIncomeData,
    })) ?? []
  ), [trends]);

  const netWorthChartData: NetWorthChartPoint[] = useMemo(() => (
    netWorth?.points.map((point: NetWorthTrendPointDto) => ({
      label: point.label,
      netWorth: point.netWorth,
      assetBalance: point.assetBalance,
      liabilityBalance: point.liabilityBalance,
    })) ?? []
  ), [netWorth]);

  const categorySeries = useMemo(() => buildTopCategorySeries(trends), [trends]);
  const categoryHeatmapData = useMemo(() => buildCategoryHeatmapData(categorySeries), [categorySeries]);

  const strongestInsight = insights?.items[0] ?? null;
  const attentionCount = insights?.items.filter((item) => item.level === "Attention").length ?? 0;
  const positiveCount = insights?.items.filter((item) => item.level === "Positive").length ?? 0;

  if (loading && !insights && !trends && !netWorth) {
    return <PageLoader label="Loading advanced insights" />;
  }

  return (
    <div className="page-stack insights-page">
      <SectionHeader
        title="Insights"
        description="Advanced trends, net worth movement, and explainable signals drawn from your recorded financial history."
        action={<Link to="/insights/health-score" className="ghost-button">Health score</Link>}
      />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {scopeMessage ? <Alert message={scopeMessage} variant="info" /> : null}
      {isScopeEmpty ? (
        <section className="panel-card">
          <EmptyState
            title="No accounts in this view"
            description={sharedAccessView === "mine" ? "Mine is focused on accounts you own. There are no owned accounts available for advanced insights." : "Shared with me is focused on guest access. There are no shared guest accounts available for advanced insights."}
          />
        </section>
      ) : null}
      <form onSubmit={handleSubmit(loadInsights)} noValidate>
        <FilterRow action={<button type="submit" className="primary-button" disabled={isSubmitting || isScopeEmpty}>{isSubmitting ? "Loading..." : "Apply filters"}</button>}>
          <Field label="Start date" error={errors.startDateUtc?.message}><input type="date" {...register("startDateUtc")} /></Field>
          <Field label="End date" error={errors.endDateUtc?.message}><input type="date" {...register("endDateUtc")} /></Field>
          <Field label="Account" error={errors.accountId?.message}>
            <SelectField {...register("accountId")}>
              <option value="">All accounts in this view</option>
              {visibleAccounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
            </SelectField>
          </Field>
          <Field label="Category" error={errors.categoryId?.message}>
            <SelectField {...register("categoryId")}>
              <option value="">Top categories in this view</option>
              {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
            </SelectField>
          </Field>
          <Field label="Bucket" error={errors.bucket?.message}>
            <SelectField {...register("bucket")}>
              <option value="Auto">Auto</option>
              <option value="Week">Weekly</option>
              <option value="Month">Monthly</option>
            </SelectField>
          </Field>
        </FilterRow>
      </form>
      {insights && trends && netWorth ? (
        <>
          <section className="panel-card insights-hero-card">
            <div className="insights-hero-card__copy">
              <span className="eyebrow">Advanced analysis</span>
              <h3>{strongestInsight?.title ?? "Financial movement at a glance"}</h3>
              <p>{strongestInsight?.message ?? insights.summary}</p>
              <small>{strongestInsight?.basis ?? trends.basisDescription}</small>
            </div>
            <div className="insights-hero-card__stats">
              <div className="insights-mini-kpi">
                <span>Insights found</span>
                <strong>{insights.items.length}</strong>
                <small>{attentionCount} attention, {positiveCount} positive</small>
              </div>
              <div className="insights-mini-kpi">
                <span>Range</span>
                <strong>{cashflowChartData.length} buckets</strong>
                <small>{trends.basisDescription}</small>
              </div>
            </div>
          </section>

          <div className="stats-grid stats-grid--four insights-stat-grid">
            <StatCard label="Current net worth" value={formatCurrency(netWorth.currentNetWorth)} hint={netWorth.basisDescription} tone={netWorth.currentNetWorth < 0 ? "negative" : "positive"} />
            <StatCard label="Net worth change" value={formatCurrency(netWorth.changeAmount)} hint={`${netWorth.includedAccountCount} accounts included`} tone={netWorth.changeAmount < 0 ? "negative" : "positive"} />
            <StatCard label="Average savings rate" value={averageSavingsRate === null ? "No income data" : `${averageSavingsRate.toFixed(1)}%`} hint="Transfer-free savings trend across the selected range." tone={averageSavingsRate !== null && averageSavingsRate < 0 ? "negative" : averageSavingsRate !== null && averageSavingsRate >= 15 ? "positive" : undefined} />
            <StatCard label="Signals to review" value={`${attentionCount}`} hint={attentionCount === 0 ? "No active warning signals in this range." : `${positiveCount} positive changes offset part of the risk picture.`} tone={attentionCount > 0 ? "negative" : "positive"} />
          </div>

          <section className="panel-card insights-findings-card">
            <div className="panel-card__header panel-card__header--inline">
              <div>
                <h3>Key findings</h3>
                <p>{insights.summary}</p>
              </div>
              <span className="status-badge status-badge--info">Comparison driven</span>
            </div>
            {insights.items.length === 0 ? (
              <EmptyState title="No notable changes" description="Insights appear when there is enough recorded data to compare one period with the previous one." />
            ) : (
              <div className="insights-findings-grid">
                {insights.items.map((item) => (
                  <article key={item.key} className="insights-finding-tile">
                    <div className="insights-finding-tile__top">
                      <strong>{item.title}</strong>
                      <span className={`status-badge status-badge--${getInsightTone(item.level)}`}>{item.level}</span>
                    </div>
                    <p>{item.message}</p>
                    <small>{item.basis}</small>
                  </article>
                ))}
              </div>
            )}
          </section>

          <div className="reports-grid insights-main-grid">
            <ChartCard title="Income, expense, and net flow" description="Compare inflows and outflows over time without transfer distortion.">
              {cashflowChartData.length === 0 ? (
                <EmptyState title="No trend data" description="Income and expense trend appears once transactions exist in the selected range." />
              ) : (
                <>
                  <div className="insights-chart-shell insights-chart-shell--tall">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={cashflowChartData} margin={{ top: 8, right: 12, left: 6, bottom: 0 }}>
                        <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                        <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                        <YAxis tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} tickFormatter={(value: number) => formatCurrency(value)} width={88} />
                        <Tooltip
                          content={({ active, payload, label }) => {
                            if (!active || !payload || payload.length === 0) {
                              return null;
                            }

                            const point = payload[0]?.payload as CashflowChartPoint | undefined;
                            if (!point) {
                              return null;
                            }

                            return (
                              <div className="forecast-tooltip">
                                <strong>{label}</strong>
                                <small>Income {formatCurrency(point.income)}</small>
                                <small>Expense {formatCurrency(point.expense)}</small>
                                <p>Net {formatCurrency(point.net)}</p>
                              </div>
                            );
                          }}
                        />
                        <Legend />
                        <Bar dataKey="income" name="Income" radius={[8, 8, 0, 0]} fill="#0B9DA4" />
                        <Bar dataKey="expense" name="Expense" radius={[8, 8, 0, 0]} fill="#D26B6B" />
                        <Line type="monotone" dataKey="net" name="Net" stroke="#4B2E2B" strokeWidth={2} dot={{ r: 3 }} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="insights-inline-summary">
                    <div><span>Period income</span><strong>{formatCurrency(trends.incomeExpenseTrend.reduce((sum, point) => sum + point.income, 0))}</strong></div>
                    <div><span>Period expense</span><strong>{formatCurrency(trends.incomeExpenseTrend.reduce((sum, point) => sum + point.expense, 0))}</strong></div>
                    <div><span>Net result</span><strong>{formatCurrency(trends.incomeExpenseTrend.reduce((sum, point) => sum + point.income - point.expense, 0))}</strong></div>
                  </div>
                </>
              )}
            </ChartCard>

            <ChartCard title="Savings rate trend" description="See whether savings are improving or tightening over the selected period.">
              {savingsRateChartData.length === 0 ? (
                <EmptyState title="No savings-rate data" description="Savings-rate trend appears once income and expense data exists in the selected range." />
              ) : (
                <>
                  <div className="insights-chart-shell insights-chart-shell--medium">
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart data={savingsRateChartData} margin={{ top: 8, right: 12, left: 6, bottom: 0 }}>
                        <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                        <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                        <YAxis tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} tickFormatter={(value: number) => `${value}%`} width={66} />
                        <ReferenceLine y={0} stroke="rgba(75, 46, 43, 0.2)" strokeDasharray="4 4" />
                        <Tooltip
                          content={({ active, payload, label }) => {
                            if (!active || !payload || payload.length === 0) {
                              return null;
                            }

                            const point = payload[0]?.payload as SavingsRateChartPoint | undefined;
                            if (!point) {
                              return null;
                            }

                            return (
                              <div className="forecast-tooltip">
                                <strong>{label}</strong>
                                <p>{point.hasIncomeData ? `${point.savingsRatePercent.toFixed(1)}% savings rate` : "No income in this bucket"}</p>
                              </div>
                            );
                          }}
                        />
                        <Line type="monotone" dataKey="savingsRatePercent" stroke="#8C5A3C" strokeWidth={2.4} dot={{ r: 3 }} activeDot={{ r: 4 }} />
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="insights-inline-summary insights-inline-summary--compact">
                    {validSavingsRatePoints.slice(-3).map((point) => (
                      <div key={point.periodStartUtc}><span>{point.label}</span><strong>{formatPercent(point.savingsRatePercent)}</strong></div>
                    ))}
                  </div>
                </>
              )}
            </ChartCard>
          </div>

          <div className="reports-grid insights-main-grid">
            <ChartCard title="Net worth tracking" description="Assets and liabilities feed the same net worth line so the direction is obvious at a glance.">
              {netWorthChartData.length === 0 ? (
                <EmptyState title="No net worth history" description="Net worth points appear once an account exists in the selected range." />
              ) : (
                <>
                  <div className="insights-chart-shell insights-chart-shell--medium">
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart data={netWorthChartData} margin={{ top: 8, right: 12, left: 6, bottom: 0 }}>
                        <defs>
                          <linearGradient id="netWorthFill" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#C08552" stopOpacity={0.28} />
                            <stop offset="95%" stopColor="#C08552" stopOpacity={0.03} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                        <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                        <YAxis tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} tickFormatter={(value: number) => formatCurrency(value)} width={90} />
                        <Tooltip
                          content={({ active, payload, label }) => {
                            if (!active || !payload || payload.length === 0) {
                              return null;
                            }

                            const point = payload[0]?.payload as NetWorthChartPoint | undefined;
                            if (!point) {
                              return null;
                            }

                            return (
                              <div className="forecast-tooltip">
                                <strong>{label}</strong>
                                <p>Net worth {formatCurrency(point.netWorth)}</p>
                                <small>Assets {formatCurrency(point.assetBalance)}</small>
                                <small>Liabilities {formatCurrency(point.liabilityBalance)}</small>
                              </div>
                            );
                          }}
                        />
                        <Area type="monotone" dataKey="netWorth" stroke="#8C5A3C" strokeWidth={2.4} fill="url(#netWorthFill)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="insights-inline-summary">
                    <div><span>Starting point</span><strong>{formatCurrency(netWorth.startingNetWorth)}</strong></div>
                    <div><span>Current point</span><strong>{formatCurrency(netWorth.currentNetWorth)}</strong></div>
                    <div><span>Change</span><strong>{formatCurrency(netWorth.changeAmount)}</strong></div>
                  </div>
                </>
              )}
            </ChartCard>

            <ChartCard title="Category movement" description={selectedCategoryName ? `Focused on ${selectedCategoryName} across the selected range.` : "Top spending categories over time, so shifts are easier to spot."}>
              {categorySeries.length === 0 ? (
                <EmptyState title="No category trends" description="Category trend lines appear once expense activity exists in the selected range." />
              ) : (
                <>
                  <div className="insights-chart-shell insights-chart-shell--medium">
                    <ResponsiveContainer width="100%" height="100%">
                      <LineChart margin={{ top: 8, right: 12, left: 6, bottom: 0 }}>
                        <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                        <XAxis dataKey="label" type="category" allowDuplicatedCategory={false} tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                        <YAxis tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} tickFormatter={(value: number) => formatCurrency(value)} width={84} />
                        <Tooltip
                          content={({ active, payload, label }) => {
                            if (!active || !payload || payload.length === 0) {
                              return null;
                            }

                            return (
                              <div className="forecast-tooltip">
                                <strong>{label}</strong>
                                {payload.map((item) => (
                                  <small key={String(item.dataKey)}>{item.name}: {formatCurrency(Number(item.value ?? 0))}</small>
                                ))}
                              </div>
                            );
                          }}
                        />
                        <Legend />
                        {categorySeries.map((series, index) => (
                          <Line
                            key={series.categoryId}
                            data={series.points}
                            dataKey="amount"
                            name={series.categoryName}
                            stroke={CATEGORY_COLORS[index % CATEGORY_COLORS.length]}
                            strokeWidth={2.2}
                            dot={{ r: 2.5 }}
                            activeDot={{ r: 4 }}
                          />
                        ))}
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                  <div className="insights-category-strip">
                    {categorySeries.map((series, index) => (
                      <div key={series.categoryId} className="insights-category-chip">
                        <i style={{ backgroundColor: CATEGORY_COLORS[index % CATEGORY_COLORS.length] }} />
                        <span>{series.categoryName}</span>
                        <strong>{formatCurrency(series.totalAmount)}</strong>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </ChartCard>
          </div>

          <ChartCard title="Category intensity map" description="A compact view of where category pressure built up across the selected buckets.">
            {categoryHeatmapData.length === 0 ? (
              <EmptyState title="No category intensity yet" description="Heatmap cells appear once category trend buckets are available." />
            ) : (
              <div className="insights-chart-shell insights-chart-shell--short">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={categoryHeatmapData} margin={{ top: 8, right: 12, left: 6, bottom: 0 }}>
                    <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                    <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                    <YAxis tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} tickFormatter={(value: number) => formatCurrency(value)} width={84} />
                    <Tooltip
                      content={({ active, payload, label }) => {
                        if (!active || !payload || payload.length === 0) {
                          return null;
                        }

                        const point = payload[0]?.payload as CategoryHeatmapPoint | undefined;
                        if (!point) {
                          return null;
                        }

                        return (
                          <div className="forecast-tooltip">
                            <strong>{point.categoryName}</strong>
                            <small>{label}</small>
                            <p>{formatCurrency(point.amount)}</p>
                          </div>
                        );
                      }}
                    />
                    <Bar dataKey="amount" radius={[7, 7, 0, 0]}>
                      {categoryHeatmapData.map((entry, index) => (
                        <Cell key={`${entry.categoryName}-${entry.label}-${index}`} fill={entry.color} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            )}
          </ChartCard>
        </>
      ) : null}
    </div>
  );
}
