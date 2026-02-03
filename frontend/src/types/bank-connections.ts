// Bank connection types for Akahu integration

export interface BankConnection {
  id: number;
  accountId: number;
  accountName: string;
  providerId: string;
  providerName: string;
  externalAccountId?: string;
  externalAccountName?: string;
  isActive: boolean;
  lastSyncAt?: string;
  lastSyncError?: string;
  createdAt: string;
}

export interface BankConnectionDetail extends BankConnection {
  recentSyncLogs: BankSyncLog[];
}

export interface BankSyncLog {
  id: number;
  syncType: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  transactionsProcessed: number;
  transactionsImported: number;
  transactionsSkipped: number;
  errorMessage?: string;
}

export interface BankSyncResult {
  bankConnectionId: number;
  isSuccess: boolean;
  errorMessage?: string;
  transactionsImported: number;
  transactionsSkipped: number;
}

export interface BankProviderInfo {
  providerId: string;
  displayName: string;
  supportsWebhooks: boolean;
  supportsBalanceFetch: boolean;
}

export interface AkahuAccount {
  id: string;
  name: string;
  formattedAccount: string;
  type: string;
  bankName: string;
  currentBalance?: number;
  currency: string;
  isAlreadyLinked: boolean;
}

export interface InitiateConnectionResult {
  // OAuth mode (Production Apps)
  authorizationUrl?: string;
  state?: string;
  // Personal App mode
  isPersonalAppMode: boolean;
  // Credential check
  requiresCredentials: boolean;
  credentialsError?: string;
  // Available accounts (when credentials are valid)
  availableAccounts?: AkahuAccount[];
}

// Request types
export interface InitiateAkahuRequest {
  email?: string;
}

// Simplified request - no longer needs code/state for Personal App mode
export interface CompleteAkahuRequest {
  accountId: number;
  akahuAccountId: string;
}

// Akahu credential management types
export interface HasAkahuCredentialsResponse {
  hasCredentials: boolean;
}

export interface SaveAkahuCredentialsRequest {
  appIdToken: string;
  userToken: string;
}

export interface SaveAkahuCredentialsResult {
  isSuccess: boolean;
  errorMessage?: string;
  availableAccounts?: AkahuAccount[];
}
