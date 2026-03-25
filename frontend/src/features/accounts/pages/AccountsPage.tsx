import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
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
import { filterAccountsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

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
  const { sharedAccessView: accountView } = useWorkspaceScope();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<AccountDto | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");

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
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Accounts are refreshing too quickly for the current session limit. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load accounts.");
      }
    } finally {
      setLoading(false);
    }
  }

  function resetForCreate() {
    setEditing(null);
    reset({ name: "", type: "1", currencyCode: "INR", openingBalance: 0, institutionName: "", last4Digits: "" });
  }

  function editAccount(account: AccountDto) {
    if (account.currentUserRole !== "Owner") {
      setErrorVariant("info");
      setErrorMessage("Only the account owner can edit shared account details in this version.");
      return;
    }

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
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Accounts are being updated too quickly. Please wait a moment before trying again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to save account.");
      }
    }
  }

  async function archiveAccount(account: AccountDto) {
    if (!accessToken) return;
    if (account.currentUserRole !== "Owner") {
      setErrorVariant("info");
      setErrorMessage("Only the account owner can archive a shared account.");
      return;
    }

    if (!window.confirm("Archive this account?")) return;
    await accountsApi.archive(accessToken, account.id);
    await loadAccounts();
  }

  const liveBalance = useMemo(() => accounts.filter((item) => !item.isArchived).reduce((sum, item) => sum + item.currentBalance, 0), [accounts]);
  const ownedPendingInviteCount = useMemo(() => accounts.reduce((sum, item) => sum + item.pendingInviteCount, 0), [accounts]);
  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(accounts), [accounts]);
  const visibleAccounts = useMemo(() => filterAccountsForView(accounts, accountView), [accountView, accounts]);

  if (loading) return <PageLoader label="Loading accounts" />;

  return (
    <div className="page-stack">
      <SectionHeader title="Accounts" description="Manage the ledgers that transactions will affect." action={<Button type="button" onClick={resetForCreate}>New account</Button>} />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {ownedPendingInviteCount > 0 ? (
        <div className="page-context-row">
          <div className="rules-preview">
            <strong>Sharing follow-up waiting.</strong>
            <p>You have {ownedPendingInviteCount} pending shared-account invite{ownedPendingInviteCount === 1 ? "" : "s"} across your owned accounts.</p>
          </div>
        </div>
      ) : null}
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
          <div className="panel-card__header panel-card__header--inline">
            <div>
              <h3>Account list</h3>
              <p>Live balance: {formatCurrency(liveBalance)}</p>
            </div>
            
          </div>
          {visibleAccounts.length === 0 ? (
            <EmptyState title="No accounts in this view" description={accountView === "shared" ? "Shared accounts you can access will appear here." : accountView === "mine" ? "Your owned and private accounts will appear here." : "Create your first account to start recording transactions safely."} />
          ) : (
            <div className="account-list">
              {visibleAccounts.map((account) => (
                <article key={account.id} className={`account-card ${account.isArchived ? "account-card--archived" : ""}`}>
                  <div>
                    <div className="account-card__title-row">
                      <strong>{account.name}</strong>
                      {account.isShared ? <span className="status-badge status-badge--default">Shared</span> : null}
                      <span className="status-badge status-badge--warning">{account.currentUserRole}</span>
                      {account.currentUserRole === "Owner" && account.pendingInviteCount > 0 ? <span className="status-badge status-badge--default">{account.pendingInviteCount} pending invite{account.pendingInviteCount === 1 ? "" : "s"}</span> : null}
                    </div>
                    <p>{typeLabels[account.type]} | {account.currencyCode}</p>
                    <small>
                      {account.institutionName || "Personal ledger"}
                      {account.isShared ? ` | ${account.memberCount} members | Owner: ${account.ownerDisplayName}` : ""}
                      {account.currentUserRole === "Owner" && account.pendingInviteCount > 0 ? ` | ${account.pendingInviteCount} invite${account.pendingInviteCount === 1 ? "" : "s"} awaiting action` : ""}
                    </small>
                  </div>
                  <div className="account-card__aside">
                    <strong>{formatCurrency(account.currentBalance, account.currencyCode)}</strong>
                    <div className="inline-actions">
                      <Link to={`/accounts/${account.id}`} className="ghost-button ghost-button--small account-card__link-button">Details</Link>
                      {account.currentUserRole === "Owner" ? (
                        <Link
                          to={`/accounts/${account.id}#sharing`}
                          className={`ghost-button ghost-button--small account-card__link-button ${account.pendingInviteCount > 0 ? "account-card__link-button--attention" : ""}`}
                        >
                          Manage sharing{account.pendingInviteCount > 0 ? ` (${account.pendingInviteCount})` : ""}
                        </Link>
                      ) : null}
                      {account.currentUserRole === "Owner" ? <button type="button" className="ghost-button ghost-button--small" onClick={() => editAccount(account)}>Edit</button> : null}
                      {!account.isArchived && account.currentUserRole === "Owner" ? <button type="button" className="ghost-button ghost-button--small" onClick={() => archiveAccount(account)}>Archive</button> : null}
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








