import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import { recurringTransactionsApi, type RecurringExecutionSummaryDto, type RecurringTransactionDto } from "../api/recurringTransactionsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate, toDateInputValue } from "../../../shared/lib/format";

const recurringSchema = z.object({
  title: z.string().trim().min(1, "Title is required.").max(120),
  type: z.enum(["1", "2", "3"]),
  amount: z.coerce.number().positive("Amount must be greater than zero."),
  categoryId: z.string().optional().or(z.literal("")),
  accountId: z.string().min(1, "Account is required."),
  transferAccountId: z.string().optional().or(z.literal("")),
  frequency: z.enum(["1", "2", "3", "4"]),
  startDateUtc: z.string().min(1, "Start date is required."),
  endDateUtc: z.string().optional().or(z.literal("")),
  autoCreateTransaction: z.boolean(),
});

type RecurringFormValues = z.infer<typeof recurringSchema>;

const frequencyLabels: Record<string, string> = { "1": "Daily", "2": "Weekly", "3": "Monthly", "4": "Yearly" };
const typeLabels: Record<string, string> = { "1": "Income", "2": "Expense", "3": "Transfer" };
const typeToValue: Record<RecurringTransactionDto["type"], RecurringFormValues["type"]> = { Income: "1", Expense: "2", Transfer: "3" };
const frequencyToValue: Record<RecurringTransactionDto["frequency"], RecurringFormValues["frequency"]> = { Daily: "1", Weekly: "2", Monthly: "3", Yearly: "4" };

export function RecurringTransactionsPage() {
  const { accessToken } = useAuth();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [rules, setRules] = useState<RecurringTransactionDto[]>([]);
  const [processingSummary, setProcessingSummary] = useState<RecurringExecutionSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<RecurringTransactionDto | null>(null);

  const { register, handleSubmit, reset, control, formState: { errors, isSubmitting } } = useForm<RecurringFormValues>({
    resolver: zodResolver(recurringSchema),
    defaultValues: {
      title: "",
      type: "2",
      amount: 0,
      categoryId: "",
      accountId: "",
      transferAccountId: "",
      frequency: "3",
      startDateUtc: new Date().toISOString().slice(0, 10),
      endDateUtc: "",
      autoCreateTransaction: true,
    },
  });

  const selectedType = useWatch({ control, name: "type" });

  useEffect(() => { void bootstrap(); }, [accessToken]);

  async function bootstrap() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [accountsResponse, categoriesResponse, rulesResponse] = await Promise.all([
        accountsApi.list(accessToken),
        categoriesApi.list(accessToken),
        recurringTransactionsApi.list(accessToken),
      ]);
      const liveAccounts = accountsResponse.filter((item) => !item.isArchived);
      setAccounts(liveAccounts);
      setCategories(categoriesResponse.filter((item) => !item.isArchived));
      setRules(rulesResponse);
      reset((current) => ({ ...current, accountId: current.accountId || liveAccounts[0]?.id || "" }));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load recurring transactions.");
    } finally {
      setLoading(false);
    }
  }

  function resetForm() {
    setEditing(null);
    reset({
      title: "",
      type: "2",
      amount: 0,
      categoryId: "",
      accountId: accounts[0]?.id ?? "",
      transferAccountId: "",
      frequency: "3",
      startDateUtc: new Date().toISOString().slice(0, 10),
      endDateUtc: "",
      autoCreateTransaction: true,
    });
  }

  function editRule(rule: RecurringTransactionDto) {
    setEditing(rule);
    reset({
      title: rule.title,
      type: typeToValue[rule.type],
      amount: rule.amount,
      categoryId: rule.categoryId ?? "",
      accountId: rule.accountId,
      transferAccountId: rule.transferAccountId ?? "",
      frequency: frequencyToValue[rule.frequency],
      startDateUtc: toDateInputValue(rule.startDateUtc),
      endDateUtc: toDateInputValue(rule.endDateUtc ?? undefined),
      autoCreateTransaction: rule.autoCreateTransaction,
    });
  }

  async function onSubmit(values: RecurringFormValues) {
    if (!accessToken) return;

    const payload = {
      title: values.title,
      type: Number(values.type),
      amount: values.amount,
      categoryId: values.type === "3" ? null : values.categoryId || null,
      accountId: values.accountId,
      transferAccountId: values.type === "3" ? values.transferAccountId || null : null,
      frequency: Number(values.frequency),
      startDateUtc: new Date(`${values.startDateUtc}T00:00:00.000Z`).toISOString(),
      endDateUtc: values.endDateUtc ? new Date(`${values.endDateUtc}T00:00:00.000Z`).toISOString() : null,
      autoCreateTransaction: values.autoCreateTransaction,
    };

    try {
      if (editing) {
        await recurringTransactionsApi.update(accessToken, editing.id, payload);
      } else {
        await recurringTransactionsApi.create(accessToken, payload);
      }
      resetForm();
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save recurring rule.");
    }
  }

  async function toggleRule(rule: RecurringTransactionDto) {
    if (!accessToken) return;
    try {
      if (rule.status === "Paused") {
        await recurringTransactionsApi.resume(accessToken, rule.id);
      } else {
        await recurringTransactionsApi.pause(accessToken, rule.id);
      }
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to update recurring rule status.");
    }
  }

  async function deleteRule(ruleId: string) {
    if (!accessToken || !window.confirm("Delete this recurring rule? Historical transactions already generated will remain.")) return;
    try {
      await recurringTransactionsApi.remove(accessToken, ruleId);
      if (editing?.id === ruleId) {
        resetForm();
      }
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to delete recurring rule.");
    }
  }

  async function processDueRules() {
    if (!accessToken) return;
    try {
      const summary = await recurringTransactionsApi.processDue(accessToken);
      setProcessingSummary(summary);
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to process due recurring rules.");
    }
  }

  const filteredCategories = useMemo(() => categories.filter((item) => selectedType === "1" ? item.type === "Income" : item.type === "Expense"), [categories, selectedType]);
  const activeCount = useMemo(() => rules.filter((rule) => rule.status === "Active").length, [rules]);

  if (loading && rules.length === 0 && accounts.length === 0) {
    return <PageLoader label="Loading recurring transactions" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader title="Recurring transactions" description="Define reusable rules, pause and resume schedules, and manually process due occurrences until background jobs are added." action={<Button type="button" onClick={processDueRules}>Process due now</Button>} />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {processingSummary ? <Alert message={`Processed ${processingSummary.occurrencesProcessed} occurrences and created ${processingSummary.transactionsCreated} transactions.`} /> : null}
      <div className="stats-grid stats-grid--four">
        <StatCard label="Rules" value={String(rules.length)} hint={`${activeCount} active schedules.`} />
        <StatCard label="Auto-create" value={String(rules.filter((rule) => rule.autoCreateTransaction).length)} hint="Rules that will generate transactions when due." />
        <StatCard label="Paused" value={String(rules.filter((rule) => rule.status === "Paused").length)} hint="Paused rules keep their history and can be resumed." />
        <StatCard label="Due today" value={String(rules.filter((rule) => rule.nextRunDateUtc && new Date(rule.nextRunDateUtc) <= new Date()).length)} hint="Rules whose next run date is now due or past due." />
      </div>
      <div className="budget-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit recurring rule" : "Create recurring rule"}</h3>
            <p>Generated transactions obey the same account, category, and balance rules as manual entries.</p>
          </div>
          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <Field label="Title" error={errors.title?.message}><input {...register("title")} placeholder="Monthly rent" /></Field>
            <div className="field-grid">
              <Field label="Type" error={errors.type?.message}>
                <SelectField {...register("type")}>
                  {Object.entries(typeLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                </SelectField>
              </Field>
              <Field label="Frequency" error={errors.frequency?.message}>
                <SelectField {...register("frequency")}>
                  {Object.entries(frequencyLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                </SelectField>
              </Field>
            </div>
            <div className="field-grid">
              <Field label="Amount" error={errors.amount?.message}><input {...register("amount")} type="number" step="0.01" /></Field>
              <Field label="Account" error={errors.accountId?.message}>
                <SelectField {...register("accountId")}>
                  <option value="">Select account</option>
                  {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                </SelectField>
              </Field>
            </div>
            {selectedType === "3" ? (
              <Field label="Destination account" error={errors.transferAccountId?.message}>
                <SelectField {...register("transferAccountId")}>
                  <option value="">Select destination account</option>
                  {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                </SelectField>
              </Field>
            ) : (
              <Field label="Category" error={errors.categoryId?.message}>
                <SelectField {...register("categoryId")}>
                  <option value="">Select category</option>
                  {filteredCategories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
                </SelectField>
              </Field>
            )}
            <div className="field-grid">
              <Field label="Start date" error={errors.startDateUtc?.message}><input {...register("startDateUtc")} type="date" /></Field>
              <Field label="End date" error={errors.endDateUtc?.message}><input {...register("endDateUtc")} type="date" /></Field>
            </div>
            <label className="checkbox-row">
              <input type="checkbox" {...register("autoCreateTransaction")} />
              <span>Automatically create transactions when the rule is due.</span>
            </label>
            <Button type="submit" loading={isSubmitting}>{editing ? "Save rule" : "Create rule"}</Button>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Recurring rules</h3>
            <p>Due dates, status, and last execution are shown for each schedule.</p>
          </div>
          {rules.length === 0 ? (
            <EmptyState title="No recurring rules yet" description="Create a schedule for repeated income, expenses, or transfers to keep future cash flow structured." />
          ) : (
            <div className="budget-list">
              {rules.map((rule) => (
                <article key={rule.id} className="budget-card">
                  <div className="budget-card__header">
                    <div>
                      <strong>{rule.title}</strong>
                      <p>{rule.accountName}{rule.transferAccountName ? ` -> ${rule.transferAccountName}` : ""} • {rule.categoryName ?? rule.type}</p>
                    </div>
                    <div className="budget-card__aside">
                      <span className={`status-badge status-badge--${rule.status === "Active" ? "default" : rule.status === "Paused" ? "warning" : "danger"}`}>{rule.status}</span>
                      <small>{rule.autoCreateTransaction ? "Auto-create on" : "Manual processing only"}</small>
                    </div>
                  </div>
                  <div className="budget-card__metrics">
                    <span>{formatCurrency(rule.amount)}</span>
                    <span>{rule.frequency}</span>
                    <span>{rule.nextRunDateUtc ? `Next due ${formatDate(rule.nextRunDateUtc)}` : "No future run"}</span>
                  </div>
                  <div className="budget-card__metrics">
                    <span>Start {formatDate(rule.startDateUtc)}</span>
                    <span>{rule.endDateUtc ? `Ends ${formatDate(rule.endDateUtc)}` : "No end date"}</span>
                    <span>{rule.lastProcessedAtUtc ? `Last processed ${formatDate(rule.lastProcessedAtUtc)}` : "Not processed yet"}</span>
                  </div>
                  <div className="inline-actions">
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => editRule(rule)}>Edit</button>
                    {rule.status === "Completed" ? null : <button type="button" className="ghost-button ghost-button--small" onClick={() => toggleRule(rule)}>{rule.status === "Paused" ? "Resume" : "Pause"}</button>}
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => deleteRule(rule.id)}>Delete</button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}