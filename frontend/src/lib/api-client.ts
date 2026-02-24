import { AuthenticationResponse, LoginRequest, RegisterRequest, ForgotPasswordRequest, ResetPasswordRequest, PasswordResetResponse, ConfirmEmailRequest, ConfirmEmailResponse, ResendVerificationRequest, ResendVerificationResponse } from '@/types/auth';
import {
  BankConnection,
  BankConnectionDetail,
  BankSyncLog,
  BankSyncResult,
  BankProviderInfo,
  AkahuAccount,
  InitiateConnectionResult,
  InitiateAkahuRequest,
  CompleteAkahuRequest,
  HasAkahuCredentialsResponse,
  SaveAkahuCredentialsRequest,
  SaveAkahuCredentialsResult,
} from '@/types/bank-connections';
import {
  BudgetSummary,
  BudgetDetail,
  BudgetSuggestion,
  BudgetRolloverResult,
  CreateBudgetRequest,
  CreateBudgetCategoryRequest,
  UpdateBudgetRequest,
  UpdateBudgetCategoryRequest,
} from '@/types/budget';
import { UpcomingBillsResponse } from '@/types/upcoming-bills';
import {
  AiSettingsResponse,
  AiSettingsRequest,
  AiConnectionTestRequest,
  AiConnectionTestResult,
  AiProviderPreset,
} from '@/types/ai-settings';
import type {
  SendChatMessageResponse,
  ChatHistoryResponse,
} from '@/types/chat';
import type {
  TelegramSettingsResponse,
  SaveTelegramSettingsRequest,
  TelegramTestResult,
} from '@/types/telegram-settings';

class ApiClient {
  private baseURL: string;
  
  constructor() {
    this.baseURL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5126';
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' });
  }

  async post<T>(endpoint: string, data?: any): Promise<T> {
    return this.request<T>(endpoint, { 
      method: 'POST',
      body: data ? JSON.stringify(data) : undefined
    });
  }

  async put<T>(endpoint: string, data?: any): Promise<T> {
    return this.request<T>(endpoint, { 
      method: 'PUT',
      body: data ? JSON.stringify(data) : undefined
    });
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' });
  }

  async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${this.baseURL}${endpoint}`;
    
    const config: RequestInit = {
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
      credentials: 'include', // Include cookies for refresh tokens
      ...options,
    };

    // Add authorization header if token exists
    const token = this.getToken();
    if (token) {
      config.headers = {
        ...config.headers,
        Authorization: `Bearer ${token}`,
      };
    }

    try {
      const response = await fetch(url, config);
      
      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        // For auth endpoints, preserve the error structure
        if (endpoint.includes('/api/auth/') && errorData.errors) {
          const error = new Error('Authentication failed') as Error & { authResponse?: unknown; status?: number };
          error.authResponse = errorData;
          error.status = response.status;
          throw error;
        }
        const error = new Error(errorData.message || `HTTP error! status: ${response.status}`) as Error & { status?: number; response?: Response };
        error.status = response.status;
        error.response = response;
        throw error;
      }

      // Handle 204 No Content responses
      if (response.status === 204) {
        return {} as T;
      }

      // Handle empty responses
      const text = await response.text();
      if (!text) {
        return {} as T;
      }

      return JSON.parse(text);
    } catch (error) {
      console.error('API request failed:', error);
      throw error;
    }
  }

  // Auth methods
  async login(credentials: LoginRequest): Promise<AuthenticationResponse> {
    return this.request<AuthenticationResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials),
    });
  }

  async register(userData: RegisterRequest): Promise<AuthenticationResponse> {
    return this.request<AuthenticationResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  }

  async healthCheck(): Promise<{ status: string; timestamp: string }> {
    return this.request<{ status: string; timestamp: string }>('/api/auth/health');
  }

  async getCurrentUser(): Promise<unknown> {
    return this.request('/api/auth/me');
  }

  async refreshToken(): Promise<AuthenticationResponse> {
    return this.request<AuthenticationResponse>('/api/auth/refresh', {
      method: 'POST',
    });
  }

  async revokeToken(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/auth/revoke', {
      method: 'POST',
    });
  }

  async forgotPassword(request: ForgotPasswordRequest): Promise<PasswordResetResponse> {
    return this.request<PasswordResetResponse>('/api/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async resetPassword(request: ResetPasswordRequest): Promise<PasswordResetResponse> {
    return this.request<PasswordResetResponse>('/api/auth/reset-password', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async confirmEmail(request: ConfirmEmailRequest): Promise<ConfirmEmailResponse> {
    return this.request<ConfirmEmailResponse>('/api/auth/confirm-email', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async resendVerificationEmail(request: ResendVerificationRequest): Promise<ResendVerificationResponse> {
    return this.request<ResendVerificationResponse>('/api/auth/resend-verification', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async updateLocale(locale: string): Promise<unknown> {
    return this.request('/api/auth/locale', {
      method: 'PATCH',
      body: JSON.stringify({ locale }),
    });
  }

  // Exchange a one-time OAuth code for an access token
  async exchangeCode(code: string): Promise<{ token: string; expiresAt: string }> {
    return this.post('/api/auth/exchange-code', { code });
  }

  // Token management
  setToken(token: string): void {
    if (typeof window !== 'undefined') {
      localStorage.setItem('auth_token', token);
    }
  }

  getToken(): string | null {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('auth_token');
    }
    return null;
  }

  removeToken(): void {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('auth_token');
    }
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  // Transaction methods
  async getTransactions(params?: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    includeTransfers?: boolean;
    onlyTransfers?: boolean;
    transferId?: string;
    categoryId?: number;
    accountId?: number;
    isReviewed?: boolean;
    isReconciled?: boolean;
    needsCategorization?: boolean;
    startDate?: string;
    endDate?: string;
    transactionType?: string;
    sortBy?: string;
    sortDirection?: string;
  }): Promise<unknown> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params?.searchTerm) queryParams.append('searchTerm', params.searchTerm);
    if (params?.includeTransfers !== undefined) queryParams.append('includeTransfers', params.includeTransfers.toString());
    if (params?.onlyTransfers !== undefined) queryParams.append('onlyTransfers', params.onlyTransfers.toString());
    if (params?.transferId) queryParams.append('transferId', params.transferId);
    if (params?.categoryId) queryParams.append('categoryId', params.categoryId.toString());
    if (params?.accountId) queryParams.append('accountId', params.accountId.toString());
    if (params?.isReviewed !== undefined) queryParams.append('isReviewed', params.isReviewed.toString());
    if (params?.isReconciled !== undefined) queryParams.append('isReconciled', params.isReconciled.toString());
    if (params?.needsCategorization !== undefined) queryParams.append('needsCategorization', params.needsCategorization.toString());
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);
    if (params?.transactionType) queryParams.append('transactionType', params.transactionType);
    if (params?.sortBy) queryParams.append('sortBy', params.sortBy);
    if (params?.sortDirection) queryParams.append('sortDirection', params.sortDirection);

    const queryString = queryParams.toString();
    const endpoint = `/api/transactions${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async getTransaction(id: number): Promise<unknown> {
    return this.request(`/api/transactions/${id}`);
  }

  async createTransaction(transaction: {
    amount: number;
    transactionDate: string;
    description: string;
    userDescription?: string;
    accountId: number; // Required field
    categoryId?: number;
    notes?: string;
    location?: string;
    tags?: string[];
    status: number; // Enum value: 1=Pending, 2=Cleared, 3=Reconciled, 4=Cancelled
  }): Promise<unknown> {
    return this.request('/api/transactions', {
      method: 'POST',
      body: JSON.stringify(transaction),
    });
  }

  async updateTransaction(id: number, transaction: {
    id: number;
    amount: number;
    transactionDate: string;
    description: string;
    userDescription?: string;
    categoryId?: number;
    notes?: string;
    location?: string;
    tags?: string[];
    status: number; // Enum value: 1=Pending, 2=Cleared, 3=Reconciled, 4=Cancelled
  }): Promise<unknown> {
    return this.request(`/api/transactions/${id}`, {
      method: 'PUT',
      body: JSON.stringify(transaction),
    });
  }

  async deleteTransaction(id: number): Promise<void> {
    return this.request(`/api/transactions/${id}`, {
      method: 'DELETE',
    });
  }

  async createAdjustmentTransaction(adjustment: {
    accountId: number;
    amount: number;
    description?: string;
    notes?: string;
  }): Promise<unknown> {
    return this.request('/api/transactions/adjustment', {
      method: 'POST',
      body: JSON.stringify(adjustment),
    });
  }

  async getRecentTransactions(count = 10): Promise<unknown[]> {
    return this.request(`/api/transactions/recent?count=${count}`);
  }

  async reviewTransaction(id: number): Promise<void> {
    return this.request(`/api/transactions/${id}/review`, {
      method: 'PATCH',
    });
  }

  async reviewAllTransactions(): Promise<{ reviewedCount: number; success: boolean; message: string }> {
    return this.request('/api/transactions/review-all', {
      method: 'POST',
    });
  }

  async bulkReviewCategorized(accountId?: number, searchText?: string): Promise<{ 
    reviewedCount: number; 
    totalProcessed: number;
    success: boolean; 
    message: string 
  }> {
    const params = new URLSearchParams();
    if (accountId) params.append('accountId', accountId.toString());
    if (searchText) params.append('searchText', searchText);
    
    const queryString = params.toString();
    const url = `/api/transactions/bulk-review-categorized${queryString ? `?${queryString}` : ''}`;
    
    return this.request(url, {
      method: 'POST',
    });
  }

  async getDescriptionSuggestions(searchTerm?: string, limit = 10): Promise<string[]> {
    const params = new URLSearchParams();
    if (searchTerm) params.append('q', searchTerm);
    params.append('limit', limit.toString());
    
    return this.request(`/api/transactions/description-suggestions?${params.toString()}`);
  }

  async getDuplicateTransactions(params?: {
    amountTolerance?: number;
    dateToleranceDays?: number;
    includeReviewed?: boolean;
    sameAccountOnly?: boolean;
    minConfidence?: number;
  }): Promise<unknown> {
    const searchParams = new URLSearchParams();
    
    if (params?.amountTolerance !== undefined) {
      searchParams.append('amountTolerance', params.amountTolerance.toString());
    }
    if (params?.dateToleranceDays !== undefined) {
      searchParams.append('dateToleranceDays', params.dateToleranceDays.toString());
    }
    if (params?.includeReviewed !== undefined) {
      searchParams.append('includeReviewed', params.includeReviewed.toString());
    }
    if (params?.sameAccountOnly !== undefined) {
      searchParams.append('sameAccountOnly', params.sameAccountOnly.toString());
    }
    if (params?.minConfidence !== undefined) {
      searchParams.append('minConfidence', params.minConfidence.toString());
    }
    
    const queryString = searchParams.toString();
    const endpoint = `/api/transactions/duplicates${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async resolveDuplicates(resolutions: Array<{
    groupId: string;
    transactionIdsToKeep: number[];
    transactionIdsToDelete: number[];
    markAsNotDuplicate?: boolean;
    notes?: string;
  }>): Promise<unknown> {
    return this.request('/api/transactions/duplicates/resolve', {
      method: 'POST',
      body: JSON.stringify({ resolutions })
    });
  }

  async getPotentialTransfers(params?: {
    amountTolerance?: number;
    dateToleranceDays?: number;
    includeReviewed?: boolean;
    minConfidence?: number;
    includeExistingTransfers?: boolean;
  }): Promise<unknown> {
    const searchParams = new URLSearchParams();
    
    if (params?.amountTolerance !== undefined) {
      searchParams.append('amountTolerance', params.amountTolerance.toString());
    }
    if (params?.dateToleranceDays !== undefined) {
      searchParams.append('dateToleranceDays', params.dateToleranceDays.toString());
    }
    if (params?.includeReviewed !== undefined) {
      searchParams.append('includeReviewed', params.includeReviewed.toString());
    }
    if (params?.minConfidence !== undefined) {
      searchParams.append('minConfidence', params.minConfidence.toString());
    }
    if (params?.includeExistingTransfers !== undefined) {
      searchParams.append('includeExistingTransfers', params.includeExistingTransfers.toString());
    }
    
    const queryString = searchParams.toString();
    const endpoint = `/api/transactions/potential-transfers${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async createMissingTransfer(request: {
    existingTransactionId: number;
    missingAccountId: number;
    description?: string;
    notes?: string;
    transactionDate?: string;
  }): Promise<unknown> {
    return this.request('/api/transactions/transfers/create-missing', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async reverseTransfer(transferId: string): Promise<unknown> {
    return this.request(`/api/transfer/${transferId}/reverse`, {
      method: 'POST',
    });
  }

  async linkTransactionsAsTransfer(request: {
    sourceTransactionId: number;
    destinationTransactionId: number;
    description?: string;
  }): Promise<unknown> {
    return this.request('/api/transactions/transfers/link', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Reports methods
  async getMonthlySummary(year: number, month: number): Promise<unknown> {
    const url = `/api/reports/monthly-summary?year=${year}&month=${month}`;
    console.log('Making request to:', url);
    console.log('Parameters:', { year, month, yearType: typeof year, monthType: typeof month });

    return this.request(url);
  }

  async getUpcomingBills(daysAhead: number = 7): Promise<UpcomingBillsResponse> {
    return this.request(`/api/reports/upcoming-bills?daysAhead=${daysAhead}`);
  }

  async getCategoryTrends(params?: {
    startDate?: string;
    endDate?: string;
    categoryIds?: number[];
    limit?: number;
  }): Promise<CategoryTrendsResponse> {
    const queryParams = new URLSearchParams();
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);
    if (params?.categoryIds?.length) queryParams.append('categoryIds', params.categoryIds.join(','));
    if (params?.limit) queryParams.append('limit', params.limit.toString());

    const queryString = queryParams.toString();
    const endpoint = `/api/reports/category-trends${queryString ? `?${queryString}` : ''}`;

    return this.request(endpoint);
  }

  // Account methods
  async getAccounts(): Promise<unknown> {
    return this.request('/api/accounts');
  }

  async getAccountsWithBalances(): Promise<unknown> {
    return this.request('/api/accounts/with-balances');
  }

  async getAccount(id: number): Promise<unknown> {
    return this.request(`/api/accounts/${id}`);
  }

  async getAccountWithBalance(id: number): Promise<unknown> {
    return this.request(`/api/accounts/${id}/with-balance`);
  }

  async getAccountDetails(id: number): Promise<unknown> {
    return this.request(`/api/accounts/${id}/details`);
  }

  async createAccount(account: {
    name: string;
    type: number;
    institution?: string;
    currentBalance: number;
    currency: string;
    notes?: string;
  }): Promise<unknown> {
    // Map currentBalance to initialBalance to match backend DTO
    const createAccountRequest = {
      name: account.name,
      type: account.type,
      institution: account.institution,
      initialBalance: account.currentBalance, // Map to correct backend field name
      currency: account.currency,
      notes: account.notes,
    };
    
    return this.request('/api/accounts', {
      method: 'POST',
      body: JSON.stringify(createAccountRequest),
    });
  }

  async updateAccount(id: number, account: {
    id: number;
    name: string;
    type: number;
    institution?: string;
    currentBalance: number;
    currency: string;
    notes?: string;
  }): Promise<unknown> {
    return this.request(`/api/accounts/${id}`, {
      method: 'PUT',
      body: JSON.stringify(account),
    });
  }

  async archiveAccount(id: number): Promise<void> {
    return this.request(`/api/accounts/${id}/archive`, {
      method: 'PATCH',
    });
  }

  async deleteAccount(id: number): Promise<void> {
    return this.request(`/api/accounts/${id}`, {
      method: 'DELETE',
    });
  }

  async getAccountTransactions(id: number): Promise<unknown> {
    return this.request(`/api/accounts/${id}/transactions`);
  }

  // Category methods
  async getCategories(params?: {
    includeSystemCategories?: boolean;
    includeInactive?: boolean;
    includeHierarchy?: boolean;
  }): Promise<unknown> {
    const queryParams = new URLSearchParams();
    if (params?.includeSystemCategories !== undefined) queryParams.append('includeSystemCategories', params.includeSystemCategories.toString());
    if (params?.includeInactive !== undefined) queryParams.append('includeInactive', params.includeInactive.toString());
    if (params?.includeHierarchy !== undefined) queryParams.append('includeHierarchy', params.includeHierarchy.toString());
    
    const queryString = queryParams.toString();
    const endpoint = `/api/categories${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async getFilteredCategories(params?: {
    searchTerm?: string;
    accountId?: number;
    isReviewed?: boolean;
    startDate?: string;
    endDate?: string;
    onlyTransfers?: boolean;
    includeTransfers?: boolean;
    minAmount?: number;
    maxAmount?: number;
    status?: number;
    isExcluded?: boolean;
    transferId?: string;
  }): Promise<unknown> {
    const queryParams = new URLSearchParams();
    if (params?.searchTerm) queryParams.append('searchTerm', params.searchTerm);
    if (params?.accountId) queryParams.append('accountId', params.accountId.toString());
    if (params?.isReviewed !== undefined) queryParams.append('isReviewed', params.isReviewed.toString());
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);
    if (params?.onlyTransfers !== undefined) queryParams.append('onlyTransfers', params.onlyTransfers.toString());
    if (params?.includeTransfers !== undefined) queryParams.append('includeTransfers', params.includeTransfers.toString());
    if (params?.minAmount !== undefined) queryParams.append('minAmount', params.minAmount.toString());
    if (params?.maxAmount !== undefined) queryParams.append('maxAmount', params.maxAmount.toString());
    if (params?.status !== undefined) queryParams.append('status', params.status.toString());
    if (params?.isExcluded !== undefined) queryParams.append('isExcluded', params.isExcluded.toString());
    if (params?.transferId) queryParams.append('transferId', params.transferId);
    
    const queryString = queryParams.toString();
    const endpoint = `/api/transactions/categories${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async getCategory(id: number): Promise<unknown> {
    return this.request(`/api/categories/${id}`);
  }

  async createCategory(category: {
    name: string;
    description?: string;
    color?: string;
    icon?: string;
    type: number;
    parentCategoryId?: number;
    sortOrder: number;
  }): Promise<unknown> {
    return this.request('/api/categories', {
      method: 'POST',
      body: JSON.stringify(category),
    });
  }

  async updateCategory(id: number, category: {
    id: number;
    name: string;
    description?: string;
    color?: string;
    icon?: string;
    type: number;
    parentCategoryId?: number;
    sortOrder: number;
    isActive: boolean;
  }): Promise<unknown> {
    return this.request(`/api/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(category),
    });
  }

  async deleteCategory(id: number): Promise<void> {
    return this.request(`/api/categories/${id}`, {
      method: 'DELETE',
    });
  }

  async getCategoryStatistics(id: number): Promise<unknown> {
    return this.request(`/api/categories/${id}/statistics`);
  }

  async initializeDefaultCategories(): Promise<{ message: string }> {
    return this.request('/api/categories/initialize', {
      method: 'POST',
    });
  }

  async getSeedLocales(): Promise<string[]> {
    return this.request('/api/categories/seed-locales');
  }

  async initializeCategories(locale: string): Promise<{ message: string; count: number }> {
    return this.request('/api/categories/initialize', {
      method: 'POST',
      body: JSON.stringify({ locale }),
    });
  }

  // Transfer methods
  async createTransfer(transfer: {
    sourceAccountId: number;
    destinationAccountId: number;
    amount: number;
    currency: string;
    description?: string;
    notes?: string;
    transferDate: string;
  }): Promise<unknown> {
    return this.request('/api/transfer', {
      method: 'POST',
      body: JSON.stringify(transfer),
    });
  }

  // OFX Import methods
  async importOfxFile(
    file: File, 
    options?: {
      accountId?: number;
      createAccount?: boolean;
      accountName?: string;
    }
  ): Promise<{
    success: boolean;
    message: string;
    importedTransactionsCount: number;
    skippedTransactionsCount: number;
    duplicateTransactionsCount: number;
    errors?: string[];
    warnings?: string[];
  }> {
    const formData = new FormData();
    formData.append('file', file);
    
    if (options?.accountId) {
      formData.append('accountId', options.accountId.toString());
    }
    if (options?.createAccount !== undefined) {
      formData.append('createAccount', options.createAccount.toString());
    }
    if (options?.accountName) {
      formData.append('accountName', options.accountName);
    }

    return this.request('/api/ofx-import/import', {
      method: 'POST',
      body: formData,
      headers: {}, // Remove Content-Type to let browser set it for FormData
    });
  }

  async validateOfxFile(file: File, includeTransactions: boolean = false): Promise<{
    success: boolean;
    message: string;
    errors?: string[];
    warnings?: string[];
    accountInfo?: {
      accountId: string;
      accountNumber: string;
      bankId?: string;
      branchId?: string;
      accountType: string;
    };
    transactionCount: number;
    statementPeriod?: {
      startDate?: string;
      endDate?: string;
    };
    transactions?: Array<{
      transactionId: string;
      amount: number;
      transactionDate: string;
      description: string;
      memo?: string;
      transactionType: string;
      checkNumber?: string;
      referenceNumber?: string;
    }>;
  }> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('includeTransactions', includeTransactions.toString());

    return this.request('/api/ofx-import/validate', {
      method: 'POST',
      body: formData,
      headers: {}, // Remove Content-Type to let browser set it for FormData
    });
  }

  // CSV Import methods
  async getCsvFormats(): Promise<Record<string, unknown>> {
    return this.request('/api/CsvImport/formats');
  }

  async analyzeCsvWithAI(
    file: File,
    options?: {
      accountType?: string;
      currencyHint?: string;
      sampleSize?: number;
    }
  ): Promise<{
    success: boolean;
    suggestedMappings: Record<string, {
      csvColumnName: string;
      targetField: string;
      confidence: number;
      interpretation: string;
      sampleValues: string[];
    }>;
    sampleRows: Record<string, string>[];
    confidenceScores: Record<string, number>;
    detectedBankFormat: string;
    detectedCurrency?: string;
    dateFormats: string[];
    amountConvention: string;
    availableColumns: string[];
    warnings: string[];
    errorMessage?: string;
  }> {
    const formData = new FormData();
    formData.append('file', file);
    
    if (options?.accountType) {
      formData.append('accountType', options.accountType);
    }
    if (options?.currencyHint) {
      formData.append('currencyHint', options.currencyHint);
    }
    if (options?.sampleSize) {
      formData.append('sampleSize', options.sampleSize.toString());
    }

    return this.request('/api/CsvImport/analyze', {
      method: 'POST',
      body: formData,
      headers: {}, // Remove Content-Type to let browser set it for FormData
    });
  }

  async importCsvWithMappings(data: {
    csvContent: string; // Base64 encoded CSV content
    mappings: {
      dateColumn?: string;
      amountColumn?: string;
      descriptionColumn?: string;
      typeColumn?: string;
      balanceColumn?: string;
      referenceColumn?: string;
      categoryColumn?: string;
      dateFormat: string;
      amountConvention: string;
    };
    accountId?: number;
    accountName?: string;
    skipDuplicates?: boolean;
    autoCategorize?: boolean;
    maxRows?: number;
  }): Promise<{
    isSuccess: boolean;
    message: string;
    importedTransactionsCount: number;
    skippedTransactionsCount: number;
    duplicateTransactionsCount: number;
    warnings: string[];
    errors: string[];
    createdAccountId?: number;
  }> {
    return this.request('/api/CsvImport/import-with-mappings', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async validateCsvMappings(
    file: File,
    mappings: {
      dateColumn?: string;
      amountColumn?: string;
      descriptionColumn?: string;
      typeColumn?: string;
      balanceColumn?: string;
      referenceColumn?: string;
      categoryColumn?: string;
      dateFormat: string;
      amountConvention: string;
    }
  ): Promise<{
    isValid: boolean;
    errors: string[];
    warnings: string[];
    validRowCount: number;
    invalidRowCount: number;
    invalidRows: Record<string, string>[];
  }> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('mappingsJson', JSON.stringify(mappings));

    return this.request('/api/CsvImport/validate-mappings', {
      method: 'POST',
      body: formData,
      headers: {}, // Remove Content-Type to let browser set it for FormData
    });
  }


  async downloadCsvTemplate(format: string): Promise<Blob> {
    const url = `${this.baseURL}/api/CsvImport/template?format=${format}`;
    
    const config: RequestInit = {
      headers: {
        ...this.getAuthHeaders(),
      },
    };

    const response = await fetch(url, config);
    
    if (!response.ok) {
      throw new Error(`Failed to download template: ${response.statusText}`);
    }

    return response.blob();
  }

  async uploadCsvFile(
    file: File,
    format: string,
    accountId?: number,
    accountName?: string,
    hasHeader: boolean = true,
    skipDuplicates: boolean = true,
    autoCategorize: boolean = true
  ): Promise<unknown> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('format', format);
    formData.append('hasHeader', hasHeader.toString());
    formData.append('skipDuplicates', skipDuplicates.toString());
    formData.append('autoCategorize', autoCategorize.toString());
    
    if (accountId) {
      formData.append('accountId', accountId.toString());
    }
    if (accountName) {
      formData.append('accountName', accountName);
    }

    return this.request('/api/CsvImport/upload', {
      method: 'POST',
      body: formData,
      headers: {}, // Remove Content-Type to let browser set it for FormData
    });
  }

  // LLM Categorization methods
  async batchCategorizeTransactions(request: {
    transactionIds: number[];
    useExistingRules?: boolean;
    generateNewRules?: boolean;
    confidenceThreshold?: number;
    maxBatchSize?: number;
  }): Promise<LlmCategorizationResponse> {
    // NEW: Use the full categorization pipeline that includes Rules → ML → LLM
    // This provides better suggestions and cost optimization
    return this.request('/api/categorization/process-for-candidates', {
      method: 'POST',
      body: JSON.stringify({
        transactionIds: request.transactionIds,
        confidenceThreshold: request.confidenceThreshold,
        maxBatchSize: request.maxBatchSize
      }),
    });
  }

  async getLlmServiceHealth(): Promise<LlmServiceHealthResponse> {
    return this.request('/api/llmcategorization/health');
  }

  // Categorization Candidates methods
  async getTransactionSuggestions(transactionId: number): Promise<{
    transactionId: number;
    suggestions: Array<{
      categoryId: number;
      categoryName: string;
      confidence: number;
      reasoning: string;
      matchingRules?: number[];
      method?: 'Rule' | 'ML' | 'LLM' | 'Manual';
    }>;
    count: number;
  }> {
    return this.request(`/api/Categorization/transaction/${transactionId}/suggestions`);
  }

  async applyCategorization(candidateId: number): Promise<{ message: string; candidateId: number }> {
    return this.request(`/api/Categorization/candidates/${candidateId}/apply`, {
      method: 'POST',
    });
  }

  async rejectCategorization(candidateId: number): Promise<{ message: string; candidateId: number }> {
    return this.request(`/api/Categorization/candidates/${candidateId}/reject`, {
      method: 'POST',
    });
  }

  async getCandidatesForTransactionQuery(params: {
    page?: number;
    pageSize?: number;
    accountId?: number;
    categoryId?: number;
    startDate?: string;
    endDate?: string;
    status?: number;
    searchTerm?: string;
    isReviewed?: boolean;
    needsCategorization?: boolean;
    includeTransfers?: boolean;
    onlyTransfers?: boolean;
    transferId?: string;
    transactionType?: string;
    sortBy?: string;
    sortDirection?: string;
    onlyWithCandidates?: boolean;
  }): Promise<{
    transactionIds: number[];
    candidatesFound: number;
    suggestions: Record<number, Array<{
      categoryId: number;
      categoryName: string;
      confidence: number;
      reasoning: string;
      matchingRules?: number[];
      method?: 'Rule' | 'ML' | 'LLM' | 'Manual';
    }>>;
    page: number;
    pageSize: number;
    totalTransactions: number;
  }> {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.accountId) queryParams.append('accountId', params.accountId.toString());
    if (params.categoryId) queryParams.append('categoryId', params.categoryId.toString());
    if (params.startDate) queryParams.append('startDate', params.startDate);
    if (params.endDate) queryParams.append('endDate', params.endDate);
    if (params.status !== undefined) queryParams.append('status', params.status.toString());
    if (params.searchTerm) queryParams.append('searchTerm', params.searchTerm);
    if (params.isReviewed !== undefined) queryParams.append('isReviewed', params.isReviewed.toString());
    if (params.needsCategorization !== undefined) queryParams.append('needsCategorization', params.needsCategorization.toString());
    if (params.includeTransfers !== undefined) queryParams.append('includeTransfers', params.includeTransfers.toString());
    if (params.onlyTransfers !== undefined) queryParams.append('onlyTransfers', params.onlyTransfers.toString());
    if (params.transferId) queryParams.append('transferId', params.transferId);
    if (params.transactionType) queryParams.append('transactionType', params.transactionType);
    if (params.sortBy) queryParams.append('sortBy', params.sortBy);
    if (params.sortDirection) queryParams.append('sortDirection', params.sortDirection);
    if (params.onlyWithCandidates !== undefined) queryParams.append('onlyWithCandidates', params.onlyWithCandidates.toString());
    
    const queryString = queryParams.toString();
    const endpoint = `/api/Categorization/candidates/for-transaction-query${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  // Reconciliation methods
  async getReconciliations(params?: {
    accountId?: number;
    page?: number;
    pageSize?: number;
  }): Promise<unknown> {
    const queryParams = new URLSearchParams();
    if (params?.accountId) queryParams.append('accountId', params.accountId.toString());
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    
    const queryString = queryParams.toString();
    const endpoint = `/api/reconciliation${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async getReconciliation(id: number): Promise<unknown> {
    return this.request(`/api/reconciliation/${id}`);
  }

  async createReconciliation(data: {
    accountId: number;
    statementEndDate: string;
    statementEndBalance: number;
    notes?: string;
  }): Promise<unknown> {
    return this.request('/api/reconciliation', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateReconciliation(id: number, data: {
    statementEndDate?: string;
    statementEndBalance?: number;
    status?: number;
    notes?: string;
  }): Promise<unknown> {
    return this.request(`/api/reconciliation/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ request: data }),
    });
  }

  async finalizeReconciliation(id: number, options?: {
    notes?: string;
    forceFinalize?: boolean;
  }): Promise<unknown> {
    return this.request(`/api/reconciliation/${id}/finalize`, {
      method: 'POST',
      body: JSON.stringify({
        notes: options?.notes,
        forceFinalize: options?.forceFinalize ?? false
      }),
    });
  }

  async getReconciliationItems(id: number, params?: {
    itemType?: string;
    minConfidence?: number;
    matchMethod?: string;
  }): Promise<unknown> {
    const queryParams = new URLSearchParams();
    if (params?.itemType) queryParams.append('itemType', params.itemType);
    if (params?.minConfidence) queryParams.append('minConfidence', params.minConfidence.toString());
    if (params?.matchMethod) queryParams.append('matchMethod', params.matchMethod);
    
    const queryString = queryParams.toString();
    const endpoint = `/api/reconciliation/${id}/items${queryString ? `?${queryString}` : ''}`;
    
    return this.request(endpoint);
  }

  async matchTransactions(id: number, data: {
    bankTransactions: Array<{
      bankTransactionId: string;
      amount: number;
      transactionDate: string;
      description: string;
      bankCategory?: string;
      reference?: string;
    }>;
    startDate?: string;
    endDate?: string;
    toleranceAmount?: number;
    useDescriptionMatching?: boolean;
    useDateRangeMatching?: boolean;
    dateRangeToleranceDays?: number;
  }): Promise<unknown> {
    return this.request(`/api/reconciliation/${id}/match-transactions`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Akahu Reconciliation methods
  async checkAkahuReconciliationAvailability(accountId: number): Promise<{
    isAvailable: boolean;
    externalAccountId?: string;
    unavailableReason?: string;
  }> {
    return this.request(`/api/reconciliation/${accountId}/akahu-availability`);
  }

  async createAkahuReconciliation(data: {
    accountId: number;
    startDate: string;
    endDate: string;
    statementEndBalance?: number;
    notes?: string;
  }): Promise<{
    reconciliationId: number;
    matchingResult: {
      totalBankTransactions: number;
      totalAppTransactions: number;
      exactMatches: number;
      fuzzyMatches: number;
      unmatchedBank: number;
      unmatchedApp: number;
      overallMatchPercentage: number;
    };
    balanceComparison?: {
      akahuBalance: number;
      myMascadaBalance: number;
      difference: number;
      isBalanced: boolean;
      isCurrentBalance: boolean;
      pendingTransactionsTotal: number;
      pendingTransactionsCount: number;
    };
  }> {
    return this.request('/api/reconciliation/akahu', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async importUnmatchedTransactions(reconciliationId: number, data: {
    itemIds?: number[];
    importAll?: boolean;
  }): Promise<{
    importedCount: number;
    skippedCount: number;
    createdTransactionIds: number[];
    errors: string[];
  }> {
    return this.request(`/api/reconciliation/${reconciliationId}/import-unmatched`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  // Import Review methods
  async analyzeImportForReview(request: {
    source: 'csv' | 'ofx';
    accountId: number;
    csvData?: {
      content: string; // Base64 encoded
      mappings: Record<string, any>;
      hasHeader: boolean;
    };
    ofxData?: {
      content: string; // Base64 encoded
      createAccount?: boolean;
      accountName?: string;
    };
    options?: {
      dateToleranceDays?: number;
      amountTolerance?: number;
      enableTransferDetection?: boolean;
      conflictDetectionLevel?: 'strict' | 'moderate' | 'relaxed';
    };
  }): Promise<{
    analysisId: string;
    accountId: number;
    reviewItems: unknown[];
    summary: {
      totalCandidates: number;
      cleanImports: number;
      exactDuplicates: number;
      potentialDuplicates: number;
      transferConflicts: number;
      manualConflicts: number;
      requiresReview: number;
    };
    analysisNotes: string[];
    warnings: string[];
    errors: string[];
    analyzedAt: string;
  }> {
    return this.request('/api/ImportReview/analyze', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async executeImportReview(request: {
    analysisId: string;
    accountId: number;
    decisions: Array<{
      reviewItemId: string;
      decision: number; // ConflictResolution enum value (0-4)
      userNotes?: string;
      candidate?: any; // ImportCandidateDto data to avoid cache dependency
    }>;
  }): Promise<{
    success: boolean;
    message: string;
    importedTransactionsCount: number;
    skippedTransactionsCount: number;
    duplicateTransactionsCount: number;
    mergedTransactionsCount: number;
    processedItems: Array<{
      reviewItemId: string;
      action: string;
      success: boolean;
      createdTransactionId?: number;
      updatedTransactionId?: number;
      error?: string;
    }>;
    warnings: string[];
    errors: string[];
    targetAccountId: number;
    createdAccountId?: number;
  }> {
    return this.request('/api/ImportReview/execute', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Rule Suggestions methods
  async getRuleSuggestions(): Promise<{
    suggestions: RuleSuggestion[];
    summary: RuleSuggestionsSummary;
  }> {
    const response = await this.request('/api/RuleSuggestions');
    return response as any;
  }

  async generateRuleSuggestions(options?: {
    maxSuggestions?: number;
    minConfidence?: number;
  }): Promise<{
    suggestions: RuleSuggestion[];
    summary: RuleSuggestionsSummary;
  }> {
    const queryParams = new URLSearchParams();
    if (options?.maxSuggestions) queryParams.set('maxSuggestions', options.maxSuggestions.toString());
    if (options?.minConfidence) queryParams.set('minConfidence', options.minConfidence.toString());
    
    const url = `/api/RuleSuggestions/generate${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
    const response = await this.request(url, { method: 'POST' });
    return response as any;
  }

  async acceptRuleSuggestion(suggestionId: number, options?: {
    customName?: string;
    customDescription?: string;
    priority?: number;
  }): Promise<{ ruleId: number }> {
    const response = await this.request(`/api/RuleSuggestions/${suggestionId}/accept`, {
      method: 'POST',
      body: JSON.stringify(options || {}),
    });
    return response as any;
  }

  async rejectRuleSuggestion(suggestionId: number): Promise<void> {
    await this.request(`/api/RuleSuggestions/${suggestionId}/reject`, {
      method: 'POST',
    });
  }

  async getRuleSuggestionsSummary(): Promise<RuleSuggestionsSummary> {
    const response = await this.request('/api/RuleSuggestions/summary');
    return response as any;
  }

  // Bank Connections methods
  async getBankConnections(): Promise<BankConnection[]> {
    return this.request('/api/BankConnections');
  }

  async getBankConnection(id: number): Promise<BankConnectionDetail> {
    return this.request(`/api/BankConnections/${id}`);
  }

  async getAvailableProviders(): Promise<BankProviderInfo[]> {
    return this.request('/api/BankConnections/providers');
  }

  async initiateAkahuConnection(request?: InitiateAkahuRequest): Promise<InitiateConnectionResult> {
    return this.request('/api/BankConnections/akahu/initiate', {
      method: 'POST',
      body: JSON.stringify(request ?? {}),
    });
  }

  async hasAkahuCredentials(): Promise<HasAkahuCredentialsResponse> {
    return this.request('/api/BankConnections/akahu/has-credentials');
  }

  async saveAkahuCredentials(request: SaveAkahuCredentialsRequest): Promise<SaveAkahuCredentialsResult> {
    return this.request('/api/BankConnections/akahu/credentials', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async completeAkahuConnection(request: CompleteAkahuRequest): Promise<BankConnection> {
    return this.request('/api/BankConnections/akahu/complete', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Note: This is for Production App OAuth mode only, not Personal App mode
  async exchangeAkahuCode(code: string, state: string, appIdToken?: string): Promise<{ accounts: AkahuAccount[]; accessToken: string }> {
    return this.request('/api/BankConnections/akahu/exchange', {
      method: 'POST',
      body: JSON.stringify({ code, state, appIdToken: appIdToken || '' }),
    });
  }

  async getAvailableAkahuAccounts(): Promise<AkahuAccount[]> {
    return this.request('/api/BankConnections/akahu/accounts');
  }

  async disconnectBankConnection(id: number): Promise<void> {
    return this.request(`/api/BankConnections/${id}`, {
      method: 'DELETE',
    });
  }

  async syncBankConnection(id: number): Promise<BankSyncResult> {
    return this.request(`/api/BankConnections/${id}/sync`, {
      method: 'POST',
    });
  }

  async syncAllConnections(): Promise<BankSyncResult[]> {
    return this.request('/api/BankConnections/sync-all', {
      method: 'POST',
    });
  }

  async getSyncHistory(connectionId: number, limit?: number): Promise<BankSyncLog[]> {
    const queryParams = new URLSearchParams();
    if (limit !== undefined) {
      queryParams.append('limit', limit.toString());
    }

    const queryString = queryParams.toString();
    const endpoint = `/api/BankConnections/${connectionId}/sync-history${queryString ? `?${queryString}` : ''}`;

    return this.request(endpoint);
  }

  // Bank Category Mappings methods
  async getBankCategoryMappings(options?: {
    providerId?: string;
    activeOnly?: boolean;
  }): Promise<BankCategoryMappingsListResponse> {
    const queryParams = new URLSearchParams();
    if (options?.providerId) queryParams.set('providerId', options.providerId);
    if (options?.activeOnly !== undefined) queryParams.set('activeOnly', options.activeOnly.toString());

    const url = `/api/bankcategorymappings${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
    const response = await this.request(url);
    return response as any;
  }

  async getBankCategoryMapping(id: number): Promise<BankCategoryMapping> {
    const response = await this.request(`/api/bankcategorymappings/${id}`);
    return response as any;
  }

  async createBankCategoryMapping(data: {
    bankCategoryName: string;
    categoryId: number;
    providerId?: string;
  }): Promise<BankCategoryMapping> {
    const response = await this.request('/api/bankcategorymappings', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return response as any;
  }

  async updateBankCategoryMapping(id: number, data: {
    categoryId: number;
  }): Promise<BankCategoryMapping> {
    const response = await this.request(`/api/bankcategorymappings/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
    return response as any;
  }

  async deleteBankCategoryMapping(id: number): Promise<void> {
    await this.request(`/api/bankcategorymappings/${id}`, {
      method: 'DELETE',
    });
  }

  async setBankCategoryExclusion(id: number, isExcluded: boolean): Promise<BankCategoryMapping> {
    const response = await this.request(`/api/bankcategorymappings/${id}/exclude`, {
      method: 'PATCH',
      body: JSON.stringify({ isExcluded }),
    });
    return response as any;
  }

  // Budget methods
  async getBudgets(params?: {
    includeInactive?: boolean;
    onlyCurrentPeriod?: boolean;
  }): Promise<BudgetSummary[]> {
    const queryParams = new URLSearchParams();
    if (params?.includeInactive !== undefined) queryParams.append('includeInactive', params.includeInactive.toString());
    if (params?.onlyCurrentPeriod !== undefined) queryParams.append('onlyCurrentPeriod', params.onlyCurrentPeriod.toString());

    const queryString = queryParams.toString();
    const endpoint = `/api/budgets${queryString ? `?${queryString}` : ''}`;

    return this.request(endpoint);
  }

  async getBudget(id: number): Promise<BudgetDetail> {
    return this.request(`/api/budgets/${id}`);
  }

  async getBudgetSuggestions(monthsToAnalyze = 3): Promise<BudgetSuggestion[]> {
    return this.request(`/api/budgets/suggestions?monthsToAnalyze=${monthsToAnalyze}`);
  }

  async createBudget(budget: CreateBudgetRequest): Promise<BudgetDetail> {
    return this.request('/api/budgets', {
      method: 'POST',
      body: JSON.stringify(budget),
    });
  }

  async updateBudget(id: number, budget: UpdateBudgetRequest): Promise<BudgetDetail> {
    return this.request(`/api/budgets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(budget),
    });
  }

  async deleteBudget(id: number): Promise<void> {
    return this.request(`/api/budgets/${id}`, {
      method: 'DELETE',
    });
  }

  async addBudgetCategory(budgetId: number, category: CreateBudgetCategoryRequest): Promise<BudgetDetail> {
    return this.request(`/api/budgets/${budgetId}/categories`, {
      method: 'POST',
      body: JSON.stringify(category),
    });
  }

  async updateBudgetCategory(budgetId: number, categoryId: number, category: UpdateBudgetCategoryRequest): Promise<BudgetDetail> {
    return this.request(`/api/budgets/${budgetId}/categories/${categoryId}`, {
      method: 'PUT',
      body: JSON.stringify(category),
    });
  }

  async removeBudgetCategory(budgetId: number, categoryId: number): Promise<BudgetDetail> {
    return this.request(`/api/budgets/${budgetId}/categories/${categoryId}`, {
      method: 'DELETE',
    });
  }

  async processBudgetRollovers(previewOnly: boolean = false): Promise<BudgetRolloverResult> {
    return this.request(`/api/budgets/process-rollovers?previewOnly=${previewOnly}`, {
      method: 'POST',
    });
  }

  // Goal methods
  async getGoals(params?: { includeCompleted?: boolean }): Promise<GoalSummary[]> {
    const queryParams = new URLSearchParams();
    if (params?.includeCompleted !== undefined) queryParams.append('includeCompleted', params.includeCompleted.toString());
    const queryString = queryParams.toString();
    return this.request(`/api/goals${queryString ? `?${queryString}` : ''}`);
  }

  async getGoal(id: number): Promise<GoalDetail> {
    return this.request(`/api/goals/${id}`);
  }

  async createGoal(goal: CreateGoalRequest): Promise<GoalDetail> {
    return this.request('/api/goals', { method: 'POST', body: JSON.stringify(goal) });
  }

  async updateGoal(id: number, goal: UpdateGoalRequest): Promise<GoalDetail> {
    return this.request(`/api/goals/${id}`, { method: 'PUT', body: JSON.stringify(goal) });
  }

  async deleteGoal(id: number): Promise<void> {
    return this.request(`/api/goals/${id}`, { method: 'DELETE' });
  }

  async updateGoalProgress(id: number, currentAmount: number): Promise<GoalDetail> {
    return this.request(`/api/goals/${id}/progress`, { method: 'PUT', body: JSON.stringify({ currentAmount }) });
  }

  async toggleGoalPin(id: number, isPinned: boolean): Promise<void> {
    return this.request(`/api/goals/${id}/pin`, { method: 'PATCH', body: JSON.stringify({ isPinned }) });
  }

  async getEmergencyFundAnalysis(goalId: number, includeLlmAnalysis = false): Promise<EmergencyFundAnalysisDto> {
    return this.request(`/api/goals/${goalId}/emergency-fund-analysis?includeLlmAnalysis=${includeLlmAnalysis}`);
  }

  // User Data methods (LGPD/GDPR compliance)
  async getUserDataSummary(): Promise<UserDataSummary> {
    return this.request('/api/UserData/summary');
  }

  async exportUserData(): Promise<Blob> {
    const url = `${this.baseURL}/api/UserData/export`;
    const token = this.getToken();

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': token ? `Bearer ${token}` : '',
      },
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to export user data');
    }

    return response.blob();
  }

  async deleteUserAccount(confirmation: string): Promise<UserDeletionResult> {
    return this.request(`/api/UserData/account?confirmation=${encodeURIComponent(confirmation)}`, {
      method: 'DELETE',
    });
  }

  // Account Sharing methods
  async getAccountShares(accountId: number): Promise<AccountShareDto[]> {
    return this.get(`/api/accounts/${accountId}/shares`);
  }

  async createAccountShare(accountId: number, email: string, role: number): Promise<CreateShareResult> {
    return this.post(`/api/accounts/${accountId}/shares`, { email, role });
  }

  async revokeAccountShare(accountId: number, shareId: number): Promise<void> {
    return this.delete(`/api/accounts/${accountId}/shares/${shareId}`);
  }

  async updateAccountShareRole(accountId: number, shareId: number, role: number): Promise<void> {
    return this.request(`/api/accounts/${accountId}/shares/${shareId}/role`, {
      method: 'PATCH',
      body: JSON.stringify({ role }),
    });
  }

  async getReceivedShares(): Promise<ReceivedShareDto[]> {
    return this.get('/api/account-shares/received');
  }

  async acceptShare(token: string): Promise<void> {
    return this.post('/api/account-shares/accept', { token });
  }

  async declineShare(token: string): Promise<void> {
    return this.post('/api/account-shares/decline', { token });
  }

  async acceptShareById(shareId: number): Promise<AccountShareDto> {
    return this.post(`/api/account-shares/${shareId}/accept`);
  }

  async declineShareById(shareId: number): Promise<void> {
    return this.post(`/api/account-shares/${shareId}/decline`);
  }

  // AI Description Cleaning methods
  async previewDescriptionCleaning(descriptions: { rawDescription: string; merchantNameHint?: string }[]): Promise<{
    results: Array<{
      rawDescription: string;
      cleanedDescription: string;
      confidence: number;
    }>;
  }> {
    return this.post('/api/description-cleaning/preview', { descriptions });
  }

  async updateAiDescriptionCleaning(enabled: boolean): Promise<unknown> {
    return this.request('/api/auth/ai-description-cleaning', {
      method: 'PATCH',
      body: JSON.stringify({ enabled }),
    });
  }

  // AI Settings methods
  async getAiSettings(purpose?: string): Promise<AiSettingsResponse | null> {
    const params = purpose ? `?purpose=${purpose}` : '';
    try {
      return await this.request<AiSettingsResponse>(`/api/ai-settings${params}`);
    } catch (error) {
      const err = error as { status?: number };
      if (err.status === 404) {
        return null;
      }
      throw error;
    }
  }

  async updateAiSettings(settings: AiSettingsRequest, purpose?: string): Promise<AiSettingsResponse> {
    const params = purpose ? `?purpose=${purpose}` : '';
    return this.request<AiSettingsResponse>(`/api/ai-settings${params}`, {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  }

  async deleteAiSettings(purpose?: string): Promise<void> {
    const params = purpose ? `?purpose=${purpose}` : '';
    return this.request(`/api/ai-settings${params}`, {
      method: 'DELETE',
    });
  }

  async testAiConnection(request: AiConnectionTestRequest, purpose?: string): Promise<AiConnectionTestResult> {
    const params = purpose ? `?purpose=${purpose}` : '';
    return this.request<AiConnectionTestResult>(`/api/ai-settings/test${params}`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async getAiProviders(): Promise<AiProviderPreset[]> {
    return this.request<AiProviderPreset[]>('/api/ai-settings/providers');
  }

  // Telegram Settings methods
  async getTelegramSettings(): Promise<TelegramSettingsResponse> {
    return this.request<TelegramSettingsResponse>('/api/telegram/settings');
  }

  async saveTelegramSettings(request: SaveTelegramSettingsRequest): Promise<TelegramSettingsResponse> {
    return this.request<TelegramSettingsResponse>('/api/telegram/settings', {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  }

  async deleteTelegramSettings(): Promise<void> {
    return this.request('/api/telegram/settings', { method: 'DELETE' });
  }

  async testTelegramConnection(request: SaveTelegramSettingsRequest): Promise<TelegramTestResult> {
    return this.request<TelegramTestResult>('/api/telegram/settings/test', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Chat methods
  async sendChatMessage(content: string): Promise<SendChatMessageResponse> {
    return this.request<SendChatMessageResponse>('/api/ai-chat/messages', {
      method: 'POST',
      body: JSON.stringify({ content }),
    });
  }

  async getChatHistory(limit?: number, before?: number): Promise<ChatHistoryResponse> {
    const params = new URLSearchParams();
    if (limit) params.set('limit', limit.toString());
    if (before) params.set('before', before.toString());
    const query = params.toString();
    return this.request<ChatHistoryResponse>(`/api/ai-chat/messages${query ? `?${query}` : ''}`);
  }

  async clearChatHistory(): Promise<void> {
    return this.request('/api/ai-chat/messages', { method: 'DELETE' });
  }

  // Dashboard Nudge Dismissal methods
  async getDismissedNudges(): Promise<string[]> {
    return this.request('/api/dashboard-nudges/dismissed');
  }

  async dismissNudge(nudgeType: string, snoozeDays?: number): Promise<void> {
    return this.request(`/api/dashboard-nudges/${nudgeType}/dismiss`, {
      method: 'POST',
      body: JSON.stringify(snoozeDays ? { snoozeDays } : {}),
    });
  }

  // Feature flags (anonymous endpoint)
  async getFeatures(): Promise<FeatureFlags> {
    return this.request('/api/Features');
  }

  // Onboarding methods
  async completeOnboarding(data: CompleteOnboardingRequest): Promise<CompleteOnboardingResponse> {
    return this.request('/api/onboarding/complete', { method: 'POST', body: JSON.stringify(data) });
  }

  async getOnboardingStatus(): Promise<OnboardingStatusResponse> {
    return this.request('/api/onboarding/status');
  }

  private getAuthHeaders(): Record<string, string> {
    const token = this.getToken();
    return token ? { 'Authorization': `Bearer ${token}` } : {};
  }
}

// Feature Flags Types
export interface FeatureFlags {
  aiCategorization: boolean;
  googleOAuth: boolean;
  bankSync: boolean;
  emailNotifications: boolean;
  accountSharing: boolean;
}

// Account Sharing Types
export interface AccountShareDto {
  id: number;
  accountId: number;
  accountName: string;
  sharedWithUserId: string;
  sharedWithUserEmail: string;
  sharedWithUserName: string;
  role: number;
  status: number;
  createdAt: string;
}

export interface ReceivedShareDto {
  id: number;
  accountId: number;
  accountName: string;
  sharedByName: string;
  role: number;
  status: number;
  createdAt: string;
}

export interface CreateShareResult {
  id: number;
  token: string;
}

// LLM Types
export interface LlmCategorizationResponse {
  success: boolean;
  categorizations: TransactionCategorization[];
  summary: CategorizationSummary;
  errors: string[];
}

export interface TransactionCategorization {
  transactionId: number;
  suggestions: CategorySuggestion[];
  recommendedCategoryId?: number;
  requiresReview: boolean;
  suggestedRule?: SuggestedRule;
}

export interface CategorySuggestion {
  categoryId: number;
  categoryName: string;
  confidence: number;
  reasoning: string;
  matchingRules: number[];
}

export interface SuggestedRule {
  pattern: string;
  ruleType: string;
  categoryId: number;
  confidence: number;
  description: string;
}

export interface CategorizationSummary {
  totalProcessed: number;
  highConfidence: number;
  mediumConfidence: number;
  lowConfidence: number;
  averageConfidence: number;
  newRulesGenerated: number;
  processingTimeMs: number;
}

export interface LlmServiceHealthResponse {
  isAvailable: boolean;
  status: string;
  checkedAt: string;
}

// Rule Suggestions Types
export interface RuleSuggestion {
  id: number;
  name: string;
  description: string;
  pattern: string;
  type: string;
  isCaseSensitive: boolean;
  confidenceScore: number;
  matchCount: number;
  generationMethod: string;
  suggestedCategoryId: number;
  suggestedCategoryName: string;
  suggestedCategoryColor?: string;
  suggestedCategoryIcon?: string;
  sampleTransactions: RuleSuggestionSample[];
  createdAt: string;
}

export interface RuleSuggestionSample {
  id: number;
  transactionId: number;
  description: string;
  amount: number;
  transactionDate: string;
  accountName: string;
}

export interface RuleSuggestionsSummary {
  totalSuggestions: number;
  averageConfidencePercentage: number;
  lastGeneratedDate?: string;
  generationMethod: string;
  categoryDistribution: Record<string, number>;
}

// Bank Category Mapping Types
export interface BankCategoryMapping {
  id: number;
  bankCategoryName: string;
  providerId: string;
  categoryId: number;
  categoryName: string;
  categoryFullPath?: string;
  confidenceScore: number;
  effectiveConfidence: number;
  source: string;
  applicationCount: number;
  overrideCount: number;
  isActive: boolean;
  isExcluded: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MappingStatistics {
  totalMappings: number;
  aiCreatedMappings: number;
  userCreatedMappings: number;
  learnedMappings: number;
  highConfidenceCount: number;
  lowConfidenceCount: number;
  totalApplications: number;
  totalOverrides: number;
}

export interface BankCategoryMappingsListResponse {
  mappings: BankCategoryMapping[];
  statistics: MappingStatistics;
}

// Category Trends Types
export interface CategoryTrendsResponse {
  startDate: string;
  endDate: string;
  categories: CategoryTrendData[];
  periodSummaries: TrendPeriodSummary[];
}

export interface CategoryTrendData {
  categoryId: number;
  categoryName: string;
  categoryColor?: string;
  totalSpent: number;
  averageMonthlySpent: number;
  periods: PeriodAmount[];
}

export interface PeriodAmount {
  periodStart: string;
  periodLabel: string;
  amount: number;
  transactionCount: number;
}

export interface TrendPeriodSummary {
  periodStart: string;
  periodLabel: string;
  totalSpent: number;
  transactionCount: number;
}

// User Data Types (LGPD/GDPR compliance)
export interface UserDataSummary {
  totalAccounts: number;
  totalTransactions: number;
  totalTransfers: number;
  totalCategories: number;
  totalRules: number;
  totalBankConnections: number;
  totalReconciliations: number;
  totalAuditLogs: number;
  oldestTransactionDate?: string;
  newestTransactionDate?: string;
  accountCreatedAt: string;
  lastLoginAt?: string;
}

export interface UserDeletionResult {
  userId: string;
  deletedAt: string;
  success: boolean;
  errorMessage?: string;
  accountsDeleted: number;
  transactionsDeleted: number;
  transfersDeleted: number;
  categoriesDeleted: number;
  rulesDeleted: number;
  reconciliationsDeleted: number;
  bankConnectionsDeleted: number;
}

// Goal Types
export interface GoalSummary {
  id: number;
  name: string;
  description?: string;
  targetAmount: number;
  currentAmount: number;
  progressPercentage: number;
  remainingAmount: number;
  goalType: string;
  status: string;
  deadline?: string;
  daysRemaining?: number;
  linkedAccountName?: string;
  isPinned: boolean;
}

export interface GoalDetail extends GoalSummary {
  linkedAccountId?: number;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateGoalRequest {
  name: string;
  description?: string;
  targetAmount: number;
  deadline?: string;
  goalType: string;
  linkedAccountId?: number;
}

export interface UpdateGoalRequest {
  name?: string;
  description?: string;
  targetAmount?: number;
  currentAmount?: number;
  status?: string;
  deadline?: string;
  linkedAccountId?: number;
}

// Emergency Fund Analysis Types
export interface EmergencyFundAnalysisDto {
  averageMonthlyExpenses: number;
  averageMonthlyExpenses6M: number;
  onboardingMonthlyExpenses: number;
  recommendedTarget3M: number;
  recommendedTarget6M: number;
  currentAmount: number;
  monthsCovered: number;
  transactionMonthsAvailable: number;
  monthlyBreakdown: MonthlyExpenseBreakdown[];
  monthlyRecurringTotal: number;
  activeRecurringCount: number;
  essentialAnalysis?: EssentialExpenseAnalysis;
}

export interface MonthlyExpenseBreakdown {
  year: number;
  month: number;
  totalExpenses: number;
  income: number;
}

export interface EssentialExpenseAnalysis {
  estimatedMonthlyEssentials: number;
  estimatedMonthlyDiscretionary: number;
  recommendedTarget3M: number;
  recommendedTarget6M: number;
  categories: ExpenseCategoryBreakdown[];
  reasoning: string;
}

export interface ExpenseCategoryBreakdown {
  categoryName: string;
  monthlyAverage: number;
  isEssential: boolean;
}

// Onboarding Types
export interface CompleteOnboardingRequest {
  monthlyIncome: number;
  monthlyExpenses: number;
  goalName: string;
  goalTargetAmount: number;
  goalType: string;
  dataEntryMethod: string;
  linkedAccountId?: number;
}

export interface CompleteOnboardingResponse {
  profileId: number;
  goalId: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  monthlyAvailable: number;
}

export interface OnboardingStatusResponse {
  isComplete: boolean;
  monthlyIncome?: number;
  monthlyExpenses?: number;
  monthlyAvailable?: number;
}

// Create a singleton instance that's safe for SSR
let apiClientInstance: ApiClient | null = null;

export const apiClient = typeof window !== 'undefined' 
  ? (apiClientInstance || (apiClientInstance = new ApiClient()))
  : new Proxy({} as ApiClient, {
      get() {
        throw new Error('ApiClient should not be used during SSR');
      }
    });
