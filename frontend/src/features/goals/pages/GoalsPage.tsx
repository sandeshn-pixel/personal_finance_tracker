import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { goalsApi, type GoalDetailsDto, type GoalDto } from "../api/goalsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate, toDateInputValue } from "../../../shared/lib/format";

const goalSchema = z.object({
  name: z.string().trim().min(1, "Goal name is required.").max(120),
  targetAmount: z.coerce.number().positive("Target amount must be greater than zero."),
  targetDateUtc: z.string().optional().or(z.literal("")),
  linkedAccountId: z.string().optional().or(z.literal("")),
  icon: z.string().trim().max(64).optional().or(z.literal("")),
  color: z.string().trim().max(32).optional().or(z.literal("")),
});

const entrySchema = z.object({
  amount: z.coerce.number().positive("Amount must be greater than zero."),
  occurredAtUtc: z.string().optional().or(z.literal("")),
  note: z.string().trim().max(240).optional().or(z.literal("")),
});

type GoalFormValues = z.infer<typeof goalSchema>;
type EntryFormValues = z.infer<typeof entrySchema>;
type EntryMode = "contribution" | "withdrawal";

type GoalIconOption = { value: string; label: string; badge: string };
type GoalColorOption = { value: string; label: string };

const goalIconOptions: GoalIconOption[] = [
  { value: "Shield", label: "Emergency", badge: "S" },
  { value: "Home", label: "Home", badge: "H" },
  { value: "Plane", label: "Travel", badge: "T" },
  { value: "Book", label: "Education", badge: "E" },
  { value: "Car", label: "Vehicle", badge: "C" },
  { value: "Gift", label: "Gift", badge: "G" },
];

const goalColorOptions: GoalColorOption[] = [
  { value: "teal", label: "Teal" },
  { value: "amber", label: "Amber" },
  { value: "navy", label: "Navy" },
  { value: "forest", label: "Forest" },
  { value: "rose", label: "Rose" },
  { value: "slate", label: "Slate" },
];

function getGoalIcon(icon?: string | null) {
  return goalIconOptions.find((option) => option.value === icon) ?? goalIconOptions[0];
}

function getGoalColor(color?: string | null) {
  return goalColorOptions.find((option) => option.value === color) ?? goalColorOptions[0];
}

export function GoalsPage() {
  const { accessToken } = useAuth();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [goals, setGoals] = useState<GoalDto[]>([]);
  const [selectedGoalId, setSelectedGoalId] = useState<string | null>(null);
  const [selectedGoal, setSelectedGoal] = useState<GoalDetailsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<GoalDto | null>(null);
  const [entryMode, setEntryMode] = useState<EntryMode>("contribution");

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<GoalFormValues>({
    resolver: zodResolver(goalSchema),
    defaultValues: { name: "", targetAmount: 0, targetDateUtc: "", linkedAccountId: "", icon: goalIconOptions[0].value, color: goalColorOptions[0].value },
  });

  const entryForm = useForm<EntryFormValues>({
    resolver: zodResolver(entrySchema),
    defaultValues: { amount: 0, occurredAtUtc: "", note: "" },
  });

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => {
    if (!accessToken || !selectedGoalId) return;
    void loadGoalDetails(selectedGoalId);
  }, [accessToken, selectedGoalId]);

  async function bootstrap() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [accountsResponse, goalsResponse] = await Promise.all([
        accountsApi.list(accessToken),
        goalsApi.list(accessToken),
      ]);
      setAccounts(accountsResponse.filter((item) => !item.isArchived));
      setGoals(goalsResponse);
      const nextGoalId = selectedGoalId && goalsResponse.some((item) => item.id === selectedGoalId)
        ? selectedGoalId
        : goalsResponse[0]?.id ?? null;
      setSelectedGoalId(nextGoalId);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load goals.");
    } finally {
      setLoading(false);
    }
  }

  async function loadGoalDetails(goalId: string) {
    if (!accessToken) return;
    try {
      setSelectedGoal(await goalsApi.get(accessToken, goalId));
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load goal details.");
    }
  }

  function resetGoalForm() {
    setEditing(null);
    reset({ name: "", targetAmount: 0, targetDateUtc: "", linkedAccountId: "", icon: goalIconOptions[0].value, color: goalColorOptions[0].value });
  }

  function editGoal(goal: GoalDto) {
    setEditing(goal);
    reset({
      name: goal.name,
      targetAmount: goal.targetAmount,
      targetDateUtc: toDateInputValue(goal.targetDateUtc ?? undefined),
      linkedAccountId: goal.linkedAccountId ?? "",
      icon: goal.icon ?? goalIconOptions[0].value,
      color: goal.color ?? goalColorOptions[0].value,
    });
  }

  async function onSubmit(values: GoalFormValues) {
    if (!accessToken) return;

    const payload = {
      name: values.name,
      targetAmount: values.targetAmount,
      targetDateUtc: values.targetDateUtc ? new Date(`${values.targetDateUtc}T00:00:00.000Z`).toISOString() : null,
      linkedAccountId: values.linkedAccountId || null,
      icon: values.icon || undefined,
      color: values.color || undefined,
    };

    try {
      if (editing) {
        await goalsApi.update(accessToken, editing.id, payload);
      } else {
        const created = await goalsApi.create(accessToken, payload);
        setSelectedGoalId(created.id);
      }

      resetGoalForm();
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save goal.");
    }
  }

  async function submitEntry(values: EntryFormValues) {
    if (!accessToken || !selectedGoalId) return;

    const payload = {
      amount: values.amount,
      occurredAtUtc: values.occurredAtUtc ? new Date(`${values.occurredAtUtc}T00:00:00.000Z`).toISOString() : null,
      note: values.note || undefined,
    };

    try {
      const details = entryMode === "contribution"
        ? await goalsApi.contribute(accessToken, selectedGoalId, payload)
        : await goalsApi.withdraw(accessToken, selectedGoalId, payload);

      setSelectedGoal(details);
      entryForm.reset({ amount: 0, occurredAtUtc: "", note: "" });
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to record goal movement.");
    }
  }

  async function completeGoal(goalId: string) {
    if (!accessToken) return;
    try {
      await goalsApi.complete(accessToken, goalId);
      await bootstrap();
      if (selectedGoalId === goalId) {
        await loadGoalDetails(goalId);
      }
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to complete goal.");
    }
  }

  async function archiveGoal(goalId: string) {
    if (!accessToken || !window.confirm("Archive this goal? Contribution history will remain available.")) return;
    try {
      await goalsApi.archive(accessToken, goalId);
      if (selectedGoalId === goalId) {
        setSelectedGoalId(null);
        setSelectedGoal(null);
      }
      await bootstrap();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to archive goal.");
    }
  }

  const activeGoals = useMemo(() => goals.filter((goal) => goal.status === "Active"), [goals]);
  const completedGoals = useMemo(() => goals.filter((goal) => goal.status === "Completed"), [goals]);
  const archivedGoals = useMemo(() => goals.filter((goal) => goal.status === "Archived"), [goals]);
  const totalTarget = useMemo(() => goals.reduce((sum, goal) => sum + goal.targetAmount, 0), [goals]);
  const totalSaved = useMemo(() => goals.reduce((sum, goal) => sum + goal.currentAmount, 0), [goals]);

  if (loading && goals.length === 0 && accounts.length === 0) {
    return <PageLoader label="Loading goals" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader title="Goals" description="Track savings targets with audited contributions, withdrawals, and optional linked-account movement." action={<Button type="button" onClick={resetGoalForm}>New goal</Button>} />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      <div className="stats-grid stats-grid--four">
        <StatCard label="Goals" value={String(goals.length)} hint={`${activeGoals.length} active, ${completedGoals.length} completed.`} />
        <StatCard label="Targeted" value={formatCurrency(totalTarget)} hint="Combined target amount across all goals." />
        <StatCard label="Saved" value={formatCurrency(totalSaved)} hint="Current total allocated to goals." tone="positive" />
        <StatCard label="Archived" value={String(archivedGoals.length)} hint="Closed goals remain historically visible." />
      </div>
      <div className="budget-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit goal" : "Create savings goal"}</h3>
            <p>Goal entries form the audit trail; current balance is never edited directly.</p>
          </div>
          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <Field label="Goal name" error={errors.name?.message}><input {...register("name")} placeholder="Emergency fund" /></Field>
            <div className="field-grid">
              <Field label="Target amount" error={errors.targetAmount?.message}><input {...register("targetAmount")} type="number" step="0.01" /></Field>
              <Field label="Target date" error={errors.targetDateUtc?.message}><input {...register("targetDateUtc")} type="date" /></Field>
            </div>
            <div className="field-grid">
              <Field label="Linked account" error={errors.linkedAccountId?.message}>
                <SelectField {...register("linkedAccountId")}>
                  <option value="">No linked account</option>
                  {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                </SelectField>
              </Field>
              <Field label="Icon" error={errors.icon?.message}>
                <SelectField {...register("icon")}>
                  {goalIconOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </SelectField>
              </Field>
            </div>
            <Field label="Color theme" error={errors.color?.message}>
              <SelectField {...register("color")}>
                {goalColorOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </SelectField>
            </Field>
            <Button type="submit" loading={isSubmitting}>{editing ? "Save goal" : "Create goal"}</Button>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Goal list</h3>
            <p>Active and completed goals stay visible with progress and linked-account context.</p>
          </div>
          {goals.length === 0 ? (
            <EmptyState title="No goals yet" description="Create a savings goal to track progress toward planned milestones." />
          ) : (
            <div className="goal-sections">
              <div>
                <h4 className="subsection-title">Active</h4>
                <div className="budget-list">
                  {activeGoals.map((goal) => {
                    const icon = getGoalIcon(goal.icon);
                    const color = getGoalColor(goal.color);
                    return (
                      <article key={goal.id} className={`goal-card goal-card--${color.value} ${selectedGoalId === goal.id ? "goal-card--selected" : ""}`}>
                        <button type="button" className="goal-card__body" onClick={() => setSelectedGoalId(goal.id)}>
                          <div className="goal-card__header">
                            <div className="goal-card__title-group">
                              <span className={`goal-badge goal-badge--${color.value}`}>{icon.badge}</span>
                              <div>
                                <strong>{goal.name}</strong>
                                <p>{icon.label}</p>
                              </div>
                            </div>
                            <span className="status-badge status-badge--default">{goal.status}</span>
                          </div>
                          <p>{formatCurrency(goal.currentAmount)} of {formatCurrency(goal.targetAmount)} saved</p>
                          <ProgressBar value={goal.progressPercent} />
                          <div className="budget-card__metrics">
                            <span>{goal.progressPercent.toFixed(2)}% funded</span>
                            <span>{formatCurrency(goal.remainingAmount)} remaining</span>
                            <span>{goal.linkedAccountName ?? "Standalone goal"}</span>
                          </div>
                        </button>
                        <div className="inline-actions">
                          <button type="button" className="ghost-button ghost-button--small" onClick={() => editGoal(goal)}>Edit</button>
                          <button type="button" className="ghost-button ghost-button--small" onClick={() => completeGoal(goal.id)}>Complete</button>
                          <button type="button" className="ghost-button ghost-button--small" onClick={() => archiveGoal(goal.id)}>Archive</button>
                        </div>
                      </article>
                    );
                  })}
                  {activeGoals.length === 0 ? <EmptyState title="No active goals" description="Active goals appear here once you create them." /> : null}
                </div>
              </div>
              <div>
                <h4 className="subsection-title">Completed</h4>
                <div className="budget-list">
                  {completedGoals.map((goal) => {
                    const icon = getGoalIcon(goal.icon);
                    const color = getGoalColor(goal.color);
                    return (
                      <article key={goal.id} className={`goal-card goal-card--${color.value} ${selectedGoalId === goal.id ? "goal-card--selected" : ""}`}>
                        <button type="button" className="goal-card__body" onClick={() => setSelectedGoalId(goal.id)}>
                          <div className="goal-card__header">
                            <div className="goal-card__title-group">
                              <span className={`goal-badge goal-badge--${color.value}`}>{icon.badge}</span>
                              <div>
                                <strong>{goal.name}</strong>
                                <p>{icon.label}</p>
                              </div>
                            </div>
                            <span className="status-badge status-badge--warning">Completed</span>
                          </div>
                          <p>{formatCurrency(goal.currentAmount)} saved • Target date {goal.targetDateUtc ? formatDate(goal.targetDateUtc) : "Flexible"}</p>
                        </button>
                      </article>
                    );
                  })}
                  {completedGoals.length === 0 ? <EmptyState title="No completed goals" description="Completed goals stay visible here for historical review." /> : null}
                </div>
              </div>
            </div>
          )}
        </section>
      </div>

      <section className="panel-card">
        <div className="panel-card__header panel-card__header--inline">
          <div>
            <h3>Goal activity</h3>
            <p>Contribution and withdrawal flows update the goal ledger and linked account together.</p>
          </div>
          {selectedGoal?.goal ? <div className="inline-actions">
            <button type="button" className="ghost-button ghost-button--small" onClick={() => setEntryMode("contribution")}>Contribution</button>
            <button type="button" className="ghost-button ghost-button--small" onClick={() => setEntryMode("withdrawal")}>Withdrawal</button>
          </div> : null}
        </div>
        {!selectedGoal?.goal ? (
          <EmptyState title="Select a goal" description="Choose a goal to review its audit trail or record a contribution or withdrawal." />
        ) : (
          <div className="goal-detail-layout">
            <section className={`panel-card panel-card--form goal-detail-panel goal-detail-panel--${getGoalColor(selectedGoal.goal.color).value}`}>
              <div className="panel-card__header">
                <div className="goal-detail-title">
                  <span className={`goal-badge goal-badge--${getGoalColor(selectedGoal.goal.color).value}`}>{getGoalIcon(selectedGoal.goal.icon).badge}</span>
                  <div>
                    <h3>{selectedGoal.goal.name}</h3>
                    <p>{selectedGoal.goal.linkedAccountName ?? "No linked account"} • {selectedGoal.goal.targetDateUtc ? formatDate(selectedGoal.goal.targetDateUtc) : "No target date"}</p>
                  </div>
                </div>
              </div>
              <ProgressBar value={selectedGoal.goal.progressPercent} tone={selectedGoal.goal.status === "Completed" ? "warning" : "default"} />
              <div className="budget-card__metrics">
                <span>{formatCurrency(selectedGoal.goal.currentAmount)} current</span>
                <span>{formatCurrency(selectedGoal.goal.targetAmount)} target</span>
                <span>{formatCurrency(selectedGoal.goal.remainingAmount)} remaining</span>
              </div>
              <form className="form-stack" onSubmit={entryForm.handleSubmit(submitEntry)} noValidate>
                <div className="field-grid">
                  <Field label={entryMode === "contribution" ? "Contribution amount" : "Withdrawal amount"} error={entryForm.formState.errors.amount?.message}><input {...entryForm.register("amount")} type="number" step="0.01" /></Field>
                  <Field label="Date" error={entryForm.formState.errors.occurredAtUtc?.message}><input {...entryForm.register("occurredAtUtc")} type="date" /></Field>
                </div>
                <Field label="Note" error={entryForm.formState.errors.note?.message}><input {...entryForm.register("note")} placeholder="Optional note" /></Field>
                <Button type="submit" loading={entryForm.formState.isSubmitting}>{entryMode === "contribution" ? "Add contribution" : "Record withdrawal"}</Button>
              </form>
            </section>
            <section className="panel-card">
              <div className="panel-card__header">
                <h3>Recent entries</h3>
                <p>Newest goal movements are shown first.</p>
              </div>
              {selectedGoal.entries.length === 0 ? (
                <EmptyState title="No entries yet" description="Goal contributions and withdrawals will build an audit trail here." />
              ) : (
                <div className="simple-list">
                  {selectedGoal.entries.map((entry) => (
                    <div key={entry.id} className="list-row">
                      <div>
                        <strong>{entry.type}</strong>
                        <p>{entry.accountName ?? "No linked account"} • {formatDate(entry.occurredAtUtc)}</p>
                        {entry.note ? <small>{entry.note}</small> : null}
                      </div>
                      <div className="transaction-row__aside">
                        <strong>{formatCurrency(entry.amount)}</strong>
                        <small>Goal balance {formatCurrency(entry.goalAmountAfterEntry)}</small>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          </div>
        )}
      </section>
    </div>
  );
}