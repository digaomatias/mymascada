'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { formatCurrency } from '@/lib/utils';
import { AccountTypeBadge } from '@/components/ui/account-type-badge';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { toast } from 'sonner';
import {
  BuildingOffice2Icon,
  CurrencyDollarIcon,
  BanknotesIcon,
  EllipsisVerticalIcon,
  PencilIcon,
  TrashIcon,
  DocumentArrowUpIcon
} from '@heroicons/react/24/outline';
import { AddAccountButton } from '@/components/buttons/add-account-button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useTranslations } from 'next-intl';

interface Account {
  id: number;
  name: string;
  type: number; // Backend returns numeric enum value
  institution?: string;
  currentBalance: number;
  calculatedBalance: number;
  currency: string;
  isActive: boolean;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

// Account type mapping removed - now using utility functions from lib/utils

export default function AccountsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [totalBalance, setTotalBalance] = useState(0);
  const [archiveConfirm, setArchiveConfirm] = useState<{ show: boolean; account?: Account }>({ show: false });
  const [deleteConfirm, setDeleteConfirm] = useState<{ show: boolean; account?: Account }>({ show: false });

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);


  useEffect(() => {
    if (isAuthenticated && typeof window !== 'undefined') {
      loadAccounts();
    }
  }, [isAuthenticated]);

  const loadAccounts = async () => {
    try {
      setLoading(true);
      const accountsData = await apiClient.getAccountsWithBalances() as Account[];
      setAccounts(accountsData || []);
      
      // Calculate total balance using calculated balance (excluding credit cards and loans which are liabilities)
      const total = (accountsData || [])
        .filter((account: Account) => account.type !== 2 && account.type !== 4) // Exclude credit cards (2) and loans (4) - 0-based values
        .reduce((sum: number, account: Account) => sum + account.calculatedBalance, 0);
      setTotalBalance(total);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    } finally {
      setLoading(false);
    }
  };

  const handleArchiveAccount = async (account: Account) => {
    try {
      // Archive the account
      await apiClient.archiveAccount(account.id);
      toast.success(t('archivedSuccess', { name: account.name }));
      loadAccounts(); // Refresh the list
      setArchiveConfirm({ show: false });
    } catch (error: unknown) {
      console.error('Failed to archive account:', error);

      // Handle specific error cases
      if (error instanceof Error && error.message?.includes('transactions')) {
        toast.error(t('archiveHasTransactions'));
      } else {
        toast.error(t('archiveFailed'));
      }
    }
  };

  const handleDeleteAccount = async (account: Account) => {
    try {
      await apiClient.deleteAccount(account.id);
      loadAccounts(); // Refresh the list
      toast.success(t('deletedSuccess', { name: account.name }));
      setDeleteConfirm({ show: false });
    } catch (error: unknown) {
      console.error('Failed to delete account:', error);
      toast.error(t('deleteFailed'));
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingOffice2Icon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900">
                {t('title')}
              </h1>
              <p className="text-gray-600 mt-1">
                {t('subtitle')}
              </p>
            </div>

            <AddAccountButton
              onSuccess={loadAccounts}
              className="flex items-center gap-2"
            />
          </div>
        </div>

        {/* Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('totalAccounts')}</p>
                  <p className="text-2xl font-bold text-gray-900">{accounts.length}</p>
                </div>
                <div className="w-12 h-12 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center">
                  <BuildingOffice2Icon className="w-6 h-6 text-white" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('netWorth')}</p>
                  <p className="text-2xl font-bold text-gray-900">{formatCurrency(totalBalance)}</p>
                </div>
                <div className="w-12 h-12 bg-gradient-to-br from-success-400 to-success-600 rounded-xl flex items-center justify-center">
                  <CurrencyDollarIcon className="w-6 h-6 text-white" />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-gray-600">{t('activeAccounts')}</p>
                  <p className="text-2xl font-bold text-gray-900">{accounts.filter(a => a.isActive).length}</p>
                </div>
                <div className="w-12 h-12 bg-gradient-to-br from-blue-400 to-blue-600 rounded-xl flex items-center justify-center">
                  <BanknotesIcon className="w-6 h-6 text-white" />
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Accounts List */}
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BuildingOffice2Icon className="w-6 h-6 text-primary-600" />
              {t('yourAccounts')}
            </CardTitle>
          </CardHeader>

          <CardContent>
            {loading ? (
              <div className="space-y-4">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="animate-pulse">
                    <div className="flex items-center gap-4 p-4 bg-gray-100 rounded-lg">
                      <div className="w-12 h-12 bg-gray-300 rounded-xl"></div>
                      <div className="flex-1">
                        <div className="h-4 bg-gray-300 rounded w-1/2 mb-2"></div>
                        <div className="h-3 bg-gray-300 rounded w-1/4"></div>
                      </div>
                      <div className="h-6 bg-gray-300 rounded w-20"></div>
                    </div>
                  </div>
                ))}
              </div>
            ) : accounts.length === 0 ? (
              <div className="text-center py-12">
                <div className="w-20 h-20 bg-gradient-to-br from-primary-400 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-6">
                  <BuildingOffice2Icon className="w-10 h-10 text-white" />
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('noAccounts')}</h3>
                <p className="text-gray-600 mb-6">
                  {t('noAccountsDescription')}
                </p>
                <AddAccountButton
                  onSuccess={loadAccounts}
                  className="flex items-center gap-2"
                >
                  {t('createFirst')}
                </AddAccountButton>
              </div>
            ) : (
              <div className="space-y-4">
                {accounts.map((account) => (
                  <div
                    key={account.id}
                    className="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors cursor-pointer"
                    onClick={() => router.push(`/accounts/${account.id}`)}
                  >
                    {/* Mobile Layout: Stack vertically */}
                    <div className="flex flex-col sm:flex-row sm:items-center gap-3 sm:gap-4">
                      {/* Top row on mobile: Icon + Name + Menu */}
                      <div className="flex items-center gap-3 sm:gap-4 min-w-0">
                        {/* Account Icon */}
                        <div className="w-10 h-10 sm:w-12 sm:h-12 flex-shrink-0 bg-gradient-to-br from-primary-400 to-primary-600 rounded-xl flex items-center justify-center">
                          <BuildingOffice2Icon className="w-5 h-5 sm:w-6 sm:h-6 text-white" />
                        </div>

                        {/* Account Name and Type */}
                        <div className="flex-1 min-w-0">
                          <h4 className="text-base sm:text-lg font-semibold text-gray-900 truncate">{account.name}</h4>
                          <div className="flex items-center gap-2 mt-1 flex-wrap">
                            <AccountTypeBadge type={account.type} />
                            {account.institution && (
                              <span className="text-xs sm:text-sm text-gray-500 truncate">{account.institution}</span>
                            )}
                          </div>
                        </div>

                        {/* Action Menu - visible on mobile, hover on desktop */}
                        <div className="relative group flex-shrink-0 sm:hidden" onClick={(e) => e.stopPropagation()}>
                          <Button variant="ghost" size="sm" className="w-8 h-8 p-0">
                            <EllipsisVerticalIcon className="w-4 h-4" />
                          </Button>

                          <div className="absolute right-0 top-full mt-1 w-48 bg-white rounded-lg shadow-lg border border-gray-200 py-1 z-10 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none group-hover:pointer-events-auto">
                            <Link
                              href={`/transactions/new?accountId=${account.id}`}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                            >
                              <DocumentArrowUpIcon className="w-4 h-4" />
                              {t('addTransaction')}
                            </Link>
                            <Link
                              href={`/accounts/${account.id}/edit`}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                            >
                              <PencilIcon className="w-4 h-4" />
                              {t('editAccount')}
                            </Link>
                            <button
                              onClick={() => setArchiveConfirm({ show: true, account })}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-orange-600 hover:bg-orange-50 w-full text-left cursor-pointer"
                            >
                              <TrashIcon className="w-4 h-4" />
                              {t('archiveAccount')}
                            </button>
                            <div className="border-t border-gray-200 my-1"></div>
                            <button
                              onClick={() => setDeleteConfirm({ show: true, account })}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-red-600 hover:bg-red-50 w-full text-left cursor-pointer"
                            >
                              <TrashIcon className="w-4 h-4" />
                              {t('deletePermanently')}
                            </button>
                          </div>
                        </div>
                      </div>

                      {/* Bottom row on mobile: Balance + Desktop Action Menu */}
                      <div className="flex items-center justify-between sm:justify-end gap-3 sm:ml-auto pl-13 sm:pl-0">
                        <div className="text-left sm:text-right">
                          <p className="text-base sm:text-lg font-bold text-gray-900">
                            {formatCurrency(account.calculatedBalance)}
                          </p>
                          <p className="text-xs text-gray-500 uppercase">
                            {account.currency}
                          </p>
                        </div>

                        {/* Desktop Action Menu */}
                        <div className="relative group hidden sm:block" onClick={(e) => e.stopPropagation()}>
                          <Button variant="ghost" size="sm" className="w-8 h-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity">
                            <EllipsisVerticalIcon className="w-4 h-4" />
                          </Button>

                          <div className="absolute right-0 top-full mt-1 w-48 bg-white rounded-lg shadow-lg border border-gray-200 py-1 z-10 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none group-hover:pointer-events-auto">
                            <Link
                              href={`/transactions/new?accountId=${account.id}`}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                            >
                              <DocumentArrowUpIcon className="w-4 h-4" />
                              {t('addTransaction')}
                            </Link>
                            <Link
                              href={`/accounts/${account.id}/edit`}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                            >
                              <PencilIcon className="w-4 h-4" />
                              {t('editAccount')}
                            </Link>
                            <button
                              onClick={() => setArchiveConfirm({ show: true, account })}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-orange-600 hover:bg-orange-50 w-full text-left cursor-pointer"
                            >
                              <TrashIcon className="w-4 h-4" />
                              {t('archiveAccount')}
                            </button>
                            <div className="border-t border-gray-200 my-1"></div>
                            <button
                              onClick={() => setDeleteConfirm({ show: true, account })}
                              className="flex items-center gap-2 px-4 py-2 text-sm text-red-600 hover:bg-red-50 w-full text-left cursor-pointer"
                            >
                              <TrashIcon className="w-4 h-4" />
                              {t('deletePermanently')}
                            </button>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </main>

      {/* Archive Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={archiveConfirm.show}
        title={t('archiveAccount')}
        description={t('archiveConfirmDetails', { name: archiveConfirm.account?.name || '' })}
        confirmText={t('archiveAccount')}
        cancelText={tCommon('cancel')}
        variant="default"
        onConfirm={() => archiveConfirm.account && handleArchiveAccount(archiveConfirm.account)}
        onClose={() => setArchiveConfirm({ show: false })}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={deleteConfirm.show}
        title={t('permanentDelete')}
        description={t('permanentDeleteConfirm', { name: deleteConfirm.account?.name || '' })}
        confirmText={t('deletePermanently')}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={() => deleteConfirm.account && handleDeleteAccount(deleteConfirm.account)}
        onClose={() => setDeleteConfirm({ show: false })}
      />
    </div>
  );
}

// Force dynamic rendering for this page
export const dynamic = 'force-dynamic';