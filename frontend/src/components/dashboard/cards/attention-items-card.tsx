'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';
import { cn } from '@/lib/utils';
import {
  BellAlertIcon,
  TagIcon,
  CalendarDaysIcon,
  ExclamationTriangleIcon,
} from '@heroicons/react/24/outline';

interface AttentionItem {
  text: string;
  detail: string;
  category: string;
  tag: string;
  severity: 'error' | 'warn' | 'info';
  href: string;
}

export function AttentionItemsCard() {
  const t = useTranslations('dashboard.cards.attention');
  const [items, setItems] = useState<AttentionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError(null);

        const [uncategorizedResponse, billsResponse, budgetsResponse] =
          await Promise.all([
            apiClient
              .getTransactions({ categoryId: -1, pageSize: 1 })
              .catch(() => ({ totalCount: 0 })) as Promise<{ totalCount: number }>,
            apiClient.getUpcomingBills(7).catch(() => ({ bills: [] })) as Promise<{
              bills: { merchantName: string; expectedAmount: number; daysUntilDue: number }[];
            }>,
            apiClient
              .getBudgets({ includeInactive: false, onlyCurrentPeriod: true })
              .catch(() => []) as Promise<
              { name: string; usedPercentage: number; totalSpent: number; totalBudgeted: number }[]
            >,
          ]);

        const attentionItems: AttentionItem[] = [];

        // Uncategorized transactions
        const uncategorizedCount = uncategorizedResponse?.totalCount || 0;
        if (uncategorizedCount > 0) {
          attentionItems.push({
            text: t('uncategorized', { count: uncategorizedCount }),
            detail: t('uncategorizedDetail'),
            category: t('categoryHygiene'),
            tag: t('tagMinutes'),
            severity: 'info',
            href: '/transactions?filter=uncategorized',
          });
        }

        // Upcoming bills
        const bills = billsResponse?.bills || [];
        bills.slice(0, 2).forEach((bill) => {
          attentionItems.push({
            text: `${bill.merchantName} ${t('dueSoon')}`,
            detail: t('billDetail', { amount: Math.abs(bill.expectedAmount).toFixed(2) }),
            category: t('categoryFixed'),
            tag: bill.daysUntilDue <= 1 ? t('tagDueSoon') : t('tagDueIn', { days: bill.daysUntilDue }),
            severity: 'warn',
            href: '/transactions',
          });
        });

        // Over-budget items
        const overBudget = (budgetsResponse || []).filter(
          (b) => b.usedPercentage >= 100,
        );
        overBudget.forEach((budget) => {
          const overAmount = budget.totalSpent - budget.totalBudgeted;
          attentionItems.push({
            text: t('overBudget', { name: budget.name, amount: overAmount.toFixed(0) }),
            detail: t('overBudgetDetail', { amount: (overAmount * 12).toFixed(0) }),
            category: t('categoryVariable'),
            tag: t('tagOverLimit'),
            severity: 'error',
            href: '/budgets',
          });
        });

        setItems(attentionItems.slice(0, 4));
      } catch (err) {
        console.error('Failed to load attention items:', err);
        setError('Failed to load attention items');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [t]);

  const getIcon = (severity: string) => {
    switch (severity) {
      case 'error':
        return ExclamationTriangleIcon;
      case 'warn':
        return CalendarDaysIcon;
      default:
        return TagIcon;
    }
  };

  const getStyles = (severity: string) => {
    switch (severity) {
      case 'error':
        return {
          card: 'border-l-rose-400 bg-rose-50/50',
          icon: 'text-rose-500',
          tag: 'bg-rose-100 text-rose-700',
        };
      case 'warn':
        return {
          card: 'border-l-amber-400 bg-amber-50/40',
          icon: 'text-amber-500',
          tag: 'bg-amber-100 text-amber-700',
        };
      default:
        return {
          card: 'border-l-violet-400 bg-violet-50/30',
          icon: 'text-violet-500',
          tag: 'bg-violet-100 text-violet-700',
        };
    }
  };

  return (
    <DashboardCard cardId="attention-items" loading={loading} error={error}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-100">
            <BellAlertIcon className="h-4 w-4 text-violet-600" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-slate-900">
            {t('title')}
          </h3>
        </div>
      </div>

      <div className="mt-5 space-y-3">
        {items.length === 0 ? (
          <p className="py-4 text-center text-sm text-slate-400">{t('allClear')}</p>
        ) : (
          items.map((item, i) => {
            const Icon = getIcon(item.severity);
            const styles = getStyles(item.severity);
            return (
              <Link
                key={i}
                href={item.href}
                className={cn(
                  'flex w-full items-start gap-3 rounded-xl border border-transparent border-l-[3px] p-3.5 text-left transition-all hover:-translate-y-0.5 hover:shadow-sm',
                  styles.card,
                )}
              >
                <Icon className={cn('mt-0.5 h-5 w-5 shrink-0', styles.icon)} />
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-slate-800">{item.text}</p>
                  <p className="mt-0.5 text-xs text-slate-500">{item.detail}</p>
                  <p className="mt-1 text-[11px] font-semibold uppercase tracking-[0.08em] text-violet-500">
                    {item.category}
                  </p>
                </div>
                <span className={cn('shrink-0 rounded-md px-2 py-1 text-[11px] font-semibold', styles.tag)}>
                  {item.tag}
                </span>
              </Link>
            );
          })
        )}
      </div>
    </DashboardCard>
  );
}
