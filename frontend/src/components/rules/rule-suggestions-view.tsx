'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  SparklesIcon,
  CheckIcon,
  XMarkIcon,
  LightBulbIcon,
  ArrowRightIcon,
  ClockIcon,
  ChartBarIcon
} from '@heroicons/react/24/outline';
import { apiClient, RuleSuggestion, RuleSuggestionsSummary } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface RuleSuggestionsResponse {
  suggestions: RuleSuggestion[];
  summary: RuleSuggestionsSummary;
}

export function RuleSuggestionsView() {
  const t = useTranslations('rules');
  const [suggestions, setSuggestions] = useState<RuleSuggestionsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [creatingRules, setCreatingRules] = useState<Set<number>>(new Set());
  const [dismissedSuggestions, setDismissedSuggestions] = useState<Set<number>>(new Set());

  useEffect(() => {
    loadSuggestions();
  }, []);

  const loadSuggestions = async () => {
    try {
      setLoading(true);
      const response = await apiClient.getRuleSuggestions();
      setSuggestions(response);
    } catch (error) {
      console.error('Failed to load suggestions:', error);
      toast.error(t('toasts.loadSuggestionsFailed'));
    } finally {
      setLoading(false);
    }
  };

  const createRuleFromSuggestion = async (suggestion: RuleSuggestion, index: number) => {
    try {
      setCreatingRules(prev => new Set([...prev, index]));

      await apiClient.acceptRuleSuggestion(suggestion.id, {
        customName: suggestion.name,
        customDescription: suggestion.description,
        priority: 0
      });

      toast.success(t('toasts.suggestionAccepted', { name: suggestion.name }));

      // Reload suggestions to get updated list (this automatically excludes related suggestions)
      await loadSuggestions();

      // Clear dismissed suggestions since we have fresh data
      setDismissedSuggestions(new Set());
    } catch (error) {
      console.error('Failed to create rule:', error);
      toast.error(t('toasts.suggestionAcceptFailed'));
    } finally {
      setCreatingRules(prev => {
        const newSet = new Set(prev);
        newSet.delete(index);
        return newSet;
      });
    }
  };

  const dismissSuggestion = async (suggestionId: number, index: number) => {
    try {
      await apiClient.rejectRuleSuggestion(suggestionId);

      // Remove from local state after successful API call
      setDismissedSuggestions(prev => new Set([...prev, index]));
      toast.success(t('toasts.suggestionDismissed'));
    } catch (error) {
      console.error('Failed to dismiss suggestion:', error);
      toast.error(t('toasts.suggestionDismissFailed'));
    }
  };

  const getConfidenceColor = (score: number) => {
    if (score >= 80) return 'bg-green-100 text-green-800';
    if (score >= 60) return 'bg-yellow-100 text-yellow-800';
    return 'bg-gray-100 text-gray-800';
  };

  const formatAmount = (amount: number) => {
    const isIncome = amount > 0;
    const absAmount = Math.abs(amount);
    return (
      <span className={isIncome ? 'text-green-600' : 'text-red-600'}>
        {isIncome ? '+' : '-'}${absAmount.toFixed(2)}
      </span>
    );
  };

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="animate-pulse">
          <div className="h-8 bg-gray-200 rounded w-64 mb-4"></div>
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-48 bg-gray-200 rounded mb-4"></div>
          ))}
        </div>
      </div>
    );
  }

  if (!suggestions || suggestions.suggestions.length === 0) {
    return (
      <Card>
        <CardContent className="text-center py-12">
          <LightBulbIcon className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">{t('suggestions.empty.title')}</h3>
          <p className="text-gray-600 mb-4">
            {t('suggestions.empty.description')}
          </p>
          <p className="text-sm text-gray-500">
            {t('suggestions.empty.help')}
          </p>
        </CardContent>
      </Card>
    );
  }

  const visibleSuggestions = suggestions.suggestions.filter((_, index) => 
    !dismissedSuggestions.has(index)
  );

  return (
    <div className="space-y-6">
      {/* Header Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center">
              <SparklesIcon className="h-5 w-5 text-blue-600 mr-2" />
              <div>
                <p className="text-sm text-gray-600">{t('suggestions.stats.totalSuggestions')}</p>
                <p className="text-lg font-semibold">{suggestions.summary.totalSuggestions}</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center">
              <ChartBarIcon className="h-5 w-5 text-green-600 mr-2" />
              <div>
                <p className="text-sm text-gray-600">{t('suggestions.stats.avgConfidence')}</p>
                <p className="text-lg font-semibold">
                  {suggestions.summary.averageConfidencePercentage}%
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center">
              <ClockIcon className="h-5 w-5 text-purple-600 mr-2" />
              <div>
                <p className="text-sm text-gray-600">{t('suggestions.stats.generated')}</p>
                <p className="text-lg font-semibold">
                  {suggestions.summary.lastGeneratedDate
                    ? new Date(suggestions.summary.lastGeneratedDate).toLocaleDateString()
                    : 'N/A'
                  }
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center">
              <LightBulbIcon className="h-5 w-5 text-orange-600 mr-2" />
              <div>
                <p className="text-sm text-gray-600">{t('suggestions.stats.method')}</p>
                <p className="text-sm font-semibold">{suggestions.summary.generationMethod}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Category Distribution */}
      {Object.keys(suggestions.summary.categoryDistribution).length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">{t('suggestions.categoryDistribution')}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap gap-2">
              {Object.entries(suggestions.summary.categoryDistribution).map(([category, count]) => (
                <Badge key={category} variant="secondary">
                  {category}: {count}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Suggestions List */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">{t('suggestions.suggestedRules')}</h2>
          {visibleSuggestions.length < suggestions.suggestions.length && (
            <p className="text-sm text-gray-500">
              {t('suggestions.showingCount', { visible: visibleSuggestions.length, total: suggestions.suggestions.length })}
            </p>
          )}
        </div>

        {visibleSuggestions.map((suggestion, index) => (
          <Card key={index} className="border-l-4 border-l-blue-500">
            <CardHeader>
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <CardTitle className="text-lg flex items-center gap-2">
                    <SparklesIcon className="h-5 w-5 text-blue-600" />
                    {suggestion.name}
                    <Badge className={getConfidenceColor(suggestion.confidenceScore * 100)}>
                      {t('suggestions.confidence', { score: Math.round(suggestion.confidenceScore * 100) })}
                    </Badge>
                  </CardTitle>
                  <p className="text-gray-600 mt-1">{suggestion.description}</p>
                </div>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    onClick={() => createRuleFromSuggestion(suggestion, index)}
                    disabled={creatingRules.has(index)}
                    className="bg-green-600 hover:bg-green-700"
                  >
                    {creatingRules.has(index) ? (
                      <div className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full" />
                    ) : (
                      <CheckIcon className="h-4 w-4" />
                    )}
                    {t('suggestions.createRule')}
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => dismissSuggestion(suggestion.id, index)}
                  >
                    <XMarkIcon className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {/* Rule Details */}
                <div className="bg-gray-50 p-4 rounded-lg">
                  <h4 className="font-medium mb-2">{t('suggestions.ruleDetails')}</h4>
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                    <div>
                      <span className="text-gray-600">{t('suggestions.pattern')}</span>
                      <code className="bg-white px-2 py-1 rounded ml-2">{suggestion.pattern}</code>
                    </div>
                    <div>
                      <span className="text-gray-600">{t('suggestions.type')}</span>
                      <span className="ml-2 font-medium">{suggestion.type}</span>
                    </div>
                    <div>
                      <span className="text-gray-600">{t('suggestions.category')}</span>
                      <span className="ml-2 font-medium">{suggestion.suggestedCategoryName || t('suggestions.unknown')}</span>
                    </div>
                    <div>
                      <span className="text-gray-600">{t('suggestions.matches')}</span>
                      <span className="ml-2 font-medium">{t('suggestions.matchCount', { count: suggestion.matchCount })}</span>
                    </div>
                  </div>
                </div>

                {/* Sample Transactions */}
                {suggestion.sampleTransactions.length > 0 && (
                  <div>
                    <h4 className="font-medium mb-2">{t('suggestions.sampleTransactions')}</h4>
                    <div className="space-y-2">
                      {suggestion.sampleTransactions.map((transaction) => (
                        <div key={transaction.id} className="bg-white p-3 rounded border">
                          <div className="flex justify-between items-start">
                            <div className="flex-1">
                              <p className="font-medium">{transaction.description}</p>
                              <p className="text-sm text-gray-600">
                                {transaction.accountName} • {new Date(transaction.transactionDate).toLocaleDateString()}
                              </p>
                            </div>
                            <div className="text-right">
                              {formatAmount(transaction.amount)}
                              <div className="flex items-center text-sm text-gray-500 mt-1">
                                <ArrowRightIcon className="h-3 w-3 mr-1" />
                                {suggestion.suggestedCategoryName || t('suggestions.unknown')}
                              </div>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {visibleSuggestions.length === 0 && suggestions.suggestions.length > 0 && (
        <Card>
          <CardContent className="text-center py-8">
            <CheckIcon className="h-12 w-12 text-green-500 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-2">{t('suggestions.allProcessed.title')}</h3>
            <p className="text-gray-600">
              {t('suggestions.allProcessed.description')}
            </p>
            <Button
              onClick={loadSuggestions}
              className="mt-4"
              variant="secondary"
            >
              {t('suggestions.allProcessed.refresh')}
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}