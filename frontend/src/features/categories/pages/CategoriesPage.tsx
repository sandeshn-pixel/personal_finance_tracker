import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { ApiError } from "../../../shared/lib/api/client";
import { categoriesApi, type CategoryDto } from "../api/categoriesApi";

const categorySchema = z.object({
  name: z.string().trim().min(1, "Category name is required.").max(100, "Category name is too long."),
  type: z.enum(["1", "2"]),
});

type CategoryFormValues = z.infer<typeof categorySchema>;

const typeLabels: Record<CategoryDto["type"], string> = {
  Income: "Income",
  Expense: "Expense",
};

export function CategoriesPage() {
  const { accessToken } = useAuth();
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<CategoryDto | null>(null);

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: "", type: "2" },
  });

  useEffect(() => {
    void loadCategories(includeArchived);
  }, [accessToken, includeArchived]);

  async function loadCategories(showArchived: boolean) {
    if (!accessToken) {
      return;
    }

    setLoading(true);
    try {
      const response = await categoriesApi.list(accessToken, showArchived);
      setCategories(response);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load categories.");
    } finally {
      setLoading(false);
    }
  }

  function resetForCreate() {
    setEditing(null);
    reset({ name: "", type: "2" });
  }

  function startEdit(category: CategoryDto) {
    setEditing(category);
    reset({
      name: category.name,
      type: category.type === "Income" ? "1" : "2",
    });
  }

  async function onSubmit(values: CategoryFormValues) {
    if (!accessToken) {
      return;
    }

    try {
      if (editing) {
        await categoriesApi.update(accessToken, editing.id, { name: values.name });
      } else {
        await categoriesApi.create(accessToken, { name: values.name, type: Number(values.type) });
      }

      resetForCreate();
      await loadCategories(includeArchived);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to save category.");
    }
  }

  async function archiveCategory(category: CategoryDto) {
    if (!accessToken || category.isArchived || !window.confirm(`Archive ${category.name}?`)) {
      return;
    }

    try {
      await categoriesApi.archive(accessToken, category.id);
      await loadCategories(includeArchived);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to archive category.");
    }
  }

  const activeCount = useMemo(() => categories.filter((item) => !item.isArchived).length, [categories]);
  const incomeCategories = useMemo(() => categories.filter((item) => item.type === "Income"), [categories]);
  const expenseCategories = useMemo(() => categories.filter((item) => item.type === "Expense"), [categories]);

  if (loading) {
    return <PageLoader label="Loading categories" />;
  }

  return (
    <div className="page-stack">
      <SectionHeader
        title="Categories"
        description="Manage the income and expense categories used by transactions, budgets, and reports."
        action={<Button type="button" onClick={resetForCreate}>New category</Button>}
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      <div className="accounts-grid">
        <section className="panel-card panel-card--form">
          <div className="panel-card__header">
            <h3>{editing ? "Edit category" : "Create category"}</h3>
            <p>{editing ? "Rename the category safely without changing its type." : "Custom categories work alongside the default system categories."}</p>
          </div>
          <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
            <Field label="Category name" error={errors.name?.message}>
              <input {...register("name")} placeholder="Dining out" />
            </Field>
            <Field label="Category type" error={errors.type?.message} hint={editing ? "Type cannot be changed after creation." : undefined}>
              <SelectField {...register("type")} disabled={Boolean(editing)}>
                <option value="2">Expense</option>
                <option value="1">Income</option>
              </SelectField>
            </Field>
            <div className="button-row">
              <Button type="submit" loading={isSubmitting}>{editing ? "Save changes" : "Create category"}</Button>
              {editing ? <button type="button" className="ghost-button" onClick={resetForCreate}>Cancel</button> : null}
            </div>
          </form>
        </section>

        <section className="panel-card">
          <div className="panel-card__header panel-card__header--inline">
            <div>
              <h3>Category list</h3>
              <p>{activeCount} active categories available.</p>
            </div>
            <label className="checkbox-row">
              <input type="checkbox" checked={includeArchived} onChange={(event) => setIncludeArchived(event.target.checked)} />
              Include archived
            </label>
          </div>

          {categories.length === 0 ? (
            <EmptyState title="No categories yet" description="Create your first category to organize transactions and budgets more precisely." />
          ) : (
            <div className="goal-sections">
              <div>
                <p className="subsection-title">Expense categories</p>
                <div className="account-list">
                  {expenseCategories.map((category) => (
                    <article key={category.id} className={`account-card ${category.isArchived ? "account-card--archived" : ""}`}>
                      <div>
                        <strong>{category.name}</strong>
                        <p>{typeLabels[category.type]} · {category.isSystem ? "System" : "Custom"}</p>
                        <small>{category.isArchived ? "Archived" : "Available for transactions and budgets"}</small>
                      </div>
                      <div className="account-card__aside">
                        <div className="inline-actions">
                          <button type="button" className="ghost-button ghost-button--small" onClick={() => startEdit(category)}>Edit</button>
                          {!category.isArchived ? <button type="button" className="ghost-button ghost-button--small" onClick={() => void archiveCategory(category)}>Archive</button> : null}
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </div>
              <div>
                <p className="subsection-title">Income categories</p>
                <div className="account-list">
                  {incomeCategories.map((category) => (
                    <article key={category.id} className={`account-card ${category.isArchived ? "account-card--archived" : ""}`}>
                      <div>
                        <strong>{category.name}</strong>
                        <p>{typeLabels[category.type]} · {category.isSystem ? "System" : "Custom"}</p>
                        <small>{category.isArchived ? "Archived" : "Available for transactions and reports"}</small>
                      </div>
                      <div className="account-card__aside">
                        <div className="inline-actions">
                          <button type="button" className="ghost-button ghost-button--small" onClick={() => startEdit(category)}>Edit</button>
                          {!category.isArchived ? <button type="button" className="ghost-button ghost-button--small" onClick={() => void archiveCategory(category)}>Archive</button> : null}
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </div>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
