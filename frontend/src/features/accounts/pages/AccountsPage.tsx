import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountDto } from "../api/accountsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency } from "../../../shared/lib/format";

const accountSchema = z.object({
  name: z.string().trim().min(1, "Account name is required.").max(100),
  type: z.enum(["1", "2", "3", "4"]),
  currencyCode: z.string().trim().length(3, "Use a 3-letter currency code."),
  openingBalance: z.coerce.number(),
  institutionName: z.string().trim().max(120).optional().or(z.literal("")),
  last4Digits: z.string().trim().max(4).optional().or(z.literal("")),
});

type AccountFormValues = z.infer<typeof accountSchema>;

const accountTypes = [
  { label: "Bank account", value: "1" },
  { label: "Credit card", value: "2" },
  { label: "Cash wallet", value: "3" },
  { label: "Savings account", value: "4" },
] as const;

const typeToValue: Record<AccountDto["type"], AccountFormValues["type"]> = {
  BankAccount: "1",
  CreditCard: "2",
  CashWallet: "3",
  SavingsAccount: "4",
};

const typeLabels: Record<AccountDto["type"], string> = {
  BankAccount: "Bank account",
  CreditCard: "Credit card",
  CashWallet: "Cash wallet",
  SavingsAccount: "Savings account",
};

export function AccountsPage() {
  const { accessToken } = useAuth();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<AccountDto | null>(null);

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<AccountFormValues>({
    resolver: zodResolver(accountSchema),
    defaultValues: { name: "", type: "1", currencyCode: "INR", openingBalance: 0, institutionName: "", last4Digits: "" },
  });

  useEffect(() => { void loadAccounts(); }, [accessToken]);

  async function loadAccounts() {
    if (!accessToken) return;
    setLoading(true);
    try {
      setAccounts(await accountsApi.list(accessToken));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load accounts.");
    } finally {
      setLoading(false);
    }
  }

  function resetForCreate() {
    setEditing(null);
    reset({ name: "", type: "1", currencyCode: "INR", openingBalance: 0, institutionName: "", last4Digits: "" });
  }

  function editAccount(account: AccountDto) {
    setEditing(account);
    reset({
      name: account.name,
      type: typeToValue[account.type],
      currencyCode: account.currencyCode,
      openingBalance: account.openingBalance,
      institutionName: account.institutionName ?? "",
      last4Digits: account.last4Digits ?? "",
    });
  }

  async function onSubmit(values: AccountFormValues) {
    if (!accessToken) return;
    const payload = {
      name: values.name,
      type: Number(values.type),
      currencyCode: values.currencyCode.toUpperCase(),
      institutionName: values.institutionName || undefined,
      last4Digits: values.last4Digits || undefined,
      ...(editing ? {} : { openingBalance: values.openingBalance }),
    };

    try {
      if (editing) {
        await accountsApi.update(accessToken, editing.id, payload);
      } else {
        await accountsApi.create(accessToken, payload);
      }
      resetForCreate();
      await loadAccounts();
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save account.");
    }
  }

  async function archiveAccount(id: string) {
    if (!accessToken || !window.confirm("Archive this account?")) return;
    await accountsApi.archive(accessToken, id);
    await loadAccounts();
  }

  const liveBalance = useMemo(() => accounts.filter((item) => !item.isArchived).reduce((sum, item) => sum + item.currentBalance, 0), [accounts]);

  if (loading) return <PageLoader label="Loading accounts" />;

  return (
    <div className="page-stack">
      <SectionHeader title="Accounts" description="Manage the ledgers that transactions will affect." action={<Button type="button" onClick={resetForCreate}>New account</Button>} />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      <div className="accounts-grid">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit account" : "Create account"}</h3>
            <p>{editing ? "Update safe metadata for the account." : "Opening balance becomes the initial ledger position."}</p>
          </div>
          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <Field label="Account name" error={errors.name?.message}><input {...register("name")} placeholder="Primary account" /></Field>
            <div className="field-grid">
              <Field label="Type" error={errors.type?.message}>
                <SelectField {...register("type")}>
                  {accountTypes.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </SelectField>
              </Field>
              <Field label="Currency" error={errors.currencyCode?.message}><input {...register("currencyCode")} maxLength={3} /></Field>
            </div>
            {!editing ? <Field label="Opening balance" error={errors.openingBalance?.message}><input {...register("openingBalance")} type="number" step="0.01" /></Field> : null}
            <div className="field-grid">
              <Field label="Institution" error={errors.institutionName?.message}><input {...register("institutionName")} placeholder="Optional" /></Field>
              <Field label="Last 4 digits" error={errors.last4Digits?.message}><input {...register("last4Digits")} maxLength={4} placeholder="1234" /></Field>
            </div>
            <Button type="submit" loading={isSubmitting}>{editing ? "Save changes" : "Create account"}</Button>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Account list</h3>
            <p>Live balance: {formatCurrency(liveBalance)}</p>
          </div>
          {accounts.length === 0 ? (
            <EmptyState title="No accounts yet" description="Create your first account to start recording transactions safely." />
          ) : (
            <div className="account-list">
              {accounts.map((account) => (
                <article key={account.id} className={`account-card ${account.isArchived ? "account-card--archived" : ""}`}>
                  <div>
                    <strong>{account.name}</strong>
                    <p>{typeLabels[account.type]} • {account.currencyCode}</p>
                    <small>{account.institutionName || "Personal ledger"}</small>
                  </div>
                  <div className="account-card__aside">
                    <strong>{formatCurrency(account.currentBalance, account.currencyCode)}</strong>
                    <div className="inline-actions">
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => editAccount(account)}>Edit</button>
                      {!account.isArchived ? <button type="button" className="ghost-button ghost-button--small" onClick={() => archiveAccount(account.id)}>Archive</button> : null}
                    </div>
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