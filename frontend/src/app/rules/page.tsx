'use client';

import { useState, useEffect, useMemo } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import {
  PlusIcon,
  PlayIcon,
  PauseIcon,
  PencilIcon,
  TrashIcon,
  SparklesIcon,
  AdjustmentsHorizontalIcon,
  EyeIcon,
  Bars3Icon,
  ArrowUpIcon,
  ArrowDownIcon,
  ArrowsUpDownIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import Link from 'next/link';
import { useTranslations } from 'next-intl';

type RuleType = 'Contains' | 'StartsWith' | 'EndsWith' | 'Equals' | 'Regex';
type RuleTypeValue = RuleType | 1 | 2 | 3 | 4 | 5;

interface CategorizationRule {
  id: number;
  name: string;
  description?: string;
  type: RuleTypeValue;
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
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | RuleType>('all');
  const [sortBy, setSortBy] = useState<'priorityAsc' | 'priorityDesc' | 'matchesDesc' | 'accuracyDesc' | 'createdDesc'>('priorityAsc');
  const [selectedRuleIds, setSelectedRuleIds] = useState<Set<number>>(new Set());
  const [isBulkUpdating, setIsBulkUpdating] = useState(false);
  const [isReorderMode, setIsReorderMode] = useState(false);
  const [reorderRuleIds, setReorderRuleIds] = useState<number[]>([]);
  const [draggedRuleId, setDraggedRuleId] = useState<number | null>(null);
  const [dragOverRuleId, setDragOverRuleId] = useState<number | null>(null);
  const [isSavingReorder, setIsSavingReorder] = useState(false);
  const [showRulePreview, setShowRulePreview] = useState(false);
  const [testResult, setTestResult] = useState<RuleTestResult | null>(null);
  const [testingRuleId, setTestingRuleId] = useState<number | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
      return;
    }

    if (isAuthenticated) {
      refreshRulesData();
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

  const refreshRulesData = async () => {
    await Promise.all([loadRules(), loadStatistics()]);
  };

  const toggleRuleStatus = async (ruleId: number, isActive: boolean) => {
    try {
      const rule = rules.find(r => r.id === ruleId);
      if (!rule) return;

      await apiClient.put(`/api/rules/${ruleId}`, {
        ...rule,
        isActive: !isActive
      });

      await refreshRulesData();
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
      await refreshRulesData();
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

  const normalizeRuleType = (type: RuleTypeValue): RuleType | null => {
    if (typeof type === 'number') {
      switch (type) {
        case 1: return 'Contains';
        case 2: return 'StartsWith';
        case 3: return 'EndsWith';
        case 4: return 'Equals';
        case 5: return 'Regex';
        default: return null;
      }
    }
    return type;
  };

  const getRuleTypeColor = (type: RuleTypeValue) => {
    switch (normalizeRuleType(type)) {
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

  const getRuleTypeLabel = (type: RuleTypeValue) => {
    switch (normalizeRuleType(type)) {
      case 'Contains': return t('builder.ruleTypes.contains');
      case 'StartsWith': return t('builder.ruleTypes.startsWith');
      case 'EndsWith': return t('builder.ruleTypes.endsWith');
      case 'Equals': return t('builder.ruleTypes.equals');
      case 'Regex': return t('builder.ruleTypes.regex');
      default: return String(type);
    }
  };

  const prioritySortedRules = useMemo(() => {
    return [...rules].sort((a, b) => {
      if (a.priority !== b.priority) {
        return a.priority - b.priority;
      }
      return a.id - b.id;
    });
  }, [rules]);

  const visibleRules = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase();

    const filteredRules = rules.filter((rule) => {
      const normalizedRuleType = normalizeRuleType(rule.type);
      if (typeFilter !== 'all' && normalizedRuleType !== typeFilter) {
        return false;
      }

      if (!normalizedQuery) {
        return true;
      }

      return [
        rule.name,
        rule.pattern,
        rule.categoryName,
        rule.description || ''
      ].some((field) => field.toLowerCase().includes(normalizedQuery));
    });

    return filteredRules.sort((a, b) => {
      switch (sortBy) {
        case 'priorityAsc':
          return a.priority - b.priority;
        case 'priorityDesc':
          return b.priority - a.priority;
        case 'matchesDesc':
          return b.matchCount - a.matchCount;
        case 'accuracyDesc':
          return b.accuracyRate - a.accuracyRate;
        case 'createdDesc':
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        default:
          return 0;
      }
    });
  }, [rules, searchQuery, sortBy, typeFilter]);

  const reorderRules = useMemo(() => {
    if (!isReorderMode) {
      return [];
    }
    const ruleMap = new Map(rules.map((rule) => [rule.id, rule]));
    return reorderRuleIds
      .map((id) => ruleMap.get(id))
      .filter((rule): rule is CategorizationRule => !!rule);
  }, [isReorderMode, reorderRuleIds, rules]);

  const selectedCount = selectedRuleIds.size;

  useEffect(() => {
    setSelectedRuleIds((previous) => {
      const validRuleIds = new Set(rules.map((rule) => rule.id));
      const next = new Set<number>();
      for (const ruleId of previous) {
        if (validRuleIds.has(ruleId)) {
          next.add(ruleId);
        }
      }
      return next;
    });
  }, [rules]);

  const moveRuleInOrder = (sourceRuleId: number, targetRuleId: number) => {
    setReorderRuleIds((previous) => {
      const sourceIndex = previous.indexOf(sourceRuleId);
      const targetIndex = previous.indexOf(targetRuleId);
      if (sourceIndex === -1 || targetIndex === -1 || sourceIndex === targetIndex) {
        return previous;
      }

      const next = [...previous];
      next.splice(sourceIndex, 1);
      next.splice(targetIndex, 0, sourceRuleId);
      return next;
    });
  };

  const moveRuleByOffset = (ruleId: number, offset: number) => {
    setReorderRuleIds((previous) => {
      const currentIndex = previous.indexOf(ruleId);
      if (currentIndex === -1) {
        return previous;
      }

      const nextIndex = currentIndex + offset;
      if (nextIndex < 0 || nextIndex >= previous.length) {
        return previous;
      }

      const next = [...previous];
      next.splice(currentIndex, 1);
      next.splice(nextIndex, 0, ruleId);
      return next;
    });
  };

  const startReorderMode = () => {
    setReorderRuleIds(prioritySortedRules.map((rule) => rule.id));
    setDraggedRuleId(null);
    setDragOverRuleId(null);
    setIsReorderMode(true);
  };

  const cancelReorderMode = () => {
    setIsReorderMode(false);
    setReorderRuleIds([]);
    setDraggedRuleId(null);
    setDragOverRuleId(null);
  };

  const saveRuleOrder = async () => {
    if (!isReorderMode || reorderRuleIds.length === 0) {
      return;
    }

    try {
      setIsSavingReorder(true);

      const rulePriorities = reorderRuleIds.reduce<Record<number, number>>((acc, ruleId, index) => {
        acc[ruleId] = index;
        return acc;
      }, {});

      await apiClient.put('/api/rules/priorities', { rulePriorities });
      await refreshRulesData();
      toast.success(t('toasts.prioritiesUpdated'));
      cancelReorderMode();
    } catch (error) {
      console.error('Failed to update priorities:', error);
      toast.error(t('toasts.prioritiesUpdateFailed'));
    } finally {
      setIsSavingReorder(false);
    }
  };

  const updateSelectionForVisibleRules = (selected: boolean) => {
    const visibleRuleIds = visibleRules.map((rule) => rule.id);
    setSelectedRuleIds((previous) => {
      const next = new Set(previous);
      for (const ruleId of visibleRuleIds) {
        if (selected) {
          next.add(ruleId);
        } else {
          next.delete(ruleId);
        }
      }
      return next;
    });
  };

  const toggleRuleSelection = (ruleId: number, selected: boolean) => {
    setSelectedRuleIds((previous) => {
      const next = new Set(previous);
      if (selected) {
        next.add(ruleId);
      } else {
        next.delete(ruleId);
      }
      return next;
    });
  };

  const runBulkStatusUpdate = async (activate: boolean) => {
    const selectedRules = rules.filter((rule) => selectedRuleIds.has(rule.id));
    const targetRules = selectedRules.filter((rule) => rule.isActive !== activate);
    if (targetRules.length === 0) {
      return;
    }

    try {
      setIsBulkUpdating(true);
      await Promise.all(
        targetRules.map((rule) =>
          apiClient.put(`/api/rules/${rule.id}`, {
            ...rule,
            isActive: activate
          })
        )
      );

      await refreshRulesData();
      setSelectedRuleIds(new Set());
      toast.success(
        activate
          ? t('toasts.bulkActivateSuccess', { count: targetRules.length })
          : t('toasts.bulkDeactivateSuccess', { count: targetRules.length })
      );
    } catch (error) {
      console.error('Failed to bulk update rule status:', error);
      toast.error(t('toasts.bulkUpdateFailed'));
    } finally {
      setIsBulkUpdating(false);
    }
  };

  const runBulkDelete = async () => {
    if (selectedRuleIds.size === 0) {
      return;
    }

    if (!confirm(t('toasts.bulkDeleteConfirm', { count: selectedRuleIds.size }))) {
      return;
    }

    const selectedIds = [...selectedRuleIds];

    try {
      setIsBulkUpdating(true);
      await Promise.all(selectedIds.map((ruleId) => apiClient.delete(`/api/rules/${ruleId}`)));
      await refreshRulesData();
      setSelectedRuleIds(new Set());
      toast.success(t('toasts.bulkDeleteSuccess', { count: selectedIds.length }));
    } catch (error) {
      console.error('Failed to bulk delete rules:', error);
      toast.error(t('toasts.bulkUpdateFailed'));
    } finally {
      setIsBulkUpdating(false);
    }
  };

  if (isLoading) {
    return (
      <AppLayout>
        <div className="min-h-[50vh] flex items-center justify-center">
          <div className="text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
              <AdjustmentsHorizontalIcon className="w-8 h-8 text-white" />
            </div>
            <div className="mt-6 text-slate-700 font-medium">{t('page.title')}</div>
          </div>
        </div>
      </AppLayout>
    );
  }

  if (!isAuthenticated) {
    return null;
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
        <section className="rounded-[20px] border border-violet-100/60 bg-white/90 p-4 shadow-sm shadow-violet-200/20 backdrop-blur-xs space-y-3">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => setIncludeInactive(e.target.checked)}
                className="rounded border-slate-300 text-violet-600 focus:ring-violet-500"
              />
              <span className="text-sm text-slate-600">{t('filters.showInactive')}</span>
            </label>
            <div className="flex items-center gap-2 text-sm text-slate-500">
              {loading && (
                <div className="w-3.5 h-3.5 animate-spin rounded-full border-2 border-slate-300 border-t-violet-500" />
              )}
              <span>{t('filters.rulesFound', { count: visibleRules.length })}</span>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label htmlFor="ruleSearch" className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('filters.search')}
              </label>
              <Input
                id="ruleSearch"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder={t('filters.searchPlaceholder')}
                className="mt-1"
              />
            </div>

            <div>
              <label htmlFor="ruleTypeFilter" className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('filters.type')}
              </label>
              <select
                id="ruleTypeFilter"
                value={typeFilter}
                onChange={(e) => setTypeFilter(e.target.value as 'all' | RuleType)}
                className="select mt-1"
              >
                <option value="all">{t('filters.allTypes')}</option>
                <option value="Contains">{getRuleTypeLabel('Contains')}</option>
                <option value="StartsWith">{getRuleTypeLabel('StartsWith')}</option>
                <option value="EndsWith">{getRuleTypeLabel('EndsWith')}</option>
                <option value="Equals">{getRuleTypeLabel('Equals')}</option>
                <option value="Regex">{getRuleTypeLabel('Regex')}</option>
              </select>
            </div>

            <div>
              <label htmlFor="ruleSort" className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('filters.sort')}
              </label>
              <select
                id="ruleSort"
                value={sortBy}
                onChange={(e) => setSortBy(e.target.value as 'priorityAsc' | 'priorityDesc' | 'matchesDesc' | 'accuracyDesc' | 'createdDesc')}
                className="select mt-1"
              >
                <option value="priorityAsc">{t('filters.sortPriorityAsc')}</option>
                <option value="priorityDesc">{t('filters.sortPriorityDesc')}</option>
                <option value="matchesDesc">{t('filters.sortMatchesDesc')}</option>
                <option value="accuracyDesc">{t('filters.sortAccuracyDesc')}</option>
                <option value="createdDesc">{t('filters.sortCreatedDesc')}</option>
              </select>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2 pt-1">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => updateSelectionForVisibleRules(true)}
              disabled={visibleRules.length === 0 || isReorderMode}
            >
              {t('bulk.selectVisible')}
            </Button>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setSelectedRuleIds(new Set())}
              disabled={selectedCount === 0}
            >
              {t('bulk.clearSelection')}
            </Button>
            <span className="text-sm text-slate-500">
              {t('bulk.selectedCount', { count: selectedCount })}
            </span>

            <div className="sm:ml-auto">
              {!isReorderMode ? (
                <Button
                  variant="secondary"
                  size="sm"
                  className="flex items-center gap-1.5"
                  onClick={startReorderMode}
                  disabled={rules.length < 2}
                >
                  <ArrowsUpDownIcon className="w-4 h-4" />
                  {t('reorder.start')}
                </Button>
              ) : (
                <Badge variant="secondary" className="bg-violet-100 text-violet-700">
                  {t('reorder.modeActive')}
                </Badge>
              )}
            </div>
          </div>

          {selectedCount > 0 && !isReorderMode && (
            <div className="rounded-xl border border-violet-100 bg-violet-50/50 p-3 flex flex-wrap items-center gap-2">
              <Button
                size="sm"
                variant="secondary"
                onClick={() => runBulkStatusUpdate(true)}
                disabled={isBulkUpdating}
              >
                {t('bulk.activate')}
              </Button>
              <Button
                size="sm"
                variant="secondary"
                onClick={() => runBulkStatusUpdate(false)}
                disabled={isBulkUpdating}
              >
                {t('bulk.deactivate')}
              </Button>
              <Button
                size="sm"
                variant="secondary"
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                onClick={runBulkDelete}
                disabled={isBulkUpdating}
              >
                {t('bulk.delete')}
              </Button>
            </div>
          )}
        </section>

        {isReorderMode && (
          <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
              <div>
                <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900">
                  {t('reorder.title')}
                </h3>
                <p className="text-sm text-slate-500">{t('reorder.help')}</p>
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={cancelReorderMode}
                  disabled={isSavingReorder}
                >
                  {t('reorder.cancel')}
                </Button>
                <Button
                  size="sm"
                  onClick={saveRuleOrder}
                  disabled={isSavingReorder}
                >
                  {isSavingReorder ? t('reorder.saving') : t('reorder.save')}
                </Button>
              </div>
            </div>

            <div className="mt-4 space-y-2">
              {reorderRules.map((rule, index) => (
                <div
                  key={rule.id}
                  draggable
                  onDragStart={(event) => {
                    setDraggedRuleId(rule.id);
                    event.dataTransfer.effectAllowed = 'move';
                  }}
                  onDragOver={(event) => {
                    event.preventDefault();
                    setDragOverRuleId(rule.id);
                  }}
                  onDrop={(event) => {
                    event.preventDefault();
                    if (draggedRuleId !== null) {
                      moveRuleInOrder(draggedRuleId, rule.id);
                    }
                    setDragOverRuleId(null);
                  }}
                  onDragEnd={() => {
                    setDraggedRuleId(null);
                    setDragOverRuleId(null);
                  }}
                  className={cn(
                    'flex items-center justify-between gap-3 rounded-2xl border border-slate-200 px-4 py-3 bg-white cursor-grab',
                    dragOverRuleId === rule.id && draggedRuleId !== rule.id && 'border-violet-300 bg-violet-50',
                    draggedRuleId === rule.id && 'opacity-70'
                  )}
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <Bars3Icon className="w-4 h-4 text-slate-400 shrink-0" />
                    <span className="font-[var(--font-dash-mono)] text-xs text-slate-400 w-8">
                      #{index}
                    </span>
                    <span className="text-sm font-medium text-slate-900 truncate">{rule.name}</span>
                    {!rule.isActive && (
                      <Badge variant="secondary" className="bg-slate-100 text-slate-500 text-[10px]">
                        {t('badges.inactive')}
                      </Badge>
                    )}
                  </div>

                  <div className="flex items-center gap-1.5 shrink-0">
                    <Button
                      variant="secondary"
                      size="sm"
                      className="w-8 h-8 p-0"
                      onClick={() => moveRuleByOffset(rule.id, -1)}
                      disabled={index === 0 || isSavingReorder}
                      title={t('reorder.moveUp')}
                    >
                      <ArrowUpIcon className="w-4 h-4" />
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      className="w-8 h-8 p-0"
                      onClick={() => moveRuleByOffset(rule.id, 1)}
                      disabled={index === reorderRules.length - 1 || isSavingReorder}
                      title={t('reorder.moveDown')}
                    >
                      <ArrowDownIcon className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Rules List */}
        <Card className="rounded-[26px] border border-violet-100/60 bg-white/90 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <CardContent className="p-0">
            {loading && rules.length === 0 ? (
              <div className="p-6 space-y-3">
                {Array.from({ length: 4 }).map((_, index) => (
                  <div key={index} className="animate-pulse h-20 rounded-2xl bg-slate-100" />
                ))}
              </div>
            ) : rules.length === 0 ? (
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
            ) : visibleRules.length === 0 ? (
              <div className="text-center py-12 px-6">
                <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
                  {t('filters.noMatches')}
                </h3>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setSearchQuery('');
                    setTypeFilter('all');
                    setSortBy('priorityAsc');
                  }}
                >
                  {t('filters.clear')}
                </Button>
              </div>
            ) : (
              <div>
                {visibleRules.map((rule) => (
                  <div
                    key={rule.id}
                    className={cn(
                      'group border-b border-slate-100 last:border-b-0 hover:bg-slate-50/60 transition-colors p-5',
                      !rule.isActive && 'opacity-60'
                    )}
                  >
                    <div className="flex items-start gap-3">
                      <input
                        type="checkbox"
                        checked={selectedRuleIds.has(rule.id)}
                        onChange={(event) => toggleRuleSelection(rule.id, event.target.checked)}
                        disabled={isReorderMode}
                        className="mt-1 rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                        aria-label={t('bulk.selectRule', { name: rule.name })}
                      />

                      <div className="flex-1 min-w-0 flex items-start justify-between gap-4">
                        {/* Rule name + badges */}
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2 flex-wrap mb-1.5">
                            <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 truncate">
                              {rule.name}
                            </h3>
                            <Badge className={cn('text-[10px] font-medium', getRuleTypeColor(rule.type))}>
                              {getRuleTypeLabel(rule.type)}
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
                            disabled={testingRuleId === rule.id || isReorderMode || isBulkUpdating}
                            className="w-8 h-8 p-0"
                            title={t('testRule')}
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
                            disabled={isReorderMode || isBulkUpdating}
                          >
                            {rule.isActive ? (
                              <PauseIcon className="w-4 h-4" />
                            ) : (
                              <PlayIcon className="w-4 h-4" />
                            )}
                          </Button>

                          <Link href={`/rules/${rule.id}/edit`}>
                            <Button variant="secondary" size="sm" className="w-8 h-8 p-0" disabled={isReorderMode || isBulkUpdating}>
                              <PencilIcon className="w-4 h-4" />
                            </Button>
                          </Link>

                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => deleteRule(rule.id, rule.name)}
                            className="w-8 h-8 p-0 text-red-600 hover:text-red-700 hover:bg-red-50"
                            disabled={isReorderMode || isBulkUpdating}
                          >
                            <TrashIcon className="w-4 h-4" />
                          </Button>
                        </div>
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
