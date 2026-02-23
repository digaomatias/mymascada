'use client';

import { useState, useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { 
  PlusIcon, 
  PlayIcon, 
  PauseIcon,
  PencilIcon,
  TrashIcon,
  ChartBarIcon,
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
      case 'StartsWith': return 'bg-green-100 text-green-800';
      case 'EndsWith': return 'bg-yellow-100 text-yellow-800';
      case 'Equals': return 'bg-purple-100 text-purple-800';
      case 'Regex': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const formatAccuracy = (accuracy: number) => {
    return `${(accuracy * 100).toFixed(1)}%`;
  };

  if (isLoading || loading) {
    return (
      <AppLayout>
        <div className="animate-pulse space-y-6">
          <div className="h-8 bg-gray-200 rounded w-48"></div>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-24 bg-gray-200 rounded"></div>
            ))}
          </div>
          <div className="space-y-4">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="h-32 bg-gray-200 rounded"></div>
            ))}
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      {/* Header */}
        <div className="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">{t('page.title')}</h1>
            <p className="text-gray-600 mt-1">{t('page.subtitle')}</p>
          </div>
          <div className="flex flex-wrap gap-2 sm:gap-3">
            <Link href="/rules/suggestions">
              <Button variant="secondary" className="flex items-center gap-2">
                <SparklesIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('page.viewSuggestions')}</span>
                <span className="sm:hidden">{t('page.suggestions')}</span>
              </Button>
            </Link>
            <Link href="/rules/new">
              <Button className="flex items-center gap-2">
                <PlusIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('page.createRule')}</span>
                <span className="sm:hidden">{t('page.create')}</span>
              </Button>
            </Link>
          </div>
        </div>

        {/* Statistics Cards */}
        {statistics && (
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">{t('statistics.totalRules')}</p>
                    <p className="text-2xl font-bold text-gray-900">{statistics.totalRules}</p>
                  </div>
                  <ChartBarIcon className="w-8 h-8 text-blue-500" />
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">{t('statistics.activeRules')}</p>
                    <p className="text-2xl font-bold text-gray-900">{statistics.activeRules}</p>
                  </div>
                  <PlayIcon className="w-8 h-8 text-green-500" />
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">{t('statistics.applications')}</p>
                    <p className="text-2xl font-bold text-gray-900">{statistics.totalApplications}</p>
                  </div>
                  <ChartBarIcon className="w-8 h-8 text-purple-500" />
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">{t('statistics.accuracy')}</p>
                    <p className="text-2xl font-bold text-gray-900">{formatAccuracy(statistics.overallAccuracy)}</p>
                  </div>
                  <SparklesIcon className="w-8 h-8 text-yellow-500" />
                </div>
              </CardContent>
            </Card>
          </div>
        )}

        {/* Filters */}
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center space-x-4">
            <label className="flex items-center space-x-2">
              <input
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => setIncludeInactive(e.target.checked)}
                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className="text-sm text-gray-700">{t('filters.showInactive')}</span>
            </label>
          </div>
          <p className="text-sm text-gray-500">
            {t('count', { count: rules.length })}
          </p>
        </div>

        {/* Rules List */}
        <div className="space-y-4">
          {rules.length === 0 ? (
            <Card>
              <CardContent className="p-8 text-center">
                <div className="mx-auto w-24 h-24 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                  <AdjustmentsHorizontalIcon className="w-12 h-12 text-gray-400" />
                </div>
                <h3 className="text-lg font-medium text-gray-900 mb-2">{t('empty.title')}</h3>
                <p className="text-gray-600 mb-4">
                  {t('empty.description')}
                </p>
                <Link href="/rules/new">
                  <Button>
                    <PlusIcon className="w-4 h-4 mr-2" />
                    {t('empty.createFirstRule')}
                  </Button>
                </Link>
              </CardContent>
            </Card>
          ) : (
            rules.map((rule) => (
              <Card key={rule.id} className={`${rule.isActive ? '' : 'opacity-60'}`}>
                <CardContent className="p-6">
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center space-x-3 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{rule.name}</h3>
                        <Badge className={getRuleTypeColor(rule.type)}>
                          {rule.type}
                        </Badge>
                        {rule.isAiGenerated && (
                          <Badge variant="secondary" className="bg-purple-100 text-purple-800">
                            <SparklesIcon className="w-3 h-3 mr-1" />
                            {t('badges.aiGenerated')}
                          </Badge>
                        )}
                        {!rule.isActive && (
                          <Badge variant="secondary" className="bg-gray-100 text-gray-600">
                            {t('badges.inactive')}
                          </Badge>
                        )}
                      </div>
                      
                      {rule.description && (
                        <p className="text-gray-600 mb-3">{rule.description}</p>
                      )}
                      
                      <div className="flex flex-wrap items-center gap-4 text-sm text-gray-500 mb-3">
                        <span>{t('card.pattern')} <code className="bg-gray-100 px-2 py-1 rounded">{rule.pattern}</code></span>
                        <span>{t('card.category')} <strong>{rule.categoryName}</strong></span>
                        <span>{t('priority')}: {rule.priority}</span>
                        {rule.accuracyRate > 0 && (
                          <span>{t('statistics.accuracy')}: <strong>{formatAccuracy(rule.accuracyRate)}</strong></span>
                        )}
                      </div>

                      <div className="flex items-center space-x-4 text-sm text-gray-500">
                        <span>{rule.matchCount} {t('card.matches')}</span>
                        {rule.correctionCount > 0 && (
                          <span>{rule.correctionCount} {t('card.corrections')}</span>
                        )}
                        <span>{t('card.created')} {new Date(rule.createdAt).toLocaleDateString()}</span>
                      </div>
                    </div>
                    
                    <div className="flex items-center space-x-2 ml-4">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => testRule(rule.id, rule.name)}
                        disabled={testingRuleId === rule.id}
                        title="Run rule preview to see uncategorized transactions that match this rule"
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
                      >
                        {rule.isActive ? (
                          <PauseIcon className="w-4 h-4" />
                        ) : (
                          <PlayIcon className="w-4 h-4" />
                        )}
                      </Button>
                      
                      <Link href={`/rules/${rule.id}/edit`}>
                        <Button variant="secondary" size="sm">
                          <PencilIcon className="w-4 h-4" />
                        </Button>
                      </Link>
                      
                      <Button 
                        variant="secondary" 
                        size="sm"
                        onClick={() => deleteRule(rule.id, rule.name)}
                        className="text-red-600 hover:text-red-700 hover:bg-red-50"
                      >
                        <TrashIcon className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </div>

        {/* Rule Preview Modal */}
        {showRulePreview && testResult && (
          <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-white rounded-lg max-w-4xl w-full max-h-[90vh] overflow-hidden">
              <div className="p-6 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div>
                    <h2 className="text-xl font-semibold text-gray-900">
                      {t('preview.title', { name: testResult.ruleName })}
                    </h2>
                    <p className="text-gray-600 mt-1">
                      {t('preview.subtitle')}
                    </p>
                  </div>
                  <button
                    onClick={() => {
                      setShowRulePreview(false);
                      setTestResult(null);
                    }}
                    className="text-gray-400 hover:text-gray-600"
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
                    <div className="mx-auto w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                      <EyeIcon className="w-8 h-8 text-gray-400" />
                    </div>
                    <h3 className="text-lg font-medium text-gray-900 mb-2">{t('preview.noMatchesTitle')}</h3>
                    <p className="text-gray-600">
                      {t('preview.noMatchesDesc')}
                    </p>
                  </div>
                ) : (
                  <>
                    <div className="mb-4">
                      <div className="flex items-center justify-between">
                        <p className="text-sm text-gray-600">
                          {t('preview.foundTransactions', { count: testResult.matchingTransactions.length })}{' '}
                          <strong className="text-blue-600">{testResult.matchingTransactions[0]?.suggestedCategoryName}</strong>
                        </p>
                      </div>
                    </div>

                    <div className="max-h-96 overflow-y-auto border border-gray-200 rounded-lg">
                      <div className="space-y-0">
                        {testResult.matchingTransactions.map((transaction, index) => (
                          <div
                            key={transaction.id}
                            className={`p-4 ${index !== testResult.matchingTransactions.length - 1 ? 'border-b border-gray-100' : ''}`}
                          >
                            <div className="flex items-center justify-between">
                              <div className="flex-1">
                                <div className="flex items-center space-x-3">
                                  <div className="flex-1">
                                    <p className="font-medium text-gray-900">{transaction.description}</p>
                                    <div className="flex items-center space-x-4 text-sm text-gray-500 mt-1">
                                      <span>{transaction.accountName}</span>
                                      <span>{new Date(transaction.transactionDate).toLocaleDateString()}</span>
                                    </div>
                                  </div>
                                  <div className="text-right">
                                    <p className={`font-semibold ${transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                                      {transaction.amount >= 0 ? '+' : ''}${Math.abs(transaction.amount).toFixed(2)}
                                    </p>
                                  </div>
                                </div>
                                <div className="mt-2 flex items-center space-x-2">
                                  <span className="text-sm text-gray-500">{t('preview.wouldCategorizeAs')}</span>
                                  <span className="px-2 py-1 bg-blue-100 text-blue-700 rounded text-sm font-medium">
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
                      <p className="text-sm text-gray-500">
                        {t('preview.autoInfo')}
                      </p>
                      <div className="flex space-x-3">
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