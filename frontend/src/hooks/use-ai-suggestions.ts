import { useState, useCallback, useEffect } from 'react';
import { useAiSuggestionsContext, AiSuggestion } from '@/contexts/ai-suggestions-context';
import { apiClient } from '@/lib/api-client';

interface UseAiSuggestionsForTransactionOptions {
  transactionId: number;
  autoFetch?: boolean;
  confidenceThreshold?: number;
}

interface UseAiSuggestionsForTransactionReturn {
  suggestions: AiSuggestion[];
  isLoading: boolean;
  error: string | null;
  fetchSuggestions: () => Promise<void>;
  hasCachedSuggestions: boolean;
}

/**
 * Hook for managing AI suggestions for a specific transaction
 * Automatically checks cache first, only makes API call if needed
 */
export function useAiSuggestionsForTransaction({
  transactionId,
  autoFetch = false,
  confidenceThreshold = 0.4
}: UseAiSuggestionsForTransactionOptions): UseAiSuggestionsForTransactionReturn {
  const {
    getSuggestionsForTransaction,
    storeSuggestionsForTransaction,
    hasCachedSuggestions: hasCached
  } = useAiSuggestionsContext();

  const [suggestions, setSuggestions] = useState<AiSuggestion[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasCheckedCache, setHasCheckedCache] = useState(false);

  const hasCachedSuggestions = hasCached(transactionId);

  const fetchSuggestions = useCallback(async () => {
    // Check cache first
    const cachedSuggestions = getSuggestionsForTransaction(transactionId);
    if (cachedSuggestions) {
      setSuggestions(cachedSuggestions);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.batchCategorizeTransactions({
        transactionIds: [transactionId],
        confidenceThreshold,
        maxBatchSize: 1
      });

      if (response.success && response.categorizations.length > 0) {
        const categorization = response.categorizations[0];
        const aiSuggestions: AiSuggestion[] = categorization.suggestions.map(s => ({
          categoryId: s.categoryId,
          categoryName: s.categoryName,
          confidence: s.confidence,
          reasoning: s.reasoning,
          matchingRules: s.matchingRules
        }));

        setSuggestions(aiSuggestions);
        storeSuggestionsForTransaction(transactionId, aiSuggestions);
      } else {
        setSuggestions([]);
        setError(response.errors.join(', ') || 'Failed to get AI suggestions');
      }
    } catch (err) {
      console.error('Failed to fetch AI suggestions:', err);
      setError('Failed to fetch AI suggestions');
      setSuggestions([]);
    } finally {
      setIsLoading(false);
    }
  }, [transactionId, confidenceThreshold, getSuggestionsForTransaction, storeSuggestionsForTransaction]);

  // Check cache on mount and when transaction changes
  useEffect(() => {
    const cachedSuggestions = getSuggestionsForTransaction(transactionId);
    if (cachedSuggestions) {
      setSuggestions(cachedSuggestions);
      setHasCheckedCache(true);
    } else if (!hasCheckedCache) {
      setHasCheckedCache(true);
      if (autoFetch) {
        fetchSuggestions();
      }
    }
  }, [transactionId, getSuggestionsForTransaction, hasCheckedCache, autoFetch, fetchSuggestions]);

  return {
    suggestions,
    isLoading,
    error,
    fetchSuggestions,
    hasCachedSuggestions
  };
}

interface UseAiSuggestionsBatchOptions {
  confidenceThreshold?: number;
  maxBatchSize?: number;
}

interface UseAiSuggestionsBatchReturn {
  isLoading: boolean;
  error: string | null;
  batchCategorize: (transactionIds: number[]) => Promise<Map<number, AiSuggestion[]> | null>;
}

/**
 * Hook for batch AI suggestions processing
 * Automatically stores results in shared cache
 */
export function useAiSuggestionsBatch({
  confidenceThreshold = 0.7,
  maxBatchSize = 25
}: UseAiSuggestionsBatchOptions = {}): UseAiSuggestionsBatchReturn {
  const { storeBatchSuggestions } = useAiSuggestionsContext();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const batchCategorize = useCallback(async (
    transactionIds: number[]
  ): Promise<Map<number, AiSuggestion[]> | null> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await apiClient.batchCategorizeTransactions({
        transactionIds,
        confidenceThreshold,
        maxBatchSize
      });

      if (!response.success) {
        setError(response.errors.join(', ') || 'Batch categorization failed');
        return null;
      }

      // Convert to Map format for caching
      const suggestionsMap = new Map<number, AiSuggestion[]>();
      
      response.categorizations.forEach(categorization => {
        const aiSuggestions: AiSuggestion[] = categorization.suggestions.map(s => ({
          categoryId: s.categoryId,
          categoryName: s.categoryName,
          confidence: s.confidence,
          reasoning: s.reasoning,
          matchingRules: s.matchingRules
        }));
        
        suggestionsMap.set(categorization.transactionId, aiSuggestions);
      });

      // Store in shared cache
      storeBatchSuggestions(suggestionsMap);

      return suggestionsMap;
    } catch (err) {
      console.error('Failed to batch categorize transactions:', err);
      setError('Failed to batch categorize transactions');
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [confidenceThreshold, maxBatchSize, storeBatchSuggestions]);

  return {
    isLoading,
    error,
    batchCategorize
  };
}

/**
 * Hook for invalidating AI suggestions when they're applied or rejected
 */
export function useAiSuggestionInvalidation() {
  const { invalidateSuggestions } = useAiSuggestionsContext();

  const invalidateTransaction = useCallback((transactionId: number) => {
    invalidateSuggestions(transactionId);
  }, [invalidateSuggestions]);

  return { invalidateTransaction };
}
