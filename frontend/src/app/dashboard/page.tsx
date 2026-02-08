'use client';

import { useAuth } from '@/contexts/auth-context';
import { useFeatures } from '@/contexts/features-context';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useState, Suspense, useCallback } from 'react';
import { toast } from 'sonner';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatCardSkeleton } from '@/components/ui/skeleton';
import { formatCurrency } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { MonthlySummary } from '@/components/dashboard/monthly-summary';
import { BudgetSummaryWidget } from '@/components/dashboard/budget-summary-widget';
import { UpcomingBillsWidget } from '@/components/dashboard/upcoming-bills-widget';
import { WelcomeScreen } from '@/components/dashboard/welcome-screen';
import { useTranslations } from 'next-intl';
import {
  CreditCardIcon,
  BanknotesIcon,
  WalletIcon,
  PlusCircleIcon,
  ArrowDownTrayIcon,
  ArrowPathIcon
} from '@heroicons/react/24/outline';
import { AkahuSyncButton } from '@/components/buttons/akahu-sync-button';
import { MobileActionsOverflow } from '@/components/ui/mobile-actions-overflow';
import { useDeviceDetect } from '@/hooks/use-device-detect';

// Fallback icon in case of import issues
const FallbackIcon = ({ className }: { className?: string }) => (
  <svg className={className} fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
  </svg>
);

// Safe icon wrapper component (keeping for potential future use)
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const SafeIcon = ({ IconComponent, className, fallbackName }: { 
  IconComponent: React.ComponentType<{ className?: string }>; 
  className?: string; 
  fallbackName?: string;
}) => {
  try {
    if (!IconComponent || typeof IconComponent !== 'function') {
      console.warn(`Icon component is invalid for ${fallbackName}, using fallback`);
      return <FallbackIcon className={className} />;
    }
    return <IconComponent className={className} />;
  } catch {
    console.error(`Error rendering icon for ${fallbackName}`);
    return <FallbackIcon className={className} />;
  }
};

function DashboardContent() {
  const { isAuthenticated, isLoading, user, loginWithToken } = useAuth();
  const { features } = useFeatures();
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations('dashboard');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const { isMobile } = useDeviceDetect();
  const [dataLoading, setDataLoading] = useState(true);
  const [hasAkahuConnection, setHasAkahuConnection] = useState(false);
  const [isSyncing, setIsSyncing] = useState(false);
  const [dashboardData, setDashboardData] = useState<{
    totalBalance: number;
    monthlyIncome: number;
    monthlyExpenses: number;
    transactionCount: number;
    accountCount: number;
    hasCategories: boolean;
    recentTransactions: {
      id: number;
      amount: number;
      transactionDate: string;
      description: string;
      categoryName?: string;
      accountName?: string;
    }[];
  }>({
    totalBalance: 0,
    monthlyIncome: 0,
    monthlyExpenses: 0,
    transactionCount: 0,
    accountCount: 0,
    hasCategories: false,
    recentTransactions: [],
  });

  // Handle Google OAuth code from URL (exchanged for a token server-side)
  useEffect(() => {
    const code = searchParams.get('code');
    if (code && !isAuthenticated) {
      apiClient.exchangeCode(code).then((result) => {
        return loginWithToken(result.token);
      }).then((success) => {
        if (success) {
          toast.success(tToasts('signedIn'));
          router.replace('/dashboard');
        } else {
          toast.error(tToasts('error.generic'));
          router.push('/auth/login');
        }
      }).catch(() => {
        toast.error(tToasts('error.generic'));
        router.push('/auth/login');
      });
    }
  }, [searchParams, isAuthenticated, loginWithToken, router]);

  useEffect(() => {
    if (!isLoading && !isAuthenticated && !searchParams.get('code')) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router, searchParams]);

  // Check Akahu connection for mobile overflow menu
  useEffect(() => {
    const checkAkahuConnection = async () => {
      try {
        const response = await apiClient.hasAkahuCredentials();
        setHasAkahuConnection(response.hasCredentials);
      } catch (error) {
        console.error('Failed to check Akahu connection:', error);
        setHasAkahuConnection(false);
      }
    };

    if (isAuthenticated) {
      checkAkahuConnection();
    }
  }, [isAuthenticated]);

  // Handle sync from mobile overflow menu
  const handleMobileSync = async () => {
    setIsSyncing(true);
    try {
      const results = await apiClient.syncAllConnections();
      const successful = results.filter((r: { isSuccess: boolean }) => r.isSuccess);
      const totalImported = results.reduce((sum: number, r: { transactionsImported: number }) => sum + r.transactionsImported, 0);

      if (results.length === 0) {
        toast.info(t('akahuSync.syncSuccessNoNew'));
      } else if (successful.length === results.length) {
        if (totalImported > 0) {
          toast.success(t('akahuSync.syncSuccess', { imported: totalImported }));
        } else {
          toast.success(t('akahuSync.syncSuccessNoNew'));
        }
      } else if (successful.length > 0) {
        toast.warning(t('akahuSync.syncPartial', {
          successful: successful.length,
          total: results.length
        }));
      } else {
        toast.error(t('akahuSync.syncFailed'));
      }

      loadDashboardData();
    } catch (error) {
      console.error('Failed to sync bank data:', error);
      toast.error(t('akahuSync.syncFailed'));
    } finally {
      setIsSyncing(false);
    }
  };

  const loadDashboardData = useCallback(async () => {
    try {
      setDataLoading(true);
      
      // Load accounts and transactions
      const accounts = (await apiClient.getAccountsWithBalances()) as { calculatedBalance: number }[];

      const transactions = (await apiClient.getRecentTransactions(50)) as {
        id: number;
        amount: number;
        transactionDate: string;
        description: string;
        categoryName?: string;
        accountName?: string;
      }[];
      console.log('Transactions loaded:', transactions?.length || 0);

      // Get total transaction count from paginated endpoint
      console.log('Fetching total transaction count...');
      const transactionsResponse = (await apiClient.getTransactions({ pageSize: 1 })) as {
        totalCount: number;
      };
      const totalTransactionCount = transactionsResponse?.totalCount || 0;
      console.log('Total transaction count:', totalTransactionCount);

      // Check if categories exist for onboarding
      let hasCategories = false;
      try {
        const categories = (await apiClient.getCategories()) as unknown[];
        hasCategories = Array.isArray(categories) && categories.length > 0;
      } catch {
        console.error('Failed to check categories');
      }
      
      // Calculate total balance from accounts using calculated balance (preferred) or transactions (fallback)
      const accountBalance = accounts?.reduce((sum, account) => sum + (account.calculatedBalance || 0), 0) || 0;
      const transactionBalance = transactions?.reduce((sum, t) => sum + (t.amount || 0), 0) || 0;
      const totalBalance = accountBalance || transactionBalance;
      console.log('Account balance:', accountBalance, 'Transaction balance:', transactionBalance, 'Using:', totalBalance);
      
      // Get proper monthly income and expenses from the monthly summary API
      const currentMonth = new Date().getMonth() + 1; // API expects 1-based month
      const currentYear = new Date().getFullYear();
      
      console.log('Fetching monthly summary for:', currentYear, currentMonth);
      console.log('Request URL will be: /api/reports/monthly-summary?year=' + currentYear + '&month=' + currentMonth);
      let monthlyIncome = 0;
      let monthlyExpenses = 0;
      
      try {
        const monthlySummary = (await apiClient.getMonthlySummary(
          currentYear,
          currentMonth
        )) as { totalIncome: number; totalExpenses: number };
        console.log('Monthly summary loaded:', monthlySummary);
        monthlyIncome = monthlySummary?.totalIncome || 0;
        monthlyExpenses = monthlySummary?.totalExpenses || 0;
      } catch (error) {
        console.error('Failed to load monthly summary from API:', error);
        
        // Log the specific error details for debugging
        if (error && typeof error === 'object' && 'message' in error) {
          console.error('API Error details:', error);
        }
        
        // Try with previous month as fallback
        try {
          const prevMonth = currentMonth === 1 ? 12 : currentMonth - 1;
          const prevYear = currentMonth === 1 ? currentYear - 1 : currentYear;
          console.log('Trying previous month as fallback:', prevYear, prevMonth);
          
          const fallbackSummary = (await apiClient.getMonthlySummary(
            prevYear,
            prevMonth
          )) as { totalIncome: number; totalExpenses: number };
          
          console.log('Fallback monthly summary loaded:', fallbackSummary);
          monthlyIncome = fallbackSummary?.totalIncome || 0;
          monthlyExpenses = fallbackSummary?.totalExpenses || 0;
        } catch (fallbackError) {
          console.error('Fallback API call also failed:', fallbackError);
        }
        
        console.log('Falling back to local calculation from', transactions?.length || 0, 'transactions');
        // Fallback to calculating from recent transactions if API fails
        const monthlyTransactions = transactions?.filter(t => {
          const transactionDate = new Date(t.transactionDate);
          return transactionDate.getMonth() + 1 === currentMonth && 
                 transactionDate.getFullYear() === currentYear;
        }) || [];
        
        console.log('Filtered monthly transactions:', monthlyTransactions.length);
        console.log('Current month/year for filtering:', currentMonth, currentYear);
        
        const incomeTransactions = monthlyTransactions.filter(t => t.amount > 0);
        const expenseTransactions = monthlyTransactions.filter(t => t.amount < 0);
        
        monthlyIncome = incomeTransactions.reduce((sum, t) => sum + t.amount, 0);
        monthlyExpenses = Math.abs(expenseTransactions.reduce((sum, t) => sum + t.amount, 0));
        
        console.log('Fallback calculation results:');
        console.log('Income transactions:', incomeTransactions.length, 'Total:', monthlyIncome);
        console.log('Expense transactions:', expenseTransactions.length, 'Total:', monthlyExpenses);
      }
      
      setDashboardData({
        totalBalance,
        monthlyIncome,
        monthlyExpenses,
        transactionCount: totalTransactionCount,
        accountCount: accounts?.length || 0,
        hasCategories,
        recentTransactions: transactions?.slice(0, 5) || []
      });
      
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
      // For debugging, let's see the specific error
      if (error instanceof Error) {
        console.error('Error details:', error.message);
      }
    } finally {
      setDataLoading(false);
    }
  }, [isAuthenticated]);

  // Load actual dashboard data
  useEffect(() => {
    if (isAuthenticated && !isLoading && apiClient.getToken()) {
      console.log('Authentication confirmed, loading dashboard data...');
      loadDashboardData();
    }
  }, [isAuthenticated, isLoading, loadDashboardData]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <CreditCardIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }


  const stats = [
    {
      title: t('totalBalance'),
      value: dashboardData.totalBalance,
      color: 'primary',
      change: '+12.5%',
      emptyText: t('emptyState.connectAccounts'),
    },
    {
      title: t('monthlyIncome'),
      value: dashboardData.monthlyIncome,
      color: 'success',
      change: '+8.2%',
      emptyText: t('emptyState.addIncome'),
    },
    {
      title: t('monthlyExpenses'),
      value: dashboardData.monthlyExpenses,
      color: 'danger',
      change: '-3.1%',
      emptyText: t('emptyState.trackSpending'),
    },
    {
      title: t('transactionsCount'),
      value: dashboardData.transactionCount,
      color: 'info',
      emptyText: t('emptyState.noTransactions'),
      isCount: true,
    },
  ];


  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-8">
        {/* Welcome Section - hidden during onboarding since WelcomeScreen has its own header */}
        {(dataLoading || dashboardData.transactionCount > 0) && (
          <div className="mb-8 lg:mb-10 animate-fade-in-up">
            <h1 className="text-3xl sm:text-4xl lg:text-5xl font-bold text-gray-900 mb-2 lg:mb-3">
              {t('welcomeBack', { name: user?.firstName || user?.userName || '' })}
            </h1>
            <p className="text-base sm:text-lg lg:text-xl text-gray-700">
              {t('overview')}
            </p>
          </div>
        )}

        {/* Onboarding: show welcome screen when no transactions */}
        {!dataLoading && dashboardData.transactionCount === 0 ? (
          <WelcomeScreen
            accountCount={dashboardData.accountCount}
            hasCategories={dashboardData.hasCategories}
            onAccountAdded={loadDashboardData}
            onCategoriesInitialized={loadDashboardData}
          />
        ) : (
        <>
        {/* Stats Grid - Row 1: All 4 stat cards at compact height */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6 mb-6">
          {dataLoading ? (
            // Show skeleton loading state
            Array.from({ length: 4 }).map((_, index) => (
              <StatCardSkeleton key={index} />
            ))
          ) : (
            stats.map((stat, index) => {
              const getBorderColor = (color: string) => {
                switch (color) {
                  case 'primary': return 'border-l-primary-500';
                  case 'success': return 'border-l-success-500';
                  case 'danger': return 'border-l-danger-500';
                  case 'info': return 'border-l-info-500';
                  default: return 'border-l-primary-500';
                }
              };

              const isEmpty = stat.value === 0;
              const isCount = 'isCount' in stat && stat.isCount;

              return (
                <Card
                  key={stat.title}
                  className={`card-hover bg-white/90 backdrop-blur-xs border-0 border-l-4 ${getBorderColor(stat.color)} shadow-lg animate-fade-in-up ${isEmpty ? 'opacity-75' : ''}`}
                  style={{ animationDelay: `${index * 0.1}s` }}
                >
                  <CardContent className="p-4 lg:p-6">
                    <div className="space-y-2">
                      <p className="text-gray-600 text-xs sm:text-sm font-medium">{stat.title}</p>
                      {isEmpty ? (
                        <>
                          <p className="text-2xl sm:text-3xl font-bold text-gray-400">–––</p>
                          <p className="text-xs sm:text-sm text-gray-500">{stat.emptyText}</p>
                        </>
                      ) : (
                        <>
                          <p className="text-2xl sm:text-3xl font-bold text-gray-900">
                            {isCount ? stat.value.toLocaleString() : formatCurrency(stat.value)}
                          </p>
                          {'change' in stat && stat.change && (
                            <p className={`text-xs sm:text-sm font-medium ${
                              stat.change.startsWith('+') ? 'text-success' : 'text-danger'
                            }`}>
                              {stat.change} {t('fromLastMonth')}
                            </p>
                          )}
                        </>
                      )}
                    </div>
                  </CardContent>
                </Card>
              );
            })
          )}
        </div>

        {/* Upcoming Bills Widget - Row 2: Dedicated row for taller content */}
        {!dataLoading && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 lg:gap-6 mb-8 lg:mb-10">
            <div className="animate-fade-in-up" style={{ animationDelay: '0.4s' }}>
              <UpcomingBillsWidget />
            </div>
          </div>
        )}

        {/* Recent Transactions Section */}
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg mb-8 animate-bounce-in">
          <CardHeader>
            <div className="flex justify-between items-center">
              <CardTitle className="text-2xl font-bold text-gray-900">{t('recentTransactions')}</CardTitle>
              <div className="flex gap-2">
                {/* Desktop: Show all buttons */}
                {!isMobile && (
                  <>
                    <AkahuSyncButton
                      onSyncComplete={() => loadDashboardData()}
                    />
                    <Link href={features.aiCategorization ? "/import/ai-csv" : "/import"}>
                      <Button variant="secondary" size="sm" className="flex items-center gap-2">
                        <ArrowDownTrayIcon className="w-4 h-4" />
                        {t('importCsv')}
                      </Button>
                    </Link>
                  </>
                )}

                {/* Mobile: Overflow menu for secondary actions */}
                {isMobile && (
                  <MobileActionsOverflow
                    actions={[
                      {
                        id: 'sync',
                        label: isSyncing ? t('akahuSync.syncing') : t('akahuSync.refresh'),
                        icon: <ArrowPathIcon className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />,
                        onClick: handleMobileSync,
                        show: hasAkahuConnection,
                        disabled: isSyncing,
                      },
                      {
                        id: 'import',
                        label: t('importCsv'),
                        icon: <ArrowDownTrayIcon className="w-4 h-4" />,
                        href: features.aiCategorization ? "/import/ai-csv" : "/import",
                      },
                    ]}
                  />
                )}

                {/* View All - always visible */}
                <Link href="/transactions">
                  <Button variant="secondary" size="sm">
                    {t('viewAll')}
                  </Button>
                </Link>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {dashboardData.recentTransactions.length === 0 ? (
              <div className="text-center py-12 lg:py-20">
                {/* Enhanced empty state with animated icon */}
                <div className="relative mb-8">
                  <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4 transform hover:scale-105 transition-transform duration-300">
                    <WalletIcon className="w-10 h-10 text-white animate-pulse" />
                  </div>
                  {/* Decorative elements */}
                  <div className="absolute -top-2 -right-2 w-6 h-6 bg-gradient-to-br from-success-400 to-success-600 rounded-full opacity-60 animate-bounce" style={{ animationDelay: '0.2s' }}></div>
                  <div className="absolute -bottom-1 -left-3 w-4 h-4 bg-gradient-to-br from-info-400 to-info-600 rounded-full opacity-40 animate-bounce" style={{ animationDelay: '0.8s' }}></div>
                </div>

                <div className="max-w-lg mx-auto">
                  <h3 className="text-2xl font-bold text-gray-900 mb-3">{t('emptyState.title')}</h3>
                  <p className="text-gray-600 text-lg mb-8 leading-relaxed">
                    {t('emptyState.subtitle')}
                    <span className="block text-primary-600 font-medium mt-2">{t('emptyState.cta')}</span>
                  </p>

                  {/* Enhanced action buttons */}
                  <div className="flex flex-col sm:flex-row gap-3 sm:gap-4 justify-center">
                    <Link href="/transactions/new">
                      <Button className="w-full sm:w-auto group relative overflow-hidden bg-gradient-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white text-base sm:text-lg px-8 sm:px-10 py-3 sm:py-4 rounded-xl shadow-lg hover:shadow-2xl transform hover:-translate-y-1 transition-all duration-300 border-0">
                        <span className="relative z-10 flex items-center justify-center gap-2">
                          <PlusCircleIcon className="w-5 h-5" />
                          {t('emptyState.addTransaction')}
                        </span>
                        <div className="absolute inset-0 bg-gradient-to-r from-white/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
                      </Button>
                    </Link>
                    <Link href={features.aiCategorization ? "/import/ai-csv" : "/import"}>
                      <Button className="w-full sm:w-auto group relative overflow-hidden bg-white border-2 border-primary-300 hover:border-primary-500 text-primary-700 hover:text-primary-800 text-base sm:text-lg px-8 sm:px-10 py-3 sm:py-4 rounded-xl shadow-lg hover:shadow-2xl transform hover:-translate-y-1 transition-all duration-300">
                        <span className="relative z-10 flex items-center justify-center gap-2">
                          <ArrowDownTrayIcon className="w-5 h-5" />
                          {t('emptyState.importData')}
                        </span>
                        <div className="absolute inset-0 bg-gradient-to-r from-primary-50 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
                      </Button>
                    </Link>
                  </div>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                {dashboardData.recentTransactions.map((transaction) => (
                  <Link key={transaction.id} href={`/transactions/${transaction.id}`} className="block p-4 border border-gray-100 rounded-lg hover:bg-gray-50 transition-colors cursor-pointer">
                    <div className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3 sm:gap-4 min-w-0 flex-1">
                        <div className={`w-10 h-10 flex-shrink-0 rounded-full flex items-center justify-center ${
                          transaction.amount >= 0 ? 'bg-green-100' : 'bg-red-100'
                        }`}>
                          <BanknotesIcon className={`w-5 h-5 ${
                            transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'
                          }`} />
                        </div>
                        <div className="min-w-0 flex-1">
                          <h4 className="font-medium text-gray-900 truncate">{transaction.description}</h4>
                          <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                            <p className="text-sm text-gray-500 whitespace-nowrap">
                              {new Date(transaction.transactionDate).toLocaleDateString()} {transaction.accountName && `• ${transaction.accountName}`}
                            </p>
                            {transaction.categoryName && (
                              <span className="px-2 py-0.5 text-xs rounded-full bg-gray-100 text-gray-600 whitespace-nowrap">
                                {transaction.categoryName}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>
                      <div className={`text-base sm:text-lg font-semibold whitespace-nowrap flex-shrink-0 ${
                        transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'
                      }`}>
                        {transaction.amount >= 0 ? '+' : ''}${Math.abs(transaction.amount).toFixed(2)}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Monthly Summary */}
        <div className="mb-8 animate-fade-in-up" style={{ animationDelay: '0.3s' }}>
          <MonthlySummary />
        </div>

        {/* Budget Summary */}
        <div className="mb-8 animate-fade-in-up" style={{ animationDelay: '0.4s' }}>
          <BudgetSummaryWidget />
        </div>
        </>
        )}
      </main>
    </div>
  );
}

export default function DashboardPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense fallback={
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <CreditCardIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    }>
      <DashboardContent />
    </Suspense>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';
