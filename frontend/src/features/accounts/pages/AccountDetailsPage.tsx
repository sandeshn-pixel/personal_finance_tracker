import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi, type AccountDto, type AccountMemberDto, type AccountPendingInviteDto } from "../api/accountsApi";
import { transactionsApi, type TransactionDto } from "../../transactions/api/transactionsApi";
import { goalsApi, type GoalDto } from "../../goals/api/goalsApi";
import { recurringTransactionsApi, type RecurringTransactionDto } from "../../recurring/api/recurringTransactionsApi";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate } from "../../../shared/lib/format";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { SelectField } from "../../../shared/components/SelectField";

const typeLabels: Record<AccountDto["type"], string> = {
  BankAccount: "Bank account",
  CreditCard: "Credit card",
  CashWallet: "Cash wallet",
  SavingsAccount: "Savings account",
};

const inviteSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  role: z.enum(["1", "2"]),
});

type InviteFormValues = z.infer<typeof inviteSchema>;

export function AccountDetailsPage() {
  const { accountId } = useParams();
  const navigate = useNavigate();
  const { accessToken } = useAuth();
  const [account, setAccount] = useState<AccountDto | null>(null);
  const [members, setMembers] = useState<AccountMemberDto[]>([]);
  const [pendingInvites, setPendingInvites] = useState<AccountPendingInviteDto[]>([]);
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [linkedGoals, setLinkedGoals] = useState<GoalDto[]>([]);
  const [recurringRules, setRecurringRules] = useState<RecurringTransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [memberMessage, setMemberMessage] = useState<string | null>(null);
  const [memberPreviewUrl, setMemberPreviewUrl] = useState<string | null>(null);
  const [memberError, setMemberError] = useState<string | null>(null);
  const [updatingMemberId, setUpdatingMemberId] = useState<string | null>(null);
  const [revokingInviteId, setRevokingInviteId] = useState<string | null>(null);
  const [resendingInviteId, setResendingInviteId] = useState<string | null>(null);

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: "", role: "2" },
  });

  useEffect(() => {
    void load();
  }, [accessToken, accountId]);

  async function load() {
    if (!accessToken || !accountId) return;

    setLoading(true);
    try {
      const [accountResponse, membersResponse, transactionResponse, goals, recurring] = await Promise.all([
        accountsApi.get(accessToken, accountId),
        accountsApi.listMembers(accessToken, accountId),
        transactionsApi.list(accessToken, { accountId, page: 1, pageSize: 8 }),
        goalsApi.list(accessToken),
        recurringTransactionsApi.list(accessToken),
      ]);

      let invitesResponse: AccountPendingInviteDto[] = [];
      if (accountResponse.currentUserRole === "Owner") {
        invitesResponse = await accountsApi.listPendingInvites(accessToken, accountId);
      }

      setAccount(accountResponse);
      setMembers(membersResponse);
      setPendingInvites(invitesResponse);
      setTransactions(transactionResponse.items);
      setLinkedGoals(goals.filter((goal) => goal.linkedAccountId === accountId));
      setRecurringRules(recurring.filter((rule) => rule.accountId === accountId || rule.transferAccountId === accountId));
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load account details.");
    } finally {
      setLoading(false);
    }
  }

  async function onInvite(values: InviteFormValues) {
    if (!accessToken || !accountId) return;

    try {
      const response = await accountsApi.inviteMember(accessToken, accountId, { email: values.email, role: Number(values.role) as 1 | 2 });
      setPendingInvites((current) => [response.invite, ...current.filter((item) => item.id !== response.invite.id)]);
      setMemberError(null);
      setMemberPreviewUrl(response.previewUrl ?? null);
      setMemberMessage(`Prepared an invite for ${response.invite.email} as ${response.invite.role.toLowerCase()}. Access starts only after they accept it.`);
      reset({ email: "", role: values.role });
      await load();
    } catch (error) {
      setMemberError(error instanceof ApiError ? error.message : "Unable to add member.");
    }
  }

  async function changeRole(member: AccountMemberDto, role: "1" | "2") {
    if (!accessToken || !accountId) return;
    setUpdatingMemberId(member.userId);
    try {
      const updated = await accountsApi.updateMember(accessToken, accountId, member.userId, { role: Number(role) as 1 | 2 });
      setMembers((current) => current.map((item) => item.userId === updated.userId ? updated : item));
      setMemberMessage(`Updated ${updated.fullName} to ${updated.role.toLowerCase()}.`);
      setMemberPreviewUrl(null);
      setMemberError(null);
    } catch (error) {
      setMemberError(error instanceof ApiError ? error.message : "Unable to update member role.");
    } finally {
      setUpdatingMemberId(null);
    }
  }

  async function removeMember(member: AccountMemberDto) {
    if (!accessToken || !accountId || !window.confirm(`Remove ${member.fullName} from this shared account?`)) return;
    setUpdatingMemberId(member.userId);
    try {
      await accountsApi.removeMember(accessToken, accountId, member.userId);
      setMembers((current) => current.filter((item) => item.userId !== member.userId));
      setMemberMessage(`Removed ${member.fullName} from this account.`);
      setMemberPreviewUrl(null);
      setMemberError(null);
    } catch (error) {
      setMemberError(error instanceof ApiError ? error.message : "Unable to remove member.");
    } finally {
      setUpdatingMemberId(null);
    }
  }

  async function resendInvite(invite: AccountPendingInviteDto) {
    if (!accessToken || !accountId) return;

    setResendingInviteId(invite.id);
    try {
      const response = await accountsApi.resendInvite(accessToken, accountId, invite.id);
      setPendingInvites((current) => current.map((item) => item.id === response.invite.id ? response.invite : item));
      setMemberMessage(`Resent the invite for ${response.invite.email}. The new link is active until ${formatDate(response.invite.expiresUtc)}.`);
      setMemberPreviewUrl(response.previewUrl ?? null);
      setMemberError(null);
    } catch (error) {
      setMemberError(error instanceof ApiError ? error.message : "Unable to resend invite.");
    } finally {
      setResendingInviteId(null);
    }
  }

  async function revokeInvite(invite: AccountPendingInviteDto) {
    if (!accessToken || !accountId || !window.confirm(`Revoke the pending invite for ${invite.email}?`)) return;

    setRevokingInviteId(invite.id);
    try {
      await accountsApi.revokeInvite(accessToken, accountId, invite.id);
      setPendingInvites((current) => current.filter((item) => item.id !== invite.id));
      setMemberMessage(`Revoked the pending invite for ${invite.email}.`);
      setMemberPreviewUrl(null);
      setMemberError(null);
    } catch (error) {
      setMemberError(error instanceof ApiError ? error.message : "Unable to revoke invite.");
    } finally {
      setRevokingInviteId(null);
    }
  }

  const netChange = useMemo(() => (account ? account.currentBalance - account.openingBalance : 0), [account]);
  const recentIncome = useMemo(() => transactions.filter((item) => item.type === "Income").reduce((total, item) => total + item.amount, 0), [transactions]);
  const recentExpense = useMemo(() => transactions.filter((item) => item.type === "Expense").reduce((total, item) => total + item.amount, 0), [transactions]);
  const canManageMembers = account?.currentUserRole === "Owner";

  if (loading) return <PageLoader label="Loading account details" />;
  if (!account) return <Alert message={errorMessage ?? "Account details are unavailable."} />;

  return (
    <div className="page-stack">
      <SectionHeader
        title={account.name}
        description="Review this account's balance, recent ledger activity, and shared access in one place."
        action={
          <div className="section-header__actions">
            <button type="button" className="ghost-button" onClick={() => navigate("/accounts")}>Back to accounts</button>
            <Link to="/transactions" className="ghost-button account-details__link-button">All transactions</Link>
          </div>
        }
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      {canManageMembers && pendingInvites.length > 0 ? (
        <div className="rules-preview">
          <strong>Sharing follow-up waiting.</strong>
          <p>You have {pendingInvites.length} pending shared-account invite{pendingInvites.length === 1 ? "" : "s"} to review for this account.</p>
        </div>
      ) : null}

      <section className="panel-card account-details-hero">
        <div className="account-details-hero__summary">
          <div>
            <p className="eyebrow">Account overview</p>
            <div className="account-card__title-row">
              <h3>{typeLabels[account.type]} | {account.currencyCode}</h3>
              {account.isShared ? <span className="status-badge status-badge--default">Shared</span> : null}
              <span className="status-badge status-badge--warning">{account.currentUserRole}</span>
            </div>
            <p>{account.institutionName || "Personal ledger"}{account.last4Digits ? ` | ending in ${account.last4Digits}` : ""}</p>
            <p>Owner: {account.ownerDisplayName} | {account.memberCount} member{account.memberCount === 1 ? "" : "s"}</p>
          </div>
          <div className="account-details-hero__balance">
            <span>Current balance</span>
            <strong>{formatCurrency(account.currentBalance, account.currencyCode)}</strong>
            <small>{account.isArchived ? "Archived account" : "Active account"}</small>
          </div>
        </div>
        <div className="stats-grid stats-grid--four">
          <article className="stat-card">
            <p>Opening balance</p>
            <strong>{formatCurrency(account.openingBalance, account.currencyCode)}</strong>
            <span>Initial ledger position.</span>
          </article>
          <article className={`stat-card ${netChange >= 0 ? "stat-card--positive" : "stat-card--negative"}`}>
            <p>Net change</p>
            <strong>{formatCurrency(netChange, account.currencyCode)}</strong>
            <span>Current balance minus opening balance.</span>
          </article>
          <article className="stat-card">
            <p>Shared access</p>
            <strong>{account.memberCount}</strong>
            <span>{account.isShared ? "People with explicit access to this account." : "Only you can access this account right now."}</span>
          </article>
          <article className="stat-card">
            <p>Your role</p>
            <strong>{account.currentUserRole}</strong>
            <span>{canManageMembers ? "You can manage sharing and account details." : "Your shared access is read-only or transaction-only."}</span>
          </article>
        </div>
      </section>

      <div className="account-details-layout">
        <section id="sharing" className="panel-card account-sharing-panel">
          <div className="panel-card__header">
            <h3>Shared with</h3>
            <p>Members who can access this account and their role boundaries.</p>
          </div>
          {memberError ? <Alert message={memberError} /> : null}
          {memberMessage ? <p className="form-status">{memberMessage}</p> : null}
          {memberPreviewUrl ? <p className="form-status"><a href={memberPreviewUrl} target="_blank" rel="noreferrer">Open the invite link preview</a></p> : null}
          {members.length === 1 ? (
            <EmptyState title="Only you have access" description="Invite an editor or viewer when you want this account to be shared." />
          ) : (
            <div className="simple-list">
              {members.map((member) => (
                <div key={member.userId} className="list-row list-row--stacked">
                  <div>
                    <div className="account-card__title-row">
                      <strong>{member.fullName}</strong>
                      <span className="status-badge status-badge--warning">{member.role}</span>
                      {member.isOwner ? <span className="status-badge status-badge--default">Owner</span> : null}
                    </div>
                    <p>{member.email}</p>
                    <small>
                      {member.isOwner
                        ? "Original owner of this account."
                        : `Invited by ${member.invitedByDisplayName ?? "someone on this account"} | Last changed by ${member.lastModifiedByDisplayName ?? "the owner"}`}
                    </small>
                  </div>
                  {!member.isOwner && canManageMembers ? (
                    <div className="account-member-actions">
                      <SelectField value={member.role === "Editor" ? "2" : "1"} onChange={(event) => void changeRole(member, event.target.value as "1" | "2")} disabled={updatingMemberId === member.userId}>
                        <option value="2">Editor</option>
                        <option value="1">Viewer</option>
                      </SelectField>
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => void removeMember(member)} disabled={updatingMemberId === member.userId}>Remove</button>
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
          )}

          <div className="panel-card__header" style={{ marginTop: 24 }}>
            <h3>Pending invites</h3>
            <p>Invites stay pending until the recipient signs in with the invited email and accepts access.</p>
          </div>
          {pendingInvites.length === 0 ? (
            <EmptyState title="No pending invites" description="New invites will appear here until the recipient accepts them or you revoke them." />
          ) : (
            <div className="simple-list">
              {pendingInvites.map((invite) => (
                <div key={invite.id} className="list-row list-row--stacked">
                  <div>
                    <div className="account-card__title-row">
                      <strong>{invite.email}</strong>
                      <span className="status-badge status-badge--default">{invite.isExpired ? "Expired" : "Pending"}</span>
                      <span className="status-badge status-badge--warning">{invite.role}</span>
                    </div>
                    <p>Invited by {invite.invitedByDisplayName}</p>
                    <small>Created {formatDate(invite.createdUtc)} | Expires {formatDate(invite.expiresUtc)}{invite.isExpired ? " | Needs resend" : ""}</small>
                  </div>
                  {canManageMembers ? (
                    <div className="account-member-actions">
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => void resendInvite(invite)} disabled={resendingInviteId === invite.id}>Resend</button>
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => void revokeInvite(invite)} disabled={revokingInviteId === invite.id}>Revoke</button>
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
          )}

          {canManageMembers ? (
            <form className="form-stack account-member-form" onSubmit={handleSubmit(onInvite)} noValidate>
              <Field label="Invite by email" error={errors.email?.message} hint="You can invite any valid email now. Access stays pending until the recipient signs in or creates an account with that same email and accepts the invite.">
                <input {...register("email")} placeholder="family.member@example.com" />
              </Field>
              <Field label="Role" error={errors.role?.message} hint="Editors can add or change transactions. Viewers can only see the account.">
                <SelectField {...register("role")}>
                  <option value="2">Editor</option>
                  <option value="1">Viewer</option>
                </SelectField>
              </Field>
              <Button type="submit" loading={isSubmitting}>Send invite</Button>
            </form>
          ) : (
            <div className="rules-preview">
              <strong>Member management is owner-only.</strong>
              <p>You can view who has access, but only the owner can invite, change roles, or remove members.</p>
            </div>
          )}
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Recent activity</h3>
            <p>Latest ledger entries affecting this account.</p>
          </div>
          {transactions.length === 0 ? (
            <EmptyState title="No activity yet" description="Transactions tied to this account will appear here once recorded." />
          ) : (
            <div className="simple-list">
              {transactions.map((item) => (
                <div key={item.id} className="list-row list-row--transaction-activity">
                  <div className={`activity-dot activity-dot--${item.type.toLowerCase()}`} />
                  <div className="transaction-activity__body">
                    <div className="transaction-activity__topline">
                      <strong className="transaction-activity__title">{item.merchant || item.categoryName || item.type}</strong>
                      <strong className="transaction-activity__amount-value">{formatCurrency(item.amount, account.currencyCode)}</strong>
                    </div>
                    <div className="transaction-activity__meta">
                      <span className={`transaction-type-pill transaction-type-pill--${item.type.toLowerCase()}`}>{item.type}</span>
                      <span>{item.categoryName || "Uncategorised"}</span>
                      <span>{formatDate(item.dateUtc)}</span>
                      <span>By {item.updatedByDisplayName}</span>
                    </div>
                    {item.note ? <p className="transaction-activity__note">{item.note}</p> : null}
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="account-details-layout">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Account pulse</h3>
            <p>Quick financial context from recent recorded activity.</p>
          </div>
          <div className="account-details-pulse">
            <div className="metric-card">
              <span>Recent income</span>
              <strong>{formatCurrency(recentIncome, account.currencyCode)}</strong>
              <p>Income transactions in the latest loaded activity for this account.</p>
            </div>
            <div className="metric-card">
              <span>Recent expense</span>
              <strong>{formatCurrency(recentExpense, account.currencyCode)}</strong>
              <p>Expense transactions in the latest loaded activity for this account.</p>
            </div>
          </div>
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Planning surfaces</h3>
            <p>Goals and recurring automation remain owner-managed, but linked items can be reviewed here for shared accounts.</p>
          </div>
          <div className="account-details-pulse">
            <div className="metric-card">
              <span>Linked goals</span>
              <strong>{linkedGoals.length}</strong>
              <p>{linkedGoals.length === 0 ? "No goals linked yet." : canManageMembers ? "Savings targets using this account." : "Read-only goal visibility for this shared account."}</p>
            </div>
            <div className="metric-card">
              <span>Recurring rules</span>
              <strong>{recurringRules.length}</strong>
              <p>{recurringRules.length === 0 ? "No recurring rules tied here." : canManageMembers ? "Automation affecting this account." : "Read-only recurring visibility for this shared account."}</p>
            </div>
          </div>
          {!canManageMembers ? (
            <div className="rules-preview">
              <strong>Planning remains owner-managed.</strong>
              <p>You can review goals and recurring items linked to this account, but only the owner can create, edit, pause, or archive them in this phase.</p>
            </div>
          ) : null}
        </section>
      </div>

      <div className="account-details-layout">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Linked goals</h3>
            <p>{canManageMembers ? "Savings goals that contribute to or withdraw from this account." : "Read-only goals linked to this shared account."}</p>
          </div>
          {linkedGoals.length === 0 ? (
            <EmptyState title="No linked goals" description="When a goal is tied to this account, it will appear here." />
          ) : (
            <div className="simple-list">
              {linkedGoals.map((goal) => (
                <div key={goal.id} className="list-row list-row--stacked">
                  <div>
                    <strong>{goal.name}</strong>
                    <p>{formatCurrency(goal.currentAmount)} saved of {formatCurrency(goal.targetAmount)}</p>
                  </div>
                  <div className="dashboard-inline-progress">
                    <ProgressBar value={goal.progressPercent} tone={goal.status === "Completed" ? "warning" : "default"} />
                    <small>{goal.progressPercent.toFixed(2)}% | {goal.targetDateUtc ? formatDate(goal.targetDateUtc) : "Flexible date"}</small>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>

        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Recurring rules</h3>
            <p>{canManageMembers ? "Scheduled rules that debit, credit, or transfer through this account." : "Read-only recurring items linked to this shared account."}</p>
          </div>
          {recurringRules.length === 0 ? (
            <EmptyState title="No recurring rules" description="Recurring items tied to this account will appear here." />
          ) : (
            <div className="simple-list">
              {recurringRules.map((rule) => (
                <div key={rule.id} className="list-row list-row--stacked">
                  <div>
                    <strong>{rule.title}</strong>
                    <p>{rule.frequency} | {rule.categoryName || rule.type}</p>
                  </div>
                  <div className="account-details-rule-aside">
                    <strong>{formatCurrency(rule.amount)}</strong>
                    <small>{rule.nextRunDateUtc ? `Next ${formatDate(rule.nextRunDateUtc)}` : "No next run scheduled"}</small>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}






