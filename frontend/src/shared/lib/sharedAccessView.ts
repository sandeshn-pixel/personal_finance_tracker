export type SharedAccessView = "all" | "mine" | "shared";

type SharedAccountShape = {
  id: string;
  isShared: boolean;
  currentUserRole: string;
};

export function isOwnedAccount<T extends SharedAccountShape>(account: T) {
  return account.currentUserRole === "Owner";
}

export function hasSharedGuestAccounts<T extends SharedAccountShape>(accounts: readonly T[]) {
  return accounts.some((account) => !isOwnedAccount(account));
}

export function filterAccountsForView<T extends SharedAccountShape>(accounts: readonly T[], view: SharedAccessView) {
  if (view === "mine") {
    return accounts.filter((account) => isOwnedAccount(account));
  }

  if (view === "shared") {
    return accounts.filter((account) => !isOwnedAccount(account));
  }

  return [...accounts];
}

export function getScopedAccountIdsForView<T extends SharedAccountShape>(accounts: readonly T[], view: SharedAccessView) {
  return view === "all" ? [] : filterAccountsForView(accounts, view).map((account) => account.id);
}

export function hasAccountsInView<T extends SharedAccountShape>(accounts: readonly T[], view: SharedAccessView) {
  return view === "all" || filterAccountsForView(accounts, view).length > 0;
}

export function getSharedAccessViewLabel(view: SharedAccessView) {
  return view === "mine" ? "Mine" : view === "shared" ? "Shared with me" : "All";
}
