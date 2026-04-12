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
  ChartBarIcon,
  BoltIcon,
} from '@heroicons/react/24/outline';
import { apiClient, RuleSuggestion, RuleSuggestionsSummary } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { CategorizationUpsellBanner } from '@/components/categorization/categorization-upsell-banner';

/**
 * Threshold for the "accept all high-confidence" bulk action, expressed as a
 * whole-number percentage so it stays consistent with the rounded value the
 * UI badge actually displays. A raw score of 0.895 rounds to "90%" in the
 * badge, so the user sees a 90%-confidence suggestion — the bulk action must
 * include that row too, otherwise the button and the UI drift apart.
 */
const HIGH_CONFIDENCE_PERCENT = 90;

/**
 * Normalize a raw confidence score (0..1) to the same rounded percentage the
 * badge renders. Callers comparing against `HIGH_CONFIDENCE_PERCENT` MUST go
 * through this helper so the gating logic tracks the displayed value.
 */
function toDisplayedConfidencePercent(score: number): number {
  return Math.round(score * 100);
}

/**
 * Notify the sidebar that the pending rule-suggestions count may have
 * changed. `navigation.tsx` listens for this and refetches
 * `/RuleSuggestions/summary` so the badge stays in sync after accepts /
 * dismisses / bulk-accepts. No-op during SSR.
 */
function notifySuggestionsChanged() {
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new CustomEvent('mymascada:rule-suggestions-changed'));
  }
}

interface RuleSuggestionsResponse {
  suggestions: RuleSuggestion[];
  summary: RuleSuggestionsSummary;
}

export function RuleSuggestionsView() {
  const t = useTranslations('rules');
  const tSuggestions = useTranslations('rules.suggestions');
  const [suggestions, setSuggestions] = useState<RuleSuggestionsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [creatingRules, setCreatingRules] = useState<Set<number>>(new Set());
  // Id-keyed (not index-keyed) so in-session dismissals stay bound to the
  // correct suggestion even if `loadSuggestions()` reloads and reorders the
  // list. Index keys would shadow a different row after a reorder.
  const [dismissedSuggestions, setDismissedSuggestions] = useState<Set<number>>(new Set());
  const [bulkAccepting, setBulkAccepting] = useState(false);

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

      // Tell the sidebar to refetch its pending-count badge — the suggestion
      // we just accepted and any siblings the backend auto-cleared are gone.
      notifySuggestionsChanged();
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

  const dismissSuggestion = async (suggestionId: number) => {
    try {
      await apiClient.rejectRuleSuggestion(suggestionId);

      // Track dismissal by suggestion id so a subsequent `loadSuggestions()`
      // reorder can't shadow a different row.
      setDismissedSuggestions(prev => new Set([...prev, suggestionId]));
      toast.success(t('toasts.suggestionDismissed'));

      // Sidebar badge should drop by one — the backend already persisted
      // the rejection, so a refetch reflects reality.
      notifySuggestionsChanged();
    } catch (error) {
      console.error('Failed to dismiss suggestion:', error);
      toast.error(t('toasts.suggestionDismissFailed'));
    }
  };

  const bulkAcceptHighConfidence = async () => {
    if (!suggestions) return;
    // Skip anything the user already dismissed in-session — otherwise the
    // button count and the actual bulk action drift apart and dismissed rows
    // get silently re-accepted. Dismissal set is id-keyed so this stays
    // correct even after a reload that reorders the list.
    const candidates = suggestions.suggestions.filter(
      (s) =>
        !dismissedSuggestions.has(s.id) &&
        toDisplayedConfidencePercent(s.confidenceScore) >= HIGH_CONFIDENCE_PERCENT,
    );
    if (candidates.length === 0) {
      toast.info(tSuggestions('bulkAcceptEmpty'));
      return;
    }

    try {
      setBulkAccepting(true);
      // Process candidates in bounded batches rather than firing every
      // request at once. Unbounded parallel writes can trip rate limits or
      // connection-pool limits when a user has many high-confidence rules.
      const BATCH_SIZE = 5;
      const results: PromiseSettledResult<unknown>[] = [];
      for (let i = 0; i < candidates.length; i += BATCH_SIZE) {
        const batch = candidates.slice(i, i + BATCH_SIZE);
        const batchResults = await Promise.allSettled(
          batch.map((s) =>
            apiClient.acceptRuleSuggestion(s.id, {
              customName: s.name,
              customDescription: s.description,
              priority: 0,
            }),
          ),
        );
        results.push(...batchResults);
      }
      const succeeded = results.filter((r) => r.status === 'fulfilled').length;
      const failed = results.length - succeeded;

      if (failed === 0) {
        toast.success(tSuggestions('bulkAcceptSuccess', { count: succeeded }));
      } else {
        toast.warning(tSuggestions('bulkAcceptPartial', { success: succeeded, failed }));
      }

      await loadSuggestions();
      setDismissedSuggestions(new Set());

      // Sidebar badge needs to drop — this is the most visible stale-data
      // path in the whole rule-suggestions flow.
      notifySuggestionsChanged();
    } catch (error) {
      console.error('Bulk accept failed:', error);
      toast.error(tSuggestions('bulkAcceptFailed'));
    } finally {
      setBulkAccepting(false);
    }
  };

  const getConfidenceColor = (score: number) => {
    if (score >= 80) return 'bg-emerald-100 text-emerald-800';
    if (score >= 60) return 'bg-amber-100 text-amber-800';
    return 'bg-ink-100 text-ink-600';
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
        <section className="rounded-[26px] border border-ink-200 bg-white/90 p-6 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="animate-pulse">
                <div className="h-3 bg-ink-200 rounded w-24 mb-2"></div>
                <div className="h-6 bg-ink-200 rounded w-16"></div>
              </div>
            ))}
          </div>
        </section>
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="h-48 bg-ink-100 rounded-[26px]"></div>
          </div>
        ))}
      </div>
    );
  }

  if (!suggestions || suggestions.suggestions.length === 0) {
    return (
      <Card className="rounded-[26px] border border-ink-200 bg-white/90 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
        <CardContent className="text-center py-12 px-6">
          <div className="w-20 h-20 bg-gradient-to-br from-amber-400 to-amber-500 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
            <LightBulbIcon className="w-10 h-10 text-white" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-xl font-semibold text-ink-900 mb-2">
            {t('suggestions.empty.title')}
          </h3>
          <p className="text-ink-500 mb-4">
            {t('suggestions.empty.description')}
          </p>
          <p className="text-sm text-ink-400">
            {t('suggestions.empty.help')}
          </p>
        </CardContent>
      </Card>
    );
  }

  const visibleSuggestions = suggestions.suggestions.filter(
    (s) => !dismissedSuggestions.has(s.id),
  );

  // Count only *visible* (non-dismissed) high-confidence suggestions so the
  // "Accept all {count}" button stays in sync with `bulkAcceptHighConfidence`,
  // which also skips dismissed rows.
  const highConfidenceCount = suggestions.suggestions.filter(
    (s) =>
      !dismissedSuggestions.has(s.id) &&
      toDisplayedConfidencePercent(s.confidenceScore) >= HIGH_CONFIDENCE_PERCENT,
  ).length;

  return (
    <div className="space-y-5">
      {/* Upsell banner — free-tier users only; self-hosted hidden */}
      <CategorizationUpsellBanner context="ruleSuggestions" dismissible />

      {/* Header Stats */}
      <section className="rounded-[26px] border border-ink-200 bg-white/90 p-6 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <SparklesIcon className="h-4 w-4 text-primary-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-400">
                {t('suggestions.stats.totalSuggestions')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-2xl font-semibold text-ink-900">
              {suggestions.summary.totalSuggestions}
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <ChartBarIcon className="h-4 w-4 text-emerald-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-400">
                {t('suggestions.stats.avgConfidence')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-2xl font-semibold text-ink-900">
              {suggestions.summary.averageConfidencePercentage}%
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <ClockIcon className="h-4 w-4 text-primary-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-400">
                {t('suggestions.stats.generated')}
              </p>
            </div>
            <p className="font-[var(--font-dash-mono)] text-lg font-semibold text-ink-900">
              {suggestions.summary.lastGeneratedDate
                ? new Date(suggestions.summary.lastGeneratedDate).toLocaleDateString()
                : 'N/A'
              }
            </p>
          </div>

          <div>
            <div className="flex items-center gap-2 mb-1">
              <LightBulbIcon className="h-4 w-4 text-amber-500" />
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-400">
                {t('suggestions.stats.method')}
              </p>
            </div>
            <p className="text-sm font-semibold text-ink-900">{suggestions.summary.generationMethod}</p>
          </div>
        </div>
      </section>

      {/* Category Distribution */}
      {Object.keys(suggestions.summary.categoryDistribution).length > 0 && (
        <section className="rounded-[26px] border border-ink-200 bg-white/90 p-5 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
          <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-ink-900 mb-3">
            {t('suggestions.categoryDistribution')}
          </h3>
          <div className="flex flex-wrap gap-2">
            {Object.entries(suggestions.summary.categoryDistribution).map(([category, count]) => (
              <Badge key={category} variant="secondary" className="bg-ink-100 text-ink-700">
                {category}: {count}
              </Badge>
            ))}
          </div>
        </section>
      )}

      {/* Suggestions List */}
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-[var(--font-dash-sans)] text-lg font-semibold text-ink-900">
            {t('suggestions.suggestedRules')}
          </h2>
          <div className="flex items-center gap-3">
            {visibleSuggestions.length < suggestions.suggestions.length && (
              <p className="text-sm text-ink-500">
                {t('suggestions.showingCount', { visible: visibleSuggestions.length, total: suggestions.suggestions.length })}
              </p>
            )}
            {highConfidenceCount > 0 && (
              <Button
                size="sm"
                onClick={bulkAcceptHighConfidence}
                disabled={bulkAccepting}
                className="bg-primary-600 hover:bg-primary-700 text-white flex items-center gap-1.5"
                data-testid="bulk-accept-high-confidence"
              >
                {bulkAccepting ? (
                  <div className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full" />
                ) : (
                  <BoltIcon className="h-4 w-4" />
                )}
                {bulkAccepting
                  ? tSuggestions('acceptingAll')
                  : tSuggestions('acceptAllHighConfidenceCount', { count: highConfidenceCount })}
              </Button>
            )}
          </div>
        </div>

        {visibleSuggestions.map((suggestion, index) => (
          <section
            key={index}
            className="rounded-[26px] border border-ink-200 border-l-4 border-l-primary-500 bg-white/90 shadow-lg shadow-primary-200/20 backdrop-blur-xs overflow-hidden"
          >
            {/* Header */}
            <div className="p-5 pb-0">
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <SparklesIcon className="h-5 w-5 text-primary-500 shrink-0" />
                    <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-ink-900 truncate">
                      {suggestion.name}
                    </h3>
                    <Badge className={cn('text-[10px] font-medium', getConfidenceColor(suggestion.confidenceScore * 100))}>
                      {t('suggestions.confidence', { score: toDisplayedConfidencePercent(suggestion.confidenceScore) })}
                    </Badge>
                  </div>
                  <p className="text-sm text-ink-500 mt-1">{suggestion.description}</p>
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
                    onClick={() => dismissSuggestion(suggestion.id)}
                    className="text-ink-400 hover:text-ink-600"
                  >
                    <XMarkIcon className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </div>

            <div className="p-5">
              <div className="space-y-4">
                {/* Impact preview — how many transactions this rule would categorize */}
                <div className="rounded-2xl border border-primary-200 bg-primary-50/60 p-3 text-sm text-primary-900 flex items-start gap-2">
                  <BoltIcon className="h-4 w-4 shrink-0 mt-0.5 text-primary-500" />
                  <span data-testid="suggestion-impact-preview">
                    {tSuggestions('impactPreview', { count: suggestion.matchCount })}
                  </span>
                </div>

                {/* Rule Details */}
                <div className="bg-ink-50 p-4 rounded-2xl">
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-400 mb-3">
                    {t('suggestions.ruleDetails')}
                  </h4>
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                    <div>
                      <span className="text-ink-500">{t('suggestions.pattern')}</span>
                      <code className="bg-white px-2 py-0.5 rounded text-xs text-ink-700 ml-2">{suggestion.pattern}</code>
                    </div>
                    <div>
                      <span className="text-ink-500">{t('suggestions.type')}</span>
                      <span className="ml-2 font-medium text-ink-700">{suggestion.type}</span>
                    </div>
                    <div>
                      <span className="text-ink-500">{t('suggestions.category')}</span>
                      <span className="ml-2 font-medium text-ink-700">{suggestion.suggestedCategoryName || t('suggestions.unknown')}</span>
                    </div>
                    <div>
                      <span className="text-ink-500">{t('suggestions.matches')}</span>
                      <span className="ml-2 font-[var(--font-dash-mono)] font-medium text-ink-700">
                        {t('suggestions.matchCount', { count: suggestion.matchCount })}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Sample Transactions */}
                {suggestion.sampleTransactions.length > 0 && (
                  <div>
                    <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-400 mb-3">
                      {t('suggestions.sampleTransactions')}
                    </h4>
                    <div className="space-y-2">
                      {suggestion.sampleTransactions.map((transaction) => (
                        <div key={transaction.id} className="bg-ink-50 p-3 rounded-xl">
                          <div className="flex justify-between items-start gap-4">
                            <div className="flex-1 min-w-0">
                              <p className="font-medium text-ink-900 truncate">{transaction.description}</p>
                              <p className="text-sm text-ink-500 mt-0.5">
                                {transaction.accountName} &middot; {new Date(transaction.transactionDate).toLocaleDateString()}
                              </p>
                            </div>
                            <div className="text-right shrink-0">
                              {formatAmount(transaction.amount)}
                              <div className="flex items-center text-xs text-ink-400 mt-0.5 justify-end">
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
        <Card className="rounded-[26px] border border-ink-200 bg-white/90 shadow-lg shadow-primary-200/20 backdrop-blur-xs">
          <CardContent className="text-center py-12 px-6">
            <div className="w-16 h-16 bg-gradient-to-br from-emerald-400 to-emerald-500 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-ink-900 mb-2">
              {t('suggestions.allProcessed.title')}
            </h3>
            <p className="text-ink-500">
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
