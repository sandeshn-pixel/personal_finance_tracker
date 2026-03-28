import { useEffect, useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useTheme, type ThemeName } from "../../../app/providers/ThemeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { settingsApi, type SampleDataSeedStatusDto, type UserSettingsDto } from "../api/settingsApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { SelectField } from "../../../shared/components/SelectField";
import { ApiError } from "../../../shared/lib/api/client";

const profileSchema = z.object({
  firstName: z.string().trim().min(1, "First name is required.").max(100),
  lastName: z.string().trim().min(1, "Last name is required.").max(100),
  email: z.string().trim().email("Use a valid email.").max(256),
});

const passwordSchema = z.object({
  currentPassword: z.string().min(1, "Current password is required."),
  newPassword: z.string().min(8, "Password must be at least 8 characters.").max(128)
    .regex(/[A-Z]/, "Password must contain an uppercase letter.")
    .regex(/[a-z]/, "Password must contain a lowercase letter.")
    .regex(/[0-9]/, "Password must contain a number.")
    .regex(/[^a-zA-Z0-9]/, "Password must contain a special character."),
  confirmPassword: z.string(),
}).refine((value) => value.newPassword === value.confirmPassword, {
  message: "Passwords must match.",
  path: ["confirmPassword"],
});

const preferencesSchema = z.object({
  preferredCurrencyCode: z.string().trim().length(3, "Use a 3-letter currency code."),
  dateFormat: z.enum(["dd MMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"]),
  landingPage: z.enum(["/dashboard", "/transactions", "/accounts", "/budgets", "/goals", "/reports", "/recurring", "/settings"]),
  theme: z.enum(["slate", "warm", "dark"]),
});

const notificationSchema = z.object({
  budgetWarningsEnabled: z.boolean(),
  goalRemindersEnabled: z.boolean(),
  recurringRemindersEnabled: z.boolean(),
});

const financialSchema = z.object({
  defaultAccountId: z.string().optional().or(z.literal("")),
  defaultPaymentMethod: z.string().trim().max(64).optional().or(z.literal("")),
  defaultBudgetAlertThresholdPercent: z.coerce.number().min(1).max(100),
});

type ProfileFormValues = z.infer<typeof profileSchema>;
type PasswordFormValues = z.infer<typeof passwordSchema>;
type PreferencesFormValues = z.infer<typeof preferencesSchema>;
type NotificationFormValues = z.infer<typeof notificationSchema>;
type FinancialFormValues = z.infer<typeof financialSchema>;
type SettingsSectionKey = "account" | "security" | "preferences" | "notifications" | "financial-defaults" | "workspace-tools";

const themeOptions: Array<{ value: ThemeName; label: string; description: string; swatches: [string, string, string] }> = [
  { value: "slate", label: "Slate teal", description: "Clean neutral workspace with teal accents.", swatches: ["#222831", "#00ADB5", "#EEEEEE"] },
  { value: "warm", label: "Warm premium", description: "Softer amber surfaces with finance-friendly warmth.", swatches: ["#4B2E2B", "#C08552", "#FFF8F0"] },
  { value: "dark", label: "Dark mode", description: "Deep slate surfaces with restrained cyan contrast.", swatches: ["#0F151B", "#5CCBD2", "#1F2A35"] },
];

const settingsSections: Array<{ key: SettingsSectionKey; label: string; caption: string }> = [
  { key: "account", label: "Account", caption: "Profile and sign-in identity" },
  { key: "security", label: "Security", caption: "Password and active sessions" },
  { key: "preferences", label: "Preferences", caption: "Display defaults and theme" },
  { key: "notifications", label: "Notifications", caption: "Reminder and alert controls" },
  { key: "financial-defaults", label: "Financial defaults", caption: "Fast-entry defaults for daily use" },
  { key: "workspace-tools", label: "Workspace tools", caption: "Safe bootstrap actions for new workspaces" },
];

export function SettingsPage() {
  const { accessToken, refreshSession, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const [settings, setSettings] = useState<UserSettingsDto | null>(null);
  const [sampleDataStatus, setSampleDataStatus] = useState<SampleDataSeedStatusDto | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeSection, setActiveSection] = useState<SettingsSectionKey>("account");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSeedingSampleData, setIsSeedingSampleData] = useState(false);

  const profileForm = useForm<ProfileFormValues>({ resolver: zodResolver(profileSchema) });
  const passwordForm = useForm<PasswordFormValues>({ resolver: zodResolver(passwordSchema), defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" } });
  const preferencesForm = useForm<PreferencesFormValues>({ resolver: zodResolver(preferencesSchema) });
  const notificationForm = useForm<NotificationFormValues>({ resolver: zodResolver(notificationSchema) });
  const financialForm = useForm<FinancialFormValues>({ resolver: zodResolver(financialSchema) });

  const selectedTheme = preferencesForm.watch("theme");
  const currentSection = useMemo(() => settingsSections.find((section) => section.key === activeSection) ?? settingsSections[0], [activeSection]);

  useEffect(() => { void load(); }, [accessToken]);

  useEffect(() => {
    if (selectedTheme) {
      setTheme(selectedTheme);
    }
  }, [selectedTheme, setTheme]);

  async function load() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [settingsResponse, accountsResponse, sampleDataStatusResponse] = await Promise.all([
        settingsApi.get(accessToken),
        accountsApi.list(accessToken),
        settingsApi.getSampleDataStatus(accessToken),
      ]);
      setSettings(settingsResponse);
      setAccounts(accountsResponse.filter((item) => !item.isArchived));
      setSampleDataStatus(sampleDataStatusResponse);
      hydrateForms(settingsResponse);
      setTheme(settingsResponse.preferences.theme);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load settings.");
    } finally {
      setLoading(false);
    }
  }

  function hydrateForms(value: UserSettingsDto) {
    profileForm.reset(value.profile);
    preferencesForm.reset(value.preferences);
    notificationForm.reset(value.notifications);
    financialForm.reset({
      defaultAccountId: value.financialDefaults.defaultAccountId ?? "",
      defaultPaymentMethod: value.financialDefaults.defaultPaymentMethod ?? "",
      defaultBudgetAlertThresholdPercent: value.financialDefaults.defaultBudgetAlertThresholdPercent,
    });
    passwordForm.reset({ currentPassword: "", newPassword: "", confirmPassword: "" });
  }

  function handleError(error: unknown, fallback: string) {
    setSuccessMessage(null);
    setErrorMessage(error instanceof ApiError ? error.message : fallback);
  }

  async function saveProfile(values: ProfileFormValues) {
    if (!accessToken) return;
    try {
      await settingsApi.updateProfile(accessToken, values);
      await refreshSession();
      setSuccessMessage("Profile updated.");
      setErrorMessage(null);
      await load();
    } catch (error) {
      handleError(error, "Unable to update profile.");
    }
  }

  async function savePassword(values: PasswordFormValues) {
    if (!accessToken) return;
    try {
      await settingsApi.changePassword(accessToken, { currentPassword: values.currentPassword, newPassword: values.newPassword });
      passwordForm.reset({ currentPassword: "", newPassword: "", confirmPassword: "" });
      setSuccessMessage("Password updated.");
      setErrorMessage(null);
    } catch (error) {
      handleError(error, "Unable to change password.");
    }
  }

  async function savePreferences(values: PreferencesFormValues) {
    if (!accessToken) return;
    try {
      const response = await settingsApi.updatePreferences(accessToken, values);
      setTheme(response.theme);
      setSettings((current) => current ? { ...current, preferences: response } : current);
      setSuccessMessage("Preferences updated.");
      setErrorMessage(null);
    } catch (error) {
      handleError(error, "Unable to update preferences.");
    }
  }

  async function saveNotifications(values: NotificationFormValues) {
    if (!accessToken) return;
    try {
      const response = await settingsApi.updateNotifications(accessToken, values);
      setSettings((current) => current ? { ...current, notifications: response } : current);
      setSuccessMessage("Notification preferences updated.");
      setErrorMessage(null);
    } catch (error) {
      handleError(error, "Unable to update notifications.");
    }
  }

  async function saveFinancialDefaults(values: FinancialFormValues) {
    if (!accessToken) return;
    try {
      const response = await settingsApi.updateFinancialDefaults(accessToken, {
        defaultAccountId: values.defaultAccountId || null,
        defaultPaymentMethod: values.defaultPaymentMethod || null,
        defaultBudgetAlertThresholdPercent: values.defaultBudgetAlertThresholdPercent,
      });
      setSettings((current) => current ? { ...current, financialDefaults: response } : current);
      setSuccessMessage("Financial defaults updated.");
      setErrorMessage(null);
    } catch (error) {
      handleError(error, "Unable to update financial defaults.");
    }
  }

  async function logoutAllSessions() {
    if (!accessToken || !window.confirm("Log out all sessions? You will need to sign in again.")) {
      return;
    }

    try {
      await settingsApi.logoutAll(accessToken);
      await logout();
      window.location.href = "/login";
    } catch (error) {
      handleError(error, "Unable to log out all sessions.");
    }
  }

  async function seedSampleData() {
    if (!accessToken || !sampleDataStatus?.canRunSeed) {
      return;
    }

    const confirmed = window.confirm("Add a carefully prepared 3-month sample history to this workspace? This is intended for a new workspace before any real transactions are recorded.");
    if (!confirmed) {
      return;
    }

    setIsSeedingSampleData(true);
    try {
      const response = await settingsApi.seedSampleData(accessToken);
      setSuccessMessage(response.message);
      setErrorMessage(null);
      await load();
    } catch (error) {
      handleError(error, "Unable to add sample data.");
    } finally {
      setIsSeedingSampleData(false);
    }
  }

  function renderSection() {
    switch (activeSection) {
      case "account":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Account</h3>
                <p>Update your core profile identity and contact details.</p>
              </div>
              <span className="status-badge status-badge--default">Profile</span>
            </div>
            <form className="form-stack" onSubmit={profileForm.handleSubmit(saveProfile)} noValidate>
              <div className="field-grid">
                <Field label="First name" error={profileForm.formState.errors.firstName?.message}><input {...profileForm.register("firstName")} /></Field>
                <Field label="Last name" error={profileForm.formState.errors.lastName?.message}><input {...profileForm.register("lastName")} /></Field>
              </div>
              <Field label="Email" error={profileForm.formState.errors.email?.message}><input type="email" {...profileForm.register("email")} /></Field>
              <Button type="submit" loading={profileForm.formState.isSubmitting}>Save profile</Button>
            </form>
          </section>
        );
      case "security":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Security</h3>
                <p>Keep access tight with a strong password and session control.</p>
              </div>
              <span className="status-badge status-badge--warning">Secure</span>
            </div>
            <form className="form-stack" onSubmit={passwordForm.handleSubmit(savePassword)} noValidate>
              <Field label="Current password" error={passwordForm.formState.errors.currentPassword?.message}><input type="password" {...passwordForm.register("currentPassword")} /></Field>
              <Field label="New password" error={passwordForm.formState.errors.newPassword?.message}><input type="password" {...passwordForm.register("newPassword")} /></Field>
              <Field label="Confirm new password" error={passwordForm.formState.errors.confirmPassword?.message}><input type="password" {...passwordForm.register("confirmPassword")} /></Field>
              <div className="button-row">
                <Button type="submit" loading={passwordForm.formState.isSubmitting}>Change password</Button>
                <button type="button" className="ghost-button" onClick={() => void logoutAllSessions()}>Logout all sessions</button>
              </div>
            </form>
          </section>
        );
      case "preferences":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Preferences</h3>
                <p>Control display defaults, landing behavior, and visual theme.</p>
              </div>
              <span className="status-badge status-badge--default">Personalized</span>
            </div>
            <form className="form-stack" onSubmit={preferencesForm.handleSubmit(savePreferences)} noValidate>
              <div className="field-grid">
                <Field label="Preferred currency" error={preferencesForm.formState.errors.preferredCurrencyCode?.message}><input maxLength={3} {...preferencesForm.register("preferredCurrencyCode")} /></Field>
                <Field label="Date format" error={preferencesForm.formState.errors.dateFormat?.message}>
                  <SelectField {...preferencesForm.register("dateFormat")}>
                    <option value="dd MMM yyyy">dd MMM yyyy</option>
                    <option value="dd/MM/yyyy">dd/MM/yyyy</option>
                    <option value="MM/dd/yyyy">MM/dd/yyyy</option>
                    <option value="yyyy-MM-dd">yyyy-MM-dd</option>
                  </SelectField>
                </Field>
              </div>
              <Field label="Landing page" error={preferencesForm.formState.errors.landingPage?.message}>
                <SelectField {...preferencesForm.register("landingPage")}>
                  <option value="/dashboard">Dashboard</option>
                  <option value="/transactions">Transactions</option>
                  <option value="/accounts">Accounts</option>
                  <option value="/categories">Categories</option>
                  <option value="/budgets">Budgets</option>
                  <option value="/goals">Goals</option>
                  <option value="/reports">Reports</option>
                  <option value="/recurring">Recurring</option>
                  <option value="/settings">Settings</option>
                </SelectField>
              </Field>
              <div className="field">
                <span className="field-label">Theme</span>
                <div className="theme-option-grid" role="radiogroup" aria-label="Theme choices">
                  {themeOptions.map((option) => {
                    const isSelected = selectedTheme === option.value;
                    return (
                      <button
                        key={option.value}
                        type="button"
                        className={`theme-option-card${isSelected ? " theme-option-card--selected" : ""}`}
                        onClick={() => preferencesForm.setValue("theme", option.value, { shouldDirty: true, shouldValidate: true })}
                        aria-pressed={isSelected}
                      >
                        <span className={`theme-option-card__preview theme-option-card__preview--${option.value}`} aria-hidden="true">
                          {option.swatches.map((swatch) => <span key={swatch} style={{ background: swatch }} />)}
                        </span>
                        <span className="theme-option-card__content">
                          <strong>{option.label}</strong>
                          <small>{option.description}</small>
                        </span>
                      </button>
                    );
                  })}
                </div>
                {preferencesForm.formState.errors.theme?.message ? <span className="field-error">{preferencesForm.formState.errors.theme.message}</span> : null}
              </div>
              <Button type="submit" loading={preferencesForm.formState.isSubmitting}>Save preferences</Button>
            </form>
          </section>
        );
      case "notifications":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Notifications</h3>
                <p>Choose which reminders stay active across budgets, goals, and recurring rules.</p>
              </div>
              <span className="status-badge status-badge--default">In-app</span>
            </div>
            <form className="form-stack" onSubmit={notificationForm.handleSubmit(saveNotifications)} noValidate>
              <label className="toggle-row"><Controller name="budgetWarningsEnabled" control={notificationForm.control} render={({ field }) => <input type="checkbox" checked={field.value} onChange={(event) => field.onChange(event.target.checked)} />} /><span>Budget warning reminders</span></label>
              <label className="toggle-row"><Controller name="goalRemindersEnabled" control={notificationForm.control} render={({ field }) => <input type="checkbox" checked={field.value} onChange={(event) => field.onChange(event.target.checked)} />} /><span>Goal reminder notifications</span></label>
              <label className="toggle-row"><Controller name="recurringRemindersEnabled" control={notificationForm.control} render={({ field }) => <input type="checkbox" checked={field.value} onChange={(event) => field.onChange(event.target.checked)} />} /><span>Recurring rule reminders</span></label>
              <Button type="submit" loading={notificationForm.formState.isSubmitting}>Save notifications</Button>
            </form>
          </section>
        );
      case "financial-defaults":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Financial defaults</h3>
                <p>Set faster defaults for transaction entry and monthly budget setup.</p>
              </div>
              <span className="status-badge status-badge--default">Workflow</span>
            </div>
            <form className="form-stack" onSubmit={financialForm.handleSubmit(saveFinancialDefaults)} noValidate>
              <div className="field-grid">
                <Field label="Default account" error={financialForm.formState.errors.defaultAccountId?.message}>
                  <SelectField {...financialForm.register("defaultAccountId")}>
                    <option value="">None</option>
                    {accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}
                  </SelectField>
                </Field>
                <Field label="Default payment method" error={financialForm.formState.errors.defaultPaymentMethod?.message}><input {...financialForm.register("defaultPaymentMethod")} placeholder="UPI, Card, Cash" /></Field>
              </div>
              <Field label="Default budget alert threshold %" error={financialForm.formState.errors.defaultBudgetAlertThresholdPercent?.message}><input type="number" min={1} max={100} {...financialForm.register("defaultBudgetAlertThresholdPercent")} /></Field>
              <Button type="submit" loading={financialForm.formState.isSubmitting}>Save financial defaults</Button>
            </form>
          </section>
        );
      case "workspace-tools":
        return (
          <section className="panel-card panel-card--form settings-panel-card">
            <div className="panel-card__header settings-panel-card__header">
              <div>
                <h3>Workspace tools</h3>
                <p>Bootstrap a realistic sample ledger for demos and product walkthroughs without relying on startup seeding.</p>
              </div>
              <span className="status-badge status-badge--default">Manual</span>
            </div>
            <div className="form-stack">
              <Alert
                message={sampleDataStatus?.canRunSeed
                  ? "Sample data is currently available because this workspace has no recorded transactions yet."
                  : "Sample data has already been used or real transactions now exist, so this bootstrap action is locked for safety."}
                variant={sampleDataStatus?.canRunSeed ? "info" : "success"}
              />
              <div className="stats-grid">
                <div className="stat-card">
                  <span>Active accounts</span>
                  <strong>{sampleDataStatus?.activeAccountCount ?? 0}</strong>
                </div>
                <div className="stat-card">
                  <span>Goals</span>
                  <strong>{sampleDataStatus?.goalCount ?? 0}</strong>
                </div>
                <div className="stat-card">
                  <span>Recurring rules</span>
                  <strong>{sampleDataStatus?.recurringRuleCount ?? 0}</strong>
                </div>
              </div>
              <p className="form-status">
                The seed creates a sensible 3-month history with income, living expenses, savings transfers, budgets, goal progress, and recurring rules so dashboard charts and trends are immediately useful.
              </p>
              <div className="button-row">
                <Button type="button" loading={isSeedingSampleData} onClick={() => void seedSampleData()} disabled={!sampleDataStatus?.canRunSeed}>
                  {sampleDataStatus?.canRunSeed ? "Add sample workspace data" : "Sample workspace already locked"}
                </Button>
              </div>
            </div>
          </section>
        );
    }
  }

  if (loading) return <PageLoader label="Loading settings" />;
  if (!settings) return <Alert message={errorMessage ?? "Settings are unavailable."} />;

  return (
    <div className="page-stack">
      <SectionHeader title="Settings" description="Manage profile, security, preferences, notifications, defaults, theme, and workspace setup tools." />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {successMessage ? <Alert message={successMessage} variant="success" /> : null}

      <div className="settings-workspace">
        <aside className="panel-card settings-nav-card" aria-label="Settings sections">
          <div className="panel-card__header">
            <h3>Workspace</h3>
            <p>Move through each settings area without losing context.</p>
          </div>
          <div className="settings-nav-list">
            {settingsSections.map((section) => (
              <button
                key={section.key}
                type="button"
                className={`settings-nav-item${activeSection === section.key ? " settings-nav-item--active" : ""}`}
                onClick={() => setActiveSection(section.key)}
              >
                <strong>{section.label}</strong>
                <small>{section.caption}</small>
              </button>
            ))}
          </div>
        </aside>

        <div className="settings-content-stack">
          <div className="panel-card settings-current-section">
            <p className="eyebrow">Now editing</p>
            <h2>{currentSection.label}</h2>
            <p>{currentSection.caption}</p>
          </div>
          {renderSection()}
        </div>
      </div>
    </div>
  );
}

