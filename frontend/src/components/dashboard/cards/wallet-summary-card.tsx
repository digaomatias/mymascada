'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { DashboardCard } from '@/components/dashboard/dashboard-card';
import { apiClient, WalletSummary } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import {
  CircleStackIcon,
  ArrowRightIcon,
  PlusIcon,
} from '@heroicons/react/24/outline';

export function WalletSummaryCard() {
  const t = useTranslations('dashboard.cards.wallet');
  const [wallets, setWallets] = useState<WalletSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await apiClient.getWallets();
        setWallets(data);
      } catch (err) {
        console.error('Failed to load wallets:', err);
        setError('Failed to load wallets');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const totalBalance = wallets.reduce((sum, w) => sum + w.balance, 0);
  const displayWallets = wallets.slice(0, 5);

  return (
    <DashboardCard cardId="wallet-pots" loading={loading} error={error}>
      {wallets.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-6 text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-violet-400 to-fuchsia-400 shadow-lg mb-4">
            <CircleStackIcon className="h-7 w-7 text-white" />
          </div>
          <h3 className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 mb-2">
            {t('noWallets')}
          </h3>
          <p className="text-sm text-slate-500 mb-4">{t('noWalletsDesc')}</p>
          <Link
            href="/wallets"
            className="inline-flex items-center gap-1 rounded-lg bg-violet-600 px-4 py-2 text-sm font-semibold text-white hover:bg-violet-700"
          >
            <PlusIcon className="h-4 w-4" />
            {t('createWallet')}
          </Link>
        </div>
      ) : (
        <div className="relative flex flex-1 flex-col">
          <div className="pointer-events-none absolute -bottom-10 -right-10 h-32 w-32 rounded-full bg-fuchsia-100/40 blur-2xl" aria-hidden />

          <div className="relative flex flex-1 flex-col">
            <div className="inline-flex w-fit items-center gap-1.5 rounded-full border border-violet-200/60 bg-violet-50 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-violet-600">
              <CircleStackIcon className="h-3 w-3" />
              {t('title')}
            </div>

            <div className="mt-3 flex items-baseline justify-between">
              <p className="text-xs font-semibold uppercase tracking-[0.12em] text-slate-400">
                {t('totalAllocated')}
              </p>
              <p className="font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                {formatCurrency(totalBalance)}
              </p>
            </div>

            <div className="mt-4 space-y-2.5">
              {displayWallets.map((wallet) => (
                <Link
                  key={wallet.id}
                  href={`/wallets/${wallet.id}`}
                  className="flex items-center gap-3 rounded-xl border border-violet-100/40 bg-violet-50/20 px-3 py-2.5 transition-colors hover:bg-violet-50/50"
                >
                  <span className="text-lg">{wallet.icon}</span>
                  <span className="flex-1 truncate text-sm font-medium text-slate-700">
                    {wallet.name}
                  </span>
                  <span className="font-[var(--font-dash-mono)] text-sm font-semibold text-slate-900">
                    {formatCurrency(wallet.balance, wallet.currency)}
                  </span>
                </Link>
              ))}
            </div>

            <div className="mt-4 flex justify-end">
              <Link
                href="/wallets"
                className="inline-flex items-center gap-1 text-xs font-semibold text-violet-600 hover:text-violet-800"
              >
                {t('viewAll')} <ArrowRightIcon className="h-3 w-3" />
              </Link>
            </div>
          </div>
        </div>
      )}
    </DashboardCard>
  );
}
