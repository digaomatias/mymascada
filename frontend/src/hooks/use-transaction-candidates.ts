import { useState, useCallback, useEffect, useRef } from 'react';
import { apiClient } from '@/lib/api-client';
import { AiSuggestion } from '@/contexts/ai-suggestions-context';

interface TransactionCandidatesParams {
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
}

interface TransactionCandidatesResponse {
  transactionIds: number[];
  candidatesFound: number;
  suggestions: Record<number, AiSuggestion[]>;
  page: number;
  pageSize: number;
  totalTransactions: number;
}

// Deep comparison function for query parameters
function deepEqual(obj1: unknown, obj2: unknown): boolean {
  if (obj1 === obj2) return true;
  if (obj1 == null || obj2 == null) return false;
  if (typeof obj1 !== 'object' || typeof obj2 !== 'object') return false;
  
  const record1 = obj1 as Record<string, unknown>;
  const record2 = obj2 as Record<string, unknown>;
  const keys1 = Object.keys(record1);
  const keys2 = Object.keys(record2);
  
  if (keys1.length !== keys2.length) return false;
  
  for (const key of keys1) {
    if (!keys2.includes(key)) return false;
    if (!deepEqual(record1[key], record2[key])) return false;
  }
  
  return true;
}

export function useTransactionCandidates({
  queryParams,
  enabled = true,
}: {
  queryParams: TransactionCandidatesParams;
  enabled?: boolean;
}) {
  const [data, setData] = useState<TransactionCandidatesResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const prevQueryParamsRef = useRef<TransactionCandidatesParams | undefined>(undefined);
  const prevEnabledRef = useRef<boolean | undefined>(undefined);

  const fetchCandidates = useCallback(async (params: TransactionCandidatesParams) => {
    if (!enabled) return;
    
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await apiClient.getCandidatesForTransactionQuery(params);
      setData({
        ...response,
        suggestions: response.suggestions as Record<number, AiSuggestion[]>
      });
    } catch (err) {
      console.error('Failed to fetch transaction candidates:', err);
      setError('Failed to fetch AI suggestions');
    } finally {
      setIsLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    // Only fetch if queryParams or enabled actually changed (deep comparison)
    const queryParamsChanged = !deepEqual(prevQueryParamsRef.current, queryParams);
    const enabledChanged = prevEnabledRef.current !== enabled;
    
    if (queryParamsChanged || enabledChanged) {
      prevQueryParamsRef.current = queryParams;
      prevEnabledRef.current = enabled;
      fetchCandidates(queryParams);
    }
  }, [queryParams, enabled, fetchCandidates]);

  // Invalidate candidates when they are applied/rejected
  const invalidateCandidates = useCallback(() => {
    // Simply refetch the data to get updated candidates
    fetchCandidates(queryParams);
  }, [fetchCandidates, queryParams]);

  // Get suggestions for a specific transaction
  const getSuggestionsForTransaction = useCallback((transactionId: number): AiSuggestion[] => {
    return data?.suggestions[transactionId] || [];
  }, [data?.suggestions]);

  // Check if a transaction has suggestions
  const hasAiSuggestions = useCallback((transactionId: number): boolean => {
    const suggestions = getSuggestionsForTransaction(transactionId);
    return suggestions.length > 0;
  }, [getSuggestionsForTransaction]);

  return {
    data,
    isLoading,
    error,
    refetch: () => fetchCandidates(queryParams),
    invalidateCandidates,
    getSuggestionsForTransaction,
    hasAiSuggestions,
  };
}

// Hook for merging transaction data with AI suggestions
export function useTransactionsWithCandidates({
  transactions,
  candidatesQuery
}: {
  transactions: Array<{ id: number; [key: string]: unknown }>;
  candidatesQuery: ReturnType<typeof useTransactionCandidates>;
}) {
  return transactions.map(transaction => ({
    ...transaction,
    aiSuggestions: candidatesQuery.getSuggestionsForTransaction(transaction.id) || [],
    hasAiSuggestions: candidatesQuery.hasAiSuggestions(transaction.id),
  }));
}