'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import AccountForm, { Account } from '@/components/forms/account-form';
import { BalanceAdjustment } from '@/components/forms/balance-adjustment';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import {
  ArrowLeftIcon,
  BuildingOffice2Icon,
  CheckIcon,
  ExclamationTriangleIcon,
  TrashIcon
} from '@heroicons/react/24/outline';

export default function EditAccountPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const accountId = params?.id as string;
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');

  const [loading, setLoading] = useState(false);
  const [loadingAccount, setLoadingAccount] = useState(true);
  const [success, setSuccess] = useState(false);
  const [account, setAccount] = useState<Account | null>(null);
  const [hasTransactions, setHasTransactions] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteAccountName, setDeleteAccountName] = useState('');

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadAccount = useCallback(async () => {
    try {
      setLoadingAccount(true);
      
      // Load account details with calculated balance
      const accountData = await apiClient.getAccountWithBalance(parseInt(accountId)) as Account & { calculatedBalance: number };
      setAccount(accountData);
      
      // Check if account has transactions
      try {
        const transactionsData = await apiClient.getAccountTransactions(parseInt(accountId)) as { hasTransactions: boolean };
        setHasTransactions(transactionsData.hasTransactions);
      } catch {
        // If transactions endpoint doesn't exist, assume no transactions
        setHasTransactions(false);
      }
      
    } catch (error) {
      console.error('Failed to load account:', error);
      router.push('/accounts'); // Redirect if account not found
    } finally {
      setLoadingAccount(false);
    }
  }, [accountId, router]);

  useEffect(() => {
    if (isAuthenticated && accountId) {
      loadAccount();
    }
  }, [isAuthenticated, accountId, loadAccount]);

  const handleSubmit = async (data: Omit<Account, 'id'>) => {
    setLoading(true);
    try {
      await apiClient.updateAccount(parseInt(accountId), {
        ...data,
        id: parseInt(accountId), // Include the ID for the update
      });
      
      setSuccess(true);
      
      // Redirect after a brief success message
      setTimeout(() => {
        router.push('/accounts');
      }, 1500);
    } catch (error) {
      console.error('Failed to update account:', error);
      throw error; // Let the form handle the error display
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    router.push('/accounts');
  };

  const handleDeleteAccount = async () => {
    if (!account || deleteAccountName !== account.name) {
      toast.error(tToasts('accountNameMismatch'));
      return;
    }

    try {
      setLoading(true);
      await apiClient.deleteAccount(parseInt(accountId));
      toast.success(tToasts('accountDeletedWithTransactions', { name: account.name }));
      router.push('/accounts');
    } catch (error) {
      console.error('Failed to delete account:', error);
      toast.error(tToasts('accountDeleteFailed'));
    } finally {
      setLoading(false);
      setShowDeleteConfirm(false);
    }
  };

  if (isLoading || loadingAccount) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">
            {loadingAccount ? t('loadingAccount') : tCommon('loading')}
          </div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (!account) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <Card className="mx-4 max-w-md w-full bg-white/90 backdrop-blur-xs border-0 shadow-2xl">
          <CardContent className="p-8 text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-red-500 to-red-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <ExclamationTriangleIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('accountNotFound')}</h2>
            <p className="text-gray-600 mb-6">{t('accountNotFoundDesc')}</p>
            <Link href="/accounts">
              <Button>{t('backToAccounts')}</Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (success) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <Card className="mx-4 max-w-md w-full bg-white/90 backdrop-blur-xs border-0 shadow-2xl">
          <CardContent className="p-8 text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-success-500 to-success-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('accountUpdated')}</h2>
            <p className="text-gray-600 mb-6">{t('accountUpdatedDesc')}</p>
            <div className="text-sm text-gray-500">{t('redirectingToAccounts')}</div>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8 mobile-form-safe">
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
          </div>

          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('editAccount')}
            </h1>
            <p className="text-gray-600 text-sm sm:text-base">
              {t('updateAccountDesc')}
            </p>
          </div>
        </div>

        {/* Account Form */}
        <div className="max-w-2xl mx-auto">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <BuildingOffice2Icon className="w-6 h-6 text-primary-600" />
                {t('accountDetails')}
              </CardTitle>
            </CardHeader>

            <CardContent>
              {/* Data Integrity Warning */}
              {hasTransactions && (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-6">
                  <div className="flex items-start gap-3">
                    <ExclamationTriangleIcon className="w-5 h-5 text-yellow-500 flex-shrink-0 mt-0.5" />
                    <div>
                      <h4 className="text-sm font-medium text-yellow-800">{t('editCarefully')}</h4>
                      <p className="text-sm text-yellow-700 mt-1">
                        {t('editCarefullyDesc')}
                      </p>
                      <ul className="text-sm text-yellow-700 mt-2 list-disc list-inside">
                        <li>{t('editCarefullyList.balance')}</li>
                        <li>{t('editCarefullyList.currency')}</li>
                        <li>{t('editCarefullyList.type')}</li>
                      </ul>
                    </div>
                  </div>
                </div>
              )}

              <AccountForm
                variant="full"
                initialData={{
                  name: account.name,
                  type: account.type,
                  institution: account.institution,
                  currentBalance: (account as Account & { calculatedBalance?: number }).calculatedBalance || account.currentBalance,
                  currency: account.currency,
                  notes: account.notes,
                }}
                onSubmit={handleSubmit}
                onCancel={handleCancel}
                loading={loading}
                submitText={t('updateAccount')}
                showCancel={true}
                hasTransactions={hasTransactions}
              />

              {/* Balance Adjustment */}
              {account && (
                <div className="mt-6 pt-6 border-t border-gray-200">
                  <BalanceAdjustment
                    currentBalance={(account as Account & { calculatedBalance?: number }).calculatedBalance || account.currentBalance}
                    currency={account.currency}
                    accountId={accountId}
                    onAdjustmentComplete={loadAccount}
                  />
                </div>
              )}
            </CardContent>
          </Card>

          {/* Danger Zone - Account Deletion */}
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg mt-6 border-red-200">
            <CardHeader className="bg-red-50 border-b border-red-200">
              <CardTitle className="flex items-center gap-2 text-red-800">
                <ExclamationTriangleIcon className="w-6 h-6" />
                {t('dangerZone')}
              </CardTitle>
            </CardHeader>

            <CardContent className="p-6">
              <div className="space-y-4">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 mb-2">{t('deleteThisAccount')}</h3>
                  <p className="text-sm text-gray-600 mb-4">
                    {t('deleteAccountWarning')}
                  </p>
                  <ul className="list-disc list-inside text-sm text-gray-600 space-y-1 mb-4">
                    <li>{t('deleteAccountList.account', { name: account?.name })}</li>
                    <li>{t('deleteAccountList.transactions')}</li>
                    <li>{t('deleteAccountList.history')}</li>
                    <li>{t('deleteAccountList.irreversible')}</li>
                  </ul>

                  {!showDeleteConfirm ? (
                    <Button
                      variant="danger"
                      onClick={() => setShowDeleteConfirm(true)}
                      className="flex items-center gap-2"
                    >
                      <TrashIcon className="w-4 h-4" />
                      {t('deleteThisAccount')}
                    </Button>
                  ) : (
                    <div className="bg-red-50 border border-red-300 rounded-lg p-4">
                      <h4 className="text-sm font-semibold text-red-800 mb-3">
                        ⚠️ {t('finalConfirmation')}
                      </h4>
                      <p className="text-sm text-red-700 mb-3">
                        {t('typeAccountName')}
                      </p>
                      <p className="font-mono font-semibold text-red-900 bg-red-100 px-3 py-2 rounded mb-3">
                        {account?.name}
                      </p>
                      <input
                        type="text"
                        value={deleteAccountName}
                        onChange={(e) => setDeleteAccountName(e.target.value)}
                        placeholder={t('typeAccountNamePlaceholder')}
                        className="w-full px-3 py-2 border border-red-300 rounded-md focus:outline-none focus:ring-2 focus:ring-red-500 mb-3"
                      />
                      <div className="flex gap-3">
                        <Button
                          variant="danger"
                          onClick={handleDeleteAccount}
                          disabled={loading || deleteAccountName !== account?.name}
                          className="flex items-center gap-2"
                        >
                          <TrashIcon className="w-4 h-4" />
                          {loading ? t('deleting') : t('confirmDelete')}
                        </Button>
                        <Button
                          variant="secondary"
                          onClick={() => {
                            setShowDeleteConfirm(false);
                            setDeleteAccountName('');
                          }}
                          disabled={loading}
                        >
                          {tCommon('cancel')}
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
