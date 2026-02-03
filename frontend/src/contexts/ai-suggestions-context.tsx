'use client';

import React, { createContext, useContext, useState, useCallback } from 'react';

// AI Suggestion types (matching your existing types)
export interface AiSuggestion {
  categoryId: number;
  categoryName: string;
  confidence: number;
  reasoning: string;
  matchingRules?: number[];
  method?: 'Rule' | 'ML' | 'LLM' | 'Manual'; // Categorization method from backend
}

interface CachedSuggestion {
  suggestions: AiSuggestion[];
  timestamp: number;
  transactionId: number;
}

interface AiSuggestionsContextType {
  // Get suggestions for a specific transaction
  getSuggestionsForTransaction: (transactionId: number) => AiSuggestion[] | null;
  
  // Store batch suggestions results
  storeBatchSuggestions: (suggestions: Map<number, AiSuggestion[]>) => void;
  
  // Store single transaction suggestions
  storeSuggestionsForTransaction: (transactionId: number, suggestions: AiSuggestion[]) => void;
  
  // Invalidate suggestions when applied/rejected
  invalidateSuggestions: (transactionId: number) => void;
  
  // Check if suggestions are cached and valid
  hasCachedSuggestions: (transactionId: number) => boolean;
  
  // Clear all cached suggestions
  clearAllSuggestions: () => void;
}

const AiSuggestionsContext = createContext<AiSuggestionsContextType | undefined>(undefined);

interface AiSuggestionsProviderProps {
  children: React.ReactNode;
  cacheTtlMs?: number; // Time-to-live for cached suggestions (default: 5 minutes)
}

export function AiSuggestionsProvider({ 
  children, 
  cacheTtlMs = 5 * 60 * 1000 // 5 minutes
}: AiSuggestionsProviderProps) {
  const [suggestionsCache, setSuggestionsCache] = useState<Map<number, CachedSuggestion>>(new Map());

  // Helper to check if cache entry is still valid
  const isCacheValid = useCallback((cachedItem: CachedSuggestion): boolean => {
    return Date.now() - cachedItem.timestamp < cacheTtlMs;
  }, [cacheTtlMs]);

  // Get suggestions for a specific transaction
  const getSuggestionsForTransaction = useCallback((transactionId: number): AiSuggestion[] | null => {
    const cached = suggestionsCache.get(transactionId);
    
    if (cached && isCacheValid(cached)) {
      return cached.suggestions;
    }
    
    // Clean up expired cache entry
    if (cached && !isCacheValid(cached)) {
      setSuggestionsCache(prev => {
        const newCache = new Map(prev);
        newCache.delete(transactionId);
        return newCache;
      });
    }
    
    return null;
  }, [suggestionsCache, isCacheValid]);

  // Store batch suggestions results
  const storeBatchSuggestions = useCallback((suggestions: Map<number, AiSuggestion[]>) => {
    setSuggestionsCache(prev => {
      const newCache = new Map(prev);
      const timestamp = Date.now();
      
      suggestions.forEach((suggestionList, transactionId) => {
        newCache.set(transactionId, {
          suggestions: suggestionList,
          timestamp,
          transactionId
        });
      });
      
      return newCache;
    });
  }, []);

  // Store single transaction suggestions
  const storeSuggestionsForTransaction = useCallback((transactionId: number, suggestions: AiSuggestion[]) => {
    setSuggestionsCache(prev => {
      const newCache = new Map(prev);
      newCache.set(transactionId, {
        suggestions,
        timestamp: Date.now(),
        transactionId
      });
      return newCache;
    });
  }, []);

  // Invalidate suggestions when applied/rejected
  const invalidateSuggestions = useCallback((transactionId: number) => {
    setSuggestionsCache(prev => {
      const newCache = new Map(prev);
      newCache.delete(transactionId);
      return newCache;
    });
  }, []);

  // Check if suggestions are cached and valid
  const hasCachedSuggestions = useCallback((transactionId: number): boolean => {
    const cached = suggestionsCache.get(transactionId);
    return cached ? isCacheValid(cached) : false;
  }, [suggestionsCache, isCacheValid]);

  // Clear all cached suggestions
  const clearAllSuggestions = useCallback(() => {
    setSuggestionsCache(new Map());
  }, []);

  // Cleanup expired cache entries periodically
  React.useEffect(() => {
    const cleanupInterval = setInterval(() => {
      setSuggestionsCache(prev => {
        const newCache = new Map(prev);
        let hasExpired = false;
        
        newCache.forEach((cached, transactionId) => {
          if (!isCacheValid(cached)) {
            newCache.delete(transactionId);
            hasExpired = true;
          }
        });
        
        return hasExpired ? newCache : prev;
      });
    }, 60 * 1000); // Check every minute

    return () => clearInterval(cleanupInterval);
  }, [isCacheValid]);

  const value: AiSuggestionsContextType = {
    getSuggestionsForTransaction,
    storeBatchSuggestions,
    storeSuggestionsForTransaction,
    invalidateSuggestions,
    hasCachedSuggestions,
    clearAllSuggestions
  };

  return (
    <AiSuggestionsContext.Provider value={value}>
      {children}
    </AiSuggestionsContext.Provider>
  );
}

export function useAiSuggestionsContext() {
  const context = useContext(AiSuggestionsContext);
  if (context === undefined) {
    throw new Error('useAiSuggestionsContext must be used within an AiSuggestionsProvider');
  }
  return context;
}