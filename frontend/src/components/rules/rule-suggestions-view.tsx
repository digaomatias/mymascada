'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
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
  // eslint-disable-next-line react-hooks/exhaustive-deps
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
    if (score >= 80) return 'bg-emerald-100 text-emerald-800';
    if (score >= 60) return 'bg-amber-100 text-amber-800';
    return 'bg-slate-100 text-slate-600';
  };

  const formatAmount = (amount: number) => {
    const isIncome = amount > 0;
    const absAmount = Math.abs(amount);
    return (
      <span className={cn(
        'font-[var(--font-dash-mono)] font-semibold',
        isIncome ? 'text-emerald-600' : 'text-red-600'
      )}>
        {isIncome ? '+' : '-'}${absAmount.toFixed(2)}
      </span>
    );
  };

  if (loading) {
    return (
      <div className="space-y-5">
        {/* Skeleton stat cards */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="animate-pulse">
                <div className="h-3 bg-slate-200 rounded w-24 mb-2"></div>
                <div className="h-6 bg-slate-200 rounded w-16"></div>
              </div>
            ))}
          </div>
        </section>
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="h-48 bg-slate-100 rounded-[26px]"></div>
          </div>
        ))}
      </div>
    );
  }

  if (!suggestions || suggestions.suggestions.length === 0) {
    return (
      <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
        <CardContent className="text-center py-12 px-6">
          <div className="w-20 h-20 bg-gradient-to-br from-amber-400 to-amber-500 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
            <LightBulbIcon className="w-10 h-10 text-white" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-xl font-semibold text-slate-900 mb-2">
            {t('suggestions.empty.title')}
          </h3>
          <p className="text-slate-500 mb-4">
            {t('suggestions.empty.description')}
          </p>
          <p className="text-sm text-slate-400">
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
    <div className="space-y-5">
      {/* Header Stats */}
      <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <SparklesIcon className="h-4 w-4 text-violet-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('suggestions.stats.totalSuggestions')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
              {suggestions.summary.totalSuggestions}
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <ChartBarIcon className="h-4 w-4 text-emerald-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('suggestions.stats.avgConfidence')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
              {suggestions.summary.averageConfidencePercentage}%
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <ClockIcon className="h-4 w-4 text-violet-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('suggestions.stats.generated')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-lg font-semibold text-slate-900">
              {suggestions.summary.lastGeneratedDate
                ? new Date(suggestions.summary.lastGeneratedDate).toLocaleDateString()
                : 'N/A'
              }
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <LightBulbIcon className="h-4 w-4 text-amber-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('suggestions.stats.method')}
              </p>
            </div>
            <p className="text-sm font-semibold text-slate-900">{suggestions.summary.generationMethod}</p>
          </div>
        </div>
      </section>

      {/* Category Distribution */}
      {Object.keys(suggestions.summary.categoryDistribution).length > 0 && (
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 mb-3">
            {t('suggestions.categoryDistribution')}
          </h3>
          <div className="flex flex-wrap gap-2">
            {Object.entries(suggestions.summary.categoryDistribution).map(([category, count]) => (
              <Badge key={category} variant="secondary" className="bg-slate-100 text-slate-700">
                {category}: {count}
              </Badge>
            ))}
          </div>
        </section>
      )}

      {/* Suggestions List */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
            {t('suggestions.suggestedRules')}
          </h2>
          {visibleSuggestions.length < suggestions.suggestions.length && (
            <p className="text-sm text-slate-500">
              {t('suggestions.showingCount', { visible: visibleSuggestions.length, total: suggestions.suggestions.length })}
            </p>
          )}
        </div>

        {visibleSuggestions.map((suggestion, index) => (
          <section
            key={index}
            className="rounded-[26px] border border-violet-100/60 border-l-4 border-l-violet-500 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs overflow-hidden"
          >
            {/* Header */}
            <div className="p-5 pb-0">
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <SparklesIcon className="h-5 w-5 text-violet-500 shrink-0" />
                    <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 truncate">
                      {suggestion.name}
                    </h3>
                    <Badge className={cn('text-[10px] font-medium', getConfidenceColor(suggestion.confidenceScore * 100))}>
                      {t('suggestions.confidence', { score: Math.round(suggestion.confidenceScore * 100) })}
                    </Badge>
                  </div>
                  <p className="text-sm text-slate-500 mt-1">{suggestion.description}</p>
                </div>
                <div className="flex gap-2 shrink-0">
                  <Button
                    size="sm"
                    onClick={() => createRuleFromSuggestion(suggestion, index)}
                    disabled={creatingRules.has(index)}
                    className="bg-emerald-600 hover:bg-emerald-700 flex items-center gap-1.5"
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
                    className="text-slate-400 hover:text-slate-600"
                  >
                    <XMarkIcon className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>

            <div className="p-5">
              <div className="space-y-4">
                {/* Rule Details */}
                <div className="bg-slate-50 p-4 rounded-2xl">
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">
                    {t('suggestions.ruleDetails')}
                  </h4>
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                    <div>
                      <span className="text-slate-500">{t('suggestions.pattern')}</span>
                      <code className="bg-white px-2 py-0.5 rounded text-xs text-slate-700 ml-2">{suggestion.pattern}</code>
                    </div>
                    <div>
                      <span className="text-slate-500">{t('suggestions.type')}</span>
                      <span className="ml-2 font-medium text-slate-700">{suggestion.type}</span>
                    </div>
                    <div>
                      <span className="text-slate-500">{t('suggestions.category')}</span>
                      <span className="ml-2 font-medium text-slate-700">{suggestion.suggestedCategoryName || t('suggestions.unknown')}</span>
                    </div>
                    <div>
                      <span className="text-slate-500">{t('suggestions.matches')}</span>
                      <span className="ml-2 font-[var(--font-dash-mono)] font-medium text-slate-700">
                        {t('suggestions.matchCount', { count: suggestion.matchCount })}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Sample Transactions */}
                {suggestion.sampleTransactions.length > 0 && (
                  <div>
                    <h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-3">
                      {t('suggestions.sampleTransactions')}
                    </h4>
                    <div className="space-y-2">
                      {suggestion.sampleTransactions.map((transaction) => (
                        <div key={transaction.id} className="bg-slate-50 p-3 rounded-xl">
                          <div className="flex justify-between items-start gap-4">
                            <div className="flex-1 min-w-0">
                              <p className="font-medium text-slate-900 truncate">{transaction.description}</p>
                              <p className="text-sm text-slate-500 mt-0.5">
                                {transaction.accountName} &middot; {new Date(transaction.transactionDate).toLocaleDateString()}
                              </p>
                            </div>
                            <div className="text-right shrink-0">
                              {formatAmount(transaction.amount)}
                              <div className="flex items-center text-xs text-slate-400 mt-0.5 justify-end">
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
            </div>
          </section>
        ))}
      </div>

      {visibleSuggestions.length === 0 && suggestions.suggestions.length > 0 && (
        <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <CardContent className="text-center py-12 px-6">
            <div className="w-16 h-16 bg-gradient-to-br from-emerald-400 to-emerald-500 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
              {t('suggestions.allProcessed.title')}
            </h3>
            <p className="text-slate-500">
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
