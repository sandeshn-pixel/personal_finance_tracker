import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { categoriesApi, type CategoryDto } from "../../categories/api/categoriesApi";
import { transactionsApi, type TransactionDto } from "../api/transactionsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate, toDateInputValue } from "../../../shared/lib/format";

const transactionSchema = z.object({
  accountId: z.string().min(1, "Account is required."),
  transferAccountId: z.string().optional(),
  type: z.enum(["1", "2", "3"]),
  amount: z.coerce.number().positive("Amount must be greater than zero."),
  dateUtc: z.string().min(1, "Date is required."),
  categoryId: z.string().optional(),
  note: z.string().max(500).optional().or(z.literal("")),
  merchant: z.string().max(120).optional().or(z.literal("")),
  paymentMethod: z.string().max(50).optional().or(z.literal("")),
  tags: z.string().optional().or(z.literal("")),
});

type TransactionFormValues = z.infer<typeof transactionSchema>;

const commonPaymentMethods = ["UPI", "Debit Card", "Credit Card", "Cash", "Bank Transfer", "Net Banking", "Wallet", "Cheque", "Internal Transfer"];

export function TransactionsPage() {
  const { accessToken } = useAuth();
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [exportMessage, setExportMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<TransactionDto | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [isExporting, setIsExporting] = useState(false);
  const [paymentManual, setPaymentManual] = useState(false);
  const [filters, setFilters] = useState({ search: "", type: "", accountId: "", categoryId: "", startDateUtc: "", endDateUtc: "" });

  const { register, control, handleSubmit, reset, watch, formState: { errors, isSubmitting } } = useForm<TransactionFormValues>({
    resolver: zodResolver(transactionSchema),
    defaultValues: { accountId: "", transferAccountId: "", type: "2", amount: 0, dateUtc: new Date().toISOString().slice(0, 10), categoryId: "", note: "", merchant: "", paymentMethod: "", tags: "" },
  });

  const type = watch("type");

  useEffect(() => { void bootstrap(); }, [accessToken]);
  useEffect(() => { void loadTransactions(); }, [accessToken, page, filters]);

  async function bootstrap() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [accountsResponse, categoriesResponse] = await Promise.all([accountsApi.list(accessToken), categoriesApi.list(accessToken)]);
      setAccounts(accountsResponse.filter((item) => !item.isArchived));
      setCategories(categoriesResponse.filter((item) => !item.isArchived));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load transaction dependencies.");
    } finally {
      setLoading(false);
    }
  }

  async function loadTransactions() {
    if (!accessToken) return;
    try {
      const response = await transactionsApi.list(accessToken, buildFilterQuery({ includePaging: true }));
      setTransactions(response.items);
      setTotalCount(response.totalCount);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load transactions.");
    }
  }

  function buildFilterQuery(options?: { includePaging?: boolean }) {
    return {
      ...(options?.includePaging ? { page, pageSize: 10 } : {}),
      ...(filters.search ? { search: filters.search } : {}),
      ...(filters.type ? { type: filters.type } : {}),
      ...(filters.accountId ? { accountId: filters.accountId } : {}),
      ...(filters.categoryId ? { categoryId: filters.categoryId } : {}),
      ...(filters.startDateUtc ? { startDateUtc: new Date(filters.startDateUtc).toISOString() } : {}),
      ...(filters.endDateUtc ? { endDateUtc: new Date(filters.endDateUtc).toISOString() } : {}),
    };
  }

  function resetForm() {
    setEditing(null);
    setPaymentManual(false);
    reset({ accountId: accounts[0]?.id ?? "", transferAccountId: "", type: "2", amount: 0, dateUtc: new Date().toISOString().slice(0, 10), categoryId: "", note: "", merchant: "", paymentMethod: "", tags: "" });
  }

  function editTransaction(item: TransactionDto) {
    setEditing(item);
    setPaymentManual(Boolean(item.paymentMethod) && !paymentMethodOptions.includes(item.paymentMethod ?? ""));
    reset({
      accountId: item.accountId,
      transferAccountId: item.transferAccountId ?? "",
      type: item.type === "Income" ? "1" : item.type === "Expense" ? "2" : "3",
      amount: item.amount,
      dateUtc: toDateInputValue(item.dateUtc),
      categoryId: item.categoryId ?? "",
      note: item.note ?? "",
      merchant: item.merchant ?? "",
      paymentMethod: item.paymentMethod ?? "",
      tags: item.tags.join(", "),
    });
  }

  async function onSubmit(values: TransactionFormValues) {
    if (!accessToken) return;
    const payload = {
      accountId: values.accountId,
      transferAccountId: values.type === "3" ? values.transferAccountId || null : null,
      type: Number(values.type),
      amount: values.amount,
      dateUtc: new Date(values.dateUtc).toISOString(),
      categoryId: values.type === "3" ? null : values.categoryId || null,
      note: values.note || undefined,
      merchant: values.merchant || undefined,
      paymentMethod: values.paymentMethod || undefined,
      recurringTransactionId: null,
      tags: (values.tags || "").split(",").map((item) => item.trim()).filter(Boolean),
    };

    try {
      if (editing) {
        await transactionsApi.update(accessToken, editing.id, payload);
      } else {
        await transactionsApi.create(accessToken, payload);
      }
      setExportMessage(null);
      resetForm();
      await Promise.all([bootstrap(), loadTransactions()]);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save transaction.");
    }
  }

  async function deleteTransaction(id: string) {
    if (!accessToken || !window.confirm("Delete this transaction and reverse its balance effect?")) return;
    await transactionsApi.remove(accessToken, id);
    await Promise.all([bootstrap(), loadTransactions()]);
  }

  async function exportTransactions() {
    if (!accessToken) return;
    setIsExporting(true);
    setExportMessage(null);
    try {
      const fileName = await transactionsApi.exportCsv(accessToken, buildFilterQuery());
      setExportMessage(`Exported ${fileName}.`);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to export transactions.");
    } finally {
      setIsExporting(false);
    }
  }

  const visibleCategories = useMemo(() => {
    if (type === "1") return categories.filter((item) => item.type === "Income");
    if (type === "2") return categories.filter((item) => item.type === "Expense");
    return [];
  }, [categories, type]);

  const paymentMethodOptions = useMemo(() => Array.from(new Set([...commonPaymentMethods, ...transactions.map((item) => item.paymentMethod?.trim()).filter(Boolean) as string[]])).sort((left, right) => left.localeCompare(right)), [transactions]);

  if (loading) return <PageLoader label="Loading transactions" />;

  return (
    <div className="page-stack">
      <SectionHeader
        title="Transactions"
        description="Record income, expenses, and transfers with server-enforced balance safety."
        action={
          <div className="section-header__actions">
            <button type="button" className="ghost-button" onClick={exportTransactions} disabled={isExporting} aria-label="Export filtered transactions to CSV">
              {isExporting ? "Exporting..." : "Export CSV"}
            </button>
            <Button type="button" onClick={resetForm}>New transaction</Button>
          </div>
        }
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {/* <Alert message="Active rules are applied only when you create a new transaction. Editing an existing transaction will not re-run rules in this version." /> */}
      {exportMessage ? <p className="form-status" role="status" aria-live="polite">{exportMessage}</p> : null}
      <div className="transactions-layout">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit transaction" : "Add transaction"}</h3>
            <p>{editing ? "Balance effects are reversed and recalculated on save." : "Transfers move value between two accounts in one operation."}</p>
          </div>
          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <div className="field-grid">
              <Field label="Type" error={errors.type?.message}>
                <SelectField {...register("type")}>
                  <option value="1">Income</option>
                  <option value="2">Expense</option>
                  <option value="3">Transfer</option>
                </SelectField>
              </Field>
              <Field label="Amount" error={errors.amount?.message}><input {...register("amount")} type="number" step="0.01" /></Field>
            </div>
            <div className="field-grid">
              <Field label={type === "3" ? "Source account" : "Account"} error={errors.accountId?.message}>
                <SelectField {...register("accountId")}>
                  <option value="">Select account</option>
                  {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                </SelectField>
              </Field>
              {type === "3" ? (
                <Field label="Destination account" error={errors.transferAccountId?.message}>
                  <SelectField {...register("transferAccountId")}>
                    <option value="">Select destination</option>
                    {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                  </SelectField>
                </Field>
              ) : (
                <Field label="Category" error={errors.categoryId?.message}>
                  <SelectField {...register("categoryId")}>
                    <option value="">Select category</option>
                    {visibleCategories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
                  </SelectField>
                </Field>
              )}
            </div>
            <Field label="Date" error={errors.dateUtc?.message}><input {...register("dateUtc")} type="date" /></Field>
            <div className="field-grid">
              <Field label="Merchant" error={errors.merchant?.message} hint="Use the name of the shop, website, company, or person involved.">
                <input {...register("merchant")} placeholder="Whom you paid?" />
              </Field>
              <Controller
                name="paymentMethod"
                control={control}
                render={({ field }) => (
                  <Field label="Payment method" error={errors.paymentMethod?.message} hint="Dropdown-first with manual fallback for new methods.">
                    <div className="dropdown-field-stack">
                      <SelectField value={paymentManual ? "__manual__" : field.value || ""} onChange={(event) => {
                        if (event.target.value === "__manual__") {
                          setPaymentManual(true);
                          field.onChange("");
                          return;
                        }
                        setPaymentManual(false);
                        field.onChange(event.target.value);
                      }}>
                        <option value="">Select payment method</option>
                        {paymentMethodOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                        <option value="__manual__">Enter manually</option>
                      </SelectField>
                      {paymentManual ? <input value={field.value || ""} onChange={field.onChange} placeholder="Enter payment method" /> : null}
                    </div>
                  </Field>
                )}
              />
            </div>
            <Field label="Note" error={errors.note?.message}><input {...register("note")} placeholder="Optional note" /></Field>
            <Field label="Tags" hint="Optional comma-separated labels for extra grouping." error={errors.tags?.message}>
              <input {...register("tags")} placeholder="office, team-lunch, reimbursable, Food, Travel, Salary" />
            </Field>
            <Button type="submit" loading={isSubmitting}>{editing ? "Save transaction" : "Add transaction"}</Button>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>History</h3>
            <p>{totalCount} matching transactions</p>
          </div>
          <div className="filters-grid" aria-label="Transaction filters">
            <input value={filters.search} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, search: event.target.value })); }} placeholder="Search merchant or note" aria-label="Search merchant or note" />
            <SelectField value={filters.type} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, type: event.target.value })); }} aria-label="Filter by transaction type">
              <option value="">All types</option>
              <option value="1">Income</option>
              <option value="2">Expense</option>
              <option value="3">Transfer</option>
            </SelectField>
            <SelectField value={filters.accountId} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, accountId: event.target.value })); }} aria-label="Filter by account">
              <option value="">All accounts</option>
              {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
            </SelectField>
            <SelectField value={filters.categoryId} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, categoryId: event.target.value })); }} aria-label="Filter by category">
              <option value="">All categories</option>
              {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
            </SelectField>
            <input type="date" value={filters.startDateUtc} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, startDateUtc: event.target.value })); }} aria-label="Filter by start date" />
            <input type="date" value={filters.endDateUtc} onChange={(event) => { setPage(1); setFilters((current) => ({ ...current, endDateUtc: event.target.value })); }} aria-label="Filter by end date" />
          </div>
          {transactions.length === 0 ? (
            <EmptyState title="No transactions found" description="Add a transaction or broaden the filters to see more results." />
          ) : (
            <div className="transactions-table">
              {transactions.map((item) => (
                <article key={item.id} className="transaction-row">
                  <div>
                    <strong>{item.merchant || item.categoryName || item.type}</strong>
                    <p>{item.accountName}{item.transferAccountName ? ` to ${item.transferAccountName}` : ""}</p>
                    <small>{item.note || formatDate(item.dateUtc)}</small>
                  </div>
                  <div className="transaction-row__aside">
                    <span className="transaction-type-pill">{item.type}</span>
                    <strong>{formatCurrency(item.amount)}</strong>
                    <div className="inline-actions">
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => editTransaction(item)} aria-label={`Edit transaction ${item.id}`}>Edit</button>
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => deleteTransaction(item.id)} aria-label={`Delete transaction ${item.id}`}>Delete</button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
          <div className="pagination-row">
            <button type="button" className="ghost-button ghost-button--small" disabled={page === 1} onClick={() => setPage((current) => current - 1)}>Previous</button>
            <span>Page {page} of {Math.max(1, Math.ceil(totalCount / 10))}</span>
            <button type="button" className="ghost-button ghost-button--small" disabled={page >= Math.ceil(totalCount / 10)} onClick={() => setPage((current) => current + 1)}>Next</button>
          </div>
        </section>
      </div>
    </div>
  );
}



