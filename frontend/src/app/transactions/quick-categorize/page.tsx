'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { toast } from 'sonner';
import {
  SparklesIcon,
  ArrowRightIcon,
  CheckBadgeIcon,
} from '@heroicons/react/24/outline';
import { AppLayout } from '@/components/app-layout';
import { BackButton } from '@/components/ui/back-button';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { apiClient } from '@/lib/api-client';
import type { UncategorizedGroupDto, UncategorizedGroupsResponse } from '@/lib/api-client';
import { useAuth } from '@/contexts/auth-context';
import { useLocale } from '@/contexts/locale-context';
import { cn } from '@/lib/utils';

interface CategoryOption {
  id: number;
  name: string;
  fullPath?: string;
  parentId?: number | null;
}

/**
 * Quick-Categorize onboarding wizard — "teach by example". Groups uncategorized
 * transactions by normalized description, walks the user through one category
 * assignment per group, and records CategorizationHistory so the ML handler
 * auto-applies the same category to future occurrences.
 */
export default function QuickCategorizePage() {
  const router = useRouter();
  const t = useTranslations('transactions.quickCategorize');
  const tCommon = useTranslations('common');
  const { isAuthenticated, isLoading: authLoading, user } = useAuth();
  const { locale } = useLocale();
  const userCurrency = user?.currency ?? 'USD';

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);
  const [groups, setGroups] = useState<UncategorizedGroupDto[]>([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string>('');
  const [saving, setSaving] = useState(false);
  const [totalCompleted, setTotalCompleted] = useState(0);

  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [authLoading, isAuthenticated, router]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setLoadError(false);
        const [groupsRes, categoriesRes] = await Promise.all([
          apiClient.getUncategorizedGroups({ maxGroups: 20, minGroupSize: 1 }),
          apiClient.getCategories() as Promise<CategoryOption[]>,
        ]);

        const typed = groupsRes as UncategorizedGroupsResponse;
        setGroups(typed.groups || []);
        setCategories(
          (categoriesRes || []).filter((c) => c && c.id != null),
        );
      } catch (err) {
        console.error('Failed to load quick-categorize data:', err);
        // Track the failure so the render path can show retry UI instead of
        // silently falling through to the "nothing to categorize" empty state.
        setLoadError(true);
        toast.error(t('loadError'));
      } finally {
        setLoading(false);
      }
    };
    if (isAuthenticated) {
      load();
    }
  }, [isAuthenticated, t, reloadTick]);

  const currentGroup = useMemo<UncategorizedGroupDto | null>(
    () => groups[currentIndex] ?? null,
    [groups, currentIndex],
  );

  const progressPercent = groups.length === 0
    ? 0
    : Math.min(100, Math.round((currentIndex / groups.length) * 100));

  const goNext = () => {
    setSelectedCategoryId('');
    if (currentIndex + 1 >= groups.length) {
      setCurrentIndex(groups.length); // triggers all-done view
      return;
    }
    setCurrentIndex((i) => i + 1);
  };

  const handleSkip = () => {
    goNext();
  };

  const handleCategorize = async () => {
    if (!currentGroup || !selectedCategoryId) return;
    const categoryId = Number.parseInt(selectedCategoryId, 10);
    if (Number.isNaN(categoryId)) return;

    // The backend caps each bulk-categorize-group request at 500 ids
    // (`CategorizationController.BulkCategorizeGroup`). A user whose
    // frequent-merchant group exceeds that limit would otherwise see a
    // hard 400 and be unable to categorize the group at all. Chunk the
    // request here and aggregate the partial results into a single
    // wizard step.
    const BATCH_SIZE = 500;
    const allIds = currentGroup.transactionIds;
    const batches: number[][] = [];
    for (let i = 0; i < allIds.length; i += BATCH_SIZE) {
      batches.push(allIds.slice(i, i + BATCH_SIZE));
    }

    try {
      setSaving(true);
      let totalUpdated = 0;
      const aggregatedErrors: string[] = [];
      const committedIds: number[] = [];
      let anySuccessFalse = false;
      let lastMessage: string | undefined;
      // Track whether CategorizationHistory has been successfully recorded for
      // this user confirmation. We flip this when the FIRST chunk that
      // actually commits rows comes back, not the first chunk we attempted —
      // otherwise a failing chunk 0 followed by successful chunks 1..N would
      // leave the ML handler with zero signal because chunk 0 asked for
      // history but committed nothing, and chunks 1..N opted out.
      let historyRecorded = false;

      for (let i = 0; i < batches.length; i++) {
        const batch = batches[i];
        const shouldRecordHistory = !historyRecorded;
        const res = await apiClient.bulkCategorizeGroup({
          transactionIds: batch,
          categoryId,
          // Record CategorizationHistory on the first chunk that actually
          // commits rows — subsequent chunks share the same user confirmation
          // and must NOT record again, otherwise MatchCount is incremented
          // once per chunk for a single action and ML/suggestion strength
          // gets skewed vs smaller groups.
          recordHistory: shouldRecordHistory,
        });
        totalUpdated += res.transactionsUpdated;
        if (res.updatedTransactionIds?.length) {
          committedIds.push(...res.updatedTransactionIds);
        }
        if (res.errors?.length) {
          aggregatedErrors.push(...res.errors);
        }
        if (!res.success) {
          anySuccessFalse = true;
          lastMessage = res.message ?? lastMessage;
        }
        // Flip the history flag only when we actually asked the backend to
        // record AND the chunk committed something — the backend writes
        // history iff `RecordHistory && changedTransactions.Count > 0`, so
        // `transactionsUpdated > 0` is a reliable proxy for "history was
        // recorded on this chunk".
        if (shouldRecordHistory && res.transactionsUpdated > 0) {
          historyRecorded = true;
        }
      }

      // The backend can return HTTP 200 with success=false when some (or all)
      // transactions in the group were skipped (e.g. transfers, missing ids).
      // Only advance the wizard when every chunk applied cleanly — otherwise
      // surface the partial/failed result so the user can decide whether to
      // retry or move on manually.
      if (!anySuccessFalse) {
        toast.success(t('success', { count: totalUpdated }));
        setTotalCompleted((prev) => prev + totalUpdated);
        goNext();
      } else if (totalUpdated > 0) {
        // Partial success: count what was saved, surface the counts in a
        // localized toast, but keep the user on the current group so the
        // skipped rows stay visible. Backend error strings are English-only
        // (e.g. "Transaction {id} is part of a transfer...") and violate the
        // "all user-facing strings must be localized" rule, so they're
        // logged to the console for debugging instead of shown as a toast.
        setTotalCompleted((prev) => prev + totalUpdated);

        // Narrow the current group's id set to the transactions that
        // were NOT committed in this attempt. Without this, the next
        // submit (with a different category) would re-send every id,
        // including ones already saved, and silently re-categorize them.
        const committedSet = new Set(committedIds);
        const remainingIds = currentGroup.transactionIds.filter(
          (id) => !committedSet.has(id),
        );
        if (remainingIds.length === 0) {
          // The backend's errors don't map to ids in `transactionIds`
          // (e.g. "access denied" with no ids to retry) — nothing left
          // for the user to act on, so advance as if fully successful.
          toast.success(t('success', { count: totalUpdated }));
          goNext();
        } else {
          setGroups((prev) => {
            const next = [...prev];
            const current = next[currentIndex];
            if (!current) return prev;
            next[currentIndex] = {
              ...current,
              transactionIds: remainingIds,
              // Keep the visible count in sync with what's actually left
              // so the button label ("Categorize N transactions") and the
              // group header don't lie to the user.
              transactionCount: remainingIds.length,
            };
            return next;
          });
          setSelectedCategoryId('');
          toast.warning(
            t('partial', {
              success: totalUpdated,
              failed: aggregatedErrors.length,
            }),
          );
        }
        if (aggregatedErrors.length > 0 || lastMessage) {
          console.warn(
            'Quick-categorize partial success. Backend errors:',
            aggregatedErrors,
            'Last message:',
            lastMessage,
          );
        }
      } else {
        // Nothing was saved — show the generic localized error toast. The
        // backend's English error details are logged to the console so we
        // can debug them without leaking untranslated strings into the UI.
        if (aggregatedErrors.length > 0 || lastMessage) {
          console.warn(
            'Quick-categorize failed. Backend errors:',
            aggregatedErrors,
            'Last message:',
            lastMessage,
          );
        }
        toast.error(t('error'));
      }
    } catch (err) {
      console.error('Quick-categorize failed:', err);
      toast.error(t('error'));
    } finally {
      setSaving(false);
    }
  };

  if (authLoading || !isAuthenticated) {
    return null;
  }

  if (loading) {
    return (
      <AppLayout>
        <BackButton href="/transactions" label={t('backToTransactions')} />
        <div className="mt-6 rounded-2xl border border-ink-200 bg-white/90 p-12 text-center text-ink-500">
          {t('loading')}
        </div>
      </AppLayout>
    );
  }

  // Distinct error state: the initial fetch failed. Without this branch the
  // component would fall through to the "nothing to categorize" empty UI and
  // hide the failure from the user.
  if (loadError) {
    return (
      <AppLayout>
        <BackButton href="/transactions" label={t('backToTransactions')} />
        <div
          className="mt-6 rounded-2xl border border-ink-200 bg-white/90 p-12 text-center shadow-lg"
          data-testid="quick-categorize-load-error"
        >
          <h2 className="font-[var(--font-dash-sans)] text-xl font-semibold text-ink-900">
            {t('loadError')}
          </h2>
          <p className="mt-2 text-ink-500">{t('loadErrorDescription')}</p>
          <Button
            className="mt-6"
            onClick={() => setReloadTick((n) => n + 1)}
            data-testid="quick-categorize-load-error-retry"
          >
            {tCommon('retry')}
          </Button>
        </div>
      </AppLayout>
    );
  }

  const finished = groups.length === 0 || currentIndex >= groups.length;

  return (
    <AppLayout>
      <BackButton href="/transactions" label={t('backToTransactions')} />

      <div className="mt-4 mb-6">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-primary-400 shadow-lg">
            <SparklesIcon className="h-5 w-5 text-white" />
          </div>
          <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-ink-900 sm:text-[2.1rem]">
            {t('title')}
          </h1>
        </div>
        <p className="mt-2 text-[15px] text-ink-500">{t('subtitle')}</p>
      </div>

      {finished && groups.length === 0 && (
        <div className="rounded-2xl border border-ink-200 bg-white/90 p-12 text-center shadow-lg">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-400 to-emerald-500 shadow-2xl">
            <CheckBadgeIcon className="h-8 w-8 text-white" />
          </div>
          <h2 className="mt-5 font-[var(--font-dash-sans)] text-xl font-semibold text-ink-900">
            {t('emptyTitle')}
          </h2>
          <p className="mt-2 text-ink-500">{t('emptyDescription')}</p>
          <Button
            className="mt-6"
            onClick={() => router.push('/dashboard')}
            data-testid="quick-categorize-empty-back"
          >
            {t('emptyBack')}
          </Button>
        </div>
      )}

      {finished && groups.length > 0 && (
        <div className="rounded-2xl border border-ink-200 bg-white/90 p-12 text-center shadow-lg">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-400 to-emerald-500 shadow-2xl">
            <CheckBadgeIcon className="h-8 w-8 text-white" />
          </div>
          <h2 className="mt-5 font-[var(--font-dash-sans)] text-xl font-semibold text-ink-900">
            {t('allDoneTitle')}
          </h2>
          <p className="mt-2 text-ink-500">{t('allDoneDescription')}</p>
          {totalCompleted > 0 && (
            <p className="mt-2 text-sm text-ink-400">
              {t('success', { count: totalCompleted })}
            </p>
          )}
          <Button
            className="mt-6"
            onClick={() => router.push('/dashboard')}
            data-testid="quick-categorize-done-back"
          >
            {t('allDoneBack')}
          </Button>
        </div>
      )}

      {!finished && currentGroup && (
        <div className="space-y-5">
          {/* Progress bar */}
          <div className="rounded-2xl border border-ink-200 bg-white/90 p-4 shadow-sm">
            <div className="flex items-center justify-between text-sm">
              <span className="font-medium text-ink-700">
                {t('stepLabel', { current: currentIndex + 1, total: groups.length })}
              </span>
              <span className="text-ink-500">
                {t('progress', { done: currentIndex, total: groups.length })}
              </span>
            </div>
            <div className="mt-2 h-2 overflow-hidden rounded-full bg-ink-100">
              <div
                className="h-full bg-gradient-to-r from-primary-500 to-primary-400 transition-all"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          </div>

          {/* Group card */}
          <div
            className="rounded-2xl border border-ink-200 bg-white/90 p-6 shadow-lg"
            data-testid="quick-categorize-group-card"
          >
            <div className="flex items-start justify-between gap-4">
              <div className="min-w-0 flex-1">
                <h2
                  className="truncate font-[var(--font-dash-sans)] text-xl font-semibold text-ink-900"
                  data-testid="quick-categorize-group-description"
                >
                  {currentGroup.sampleDescription}
                </h2>
                <p className="mt-1 text-sm text-ink-500">
                  {t('groupCount', { count: currentGroup.transactionCount })}
                  {' · '}
                  {t('totalAmount', {
                    amount: currentGroup.totalAmount.toLocaleString(locale, {
                      style: 'currency',
                      currency: userCurrency,
                    }),
                  })}
                </p>
              </div>
            </div>

            {/* Samples */}
            {currentGroup.samples.length > 0 && (
              <div className="mt-5">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-400">
                  {t('samples')}
                </h3>
                <ul className="mt-2 space-y-2">
                  {currentGroup.samples.map((sample) => (
                    <li
                      key={sample.id}
                      className="flex items-center justify-between rounded-xl bg-ink-50 px-3 py-2 text-sm"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-medium text-ink-800">
                          {sample.description}
                        </p>
                        <p className="text-xs text-ink-500">
                          {sample.accountName} ·{' '}
                          {new Date(sample.transactionDate).toLocaleDateString(locale)}
                        </p>
                      </div>
                      <span
                        className={cn(
                          'ml-3 font-[var(--font-dash-mono)] text-sm font-semibold',
                          sample.amount >= 0 ? 'text-emerald-600' : 'text-red-600',
                        )}
                      >
                        {sample.amount.toLocaleString(locale, {
                          style: 'currency',
                          currency: userCurrency,
                        })}
                      </span>
                    </li>
                  ))}
                </ul>
                {currentGroup.transactionCount > currentGroup.samples.length && (
                  <p className="mt-2 text-xs text-ink-400">
                    {t('moreSamples', {
                      count: currentGroup.transactionCount - currentGroup.samples.length,
                    })}
                  </p>
                )}
              </div>
            )}

            {/* Category picker */}
            <div className="mt-6">
              <label
                htmlFor="quick-categorize-category"
                className="mb-2 block text-sm font-semibold text-ink-800"
              >
                {t('selectCategory')}
              </label>
              <Select
                id="quick-categorize-category"
                value={selectedCategoryId}
                onChange={(e) => setSelectedCategoryId(e.target.value)}
                data-testid="quick-categorize-category-select"
              >
                {/*
                  Explicit empty option (instead of the shared Select's
                  `placeholder` prop, which renders a disabled row) so the
                  user can clear their choice after picking a category. The
                  submit button is already disabled when selectedCategoryId
                  is falsy, so re-selecting the empty row is a valid "undo".
                */}
                <option value="">{t('selectCategory')}</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.fullPath || category.name}
                  </option>
                ))}
              </Select>
            </div>

            {/* Hint */}
            <p className="mt-4 text-xs text-ink-400">{t('hint')}</p>

            {/* Actions */}
            <div className="mt-6 flex items-center justify-between gap-3">
              <Button
                variant="ghost"
                onClick={handleSkip}
                disabled={saving}
                data-testid="quick-categorize-skip"
              >
                {t('skip')}
              </Button>
              <Button
                onClick={handleCategorize}
                disabled={saving || !selectedCategoryId}
                className="bg-primary-600 hover:bg-primary-700 text-white"
                data-testid="quick-categorize-submit"
              >
                {saving
                  ? t('saving')
                  : t('categorizeGroup', { count: currentGroup.transactionCount })}
                {!saving && <ArrowRightIcon className="ml-2 h-4 w-4" />}
              </Button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
