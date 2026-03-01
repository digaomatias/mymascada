'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfidenceIndicator } from '@/components/ui/confidence-indicator';
import { apiClient, TransactionCategorization } from '@/lib/api-client';
import { useAiSuggestionsBatch } from '@/hooks/use-ai-suggestions';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { formatCurrency } from '@/lib/utils';
import {
  SparklesIcon,
  CheckIcon,
  XMarkIcon,
  ArrowPathIcon,
  LightBulbIcon,
  Cog6ToothIcon,
  PlayIcon
} from '@heroicons/react/24/outline';

interface Transaction {
  id: number;
  amount: number;
  description: string;
  userDescription?: string;
  categoryId?: number;
  transactionDate: string;
  accountName?: string;
}

interface CategorizationRibbonProps {
  transactions: Transaction[];
  onTransactionCategorized: (transactionId: number, categoryId: number) => void;
  onRefresh: () => void;
  onBatchCategorizationComplete?: () => void;
  // Filter criteria for rule auto-categorization
  filterCriteria?: {
    accountIds?: number[];
    startDate?: string;
    endDate?: string;
    minAmount?: number;
    maxAmount?: number;
    transactionType?: string;
    searchText?: string;
    onlyUnreviewed?: boolean;
    excludeTransfers?: boolean;
  };
}

export function CategorizationRibbon({
  transactions,
  onTransactionCategorized,
  onRefresh,
  onBatchCategorizationComplete,
  filterCriteria
}: CategorizationRibbonProps) {
  const tCommon = useTranslations('common');
  const tTransactions = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const [selectedTransactions, setSelectedTransactions] = useState<number[]>([]);
  const [suggestions, setSuggestions] = useState<TransactionCategorization[]>([]);
  const [isHealthy, setIsHealthy] = useState<boolean | null>(null);
  const [isCheckingHealth, setIsCheckingHealth] = useState(true);
  const [showAiSection, setShowAiSection] = useState(false);
  const [isApplyingRules, setIsApplyingRules] = useState(false);
  const [rulePreview, setRulePreview] = useState<{
    summary: string;
    totalExamined: number;
    transactionsMatched: number;
    transactionsSkipped: number;
    transactionsUnmatched: number;
    ruleMatches?: Array<{
      transactionId: number;
      transactionDescription: string;
      transactionAmount: number;
      transactionDate: string;
      accountName: string;
      ruleName: string;
      rulePattern: string;
      categoryId: number;
      categoryName: string;
      currentCategoryName?: string;
      wouldChangeCategory: boolean;
      confidenceScore: number;
      ruleId: number;
      isExistingCandidate: boolean;
      candidateId?: number;
      isSelected: boolean;
      canAutoApply: boolean;
    }>;
  } | null>(null);
  const [showRulePreview, setShowRulePreview] = useState(false);
  
  // Use the batch AI suggestions hook
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
      const suggestionsMap = await batchCategorize(selectedTransactions);
      
      if (!suggestionsMap) {
        toast.error(tToasts('aiCategorizationBatchFailed'));
        return;
      }

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
      
      const totalProcessed = categorizations.length;
      const highConfidence = categorizations.filter(c => 
        c.suggestions.length > 0 && c.suggestions[0].confidence >= 0.8
      ).length;
      
      toast.success(
        tToasts('aiCategorizationSummary', { total: totalProcessed, highConfidence: highConfidence })
      );
      
      onBatchCategorizationComplete?.();
    } catch (error) {
      console.error('Failed to categorize transactions:', error);
      toast.error(tToasts('aiCategorizationFailed'));
    }
  };

  const handleRuleAutoCategorization = async () => {
    if (!filterCriteria) {
      toast.error(tToasts('ruleCategorizationFilterUnavailable'));
      return;
    }
    
    setIsApplyingRules(true);
    try {
      const endpoint = '/api/Categorization/auto-categorize/rules/preview';
      const result = await apiClient.post(endpoint, filterCriteria) as {
        summary: string;
        totalExamined: number;
        transactionsMatched: number;
        transactionsSkipped: number;
        transactionsUnmatched: number;
        ruleMatches?: Array<{
          transactionId: number;
          transactionDescription: string;
          transactionAmount: number;
          transactionDate: string;
          accountName: string;
          ruleName: string;
          rulePattern: string;
          categoryId: number;
          categoryName: string;
          currentCategoryName?: string;
          wouldChangeCategory: boolean;
          confidenceScore: number;
          ruleId: number;
          isExistingCandidate: boolean;
          candidateId?: number;
          isSelected: boolean;
          canAutoApply: boolean;
        }>;
      };

      setRulePreview(result);
      setShowRulePreview(true);
      
      const existingCount = result.ruleMatches?.filter((m: { isExistingCandidate: boolean }) => m.isExistingCandidate).length || 0;
      const newCount = result.ruleMatches?.filter((m: { isExistingCandidate: boolean }) => !m.isExistingCandidate).length || 0;
      
      if (existingCount > 0 && newCount > 0) {
        toast.info(tToasts('ruleCategorizationFoundExistingAndNew', { existing: existingCount, totalNew: newCount }));
      } else if (existingCount > 0) {
        toast.info(tToasts('ruleCategorizationFoundExisting', { count: existingCount }));
      } else if (newCount > 0) {
        toast.info(tToasts('ruleCategorizationFoundNew', { count: newCount }));
      } else {
        toast.info(tToasts('ruleCategorizationNoMatches'));
      }
    } catch (error) {
      console.error('Failed to preview rule auto-categorization:', error);
      toast.error(tToasts('ruleCategorizationPreviewFailed'));
    } finally {
      setIsApplyingRules(false);
    }
  };

  const handleSelectAllRuleMatches = () => {
    if (!rulePreview?.ruleMatches) return;
    
    const allSelected = rulePreview.ruleMatches.every(match => match.isSelected);
    const updatedMatches = rulePreview.ruleMatches.map(match => ({
      ...match,
      isSelected: !allSelected
    }));
    
    setRulePreview({
      ...rulePreview,
      ruleMatches: updatedMatches
    });
  };

  const handleToggleRuleMatch = (index: number) => {
    if (!rulePreview?.ruleMatches) return;
    
    const updatedMatches = [...rulePreview.ruleMatches];
    updatedMatches[index] = {
      ...updatedMatches[index],
      isSelected: !updatedMatches[index].isSelected
    };
    
    setRulePreview({
      ...rulePreview,
      ruleMatches: updatedMatches
    });
  };

  const handleApplySelectedRules = async () => {
    if (!rulePreview?.ruleMatches) return;
    
    const selectedMatches = rulePreview.ruleMatches.filter(match => match.isSelected);
    
    if (selectedMatches.length === 0) {
      toast.error(tToasts('ruleCategorizationSelectAtLeastOne'));
      return;
    }
    
    setIsApplyingRules(true);
    try {
      const result = await apiClient.post('/api/Categorization/auto-categorize/rules/apply-selected', {
        selectedMatches
      }) as {
        success: boolean;
        summary?: string;
        errors?: string[];
      };

      // Close preview and return to ribbon
      setShowRulePreview(false);
      setRulePreview(null);
      
      if (result.success) {
        toast.success(result.summary || tToasts('ruleCategorizationApplied', { count: selectedMatches.length }));
        onRefresh(); // Refresh the transaction list
        onBatchCategorizationComplete?.(); // Trigger refresh of category pickers
      } else {
        console.error('Rule application failed:', result);
        const errorMessage = result.errors && result.errors.length > 0 
          ? tToasts('ruleCategorizationApplyFailedWithMessage', { message: result.errors[0] })
          : tToasts('ruleCategorizationApplyFailedWithMessage', { message: result.summary || tCommon('error') });
        toast.error(errorMessage);
      }
    } catch (error) {
      console.error('Failed to apply selected rule matches:', error);
      toast.error(tToasts('ruleCategorizationApplyFailed'));
    } finally {
      setIsApplyingRules(false);
    }
  };

  const handleApplySuggestion = async (suggestion: TransactionCategorization) => {
    if (!suggestion.recommendedCategoryId) return;

    try {
      await onTransactionCategorized(suggestion.transactionId, suggestion.recommendedCategoryId);
      
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

  // Hide when no transactions available
  if (transactions.length === 0) {
    return null;
  }

  // Show loading state while checking health
  if (isCheckingHealth || isHealthy === null) {
    return (
      <Card className="mb-6 bg-gradient-to-r from-blue-50 to-purple-50 border-blue-200">
        <CardContent className="p-4">
          <div className="flex items-center justify-center gap-3">
            <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-purple-500 rounded-lg flex items-center justify-center">
              <SparklesIcon className="w-4 h-4 text-white animate-pulse" />
            </div>
            <p className="text-sm text-gray-600 animate-pulse">
              {tTransactions('aiCategorization.checkingAvailability')}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  // Main ribbon with two buttons
  return (
    <div className="mb-6 space-y-4">
      {/* Clean Ribbon - Hide when showing previews for better focus */}
      {!showRulePreview && !showAiSection && (
        <Card className="bg-gradient-to-r from-blue-50 to-purple-50 border-blue-200">
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-purple-500 rounded-lg flex items-center justify-center">
                  <SparklesIcon className="w-4 h-4 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">{tTransactions('categorizationRibbon.title')}</h3>
                  <p className="text-xs text-gray-600">{tTransactions('categorizationRibbon.subtitle')}</p>
                </div>
              </div>
              
              <div className="flex items-center gap-3">
                {/* AI Assistant Button */}
                <Button
                  onClick={() => setShowAiSection(true)}
                  disabled={isHealthy === false}
                  variant="secondary"
                  size="sm"
                  className="hover:bg-purple-100 border-purple-200"
                >
                  <SparklesIcon className="w-4 h-4 mr-2" />
                  {tTransactions('aiCategorization.title')}
                  {isHealthy === false && <span className="ml-1 text-xs">({tTransactions('aiCategorization.unavailableShort')})</span>}
                </Button>
                
                {/* Auto-Categorize with Rules Button */}
                <Button
                  onClick={handleRuleAutoCategorization}
                  disabled={isApplyingRules}
                  variant="secondary"
                  size="sm"
                  className="hover:bg-green-100 border-green-200"
                >
                  {isApplyingRules ? (
                    <>
                      <ArrowPathIcon className="w-4 h-4 mr-2 animate-spin" />
                      {tCommon('processing')}
                    </>
                  ) : (
                    <>
                      <Cog6ToothIcon className="w-4 h-4 mr-2" />
                      {tTransactions('ruleCategorization.autoCategorize')}
                    </>
                  )}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Rule Preview Modal */}
      {showRulePreview && rulePreview && (
        <Card className="bg-gradient-to-r from-green-50 to-blue-50 border-green-200">
          <CardContent className="p-4">
            <div className="flex items-start justify-between mb-4">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 bg-gradient-to-r from-green-500 to-blue-500 rounded-lg flex items-center justify-center">
                  <LightBulbIcon className="w-4 h-4 text-white" />
                </div>
                <div>
                  <h4 className="font-semibold text-gray-900">{tTransactions('ruleCategorization.previewTitle')}</h4>
                  <p className="text-sm text-gray-600">{rulePreview.summary}</p>
                </div>
              </div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setShowRulePreview(false)}
              >
                <XMarkIcon className="w-4 h-4" />
              </Button>
            </div>
            
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4 text-sm">
              <div className="text-center p-2 bg-white rounded-lg">
                <div className="font-semibold text-blue-600">{rulePreview.totalExamined}</div>
                <div className="text-gray-600">{tTransactions('ruleCategorization.examined')}</div>
              </div>
              <div className="text-center p-2 bg-white rounded-lg">
                <div className="font-semibold text-green-600">{rulePreview.transactionsMatched}</div>
                <div className="text-gray-600">{tTransactions('ruleCategorization.wouldMatch')}</div>
              </div>
              <div className="text-center p-2 bg-white rounded-lg">
                <div className="font-semibold text-yellow-600">{rulePreview.transactionsSkipped}</div>
                <div className="text-gray-600">{tTransactions('ruleCategorization.skipped')}</div>
              </div>
              <div className="text-center p-2 bg-white rounded-lg">
                <div className="font-semibold text-gray-600">{rulePreview.transactionsUnmatched}</div>
                <div className="text-gray-600">{tTransactions('ruleCategorization.noMatch')}</div>
              </div>
            </div>
            
            {/* Rule suggestions with selection controls */}
            {rulePreview.ruleMatches && rulePreview.ruleMatches.length > 0 && (
              <div className="mb-4">
                <div className="flex items-center justify-between mb-3">
                  <h5 className="font-medium text-gray-900 text-sm">
                    {tTransactions('ruleCategorization.ruleSuggestions', {
                      selected: rulePreview.ruleMatches.filter(m => m.isSelected).length,
                      total: rulePreview.ruleMatches.length
                    })}
                  </h5>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={handleSelectAllRuleMatches}
                      className="text-xs"
                    >
                      {rulePreview.ruleMatches.every(m => m.isSelected)
                        ? tTransactions('ruleCategorization.deselectAll')
                        : tTransactions('ruleCategorization.selectAll')}
                    </Button>
                  </div>
                </div>
                
                <div className="bg-white rounded-lg border border-gray-200 max-h-80 overflow-y-auto">
                  <div className="divide-y divide-gray-100">
                    {rulePreview.ruleMatches.map((match, index) => (
                      <div key={index} className="p-3 text-sm">
                        <div className="flex items-start gap-3">
                          <div className="flex items-center mt-1">
                            <input
                              type="checkbox"
                              checked={match.isSelected}
                              onChange={() => handleToggleRuleMatch(index)}
                              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                            />
                          </div>
                          
                          <div className="flex-1 min-w-0">
                            <div className="flex justify-between items-start mb-2">
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2">
                                  <p className="font-medium text-gray-900 truncate">
                                    {match.transactionDescription}
                                  </p>
                                  {match.isExistingCandidate && (
                                    <span className="px-2 py-1 text-xs bg-blue-100 text-blue-700 rounded">
                                      {tTransactions('ruleCategorization.existing')}
                                    </span>
                                  )}
                                  {match.canAutoApply && (
                                    <span className="px-2 py-1 text-xs bg-green-100 text-green-700 rounded">
                                      {tTransactions('ruleCategorization.highConfidence')}
                                    </span>
                                  )}
                                </div>
                                <div className="flex items-center gap-2 mt-1">
                                  <span className="text-xs text-gray-500">
                                    {new Date(match.transactionDate).toLocaleDateString()}
                                  </span>
                                  <span className="text-xs text-gray-400">•</span>
                                  <span className="text-xs text-gray-500">
                                    {match.accountName}
                                  </span>
                                </div>
                              </div>
                              <div className="text-right ml-3">
                                <p className={`font-medium ${match.transactionAmount > 0 ? 'text-green-600' : 'text-red-600'}`}>
                                  {match.transactionAmount > 0 ? '+' : ''}
                                  {formatCurrency(Math.abs(match.transactionAmount))}
                                </p>
                              </div>
                            </div>
                            
                            <div className="flex items-center justify-between mb-2">
                              <div className="flex items-center gap-2 text-xs">
                                <span className="text-gray-500">{tTransactions('ruleCategorization.ruleLabel')}</span>
                                <span className="font-medium text-blue-600">{match.ruleName}</span>
                                {match.rulePattern && (
                                  <>
                                    <span className="text-gray-400">•</span>
                                    <span className="text-gray-500 font-mono">&quot;{match.rulePattern}&quot;</span>
                                  </>
                                )}
                              </div>
                              <div className="text-xs text-gray-500">
                                {tTransactions('ruleCategorization.confidencePercent', {
                                  percent: Math.round(match.confidenceScore * 100)
                                })}
                              </div>
                            </div>
                            
                            <div className="flex items-center gap-2 text-xs">
                              {match.wouldChangeCategory && match.currentCategoryName ? (
                                <>
                                  <span className="text-gray-500">{tTransactions('ruleCategorization.changeLabel')}</span>
                                  <span className="px-2 py-1 bg-red-100 text-red-700 rounded">
                                    {match.currentCategoryName}
                                  </span>
                                  <span className="text-gray-400">→</span>
                                  <span className="px-2 py-1 bg-green-100 text-green-700 rounded">
                                    {match.categoryName}
                                  </span>
                                </>
                              ) : (
                                <>
                                  <span className="text-gray-500">{tTransactions('ruleCategorization.assignTo')}</span>
                                  <span className="px-2 py-1 bg-blue-100 text-blue-700 rounded">
                                    {match.categoryName}
                                  </span>
                                  {!match.wouldChangeCategory && (
                                    <span className="text-gray-400">{tTransactions('ruleCategorization.noChangeNeeded')}</span>
                                  )}
                                </>
                              )}
                            </div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
            
            <div className="flex justify-between items-center">
              <div className="text-sm text-gray-600">
                {rulePreview.ruleMatches && (
                  <>
                    {tTransactions('ruleCategorization.rulesSelected', {
                      selected: rulePreview.ruleMatches.filter(m => m.isSelected).length,
                      total: rulePreview.ruleMatches.length
                    })}
                    {rulePreview.ruleMatches.some(m => m.isExistingCandidate) && (
                      <span className="ml-2 text-blue-600">
                        • {tTransactions('ruleCategorization.existingCount', {
                          count: rulePreview.ruleMatches.filter(m => m.isExistingCandidate).length
                        })}
                      </span>
                    )}
                  </>
                )}
              </div>
              
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setShowRulePreview(false)}
                >
                  {tCommon('cancel')}
                </Button>
                <Button
                  onClick={handleApplySelectedRules}
                  disabled={isApplyingRules || !rulePreview.ruleMatches?.some(m => m.isSelected)}
                  size="sm"
                  className="bg-gradient-to-r from-green-500 to-blue-500 hover:from-green-600 hover:to-blue-600"
                >
                  {isApplyingRules ? (
                    <>
                      <ArrowPathIcon className="w-4 h-4 mr-2 animate-spin" />
                      {tTransactions('ruleCategorization.applying')}
                    </>
                  ) : (
                    <>
                      <PlayIcon className="w-4 h-4 mr-2" />
                      {tTransactions('ruleCategorization.applySelected', { count: rulePreview.ruleMatches?.filter(m => m.isSelected).length || 0 })}
                    </>
                  )}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* AI Assistant Section (Progressive Disclosure) */}
      {showAiSection && isHealthy && (
        <Card className="bg-gradient-to-r from-purple-50 to-blue-50 border-purple-200">
          <CardContent className="p-6">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-gradient-to-r from-purple-500 to-blue-500 rounded-lg flex items-center justify-center">
                  <SparklesIcon className="w-5 h-5 text-white" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-gray-900">{tTransactions('aiCategorization.title')}</h3>
                  <p className="text-sm text-gray-600">
                    {tTransactions('aiCategorization.subtitleLimited', { limit: 25 })}
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
                    checked={selectedTransactions.length === Math.min(transactions.length, 25) && transactions.length > 0}
                    onChange={handleSelectAll}
                    className="rounded border-gray-300 text-purple-600 focus:ring-purple-500"
                  />
                  <span className="text-sm font-medium text-gray-700">
                    {tTransactions('aiCategorization.selectAllCount', {
                      selected: selectedTransactions.length,
                      total: Math.min(transactions.length, 25)
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

              {/* Quick transaction selection - Limited to 25 */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 max-h-40 overflow-y-auto">
                {transactions.slice(0, 25).map((transaction) => (
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
              
              {transactions.length > 25 && (
                <p className="text-xs text-gray-500 mt-2">
                  {tTransactions('aiCategorization.limitNotice', { total: transactions.length })}
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
      )}
    </div>
  );
}
