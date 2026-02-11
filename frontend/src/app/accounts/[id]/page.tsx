'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback, Suspense } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { formatCurrency, formatMonthYearFromName } from '@/lib/utils';
import { AccountTypeBadge } from '@/components/ui/account-type-badge';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { toast } from 'sonner';
import {
  BuildingOffice2Icon,
  CurrencyDollarIcon,
  CalendarIcon,
  PencilIcon,
  ArrowLeftIcon,
  DocumentArrowUpIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  CheckBadgeIcon
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
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loadingAccount')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !account) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <Link href="/accounts">
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                <span className="hidden sm:inline">{t('backToAccounts')}</span>
                <span className="sm:hidden">{t('back')}</span>
              </Button>
            </Link>

            <div className="flex items-center gap-2">
              <AddTransactionButton
                accountId={account.id.toString()}
                onSuccess={handleTransactionUpdate}
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
          </div>
          
          {/* Account Header */}
          <div className="text-center mb-8">
            <div className="w-20 h-20 bg-gradient-to-br from-primary-500 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <BuildingOffice2Icon className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {account.name}
            </h1>
            <div className="flex items-center justify-center gap-3">
              <AccountTypeBadge type={account.type} />
              {account.institution && (
                <span className="text-gray-600 text-sm">{account.institution}</span>
              )}
            </div>
          </div>
        </div>

        {/* Balance Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {/* Current Balance */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('currentBalance')}</p>
                  <p className="text-2xl font-bold text-gray-900">{formatCurrency(account.calculatedBalance || 0)}</p>
                  <p className="text-xs text-gray-500 mt-1">{account.currency}</p>
                </div>
                <div className="w-12 h-12 flex-shrink-0 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center">
                  <CurrencyDollarIcon className="w-6 h-6 text-white" />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Filtered Balance */}
          {filteredBalance !== null && (
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6">
                <div className="flex items-start justify-between">
                  <div>
                    <p className="text-sm font-medium text-gray-600">{t('filteredBalance')}</p>
                    <p className="text-2xl font-bold text-gray-900">{formatCurrency(filteredBalance || 0)}</p>
                    <p className="text-xs text-gray-500 mt-1">{t('forCurrentFilters')}</p>
                  </div>
                  <div className={`w-12 h-12 flex-shrink-0 rounded-xl flex items-center justify-center ${
                    (filteredBalance || 0) >= 0
                      ? 'bg-gradient-to-br from-success-400 to-success-600'
                      : 'bg-gradient-to-br from-red-400 to-red-600'
                  }`}>
                    {(filteredBalance || 0) >= 0 ? (
                      <ArrowTrendingUpIcon className="w-6 h-6 text-white" />
                    ) : (
                      <ArrowTrendingDownIcon className="w-6 h-6 text-white" />
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Monthly Spending */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('monthlySpending')}</p>
                  <p className="text-2xl font-bold text-gray-900">
                    {formatCurrency(account.monthlySpending.currentMonthSpending)}
                  </p>
                  <div className="flex items-center gap-1 mt-1 text-xs text-gray-500">
                    <CalendarIcon className="w-3 h-3" />
                    {formatMonthYearFromName(
                      account.monthlySpending.monthName,
                      account.monthlySpending.year,
                      locale
                    )}
                  </div>
                  {/* Change indicator */}
                  {account.monthlySpending.changePercentage !== 0 && (
                    <div className={`flex items-center gap-1 mt-1 text-xs ${
                      account.monthlySpending.trendDirection === 'up'
                        ? 'text-red-600'
                        : account.monthlySpending.trendDirection === 'down'
                        ? 'text-green-600'
                        : 'text-gray-500'
                    }`}>
                      {account.monthlySpending.trendDirection === 'up' ? (
                        <ArrowTrendingUpIcon className="w-3 h-3" />
                      ) : account.monthlySpending.trendDirection === 'down' ? (
                        <ArrowTrendingDownIcon className="w-3 h-3" />
                      ) : null}
                      {t('vsLastMonth', { percentage: Math.abs(account.monthlySpending.changePercentage) })}
                    </div>
                  )}
                </div>
                <div className={`w-12 h-12 flex-shrink-0 rounded-xl flex items-center justify-center ${
                  account.monthlySpending.trendDirection === 'up'
                    ? 'bg-gradient-to-br from-red-400 to-red-600'
                    : account.monthlySpending.trendDirection === 'down'
                    ? 'bg-gradient-to-br from-green-400 to-green-600'
                    : 'bg-gradient-to-br from-primary-400 to-primary-600'
                }`}>
                  {account.monthlySpending.trendDirection === 'up' ? (
                    <ArrowTrendingUpIcon className="w-6 h-6 text-white" />
                  ) : account.monthlySpending.trendDirection === 'down' ? (
                    <ArrowTrendingDownIcon className="w-6 h-6 text-white" />
                  ) : (
                    <CurrencyDollarIcon className="w-6 h-6 text-white" />
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Reconciliation Status */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('lastReconciled')}</p>
                  {account.lastReconciledDate ? (
                    <>
                      <p className="text-2xl font-bold text-gray-900">
                        {new Date(account.lastReconciledDate).toLocaleDateString()}
                      </p>
                      {account.lastReconciledBalance !== undefined && account.lastReconciledBalance !== null && (
                        <p className="text-xs text-gray-500 mt-1">
                          {t('reconciledBalance')}: {formatCurrency(account.lastReconciledBalance)}
                        </p>
                      )}
                    </>
                  ) : (
                    <p className="text-2xl font-medium text-gray-400">{t('neverReconciled')}</p>
                  )}
                </div>
                <div className={`w-12 h-12 flex-shrink-0 rounded-xl flex items-center justify-center ${
                  account.lastReconciledDate
                    ? 'bg-gradient-to-br from-purple-400 to-purple-600'
                    : 'bg-gradient-to-br from-gray-300 to-gray-400'
                }`}>
                  <CheckBadgeIcon className="w-6 h-6 text-white" />
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Account Notes */}
        {account.notes && (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg mb-8">
            <CardHeader>
              <CardTitle className="text-lg">{tCommon('notes')}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-gray-700 whitespace-pre-wrap">{account.notes}</p>
            </CardContent>
          </Card>
        )}

        {/* Transactions Section */}
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg mb-8">
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              <span className="flex items-center gap-2">
                <DocumentArrowUpIcon className="w-6 h-6 text-primary-600" />
                {t('accountTransactions')}
              </span>
              <div className="flex items-center gap-2">
                <ReconcileAccountButton
                  accountId={account.id}
                  className="btn-sm"
                />
                <AddTransactionButton
                  accountId={account.id.toString()}
                  onSuccess={handleTransactionUpdate}
                  className="btn-sm"
                >
                  {t('addTransaction')}
                </AddTransactionButton>
              </div>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-6">
            <TransactionList
              accountId={account.id}
              onTransactionUpdate={handleTransactionUpdate}
              onFilteredBalanceChange={setFilteredBalance}
              showAccountFilter={false}
              compact={false}
              title={t('accountTransactions')}
            />
          </CardContent>
        </Card>
      </main>
    </div>
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
