'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import Link from 'next/link';
import {
  CreditCardIcon,
  ArrowLeftIcon,
  CheckCircleIcon,
  ExclamationCircleIcon,
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import { apiClient, BillingStatusResponse } from '@/lib/api-client';
import { toast } from 'sonner';
import { useFeatures } from '@/contexts/features-context';
import { useLocale } from '@/contexts/locale-context';

export default function BillingPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const { features } = useFeatures();
  const { locale } = useLocale();
  const t = useTranslations('settings.billing');
  const tCommon = useTranslations('common');

  const [billingStatus, setBillingStatus] = useState<BillingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [checkoutLoading, setCheckoutLoading] = useState(false);
  const [portalLoading, setPortalLoading] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadBillingStatus = useCallback(async () => {
    setLoading(true);
    try {
      const status = await apiClient.getBillingStatus();
      setBillingStatus(status);
    } catch (error) {
      console.error('Failed to load billing status:', error);
      toast.error(t('errors.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (isAuthenticated && !isLoading && features.stripeBilling) {
      loadBillingStatus();
    }
  }, [isAuthenticated, isLoading, features.stripeBilling, loadBillingStatus]);

  const handleCheckout = async (priceId: string) => {
    setCheckoutLoading(true);
    try {
      const response = await apiClient.createCheckoutSession(
        priceId,
        window.location.href
      );
      window.location.href = response.url;
    } catch (error) {
      console.error('Checkout failed:', error);
      toast.error(t('checkoutError'));
    } finally {
      setCheckoutLoading(false);
    }
  };

  const handleManageBilling = async () => {
    setPortalLoading(true);
    try {
      const response = await apiClient.createPortalSession(
        window.location.href
      );
      window.location.href = response.url;
    } catch (error) {
      console.error('Portal failed:', error);
      toast.error(t('portalError'));
    } finally {
      setPortalLoading(false);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <CreditCardIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !features.stripeBilling) {
    return null;
  }

  const isPaid = billingStatus?.status === 'active';
  const isCanceled = billingStatus?.status === 'canceled';

  const formatLimit = (value: number) => {
    if (value === 0) return t('unlimited');
    return value.toLocaleString();
  };

  const getUsagePercent = (current: number, max: number) => {
    if (max === 0) return 0;
    return Math.min(Math.round((current / max) * 100), 100);
  };

  return (
    <AppLayout>
      {/* Back link */}
      <Link
        href="/settings"
        className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 mb-4"
      >
        <ArrowLeftIcon className="w-4 h-4" />
        {t('backToSettings')}
      </Link>

      {/* Header */}
      <div className="mb-6 lg:mb-8">
        <div className="flex items-center gap-3 mb-1">
          <div className="w-10 h-10 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center">
            <CreditCardIcon className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
              {t('title')}
            </h1>
            <p className="text-gray-600 mt-0.5">{t('subtitle')}</p>
          </div>
        </div>
      </div>

      {loading ? (
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="p-6">
            <div className="animate-pulse space-y-4">
              <div className="h-4 bg-gray-200 rounded w-1/3"></div>
              <div className="h-10 bg-gray-200 rounded"></div>
              <div className="h-4 bg-gray-200 rounded w-1/4"></div>
              <div className="h-10 bg-gray-200 rounded"></div>
            </div>
          </CardContent>
        </Card>
      ) : billingStatus ? (
        <div className="space-y-4">
          {/* Current Plan Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-start gap-3">
                {isPaid ? (
                  <CheckCircleIcon className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
                ) : (
                  <ExclamationCircleIcon className="w-5 h-5 text-amber-500 shrink-0 mt-0.5" />
                )}
                <div className="flex-1">
                  <h3 className="text-lg font-semibold text-gray-900">
                    {t('currentPlan')}
                  </h3>
                  <p className="text-sm text-gray-600 mt-1">
                    {t('planName', { plan: billingStatus.planName })}
                  </p>
                  <p className="text-sm text-gray-600">
                    {t('planStatus', { status: billingStatus.status })}
                  </p>
                  {billingStatus.currentPeriodEnd && (
                    <p className="text-sm text-gray-500 mt-1">
                      {t('periodEnd')}: {new Date(billingStatus.currentPeriodEnd).toLocaleDateString(locale)}
                    </p>
                  )}
                  {isCanceled && (
                    <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-2 mt-2">
                      {t('canceledNotice')}
                    </p>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Usage Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <h3 className="text-lg font-semibold text-gray-900 mb-4">
                {t('currentUsage')}
              </h3>
              <div className="space-y-4">
                {/* Accounts usage */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-600">{t('accounts')}</span>
                    <span className="font-medium text-gray-900">
                      {billingStatus.currentAccountCount} / {formatLimit(billingStatus.maxAccounts)}
                    </span>
                  </div>
                  {billingStatus.maxAccounts > 0 && (
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className={`h-2 rounded-full transition-all ${
                          getUsagePercent(billingStatus.currentAccountCount, billingStatus.maxAccounts) >= 90
                            ? 'bg-red-500'
                            : getUsagePercent(billingStatus.currentAccountCount, billingStatus.maxAccounts) >= 70
                              ? 'bg-amber-500'
                              : 'bg-primary-500'
                        }`}
                        style={{ width: `${getUsagePercent(billingStatus.currentAccountCount, billingStatus.maxAccounts)}%` }}
                      />
                    </div>
                  )}
                </div>

                {/* Transactions usage */}
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-gray-600">{t('transactionsThisMonth')}</span>
                    <span className="font-medium text-gray-900">
                      {billingStatus.currentMonthTransactionCount} / {formatLimit(billingStatus.maxTransactionsPerMonth)}
                    </span>
                  </div>
                  {billingStatus.maxTransactionsPerMonth > 0 && (
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className={`h-2 rounded-full transition-all ${
                          getUsagePercent(billingStatus.currentMonthTransactionCount, billingStatus.maxTransactionsPerMonth) >= 90
                            ? 'bg-red-500'
                            : getUsagePercent(billingStatus.currentMonthTransactionCount, billingStatus.maxTransactionsPerMonth) >= 70
                              ? 'bg-amber-500'
                              : 'bg-primary-500'
                        }`}
                        style={{ width: `${getUsagePercent(billingStatus.currentMonthTransactionCount, billingStatus.maxTransactionsPerMonth)}%` }}
                      />
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Actions Card */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex flex-col sm:flex-row gap-3">
                {billingStatus.stripeCustomerId && (
                  <Button
                    variant="primary"
                    onClick={handleManageBilling}
                    loading={portalLoading}
                    disabled={portalLoading}
                  >
                    {t('manageBilling')}
                  </Button>
                )}
                {/* TODO: Implement plan selection UI that provides the correct Stripe priceId */}
                {!isPaid && (
                  <Button
                    variant="secondary"
                    disabled={true}
                    title="Plan selection coming soon"
                  >
                    {t('upgradePlan')}
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      ) : (
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="p-6">
            <div className="flex items-start gap-3">
              <ExclamationCircleIcon className="w-5 h-5 text-amber-500 shrink-0 mt-0.5" />
              <div>
                <p className="text-sm text-amber-800">{t('noBilling')}</p>
                <p className="text-sm text-gray-600 mt-2">
                  {t('setupBilling')}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
