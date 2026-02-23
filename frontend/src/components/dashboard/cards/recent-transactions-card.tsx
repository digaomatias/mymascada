'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient } from '@/lib/api-client';
import { cn, formatCurrency } from '@/lib/utils';
import { ArrowRightIcon } from '@heroicons/react/24/outline';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  categoryName?: string;
  accountName?: string;
}

function getInitial(name: string): string {
  return name.charAt(0).toUpperCase();
}

function getInitialBg(name: string): string {
  const colors = [
    'bg-emerald-100 text-emerald-700',
    'bg-violet-100 text-violet-700',
    'bg-slate-100 text-slate-700',
    'bg-rose-100 text-rose-700',
    'bg-sky-100 text-sky-700',
    'bg-amber-100 text-amber-700',
  ];
  const idx = name.charCodeAt(0) % colors.length;
  return colors[idx];
}

export function RecentTransactionsCard() {
  const t = useTranslations('dashboard.cards.transactions');
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = (await apiClient.getRecentTransactions(5)) as Transaction[];
        setTransactions(data || []);
      } catch (err) {
        console.error('Failed to load transactions:', err);
        setError('Failed to load transactions');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  return (
    <DashboardCard cardId="recent-transactions" loading={loading} error={error}>
      <div className="flex items-center justify-between">
        <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold tracking-[-0.02em] text-slate-900">
          {t('title')}
        </h3>
        <Link
          href="/transactions"
          className="inline-flex items-center gap-1 text-sm font-semibold text-violet-600 transition-colors hover:text-violet-800"
        >
          {t('viewAll')} <ArrowRightIcon className="h-3.5 w-3.5" />
        </Link>
      </div>
      <div className="mt-4 space-y-1">
        {transactions.length === 0 ? (
          <p className="py-4 text-center text-sm text-slate-400">{t('empty')}</p>
        ) : (
          transactions.map((tx) => {
            const positive = tx.amount > 0;
            return (
              <Link
                key={tx.id}
                href={`/transactions/${tx.id}`}
                className="flex items-center gap-3.5 rounded-xl px-1 py-3 transition-colors hover:bg-violet-50/40"
              >
                <div className={cn('flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-sm font-semibold', getInitialBg(tx.description))}>
                  {getInitial(tx.description)}
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-slate-800 truncate">{tx.description}</p>
                  <p className="mt-0.5 text-xs text-slate-400">
                    {tx.categoryName || ''}{tx.categoryName && tx.accountName ? ' · ' : ''}{tx.accountName || ''}{' · '}
                    {new Date(tx.transactionDate).toLocaleDateString()}
                  </p>
                </div>
                <p className={cn('font-[var(--font-dash-mono)] text-sm font-semibold tabular-nums', positive ? 'text-emerald-600' : 'text-slate-800')}>
                  {positive ? '+' : ''}{formatCurrency(tx.amount)}
                </p>
              </Link>
            );
          })
        )}
      </div>
    </DashboardCard>
  );
}
