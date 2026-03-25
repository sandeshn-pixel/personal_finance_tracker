import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import { rulesApi, type RuleActionType, type RuleConditionField, type RuleConditionOperator, type TransactionRuleDto } from "../api/rulesApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { ApiError } from "../../../shared/lib/api/client";
import { formatDate } from "../../../shared/lib/format";
import { filterAccountsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

const conditionFieldValues = ["Merchant", "Amount", "Category", "TransactionType", "Account"] as const;
const conditionOperatorValues = ["Equals", "Contains", "GreaterThan", "LessThan"] as const;
const actionTypeValues = ["SetCategory", "AddTag", "CreateAlert"] as const;
const transactionTypeValues = ["Income", "Expense", "Transfer"] as const;

const ruleSchema = z.object({
  name: z.string().trim().min(1, "Rule name is required.").max(80, "Rule name must be 80 characters or fewer."),
  priority: z.coerce.number().int().min(1, "Priority must be at least 1.").max(999, "Priority must be 999 or lower."),
  isActive: z.boolean(),
  conditionField: z.enum(conditionFieldValues),
  conditionOperator: z.enum(conditionOperatorValues),
  conditionTextValue: z.string().max(120, "Merchant value must be 120 characters or fewer.").optional().or(z.literal("")),
  conditionAmountValue: z.string().optional().or(z.literal("")),
  conditionCategoryId: z.string().optional().or(z.literal("")),
  conditionAccountId: z.string().optional().or(z.literal("")),
  conditionTransactionType: z.enum(transactionTypeValues).optional(),
  actionType: z.enum(actionTypeValues),
  actionCategoryId: z.string().optional().or(z.literal("")),
  actionTag: z.string().max(40, "Tag must be 40 characters or fewer.").optional().or(z.literal("")),
  actionAlertTitle: z.string().max(120, "Alert title must be 120 characters or fewer.").optional().or(z.literal("")),
  actionAlertMessage: z.string().max(320, "Alert message must be 320 characters or fewer.").optional().or(z.literal("")),
}).superRefine((values, ctx) => {
  if (values.conditionField === "Merchant" && !["Equals", "Contains"].includes(values.conditionOperator)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionOperator"], message: "Merchant rules support equals or contains." });
  }

  if (values.conditionField === "Amount" && !["GreaterThan", "LessThan"].includes(values.conditionOperator)) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionOperator"], message: "Amount rules support greater than or less than." });
  }

  if (["Category", "TransactionType", "Account"].includes(values.conditionField) && values.conditionOperator !== "Equals") {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionOperator"], message: "This rule type only supports equals." });
  }

  if (values.conditionField === "Merchant" && !values.conditionTextValue?.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionTextValue"], message: "Merchant text is required." });
  }

  if (values.conditionField === "Amount") {
    const parsed = Number(values.conditionAmountValue);
    if (!values.conditionAmountValue || Number.isNaN(parsed) || parsed <= 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionAmountValue"], message: "Enter a positive amount." });
    }
  }

  if (values.conditionField === "Category" && !values.conditionCategoryId) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionCategoryId"], message: "Choose a category." });
  }

  if (values.conditionField === "Account" && !values.conditionAccountId) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionAccountId"], message: "Choose an account." });
  }

  if (values.conditionField === "TransactionType" && !values.conditionTransactionType) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["conditionTransactionType"], message: "Choose a transaction type." });
  }

  if (values.actionType === "SetCategory" && !values.actionCategoryId) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["actionCategoryId"], message: "Choose the category to apply." });
  }

  if (values.actionType === "AddTag" && !values.actionTag?.trim()) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["actionTag"], message: "Tag is required." });
  }

  if (values.actionType === "CreateAlert") {
    if (!values.actionAlertTitle?.trim()) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["actionAlertTitle"], message: "Alert title is required." });
    }
    if (!values.actionAlertMessage?.trim()) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["actionAlertMessage"], message: "Alert message is required." });
    }
  }
});

type RuleFormValues = z.infer<typeof ruleSchema>;

const defaultValues: RuleFormValues = {
  name: "",
  priority: 100,
  isActive: true,
  conditionField: "Merchant",
  conditionOperator: "Contains",
  conditionTextValue: "",
  conditionAmountValue: "",
  conditionCategoryId: "",
  conditionAccountId: "",
  conditionTransactionType: "Expense",
  actionType: "SetCategory",
  actionCategoryId: "",
  actionTag: "",
  actionAlertTitle: "",
  actionAlertMessage: "",
};

const fieldOperatorOptions: Record<RuleConditionField, RuleConditionOperator[]> = {
  Merchant: ["Equals", "Contains"],
  Amount: ["GreaterThan", "LessThan"],
  Category: ["Equals"],
  TransactionType: ["Equals"],
  Account: ["Equals"],
};

export function RulesPage() {
  const { accessToken } = useAuth();
  const { sharedAccessView } = useWorkspaceScope();
  const [rules, setRules] = useState<TransactionRuleDto[]>([]);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");
  const [editing, setEditing] = useState<TransactionRuleDto | null>(null);

  const { register, watch, handleSubmit, reset, formState: { errors, isSubmitting }, setError } = useForm<RuleFormValues>({
    resolver: zodResolver(ruleSchema),
    defaultValues,
  });

  const conditionField = watch("conditionField");
  const actionType = watch("actionType");
  const formValues = watch();
  const availableOperators = fieldOperatorOptions[conditionField];

  useEffect(() => {
    void bootstrap();
  }, [accessToken]);

  async function bootstrap() {
    if (!accessToken) {
      return;
    }

    setLoading(true);
    try {
      const [rulesResponse, accountsResponse, categoriesResponse] = await Promise.all([
        rulesApi.list(accessToken),
        accountsApi.list(accessToken),
        categoriesApi.list(accessToken),
      ]);
      setRules(rulesResponse);
      setAccounts(accountsResponse.filter((item) => !item.isArchived));
      setCategories(categoriesResponse.filter((item) => !item.isArchived));
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Rules loaded too quickly for the current session limit. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load rules.");
      }
    } finally {
      setLoading(false);
    }
  }

  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(accounts), [accounts]);
  const scopedAccounts = useMemo(() => filterAccountsForView(accounts, sharedAccessView), [accounts, sharedAccessView]);
  const scopedAccountIds = useMemo(() => new Set(scopedAccounts.map((account) => account.id)), [scopedAccounts]);
  const visibleRules = useMemo(() => rules.filter((rule) => rule.condition.field !== "Account" || !rule.condition.accountId || scopedAccountIds.has(rule.condition.accountId)), [rules, scopedAccountIds]);
  const scopeMessage = useMemo(() => {
    if (sharedAccessView === "mine" && showSharedViewToggle) {
      return "Rules are focused on your own account space. Account-specific rules targeting shared accounts are hidden in this view.";
    }

    if (sharedAccessView === "shared" && showSharedViewToggle) {
      return "Rules are focused on accounts shared with you. Account-specific rules for your own accounts are hidden in this view.";
    }

    return "Rules run when a new transaction is created. Existing transactions are never reprocessed, and edits do not re-run rules in this version.";
  }, [sharedAccessView, showSharedViewToggle]);

  useEffect(() => {
    if (formValues.conditionField === "Account" && formValues.conditionAccountId && !scopedAccountIds.has(formValues.conditionAccountId)) {
      reset({ ...formValues, conditionAccountId: "" });
    }
  }, [formValues, reset, scopedAccountIds]);

  function resetForm() {
    setEditing(null);
    reset(defaultValues);
    setErrorMessage(null);
  }

  function editRule(rule: TransactionRuleDto) {
    if (rule.condition.field === "Account" && rule.condition.accountId && !scopedAccountIds.has(rule.condition.accountId)) {
      setErrorVariant("info");
      setErrorMessage("That rule targets an account outside the current workspace scope. Switch the scope to edit it.");
      return;
    }

    setEditing(rule);
    reset({
      name: rule.name,
      priority: rule.priority,
      isActive: rule.isActive,
      conditionField: rule.condition.field,
      conditionOperator: rule.condition.operator,
      conditionTextValue: rule.condition.textValue ?? "",
      conditionAmountValue: rule.condition.amountValue?.toString() ?? "",
      conditionCategoryId: rule.condition.categoryId ?? "",
      conditionAccountId: rule.condition.accountId ?? "",
      conditionTransactionType: rule.condition.transactionType ?? "Expense",
      actionType: rule.action.type,
      actionCategoryId: rule.action.categoryId ?? "",
      actionTag: rule.action.tag ?? "",
      actionAlertTitle: rule.action.alertTitle ?? "",
      actionAlertMessage: rule.action.alertMessage ?? "",
    });
    setErrorMessage(null);
  }

  async function onSubmit(values: RuleFormValues) {
    if (!accessToken) {
      return;
    }

    const payload = {
      name: values.name,
      priority: values.priority,
      isActive: values.isActive,
      condition: {
        field: values.conditionField,
        operator: values.conditionOperator,
        textValue: values.conditionField === "Merchant" ? (values.conditionTextValue ?? "").trim() : null,
        amountValue: values.conditionField === "Amount" ? Number(values.conditionAmountValue) : null,
        categoryId: values.conditionField === "Category" ? values.conditionCategoryId || null : null,
        accountId: values.conditionField === "Account" ? values.conditionAccountId || null : null,
        transactionType: values.conditionField === "TransactionType" ? values.conditionTransactionType : null,
      },
      action: {
        type: values.actionType,
        categoryId: values.actionType === "SetCategory" ? values.actionCategoryId || null : null,
        tag: values.actionType === "AddTag" ? (values.actionTag ?? "").trim() : null,
        alertTitle: values.actionType === "CreateAlert" ? (values.actionAlertTitle ?? "").trim() : null,
        alertMessage: values.actionType === "CreateAlert" ? (values.actionAlertMessage ?? "").trim() : null,
      },
    } as const;

    try {
      if (editing) {
        await rulesApi.update(accessToken, editing.id, payload);
      } else {
        await rulesApi.create(accessToken, payload);
      }
      resetForm();
      await bootstrap();
    } catch (error) {
      if (error instanceof ApiError && error.fieldErrors) {
        Object.entries(error.fieldErrors).forEach(([field, messages]) => {
          const first = messages[0];
          const mappedField = mapApiFieldToFormField(field);
          if (first && mappedField) {
            setError(mappedField, { message: first });
          }
        });
      }
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("You are saving rules too quickly. Please wait a moment before trying again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to save rule.");
      }
    }
  }

  async function deleteRule(ruleId: string) {
    if (!accessToken || !window.confirm("Delete this rule?")) {
      return;
    }

    try {
      await rulesApi.remove(accessToken, ruleId);
      if (editing?.id === ruleId) {
        resetForm();
      }
      await bootstrap();
    } catch (error) {
      setErrorVariant("error");
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to delete rule.");
    }
  }

  const rulePreview = useMemo(() => buildRulePreview({ values: formValues, categories, accounts: scopedAccounts }), [formValues, categories, scopedAccounts]);

  if (loading) {
    return <PageLoader label="Loading rules" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader
        title="Rules"
        description="Define calm, explicit automations that classify new transactions, add tags, or raise alerts when a condition matches."
        action={<Button type="button" onClick={resetForm}>New rule</Button>}
      />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      <div className="page-context-row">
        <div className="rules-preview">
          <strong>Rule scope</strong>
          <p>{scopeMessage}</p>
        </div>
      </div>

      <div className="rules-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit rule" : "Create rule"}</h3>
            <p>Rules run when a new transaction is created. They never move money or rewrite old transactions.</p>
          </div>

          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <div className="field-grid">
              <Field label="Rule name" error={errors.name?.message}>
                <input {...register("name")} placeholder="Uber to Transport" />
              </Field>
              <Field label="Priority" error={errors.priority?.message} hint="Lower numbers run first.">
                <input {...register("priority")} type="number" min={1} max={999} />
              </Field>
            </div>

            <Field label="Rule status">
              <label className="checkbox-field">
                <input {...register("isActive")} type="checkbox" />
                <span>Rule is enabled</span>
              </label>
            </Field>

            <section className="rules-builder-card">
              <div className="panel-card__header panel-card__header--compact">
                <h3>Condition</h3>
                <p>Choose the transaction field and the exact kind of match this rule should use.</p>
              </div>
              <div className="field-grid">
                <Field label="Field" error={errors.conditionField?.message}>
                  <SelectField {...register("conditionField")}>
                    <option value="Merchant">Merchant</option>
                    <option value="Amount">Amount</option>
                    <option value="Category">Category</option>
                    <option value="TransactionType">Transaction type</option>
                    <option value="Account">Account</option>
                  </SelectField>
                </Field>
                <Field label="Operator" error={errors.conditionOperator?.message}>
                  <SelectField {...register("conditionOperator")}>
                    {availableOperators.map((operator) => <option key={operator} value={operator}>{formatOperator(operator)}</option>)}
                  </SelectField>
                </Field>
              </div>

              {conditionField === "Merchant" ? (
                <Field label="Merchant match" error={errors.conditionTextValue?.message}>
                  <input {...register("conditionTextValue")} placeholder="Uber" />
                </Field>
              ) : null}
              {conditionField === "Amount" ? (
                <Field label="Amount threshold" error={errors.conditionAmountValue?.message}>
                  <input {...register("conditionAmountValue")} type="number" min={0} step="0.01" placeholder="5000" />
                </Field>
              ) : null}
              {conditionField === "Category" ? (
                <Field label="Category" error={errors.conditionCategoryId?.message}>
                  <SelectField {...register("conditionCategoryId")}>
                    <option value="">Select category</option>
                    {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
                  </SelectField>
                </Field>
              ) : null}
              {conditionField === "Account" ? (
                <Field label="Account" error={errors.conditionAccountId?.message}>
                  <SelectField {...register("conditionAccountId")}>
                    <option value="">Select account</option>
                    {scopedAccounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                  </SelectField>
                </Field>
              ) : null}
              {conditionField === "TransactionType" ? (
                <Field label="Transaction type" error={errors.conditionTransactionType?.message}>
                  <SelectField {...register("conditionTransactionType")}>
                    <option value="Income">Income</option>
                    <option value="Expense">Expense</option>
                    <option value="Transfer">Transfer</option>
                  </SelectField>
                </Field>
              ) : null}
            </section>

            <section className="rules-builder-card">
              <div className="panel-card__header panel-card__header--compact">
                <h3>Action</h3>
                <p>Choose what should happen when the condition matches. Actions are limited to safe metadata changes and alerts.</p>
              </div>
              <Field label="Action" error={errors.actionType?.message}>
                <SelectField {...register("actionType")}>
                  <option value="SetCategory">Set category</option>
                  <option value="AddTag">Add tag</option>
                  <option value="CreateAlert">Create alert</option>
                </SelectField>
              </Field>

              {actionType === "SetCategory" ? (
                <Field label="Category to apply" error={errors.actionCategoryId?.message}>
                  <SelectField {...register("actionCategoryId")}>
                    <option value="">Select category</option>
                    {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
                  </SelectField>
                </Field>
              ) : null}
              {actionType === "AddTag" ? (
                <Field label="Tag to add" error={errors.actionTag?.message} hint="For grouping new matching transactions later in reports or filters.">
                  <input {...register("actionTag")} placeholder="monthly-food" />
                </Field>
              ) : null}
              {actionType === "CreateAlert" ? (
                <>
                  <Field label="Alert title" error={errors.actionAlertTitle?.message}>
                    <input {...register("actionAlertTitle")} placeholder="Large spend detected" />
                  </Field>
                  <Field label="Alert message" error={errors.actionAlertMessage?.message}>
                    <input {...register("actionAlertMessage")} placeholder="Review this transaction before month end." />
                  </Field>
                </>
              ) : null}
            </section>

            <div className="rules-preview">
              <strong>Preview</strong>
              <p>{rulePreview}</p>
            </div>

            <div className="button-row">
              <Button type="submit" loading={isSubmitting}>{editing ? "Save rule" : "Create rule"}</Button>
              {editing ? <button type="button" className="ghost-button" onClick={resetForm}>Cancel editing</button> : null}
            </div>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Active rule set</h3>
            <p>{visibleRules.length} rule{visibleRules.length === 1 ? "" : "s"} visible in the current scope. Lower priority numbers run first.</p>
          </div>
          {visibleRules.length === 0 ? (
            <EmptyState
              title="No rules in this view"
              description="Switch workspace scope or create a new rule to auto-categorize transactions, add tags, or raise alerts when a condition matches."
            />
          ) : (
            <div className="rules-list">
              {visibleRules.map((rule) => (
                <article key={rule.id} className="rule-row">
                  <div className="rule-row__top">
                    <div>
                      <strong>{rule.name}</strong>
                      <p>{rule.conditionSummary}</p>
                    </div>
                    <span className={`status-badge status-badge--${rule.isActive ? "default" : "warning"}`}>{rule.isActive ? "Enabled" : "Disabled"}</span>
                  </div>
                  <div className="rule-row__meta">
                    <span>Then: {rule.actionSummary}</span>
                    <span>Priority {rule.priority}</span>
                    <span>Updated {formatDate(rule.updatedUtc)}</span>
                  </div>
                  <div className="inline-actions">
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => editRule(rule)}>Edit</button>
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => void deleteRule(rule.id)}>Delete</button>
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

function buildRulePreview(input: { values: RuleFormValues; categories: CategoryDto[]; accounts: AccountDto[] }) {
  const { values, categories, accounts } = input;

  const conditionValue = (() => {
    switch (values.conditionField) {
      case "Merchant":
        return values.conditionTextValue?.trim() ? `"${(values.conditionTextValue ?? "").trim()}"` : "a merchant value";
      case "Amount":
        return values.conditionAmountValue ? values.conditionAmountValue : "an amount";
      case "Category":
        return categories.find((item) => item.id === values.conditionCategoryId)?.name ?? "a category";
      case "TransactionType":
        return values.conditionTransactionType ?? "a transaction type";
      case "Account":
        return accounts.find((item) => item.id === values.conditionAccountId)?.name ?? "an account";
    }
  })();

  const actionValue = (() => {
    switch (values.actionType) {
      case "SetCategory":
        return `set the category to ${categories.find((item) => item.id === values.actionCategoryId)?.name ?? "the selected category"}`;
      case "AddTag":
        return `add the tag "${values.actionTag?.trim() || "tag"}"`;
      case "CreateAlert":
        return `create an alert titled "${values.actionAlertTitle?.trim() || "alert"}"`;
    }
  })();

  return `If ${formatField(values.conditionField)} ${formatOperator(values.conditionOperator)} ${conditionValue}, ${actionValue}.`;
}

function formatField(value: RuleConditionField) {
  switch (value) {
    case "TransactionType":
      return "transaction type";
    default:
      return value.toLowerCase();
  }
}

function formatOperator(value: RuleConditionOperator) {
  switch (value) {
    case "Equals":
      return "equals";
    case "Contains":
      return "contains";
    case "GreaterThan":
      return "is greater than";
    case "LessThan":
      return "is less than";
  }
}

function mapApiFieldToFormField(field: string): keyof RuleFormValues | null {
  const normalized = field.toLowerCase();

  if (normalized.endsWith("name")) return "name";
  if (normalized.endsWith("priority")) return "priority";
  if (normalized.includes("conditiontextvalue") || normalized.includes("condition.textvalue")) return "conditionTextValue";
  if (normalized.includes("conditionamountvalue") || normalized.includes("condition.amountvalue")) return "conditionAmountValue";
  if (normalized.includes("conditioncategoryid") || normalized.includes("condition.categoryid")) return "conditionCategoryId";
  if (normalized.includes("conditionaccountid") || normalized.includes("condition.accountid")) return "conditionAccountId";
  if (normalized.includes("conditiontransactiontype") || normalized.includes("condition.transactiontype")) return "conditionTransactionType";
  if (normalized.includes("conditionoperator") || normalized.includes("condition.operator")) return "conditionOperator";
  if (normalized.includes("actioncategoryid") || normalized.includes("action.categoryid")) return "actionCategoryId";
  if (normalized.includes("actiontag") || normalized.includes("action.tag")) return "actionTag";
  if (normalized.includes("actionalerttitle") || normalized.includes("action.alerttitle")) return "actionAlertTitle";
  if (normalized.includes("actionalertmessage") || normalized.includes("action.alertmessage")) return "actionAlertMessage";
  return null;
}


