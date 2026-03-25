import { apiClient } from "../../../shared/lib/api/client";

export type AccountType = "BankAccount" | "CreditCard" | "CashWallet" | "SavingsAccount";
export type AccountMemberRole = "Owner" | "Editor" | "Viewer";

export type AccountDto = {
  id: string;
  name: string;
  type: AccountType;
  currencyCode: string;
  openingBalance: number;
  currentBalance: number;
  institutionName?: string | null;
  last4Digits?: string | null;
  isArchived: boolean;
  isShared: boolean;
  currentUserRole: AccountMemberRole;
  ownerDisplayName: string;
  memberCount: number;
  pendingInviteCount: number;
};

export type AccountMemberDto = {
  userId: string;
  email: string;
  fullName: string;
  role: AccountMemberRole;
  isOwner: boolean;
  invitedByDisplayName?: string | null;
  lastModifiedByDisplayName?: string | null;
  createdUtc: string;
  updatedUtc: string;
};

export type AccountPendingInviteDto = {
  id: string;
  email: string;
  role: AccountMemberRole;
  invitedByDisplayName: string;
  createdUtc: string;
  expiresUtc: string;
  isExpired: boolean;
};

export type InviteAccountMemberResponse = {
  invite: AccountPendingInviteDto;
  previewUrl?: string | null;
};

export type AccountInvitePreviewDto = {
  inviteId: string;
  accountId: string;
  accountName: string;
  ownerDisplayName: string;
  email: string;
  role: AccountMemberRole;
  status: string;
  expiresUtc: string;
  canAccept: boolean;
  requiresDifferentAccount: boolean;
  statusMessage: string;
};

export type InviteAccountMemberPayload = {
  email: string;
  role: 1 | 2;
};

export type UpdateAccountMemberPayload = {
  role: 1 | 2;
};

export type AccountPayload = {
  name: string;
  type: number;
  currencyCode: string;
  openingBalance?: number;
  institutionName?: string;
  last4Digits?: string;
};

export const accountsApi = {
  list: (accessToken: string, includeArchived = false) => apiClient<AccountDto[]>(`/accounts?includeArchived=${includeArchived}`, { accessToken }),
  get: (accessToken: string, id: string) => apiClient<AccountDto>(`/accounts/${id}`, { accessToken }),
  create: (accessToken: string, payload: AccountPayload) => apiClient<AccountDto>("/accounts", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, id: string, payload: Omit<AccountPayload, "openingBalance">) => apiClient<AccountDto>(`/accounts/${id}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  archive: (accessToken: string, id: string) => apiClient<void>(`/accounts/${id}`, { method: "DELETE", accessToken }),
  listMembers: (accessToken: string, accountId: string) => apiClient<AccountMemberDto[]>(`/accounts/${accountId}/members`, { accessToken }),
  listPendingInvites: (accessToken: string, accountId: string) => apiClient<AccountPendingInviteDto[]>(`/accounts/${accountId}/invites`, { accessToken }),
  inviteMember: (accessToken: string, accountId: string, payload: InviteAccountMemberPayload) => apiClient<InviteAccountMemberResponse>(`/accounts/${accountId}/invite`, { method: "POST", body: JSON.stringify(payload), accessToken }),
  resendInvite: (accessToken: string, accountId: string, inviteId: string) => apiClient<InviteAccountMemberResponse>(`/accounts/${accountId}/invites/${inviteId}/resend`, { method: "POST", accessToken }),
  previewInvite: (token: string, accessToken?: string | null) => apiClient<AccountInvitePreviewDto>(`/accounts/invites/preview?token=${encodeURIComponent(token)}`, { accessToken }),
  acceptInvite: (accessToken: string, token: string) => apiClient<AccountMemberDto>("/accounts/invites/accept", { method: "POST", body: JSON.stringify({ token }), accessToken }),
  updateMember: (accessToken: string, accountId: string, memberUserId: string, payload: UpdateAccountMemberPayload) => apiClient<AccountMemberDto>(`/accounts/${accountId}/members/${memberUserId}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  removeMember: (accessToken: string, accountId: string, memberUserId: string) => apiClient<void>(`/accounts/${accountId}/members/${memberUserId}`, { method: "DELETE", accessToken }),
  revokeInvite: (accessToken: string, accountId: string, inviteId: string) => apiClient<void>(`/accounts/${accountId}/invites/${inviteId}`, { method: "DELETE", accessToken }),
};
