'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfidenceIndicator } from '@/components/ui/confidence-indicator';
import { apiClient, TransactionCategorization } from '@/lib/api-client';
import { useAiSuggestionsBatch } from '@/hooks/use-ai-suggestions';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import {
  SparklesIcon,
  CheckIcon,
  XMarkIcon,
  ArrowPathIcon,
  ExclamationTriangleIcon,
  LightBulbIcon
} from '@heroicons/react/24/outline';

interface Transaction {
  id: number;
  amount: number;
  description: string;
  userDescription?: string;
  categoryId?: number;
}

interface LlmCategorizationSectionProps {
  transactions: Transaction[];
  onTransactionCategorized: (transactionId: number, categoryId: number) => void;
  onRefresh: () => void;
  onBatchCategorizationComplete?: () => void; // Called when batch categorization finishes
  hideWhenUnavailable?: boolean; // Option to completely hide the section when AI is unavailable
}

export function LlmCategorizationSection({
  transactions,
  onTransactionCategorized,
  onRefresh,
  onBatchCategorizationComplete,
  hideWhenUnavailable = false
}: LlmCategorizationSectionProps) {
  const tCommon = useTranslations('common');
  const tTransactions = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const [selectedTransactions, setSelectedTransactions] = useState<number[]>([]);
  const [suggestions, setSuggestions] = useState<TransactionCategorization[]>([]);
  const [isHealthy, setIsHealthy] = useState<boolean | null>(null); // null = loading, true = healthy, false = unhealthy
  const [isCheckingHealth, setIsCheckingHealth] = useState(true);
  
  // Use the batch AI suggestions hook that automatically caches results
  const { isLoading: isProcessing, batchCategorize } = useAiSuggestionsBatch();

  // Check service health on mount
  useEffect(() => {
    checkServiceHealth();
  }, []);

  const checkServiceHealth = async () => {
    setIsCheckingHealth(true);
    try {
      const health = await apiClient.getLlmServiceHealth();
      setIsHealthy(health.isAvailable);
    } catch (error) {
      console.error('Failed to check LLM service health:', error);
      setIsHealthy(false);
    } finally {
      setIsCheckingHealth(false);
    }
  };

  const handleSelectAll = () => {
    if (selectedTransactions.length === transactions.length) {
      setSelectedTransactions([]);
    } else {
      setSelectedTransactions(transactions.map(t => t.id));
    }
  };

  const handleSelectTransaction = (transactionId: number) => {
    setSelectedTransactions(prev => 
      prev.includes(transactionId)
        ? prev.filter(id => id !== transactionId)
        : [...prev, transactionId]
    );
  };

  const handleBatchCategorize = async () => {
    if (selectedTransactions.length === 0) {
      toast.error(tToasts('aiCategorizationSelectTransactions'));
      return;
    }

    try {
      // Use the hook which automatically caches results for individual CategoryPickers
      const suggestionsMap = await batchCategorize(selectedTransactions);
      
      if (!suggestionsMap) {
        toast.error(tToasts('aiCategorizationBatchFailed'));
        return;
      }

      // The hook already called the API and cached results, so we can use them directly
      // Convert cached results to display format
      const categorizations = Array.from(suggestionsMap.entries()).map(([transactionId, suggestions]) => ({
        transactionId,
        suggestions: suggestions.map(s => ({
          categoryId: s.categoryId,
          categoryName: s.categoryName,
          confidence: s.confidence,
          reasoning: s.reasoning,
          matchingRules: s.matchingRules || []
        })),
        recommendedCategoryId: suggestions.length > 0 ? suggestions[0].categoryId : undefined,
        requiresReview: suggestions.length === 0 || suggestions[0].confidence < 0.8
      }));

      setSuggestions(categorizations);
      
      // Calculate summary from cached results
      const totalProcessed = categorizations.length;
      const highConfidence = categorizations.filter(c => 
        c.suggestions.length > 0 && c.suggestions[0].confidence >= 0.8
      ).length;
      
      toast.success(
        tToasts('aiCategorizationSummary', {
          total: totalProcessed,
          highConfidence: highConfidence
        })
      );
      
      // Trigger refresh of category pickers to show AI suggestions
      onBatchCategorizationComplete?.();
    } catch (error) {
      console.error('Failed to categorize transactions:', error);
      toast.error(tToasts('aiCategorizationFailed'));
    }
  };

  const handleApplySuggestion = async (suggestion: TransactionCategorization) => {
    if (!suggestion.recommendedCategoryId) return;

    try {
      await onTransactionCategorized(suggestion.transactionId, suggestion.recommendedCategoryId);
      
      // Remove from suggestions and selected transactions
      setSuggestions(prev => prev.filter(s => s.transactionId !== suggestion.transactionId));
      setSelectedTransactions(prev => prev.filter(id => id !== suggestion.transactionId));
      
      const bestSuggestion = suggestion.suggestions[0];
      toast.success(tToasts('aiCategorizationApplied', {
        category: bestSuggestion?.categoryName,
        confidence: Math.round((bestSuggestion?.confidence || 0) * 100)
      }));
    } catch (error) {
      console.error('Failed to apply suggestion:', error);
      toast.error(tToasts('aiCategorizationApplyFailed'));
    }
  };

  const handleRejectSuggestion = (suggestion: TransactionCategorization) => {
    setSuggestions(prev => prev.filter(s => s.transactionId !== suggestion.transactionId));
    setSelectedTransactions(prev => prev.filter(id => id !== suggestion.transactionId));
    toast.info(tToasts('aiCategorizationSuggestionDismissed'));
  };

  const handleApplyAllHighConfidence = async () => {
    const highConfidenceSuggestions = suggestions.filter(s => 
      s.suggestions.length > 0 && s.suggestions[0].confidence >= 0.8
    );

    if (highConfidenceSuggestions.length === 0) {
      toast.error(tToasts('aiCategorizationNoHighConfidence'));
      return;
    }

    let applied = 0;
    
    try {
      for (const suggestion of highConfidenceSuggestions) {
        if (suggestion.recommendedCategoryId) {
          await onTransactionCategorized(suggestion.transactionId, suggestion.recommendedCategoryId);
          applied++;
        }
      }

      // Remove applied suggestions
      const appliedIds = highConfidenceSuggestions.map(s => s.transactionId);
      setSuggestions(prev => prev.filter(s => !appliedIds.includes(s.transactionId)));
      setSelectedTransactions(prev => prev.filter(id => !appliedIds.includes(id)));

      toast.success(tToasts('aiCategorizationAppliedCount', { count: applied }));
      onRefresh();
    } catch (error) {
      console.error('Failed to apply suggestions:', error);
      toast.error(tToasts('aiCategorizationApplySomeFailed'));
    }
  };

  // Show loading state while checking health
  if (isCheckingHealth || isHealthy === null) {
    return (
      <Card className="mb-6 bg-gradient-to-r from-purple-50 to-blue-50 border-purple-200">
        <CardContent className="p-6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-gradient-to-r from-purple-500 to-blue-500 rounded-lg flex items-center justify-center">
              <SparklesIcon className="w-5 h-5 text-white animate-pulse" />
            </div>
            <div>
              <h3 className="text-lg font-semibold text-gray-900">{tTransactions('aiCategorization.title')}</h3>
              <p className="text-sm text-gray-600 animate-pulse">
                {tTransactions('aiCategorization.checkingAvailability')}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  // Show unavailable state only after confirming service is down
  if (isHealthy === false) {
    // Option to completely hide when unavailable
    if (hideWhenUnavailable) {
      return null;
    }
    
    return (
      <Card className="mb-6 bg-gradient-to-r from-yellow-50 to-orange-50 border-yellow-200">
        <CardContent className="p-6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-yellow-500 rounded-lg flex items-center justify-center">
              <ExclamationTriangleIcon className="w-5 h-5 text-white" />
            </div>
            <div>
              <h3 className="text-lg font-semibold text-gray-900">{tTransactions('aiCategorization.unavailableTitle')}</h3>
              <p className="text-sm text-gray-600">
                {tTransactions('aiCategorization.unavailableDescription')}
              </p>
            </div>
            <Button
              variant="secondary"
              size="sm"
              onClick={checkServiceHealth}
              disabled={isCheckingHealth}
              className="ml-auto"
            >
              {isCheckingHealth ? (
                <>
                  <ArrowPathIcon className="w-4 h-4 mr-2 animate-spin" />
                  {tTransactions('aiCategorization.checking')}
                </>
              ) : (
                tCommon('retry')
              )}
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="mb-6 bg-gradient-to-r from-purple-50 to-blue-50 border-purple-200">
      <CardContent className="p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-gradient-to-r from-purple-500 to-blue-500 rounded-lg flex items-center justify-center">
              <SparklesIcon className="w-5 h-5 text-white" />
            </div>
            <div>
              <h3 className="text-lg font-semibold text-gray-900">{tTransactions('aiCategorization.title')}</h3>
              <p className="text-sm text-gray-600">
                {tTransactions('aiCategorization.subtitle')}
              </p>
            </div>
          </div>
          
          {suggestions.length > 0 && (
            <Button
              onClick={handleApplyAllHighConfidence}
              disabled={isProcessing}
              size="sm"
              className="bg-gradient-to-r from-green-500 to-green-600 hover:from-green-600 hover:to-green-700"
            >
              <CheckIcon className="w-4 h-4 mr-2" />
              {tTransactions('aiCategorization.applyAllHighConfidence')}
            </Button>
          )}
        </div>

        {/* Transaction Selection */}
        <div className="mb-4">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={selectedTransactions.length === transactions.length && transactions.length > 0}
                onChange={handleSelectAll}
                className="rounded border-gray-300 text-purple-600 focus:ring-purple-500"
              />
              <span className="text-sm font-medium text-gray-700">
                {tTransactions('aiCategorization.selectAllCount', {
                  selected: selectedTransactions.length,
                  total: transactions.length
                })}
              </span>
            </div>
            
            <Button
              onClick={handleBatchCategorize}
              disabled={isProcessing || selectedTransactions.length === 0}
              className="bg-gradient-to-r from-purple-500 to-blue-500 hover:from-purple-600 hover:to-blue-600"
            >
              {isProcessing ? (
                <>
                  <ArrowPathIcon className="w-4 h-4 mr-2 animate-spin" />
                  {tTransactions('aiCategorization.analyzing')}
                </>
              ) : (
                <>
                  <SparklesIcon className="w-4 h-4 mr-2" />
                  {tTransactions('aiCategorization.categorizeSelected')}
                </>
              )}
            </Button>
          </div>

          {/* Quick transaction selection */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 max-h-40 overflow-y-auto">
            {transactions.slice(0, 12).map((transaction) => (
              <label key={transaction.id} className="flex items-center gap-2 p-2 rounded border hover:bg-gray-50 cursor-pointer">
                <input
                  type="checkbox"
                  checked={selectedTransactions.includes(transaction.id)}
                  onChange={() => handleSelectTransaction(transaction.id)}
                  className="rounded border-gray-300 text-purple-600 focus:ring-purple-500"
                />
                <span className="text-xs text-gray-600 truncate">
                  {transaction.userDescription || transaction.description}
                </span>
              </label>
            ))}
          </div>
          
          {transactions.length > 12 && (
            <p className="text-xs text-gray-500 mt-2">
              {tTransactions('aiCategorization.showingFirst', { total: transactions.length })}
            </p>
          )}
        </div>

        {/* AI Suggestions */}
        {suggestions.length > 0 && (
          <div className="border-t border-purple-200 pt-4">
            <div className="flex items-center gap-2 mb-3">
              <LightBulbIcon className="w-5 h-5 text-purple-600" />
              <h4 className="font-medium text-gray-900">{tTransactions('aiCategorization.suggestionsTitle')}</h4>
              <span className="text-sm text-gray-500">{tTransactions('aiCategorization.suggestionsReady', { count: suggestions.length })}</span>
            </div>
            
            <div className="space-y-3 max-h-80 overflow-y-auto">
              {suggestions.map((suggestion) => {
                const transaction = transactions.find(t => t.id === suggestion.transactionId);
                const bestSuggestion = suggestion.suggestions[0];
                
                if (!transaction || !bestSuggestion) return null;

                return (
                  <div key={suggestion.transactionId} className="p-3 bg-white rounded-lg border border-purple-200">
                    <div className="flex items-start justify-between">
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-900 truncate">
                          {transaction.userDescription || transaction.description}
                        </p>
                        <div className="flex items-center gap-3 mt-1">
                          <span className="text-sm font-medium text-purple-700">
                            {bestSuggestion.categoryName}
                          </span>
                          <ConfidenceIndicator confidence={bestSuggestion.confidence} />
                        </div>
                        {bestSuggestion.reasoning && (
                          <p className="text-xs text-gray-600 mt-1 line-clamp-2">
                            {bestSuggestion.reasoning}
                          </p>
                        )}
                      </div>
                      
                      <div className="flex gap-2 ml-3">
                        <Button
                          size="sm"
                          onClick={() => handleApplySuggestion(suggestion)}
                          disabled={isProcessing}
                          className="text-purple-700 border-purple-200 hover:bg-purple-100"
                          variant="secondary"
                        >
                          <CheckIcon className="w-3 h-3 mr-1" />
                          {tCommon('apply')}
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => handleRejectSuggestion(suggestion)}
                          className="text-gray-600 border-gray-200 hover:bg-gray-100"
                        >
                          <XMarkIcon className="w-3 h-3 mr-1" />
                          {tTransactions('aiCategorization.skip')}
                        </Button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
