'use client';

import { useState, useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import {
  PlusIcon,
  PlayIcon,
  PauseIcon,
  PencilIcon,
  TrashIcon,
  SparklesIcon,
  AdjustmentsHorizontalIcon,
  EyeIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import Link from 'next/link';
import { useTranslations } from 'next-intl';

interface CategorizationRule {
  id: number;
  name: string;
  description?: string;
  type: 'Contains' | 'StartsWith' | 'EndsWith' | 'Equals' | 'Regex';
  pattern: string;
  isCaseSensitive: boolean;
  priority: number;
  isActive: boolean;
  isAiGenerated: boolean;
  confidenceScore?: number;
  matchCount: number;
  correctionCount: number;
  minAmount?: number;
  maxAmount?: number;
  accountTypes?: string;
  categoryId: number;
  categoryName: string;
  logic: 'All' | 'Any';
  accuracyRate: number;
  createdAt: string;
  updatedAt: string;
  conditions: any[];
  applicationCount: number;
}

interface RuleStatistics {
  totalRules: number;
  activeRules: number;
  totalApplications: number;
  totalCorrections: number;
  overallAccuracy: number;
}

interface MatchingTransaction {
  id: number;
  description: string;
  amount: number;
  transactionDate: string;
  accountName: string;
  currentCategoryName?: string;
  suggestedCategoryName?: string;
  wouldChangeCategory: boolean;
}

interface RuleTestResult {
  ruleId: number;
  ruleName: string;
  totalMatches: number;
  matchingTransactions: MatchingTransaction[];
  testedAt: string;
  testSummary: string;
}

export default function RulesPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('rules');
  const tCommon = useTranslations('common');
  const [rules, setRules] = useState<CategorizationRule[]>([]);
  const [statistics, setStatistics] = useState<RuleStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [showRulePreview, setShowRulePreview] = useState(false);
  const [testResult, setTestResult] = useState<RuleTestResult | null>(null);
  const [testingRuleId, setTestingRuleId] = useState<number | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
      return;
    }

    if (isAuthenticated) {
      loadRules();
      loadStatistics();
    }
  }, [isAuthenticated, isLoading, router, includeInactive]);

  const loadRules = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get(`/api/rules?includeInactive=${includeInactive}`) as any[];
      setRules(response);
    } catch (error) {
      console.error('Failed to load rules:', error);
      toast.error(t('toasts.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const loadStatistics = async () => {
    try {
      const response = await apiClient.get('/api/rules/statistics');
      setStatistics(response as any);
    } catch (error) {
      console.error('Failed to load statistics:', error);
    }
  };

  const toggleRuleStatus = async (ruleId: number, isActive: boolean) => {
    try {
      const rule = rules.find(r => r.id === ruleId);
      if (!rule) return;

      await apiClient.put(`/api/rules/${ruleId}`, {
        ...rule,
        isActive: !isActive
      });

      await loadRules();
      toast.success(!isActive ? t('toasts.ruleActivated') : t('toasts.ruleDeactivated'));
    } catch (error) {
      console.error('Failed to toggle rule status:', error);
      toast.error(t('toasts.statusUpdateFailed'));
    }
  };

  const deleteRule = async (ruleId: number, ruleName: string) => {
    if (!confirm(t('toasts.deleteConfirm', { name: ruleName }))) {
      return;
    }

    try {
      await apiClient.delete(`/api/rules/${ruleId}`);
      await loadRules();
      toast.success(t('toasts.deleteSuccess'));
    } catch (error) {
      console.error('Failed to delete rule:', error);
      toast.error(t('toasts.deleteFailed'));
    }
  };

  const testRule = async (ruleId: number, ruleName: string) => {
    try {
      setTestingRuleId(ruleId);
      const result = await apiClient.post(`/api/rules/${ruleId}/test`, null) as RuleTestResult;

      // Filter to show only uncategorized transactions
      const uncategorizedTransactions = result.matchingTransactions.filter(
        tx => !tx.currentCategoryName || tx.currentCategoryName === 'Uncategorized'
      );

      setTestResult({
        ...result,
        matchingTransactions: uncategorizedTransactions,
        totalMatches: uncategorizedTransactions.length
      });
      setShowRulePreview(true);

      if (uncategorizedTransactions.length === 0) {
        toast.info(t('toasts.testNoMatches', { name: ruleName }));
      } else {
        toast.success(t('toasts.testMatchesFound', { count: uncategorizedTransactions.length, name: ruleName }));
      }
    } catch (error) {
      console.error('Failed to test rule:', error);
      toast.error(t('toasts.testFailed'));
    } finally {
      setTestingRuleId(null);
    }
  };

  const getRuleTypeColor = (type: string) => {
    switch (type) {
      case 'Contains': return 'bg-blue-100 text-blue-800';
      case 'StartsWith': return 'bg-emerald-100 text-emerald-800';
      case 'EndsWith': return 'bg-amber-100 text-amber-800';
      case 'Equals': return 'bg-violet-100 text-violet-800';
      case 'Regex': return 'bg-red-100 text-red-800';
      default: return 'bg-slate-100 text-slate-800';
    }
  };

  const formatAccuracy = (accuracy: number) => {
    return `${(accuracy * 100).toFixed(1)}%`;
  };

  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <AdjustmentsHorizontalIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('page.title')}</div>
        </div>
      </div>
    );
  }

  return (
    <AppLayout>
      {/* Header */}
      <div className="mb-5">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
              {t('page.title')}
            </h1>
            <p className="text-[15px] text-slate-500 mt-1.5">
              {t('page.subtitle')}
            </p>
          </div>
          <div className="flex flex-wrap gap-2 sm:gap-3">
            <Link href="/rules/suggestions">
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <SparklesIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('page.viewSuggestions')}</span>
                <span className="sm:hidden">{t('page.suggestions')}</span>
              </Button>
            </Link>
            <Link href="/rules/new">
              <Button size="sm" className="flex items-center gap-2">
                <PlusIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('page.createRule')}</span>
                <span className="sm:hidden">{t('page.create')}</span>
              </Button>
            </Link>
          </div>
        </div>
      </div>

      <div className="space-y-5">
        {/* Statistics Cards */}
        {statistics && (
          <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-5">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('statistics.totalRules')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {statistics.totalRules}
                </p>
              </div>

              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('statistics.activeRules')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-emerald-600">
                  {statistics.activeRules}
                </p>
              </div>

              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('statistics.applications')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {statistics.totalApplications}
                </p>
              </div>

              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('statistics.accuracy')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-2xl font-semibold text-slate-900">
                  {formatAccuracy(statistics.overallAccuracy)}
                </p>
              </div>
            </div>
          </section>
        )}

        {/* Filters */}
        <div className="flex items-center justify-between">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
              className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
            />
            <span className="text-sm text-slate-600">{t('filters.showInactive')}</span>
          </label>
          <p className="text-sm text-slate-500">
            {t('count', { count: rules.length })}
          </p>
        </div>

        {/* Rules List */}
        <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <CardContent className="p-0">
            {rules.length === 0 ? (
              <div className="text-center py-12 px-6">
                <div className="w-20 h-20 bg-gradient-to-br from-violet-400 to-fuchsia-500 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                  <AdjustmentsHorizontalIcon className="w-10 h-10 text-white" />
                </div>
                <h3 className="font-[var(--font-dash-sans)] text-xl font-semibold text-slate-900 mb-2">
                  {t('empty.title')}
                </h3>
                <p className="text-slate-500 mb-6">
                  {t('empty.description')}
                </p>
                <Link href="/rules/new">
                  <Button className="flex items-center gap-2">
                    <PlusIcon className="w-4 h-4" />
                    {t('empty.createFirstRule')}
                  </Button>
                </Link>
              </div>
            ) : (
              <div>
                {rules.map((rule) => (
                  <div
                    key={rule.id}
                    className={cn(
                      'group border-b border-slate-100 last:border-b-0 hover:bg-slate-50/60 transition-colors p-5',
                      !rule.isActive && 'opacity-60'
                    )}
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1 min-w-0">
                        {/* Rule name + badges */}
                        <div className="flex items-center gap-2 flex-wrap mb-1.5">
                          <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 truncate">
                            {rule.name}
                          </h3>
                          <Badge className={cn('text-[10px] font-medium', getRuleTypeColor(rule.type))}>
                            {rule.type}
                          </Badge>
                          {rule.isAiGenerated && (
                            <Badge variant="secondary" className="bg-violet-100 text-violet-800 text-[10px]">
                              <SparklesIcon className="w-3 h-3 mr-1" />
                              {t('badges.aiGenerated')}
                            </Badge>
                          )}
                          {!rule.isActive && (
                            <Badge variant="secondary" className="bg-slate-100 text-slate-500 text-[10px]">
                              {t('badges.inactive')}
                            </Badge>
                          )}
                        </div>

                        {rule.description && (
                          <p className="text-sm text-slate-500 mb-2">{rule.description}</p>
                        )}

                        {/* Rule metadata */}
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500 mb-2">
                          <span>
                            {t('card.pattern')}{' '}
                            <code className="bg-slate-100 px-2 py-0.5 rounded text-xs text-slate-700">{rule.pattern}</code>
                          </span>
                          <span>
                            {t('card.category')}{' '}
                            <strong className="text-slate-700">{rule.categoryName}</strong>
                          </span>
                          <span>{t('priority')}: {rule.priority}</span>
                          {rule.accuracyRate > 0 && (
                            <span>
                              {t('statistics.accuracy')}:{' '}
                              <strong className="font-[var(--font-dash-mono)] text-slate-700">{formatAccuracy(rule.accuracyRate)}</strong>
                            </span>
                          )}
                        </div>

                        {/* Stats row */}
                        <div className="flex items-center gap-4 text-xs text-slate-400">
                          <span className="font-[var(--font-dash-mono)]">{rule.matchCount} {t('card.matches')}</span>
                          {rule.correctionCount > 0 && (
                            <span className="font-[var(--font-dash-mono)]">{rule.correctionCount} {t('card.corrections')}</span>
                          )}
                          <span>{t('card.created')} {new Date(rule.createdAt).toLocaleDateString()}</span>
                        </div>
                      </div>

                      {/* Actions */}
                      <div className="flex items-center gap-1.5 shrink-0">
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => testRule(rule.id, rule.name)}
                          disabled={testingRuleId === rule.id}
                          className="w-8 h-8 p-0"
                          title={t('card.testRule')}
                        >
                          {testingRuleId === rule.id ? (
                            <div className="w-4 h-4 animate-spin rounded-full border-2 border-current border-t-transparent" />
                          ) : (
                            <EyeIcon className="w-4 h-4" />
                          )}
                        </Button>

                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => toggleRuleStatus(rule.id, rule.isActive)}
                          className="w-8 h-8 p-0"
                        >
                          {rule.isActive ? (
                            <PauseIcon className="w-4 h-4" />
                          ) : (
                            <PlayIcon className="w-4 h-4" />
                          )}
                        </Button>

                        <Link href={`/rules/${rule.id}/edit`}>
                          <Button variant="secondary" size="sm" className="w-8 h-8 p-0">
                            <PencilIcon className="w-4 h-4" />
                          </Button>
                        </Link>

                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => deleteRule(rule.id, rule.name)}
                          className="w-8 h-8 p-0 text-red-600 hover:text-red-700 hover:bg-red-50"
                        >
                          <TrashIcon className="w-4 h-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Rule Preview Modal */}
      {showRulePreview && testResult && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="rounded-[26px] border border-violet-100/60 bg-white shadow-lg shadow-violet-200/20 max-w-4xl w-full max-h-[90vh] overflow-hidden">
            <div className="p-6 border-b border-slate-200">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="font-[var(--font-dash-sans)] text-xl font-semibold text-slate-900">
                    {t('preview.title', { name: testResult.ruleName })}
                  </h2>
                  <p className="text-[15px] text-slate-500 mt-1">
                    {t('preview.subtitle')}
                  </p>
                </div>
                <button
                  onClick={() => {
                    setShowRulePreview(false);
                    setTestResult(null);
                  }}
                  className="text-slate-400 hover:text-slate-600 transition-colors"
                >
                  <span className="sr-only">{tCommon('close')}</span>
                  <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>

            <div className="p-6">
              {testResult.matchingTransactions.length === 0 ? (
                <div className="text-center py-8">
                  <div className="w-16 h-16 bg-gradient-to-br from-slate-200 to-slate-300 rounded-2xl flex items-center justify-center mx-auto mb-4">
                    <EyeIcon className="w-8 h-8 text-slate-500" />
                  </div>
                  <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
                    {t('preview.noMatchesTitle')}
                  </h3>
                  <p className="text-slate-500">
                    {t('preview.noMatchesDesc')}
                  </p>
                </div>
              ) : (
                <>
                  <div className="mb-4">
                    <p className="text-sm text-slate-600">
                      {t('preview.foundTransactions', { count: testResult.matchingTransactions.length })}{' '}
                      <strong className="text-violet-600">{testResult.matchingTransactions[0]?.suggestedCategoryName}</strong>
                    </p>
                  </div>

                  <div className="max-h-96 overflow-y-auto rounded-[16px] border border-slate-200">
                    <div className="space-y-0">
                      {testResult.matchingTransactions.map((transaction, index) => (
                        <div
                          key={transaction.id}
                          className={cn(
                            'p-4',
                            index !== testResult.matchingTransactions.length - 1 && 'border-b border-slate-100'
                          )}
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-3">
                                <div className="flex-1 min-w-0">
                                  <p className="font-medium text-slate-900 truncate">{transaction.description}</p>
                                  <div className="flex items-center gap-4 text-sm text-slate-500 mt-0.5">
                                    <span>{transaction.accountName}</span>
                                    <span>{new Date(transaction.transactionDate).toLocaleDateString()}</span>
                                  </div>
                                </div>
                                <div className="text-right shrink-0">
                                  <p className={cn(
                                    'font-[var(--font-dash-mono)] font-semibold',
                                    transaction.amount >= 0 ? 'text-emerald-600' : 'text-red-600'
                                  )}>
                                    {transaction.amount >= 0 ? '+' : ''}${Math.abs(transaction.amount).toFixed(2)}
                                  </p>
                                </div>
                              </div>
                              <div className="mt-2 flex items-center gap-2">
                                <span className="text-sm text-slate-500">{t('preview.wouldCategorizeAs')}</span>
                                <span className="px-2 py-0.5 bg-violet-100 text-violet-700 rounded-full text-xs font-medium">
                                  {transaction.suggestedCategoryName}
                                </span>
                              </div>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="mt-6 flex items-center justify-between">
                    <p className="text-sm text-slate-400">
                      {t('preview.autoInfo')}
                    </p>
                    <Button
                      variant="secondary"
                      onClick={() => {
                        setShowRulePreview(false);
                        setTestResult(null);
                      }}
                    >
                      {tCommon('close')}
                    </Button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
