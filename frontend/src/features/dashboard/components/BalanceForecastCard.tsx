import { Link } from "react-router-dom";
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Line,
  ReferenceArea,
  ReferenceDot,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { ForecastDailyResponseDto, ForecastMonthSummaryDto } from "../../forecast/api/forecastApi";
import { EmptyState } from "../../../shared/components/EmptyState";
import { formatCurrency, formatDate } from "../../../shared/lib/format";

const riskBadgeTone: Record<string, "default" | "warning" | "danger"> = {
  Low: "default",
  Medium: "warning",
  High: "danger",
};

type BalanceForecastCardProps = {
  forecastMonth: ForecastMonthSummaryDto;
  forecastDaily: ForecastDailyResponseDto;
};

type ForecastChartPoint = {
  key: string;
  label: string;
  fullDate: string;
  projectedBalance: number;
  recurringNetChange: number;
  kind: "current" | "projection";
};

export function BalanceForecastCard({ forecastMonth, forecastDaily }: BalanceForecastCardProps) {
  const forecastPoints = forecastDaily.points ?? [];

  const chartData: ForecastChartPoint[] = forecastPoints.length === 0
    ? []
    : [
        {
          key: "current",
          label: "Now",
          fullDate: forecastPoints[0]?.dateUtc ?? new Date().toISOString(),
          projectedBalance: forecastMonth.currentBalance,
          recurringNetChange: 0,
          kind: "current",
        },
        ...forecastPoints.map((point) => ({
          key: point.dateUtc,
          label: new Date(point.dateUtc).getDate().toString(),
          fullDate: point.dateUtc,
          projectedBalance: point.projectedBalance,
          recurringNetChange: point.recurringNetChange,
          kind: "projection" as const,
        })),
      ];

  const fallbackPoint: ForecastChartPoint = {
    key: "fallback",
    label: "Now",
    fullDate: new Date().toISOString(),
    projectedBalance: forecastMonth.currentBalance,
    recurringNetChange: 0,
    kind: "current",
  };

  const lowestPoint = chartData.reduce(
    (lowest, point) => (point.projectedBalance < lowest.projectedBalance ? point : lowest),
    chartData[0] ?? fallbackPoint,
  );

  const endPoint = chartData[chartData.length - 1] ?? lowestPoint;
  const maxBalance = Math.max(...chartData.map((point) => point.projectedBalance), forecastMonth.currentBalance);
  const minBalance = Math.min(...chartData.map((point) => point.projectedBalance), forecastMonth.currentBalance);
  const padding = Math.max((maxBalance - minBalance) * 0.15, Math.abs(maxBalance) * 0.05, 1000);
  const domainMin = Math.min(minBalance - padding, forecastMonth.minimumProjectedBalance - padding, -1000);
  const domainMax = Math.max(maxBalance + padding, forecastMonth.currentBalance + padding);
  const cautionThreshold = Math.max(forecastMonth.currentBalance * 0.15, 1000);

  const explanation = forecastMonth.minimumProjectedBalance < 0
    ? `Your balance may drop below zero and reach ${formatCurrency(forecastMonth.minimumProjectedBalance)} on ${formatDate(lowestPoint.fullDate)}. Be cautious with spending.`
    : forecastMonth.minimumProjectedBalance <= cautionThreshold
      ? `Your balance may dip to ${formatCurrency(forecastMonth.minimumProjectedBalance)} on ${formatDate(lowestPoint.fullDate)}. Keep discretionary spending tight.`
      : "Your balance is expected to stay positive throughout the month.";

  const upcomingBillsText = forecastMonth.upcomingRecurring.totalExpectedExpense > 0
    ? `Upcoming bills: ${formatCurrency(forecastMonth.upcomingRecurring.totalExpectedExpense)} before month end`
    : "No major bills are scheduled before month end.";

  const topUpcomingBills = forecastMonth.upcomingRecurring.items
    .filter((item) => item.type === "Expense")
    .slice(0, 3);

  return (
    <section className="panel-card panel-card--large dashboard-flow__item forecast-card">
      <div className="panel-card__header panel-card__header--inline forecast-card__header">
        <div>
          <h3>Balance forecast</h3>
          <p>Projected balance through month end.</p>
        </div>
        <span className={`status-badge status-badge--${riskBadgeTone[forecastMonth.riskLevel]} forecast-risk-badge`}>
          {forecastMonth.riskLevel} risk
        </span>
      </div>

      <div className="forecast-summary-strip forecast-summary-strip--four">
        <div>
          <span>Current balance</span>
          <strong>{formatCurrency(forecastMonth.currentBalance)}</strong>
        </div>
        <div>
          <span>Projected end balance</span>
          <strong>{formatCurrency(forecastMonth.projectedEndOfMonthBalance)}</strong>
        </div>
        <div>
          <span>Lowest balance</span>
          <strong>{formatCurrency(forecastMonth.minimumProjectedBalance)}</strong>
        </div>
        <div>
          <span>Safe to spend</span>
          <strong>{formatCurrency(forecastMonth.safeToSpend)}</strong>
        </div>
      </div>

      {chartData.length === 0 ? (
        <EmptyState
          title="No forecast yet"
          description="Forecast projections appear once at least one active account is available."
          action={<Link to="/accounts" className="ghost-button">Add account</Link>}
        />
      ) : (
        <>
          <div className="forecast-explainer">
            <strong>{explanation}</strong>
          </div>

          <div className="forecast-chart-block">
            <div className="forecast-line-chart-wrap" aria-label="Projected daily balance through month end">
              <ResponsiveContainer width="100%" height={220}>
              <ComposedChart data={chartData} margin={{ top: 12, right: 12, bottom: 8, left: 4 }}>
                <CartesianGrid stroke="rgba(231, 221, 212, 0.75)" vertical={false} />
                <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6E625B", fontSize: 12 }} />
                <YAxis
                  tickLine={false}
                  axisLine={false}
                  tick={{ fill: "#6E625B", fontSize: 12 }}
                  width={96}
                  domain={[domainMin, domainMax]}
                  tickFormatter={(value: number) => formatCurrency(value)}
                />
                <Tooltip
                  cursor={{ stroke: "rgba(75, 46, 43, 0.15)", strokeWidth: 1 }}
                  content={({ active, payload }) => {
                    if (!active || !payload || payload.length === 0) {
                      return null;
                    }

                    const point = payload[0]?.payload as ForecastChartPoint | undefined;
                    if (!point) {
                      return null;
                    }

                    return (
                      <div className="forecast-tooltip">
                        <strong>{point.kind === "current" ? "Current balance" : formatDate(point.fullDate)}</strong>
                        <p>{formatCurrency(point.projectedBalance)}</p>
                        {point.kind === "projection" ? (
                          <small>
                            {point.recurringNetChange === 0
                              ? "No scheduled recurring change"
                              : point.recurringNetChange < 0
                                ? `Recurring outflow ${formatCurrency(Math.abs(point.recurringNetChange))}`
                                : `Recurring inflow ${formatCurrency(point.recurringNetChange)}`}
                          </small>
                        ) : null}
                      </div>
                    );
                  }}
                />
                <ReferenceArea y1={0} y2={cautionThreshold} fill="rgba(192, 133, 82, 0.08)" />
                <ReferenceArea y1={domainMin} y2={0} fill="rgba(170, 78, 63, 0.08)" />
                <Area type="monotone" dataKey="projectedBalance" stroke="none" fill="rgba(192, 133, 82, 0.10)" />
                <Line type="monotone" dataKey="projectedBalance" stroke="#8C5A3C" strokeWidth={2} dot={false} activeDot={{ r: 4, fill: "#8C5A3C" }} />
                <ReferenceDot x={chartData[0]?.label} y={forecastMonth.currentBalance} r={4} fill="#4B2E2B" stroke="#FFF8F0" strokeWidth={2} />
                <ReferenceDot x={endPoint.label} y={endPoint.projectedBalance} r={4} fill="#8C5A3C" stroke="#FFF8F0" strokeWidth={2} />
                <ReferenceDot x={lowestPoint.label} y={lowestPoint.projectedBalance} r={5} fill="#C85C46" stroke="#FFF8F0" strokeWidth={2} />
              </ComposedChart>
            </ResponsiveContainer>
            </div>

            <div className="forecast-chart-legend">
              <span><i className="forecast-chart-legend__dot forecast-chart-legend__dot--current" />Current</span>
              <span><i className="forecast-chart-legend__dot forecast-chart-legend__dot--end" />Month end</span>
              <span><i className="forecast-chart-legend__dot forecast-chart-legend__dot--lowest" />Lowest point</span>
            </div>
          </div>

          <div className="forecast-upcoming-bills">
            <strong>{upcomingBillsText}</strong>
            {topUpcomingBills.length > 0 ? (
              <div className="forecast-upcoming-bills__list">
                {topUpcomingBills.map((item) => (
                  <span key={`${item.title}-${item.scheduledDateUtc}-${item.accountName}`}>
                    {item.title} {formatCurrency(item.amount)} on {formatDate(item.scheduledDateUtc)}
                  </span>
                ))}
              </div>
            ) : null}
          </div>
        </>
      )}
    </section>
  );
}
