'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback, Suspense } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { formatCurrency, formatMonthYearFromName, cn } from '@/lib/utils';
import { AccountTypeBadge } from '@/components/ui/account-type-badge';
import { getAccountTypeStyle } from '@/lib/account-styles';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { toast } from 'sonner';
import {
  BuildingOffice2Icon,
  CalendarIcon,
  PencilIcon,
  ArrowLeftIcon,

  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
} from '@heroicons/react/24/outline';
import { AddTransactionButton } from '@/components/buttons/add-transaction-button';
import { ReconcileAccountButton } from '@/components/buttons/reconcile-account-button';
import { TransactionList } from '@/components/transaction-list';
import { useTranslations } from 'next-intl';
import { useLocale } from '@/contexts/locale-context';

interface Account {
  id: number;
  name: string;
  type: number;
  institution?: string;
  currentBalance: number;
  calculatedBalance: number;
  currency: string;
  isActive: boolean;
  notes?: string;
  createdAt: string;
  updatedAt: string;
  lastReconciledDate?: string;
  lastReconciledBalance?: number;
}

interface MonthlySpending {
  currentMonthSpending: number;
  previousMonthSpending: number;
  changeAmount: number;
  changePercentage: number;
  trendDirection: 'up' | 'down' | 'neutral';
  monthName: string;
  year: number;
}

interface AccountDetails extends Account {
  monthlySpending: MonthlySpending;
}

function AccountDetailsPageContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const accountId = params.id as string;
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const { locale } = useLocale();

  const [account, setAccount] = useState<AccountDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [filteredBalance, setFilteredBalance] = useState<number | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadAccountDetails = useCallback(async () => {
    try {
      setLoading(true);
      const accountData = await apiClient.getAccountDetails(parseInt(accountId)) as AccountDetails;
      setAccount(accountData);
    } catch (error) {
      console.error('Failed to load account:', error);
      toast.error(t('failedToLoad'));
      router.push('/accounts');
    } finally {
      setLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accountId, router]);

  useEffect(() => {
    if (isAuthenticated && accountId) {
      loadAccountDetails();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, accountId]);

  const handleTransactionUpdate = useCallback(() => {
    loadAccountDetails();
  }, [loadAccountDetails]);


  if (isLoading || loading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('loadingAccount')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !account) {
    return null;
  }

  const typeStyle = getAccountTypeStyle(account.type);
  const TypeIcon = typeStyle.icon;

  return (
    <AppLayout>
      {/* Navigation Bar */}
      <header className="flex flex-wrap items-center justify-between gap-4 mb-5">
        <Link href="/accounts">
          <Button variant="secondary" size="sm" className="flex items-center gap-2">
            <ArrowLeftIcon className="w-4 h-4" />
            <span className="hidden sm:inline">{t('backToAccounts')}</span>
            <span className="sm:hidden">{t('back')}</span>
          </Button>
        </Link>

        <div className="flex items-center gap-2">
          <ReconcileAccountButton
            accountId={account.id}
            className="btn-sm"
          />
          <Link href={`/accounts/${account.id}/edit`}>
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <PencilIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{t('editAccount')}</span>
              <span className="sm:hidden">{tCommon('edit')}</span>
            </Button>
          </Link>
        </div>
      </header>

      <div className="space-y-5">
        {/* Hero Section */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-6">
            {/* Left: Account identity + balance */}
            <div className="min-w-0 flex-1">
              {/* Account Icon + Name */}
              <div className="flex items-center gap-3">
                <div
                  className={cn(
                    'flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br',
                    typeStyle.gradient,
                  )}
                >
                  <TypeIcon className="h-6 w-6 text-white" />
                </div>
                <div className="min-w-0">
                  <h1 className="font-[var(--font-dash-sans)] text-2xl sm:text-3xl font-semibold tracking-[-0.03em] text-slate-900 truncate">
                    {account.name}
                  </h1>
                  <div className="mt-0.5 flex flex-wrap items-center gap-2">
                    <AccountTypeBadge type={account.type} />
                    {account.institution && (
                      <span className="text-xs text-slate-500">{account.institution}</span>
                    )}
                  </div>
                </div>
              </div>

              {/* Balance */}
              <div className="mt-5">
                <p className="text-sm font-semibold text-slate-500">{t('currentBalance')}</p>
                <div className="mt-1 flex items-baseline gap-3">
                  <p className="font-[var(--font-dash-mono)] text-4xl sm:text-5xl font-semibold tracking-[-0.02em] text-slate-900">
                    {formatCurrency(account.calculatedBalance || 0)}
                  </p>
                </div>
                <p className="mt-1 text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-400">
                  {account.currency}
                </p>
              </div>

              {/* Filtered balance indicator */}
              {filteredBalance !== null && (
                <div className="mt-3 flex items-center gap-2">
                  <div
                    className={cn(
                      'flex h-6 w-6 shrink-0 items-center justify-center rounded-md',
                      (filteredBalance || 0) >= 0
                        ? 'bg-emerald-100 text-emerald-600'
                        : 'bg-red-100 text-red-600'
                    )}
                  >
                    {(filteredBalance || 0) >= 0 ? (
                      <ArrowTrendingUpIcon className="h-3.5 w-3.5" />
                    ) : (
                      <ArrowTrendingDownIcon className="h-3.5 w-3.5" />
                    )}
                  </div>
                  <div>
                    <p className="text-xs font-medium text-slate-500">{t('filteredBalance')}</p>
                    <p className="font-[var(--font-dash-mono)] text-lg font-semibold text-slate-900">
                      {formatCurrency(filteredBalance || 0)}
                    </p>
                  </div>
                </div>
              )}
            </div>

            {/* Right: Quick stats */}
            <div className="flex items-start gap-5 lg:gap-6">
              {/* Monthly Spending */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('monthlySpending')}
                </p>
                <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                  {formatCurrency(account.monthlySpending.currentMonthSpending)}
                </p>
                <div className="flex items-center lg:justify-end gap-1 mt-0.5 text-xs text-slate-500">
                  <CalendarIcon className="w-3 h-3" />
                  {formatMonthYearFromName(
                    account.monthlySpending.monthName,
                    account.monthlySpending.year,
                    locale
                  )}
                </div>
                {account.monthlySpending.changePercentage !== 0 && (
                  <div
                    className={cn('flex items-center lg:justify-end gap-1 mt-0.5 text-xs', {
                      'text-red-600': account.monthlySpending.trendDirection === 'up',
                      'text-emerald-600': account.monthlySpending.trendDirection === 'down',
                      'text-slate-500': account.monthlySpending.trendDirection === 'neutral',
                    })}
                  >
                    {account.monthlySpending.trendDirection === 'up' ? (
                      <ArrowTrendingUpIcon className="w-3 h-3" />
                    ) : account.monthlySpending.trendDirection === 'down' ? (
                      <ArrowTrendingDownIcon className="w-3 h-3" />
                    ) : null}
                    {t('vsLastMonth', { percentage: Math.abs(account.monthlySpending.changePercentage) })}
                  </div>
                )}
              </div>

              <div className="h-12 w-px bg-slate-200 self-center" />

              {/* Reconciliation */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {t('lastReconciled')}
                </p>
                {account.lastReconciledDate ? (
                  <>
                    <p className="mt-1 font-[var(--font-dash-mono)] text-xl font-semibold text-slate-900">
                      {new Date(account.lastReconciledDate).toLocaleDateString()}
                    </p>
                    {account.lastReconciledBalance !== undefined && account.lastReconciledBalance !== null && (
                      <p className="text-xs text-slate-500 mt-0.5">
                        {t('reconciledBalance')}: {formatCurrency(account.lastReconciledBalance)}
                      </p>
                    )}
                  </>
                ) : (
                  <p className="mt-1 text-sm font-medium text-slate-400">{t('neverReconciled')}</p>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* Account Notes */}
        {account.notes && (
          <section className="rounded-[26px] border border-violet-100/80 bg-white/90 p-5 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)]">
            <h2 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 mb-2">
              {tCommon('notes')}
            </h2>
            <p className="text-sm text-slate-600 whitespace-pre-wrap">{account.notes}</p>
          </section>
        )}

        {/* Transactions Section */}
        <section className="rounded-[26px] border border-violet-100/80 bg-white/90 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.48)] p-5">
          <TransactionList
            accountId={account.id}
            onTransactionUpdate={handleTransactionUpdate}
            onFilteredBalanceChange={setFilteredBalance}
            showAccountFilter={false}
            compact={false}
            title={t('accountTransactions')}
            headerActions={
              <AddTransactionButton
                accountId={account.id.toString()}
                onSuccess={handleTransactionUpdate}
                className="btn-sm"
              >
                {t('addTransaction')}
              </AddTransactionButton>
            }
          />
        </section>
      </div>
    </AppLayout>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';

export default function AccountDetailsPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense fallback={<div>{tCommon('loading')}</div>}>
      <AccountDetailsPageContent />
    </Suspense>
  );
}
