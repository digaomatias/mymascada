'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import Navigation from '@/components/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, formatDate } from '@/lib/utils';
import { EditTransactionButton } from '@/components/buttons/edit-transaction-button';
import { CategoryPicker } from '@/components/forms/category-picker';
import { TransactionBackButton } from '@/components/ui/smart-back-button';
import { useTranslations } from 'next-intl';
import {
  EyeIcon,
  TrashIcon,
  ExclamationTriangleIcon,
  CalendarIcon,
  TagIcon,
  WalletIcon,
  MapPinIcon,
  DocumentTextIcon,
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon
} from '@heroicons/react/24/outline';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';

interface Category {
  id: number;
  name: string;
  type: number;
  parentId: number | null;
  color?: string;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountId: number;
  accountName?: string;
  categoryId?: number;
  categoryName?: string;
  categoryColor?: string;
  notes?: string;
  location?: string;
  tags?: string[];
  status: string | number;
  source: string | number;
  isReviewed: boolean;
  externalId?: string;
}

// Utility functions to map enum values to translation keys
const getStatusKey = (status: string | number): string => {
  if (typeof status === 'string') return status.toLowerCase();

  switch (status) {
    case 1: return 'pending';
    case 2: return 'cleared';
    case 3: return 'reconciled';
    case 4: return 'cancelled';
    default: return 'unknown';
  }
};

const getSourceKey = (source: string | number): string => {
  if (typeof source === 'string') return source.toLowerCase().replace(' ', '');

  switch (source) {
    case 1: return 'manual';
    case 2: return 'csvImport';
    case 3: return 'bankApi';
    case 4: return 'ofxImport';
    case 5: return 'import';
    default: return 'unknown';
  }
};

export default function TransactionDetailsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const router = useRouter();
  const params = useParams();
  const transactionId = params?.id as string;
  
  const [loadingTransaction, setLoadingTransaction] = useState(true);
  const [transaction, setTransaction] = useState<Transaction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState({ show: false, transactionId: 0 });
  const [deleting, setDeleting] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [updatingCategory, setUpdatingCategory] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const loadTransaction = useCallback(async () => {
    try {
      setLoadingTransaction(true);
      setError(null);
      
      const transactionData = await apiClient.getTransaction(parseInt(transactionId)) as Transaction;
      setTransaction(transactionData);
    } catch (err) {
      console.error('Failed to load transaction:', err);
      setError(t('failedToLoadTransaction'));
    } finally {
      setLoadingTransaction(false);
    }
  }, [transactionId]);

  const loadCategories = useCallback(async () => {
    try {
      setLoadingCategories(true);
      const categoriesData = await apiClient.getCategories() as Category[];
      setCategories(categoriesData);
    } catch (err) {
      console.error('Failed to load categories:', err);
    } finally {
      setLoadingCategories(false);
    }
  }, []);

  const handleCategoryAssignment = async (categoryId: string | number) => {
    if (!transaction) return;
    
    try {
      setUpdatingCategory(true);
      
      // Map status to enum values
      const statusMap: Record<string, number> = {
        'pending': 1,
        'cleared': 2, 
        'reconciled': 3,
        'cancelled': 4
      };
      
      const statusString = getStatusKey(transaction.status);
      const statusValue = statusMap[statusString] || 2; // Default to Cleared

      await apiClient.updateTransaction(transaction.id, {
        ...transaction,
        categoryId: Number(categoryId),
        transactionDate: transaction.transactionDate,
        status: statusValue,
      });
      
      // Reload transaction to get updated category info
      await loadTransaction();
      toast.success(tToasts('categoryAssigned'));
    } catch (err) {
      console.error('Failed to assign category:', err);
      toast.error(tToasts('categoryAssignFailed'));
    } finally {
      setUpdatingCategory(false);
    }
  };

  useEffect(() => {
    if (isAuthenticated && transactionId) {
      loadTransaction();
      loadCategories();
    }
  }, [isAuthenticated, transactionId, loadTransaction, loadCategories]);

  const handleDeleteTransaction = async () => {
    try {
      setDeleting(true);
      await apiClient.deleteTransaction(deleteConfirm.transactionId);
      toast.success(tToasts('transactionDeleted'), { duration: 4000 });
      router.push('/transactions');
    } catch (err) {
      console.error('Failed to delete transaction:', err);
      toast.error(t('failedToDeleteTransaction'), { duration: 4000 });
    } finally {
      setDeleting(false);
      setDeleteConfirm({ show: false, transactionId: 0 });
    }
  };

  if (isLoading || loadingTransaction) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <EyeIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loadingTransaction')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (error && !transaction) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
        <Navigation />
        
        <main className="container-responsive py-4 sm:py-6 lg:py-8">
          <Card className="max-w-2xl mx-auto bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-8 text-center">
              <ExclamationTriangleIcon className="w-16 h-16 text-danger-500 mx-auto mb-4" />
              <h2 className="text-xl font-semibold text-gray-900 mb-2">{t('transactionNotFound')}</h2>
              <p className="text-gray-600 mb-6">{error}</p>
              <TransactionBackButton />
            </CardContent>
          </Card>
        </main>
      </div>
    );
  }

  if (!transaction) {
    return null;
  }

  const isIncome = transaction.amount > 0; // Positive = Income, Negative = Expense

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />
      
      <main className="container-responsive py-4 sm:py-6 lg:py-8 mobile-form-safe">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          {/* Navigation Bar */}
          <div className="flex items-center justify-between mb-6">
            <TransactionBackButton />
          </div>
          
          
          {/* Page Title */}
          <div className="text-center mb-8">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('transactionDetails')}
            </h1>
            <p className="text-gray-600 text-sm sm:text-base">
              {t('viewAndManage')}
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Details */}
          <div className="lg:col-span-2 space-y-6">
            {/* Transaction Overview */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg relative z-10 overflow-visible">
              <CardHeader>
                <CardTitle className="text-xl font-bold text-gray-900 flex items-center gap-2">
                  {isIncome ? (
                    <ArrowTrendingUpIcon className="w-5 h-5 text-success-500" />
                  ) : (
                    <ArrowTrendingDownIcon className="w-5 h-5 text-red-500" />
                  )}
                  {transaction.userDescription || transaction.description}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center justify-between">
                  <span className="text-gray-600">{tCommon('amount')}</span>
                  <span className={`text-2xl font-bold ${isIncome ? 'text-success-600' : 'text-red-600'}`}>
                    {formatCurrency(transaction.amount)}
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-gray-600">{tCommon('date')}</span>
                  <div className="flex items-center gap-2">
                    <CalendarIcon className="w-4 h-4 text-gray-400" />
                    <span className="font-medium">{formatDate(transaction.transactionDate)}</span>
                  </div>
                </div>

                {transaction.accountName && (
                  <div className="flex items-center justify-between">
                    <span className="text-gray-600">{tCommon('account')}</span>
                    <div className="flex items-center gap-2">
                      <WalletIcon className="w-4 h-4 text-gray-400" />
                      <span className="font-medium">{transaction.accountName}</span>
                    </div>
                  </div>
                )}

                <div className="flex items-center justify-between">
                  <span className="text-gray-600">{tCommon('category')}</span>
                  {transaction.categoryName ? (
                    <Badge 
                      variant="secondary"
                      style={{ backgroundColor: transaction.categoryColor + '20', color: transaction.categoryColor }}
                      className="border-0"
                    >
                      <TagIcon className="w-3 h-3 mr-1" />
                      {transaction.categoryName}
                    </Badge>
                  ) : (
                    <div className="flex-1 max-w-xs ml-4">
                      {updatingCategory ? (
                        <div className="flex items-center justify-center py-2">
                          <div className="w-4 h-4 border-2 border-primary-500 border-t-transparent rounded-full animate-spin" />
                          <span className="ml-2 text-sm text-gray-600">{t('saving')}</span>
                        </div>
                      ) : (
                        <CategoryPicker
                          value=""
                          onChange={handleCategoryAssignment}
                          categories={categories}
                          placeholder={t('chooseCategory')}
                          disabled={loadingCategories || updatingCategory}
                          disableQuickPicks={true}
                        />
                      )}
                    </div>
                  )}
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-gray-600">{tCommon('status')}</span>
                  <Badge variant={getStatusKey(transaction.status) === 'cleared' ? 'default' : 'secondary'}>
                    {t(`status.${getStatusKey(transaction.status)}`)}
                  </Badge>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-gray-600">{tCommon('source')}</span>
                  <Badge variant="outline">
                    {t(`source.${getSourceKey(transaction.source)}`)}
                  </Badge>
                </div>
              </CardContent>
            </Card>

            {/* Additional Details */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900 flex items-center gap-2">
                  <DocumentTextIcon className="w-5 h-5" />
                  {t('additionalInformation')}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {transaction.description !== transaction.userDescription && (
                  <div>
                    <label className="text-sm font-medium text-gray-600 mb-1 block">{t('originalDescription')}</label>
                    <p className="text-gray-900">{transaction.description}</p>
                  </div>
                )}

                {transaction.notes && (
                  <div>
                    <label className="text-sm font-medium text-gray-600 mb-1 block">{tCommon('notes')}</label>
                    <p className="text-gray-900">{transaction.notes}</p>
                  </div>
                )}

                {transaction.location && (
                  <div>
                    <label className="text-sm font-medium text-gray-600 mb-1 block">{t('location')}</label>
                    <div className="flex items-center gap-2">
                      <MapPinIcon className="w-4 h-4 text-gray-400" />
                      <span className="text-gray-900">{transaction.location}</span>
                    </div>
                  </div>
                )}

                {Array.isArray(transaction.tags) && transaction.tags.length > 0 && (
                  <div>
                    <label className="text-sm font-medium text-gray-600 mb-2 block">{t('tags')}</label>
                    <div className="flex flex-wrap gap-2">
                      {transaction.tags.map((tag, index) => (
                        <Badge key={index} variant="outline" className="text-xs">
                          {tag}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}

                {transaction.externalId && (
                  <div>
                    <label className="text-sm font-medium text-gray-600 mb-1 block">{t('externalId')}</label>
                    <code className="text-xs bg-gray-100 px-2 py-1 rounded text-gray-700">
                      {transaction.externalId}
                    </code>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Actions Sidebar */}
          <div className="space-y-6">
            {/* Quick Actions */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900">{tCommon('actions')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <EditTransactionButton
                  transactionId={transaction.id.toString()}
                  onSuccess={loadTransaction}
                  className="w-full bg-primary-500 hover:bg-primary-600"
                />

                <Button
                  variant="secondary"
                  className="w-full border-danger-300 text-danger-600 hover:bg-danger-50"
                  onClick={() => setDeleteConfirm({ show: true, transactionId: transaction.id })}
                >
                  <TrashIcon className="w-4 h-4 mr-2" />
                  {t('deleteTransaction')}
                </Button>
              </CardContent>
            </Card>

            {/* Transaction Info */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900">{t('transactionInfo')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">{t('id')}</span>
                  <span className="font-mono text-gray-900">#{transaction.id}</span>
                </div>

                <div className="flex justify-between">
                  <span className="text-gray-600">{tCommon('type')}</span>
                  <Badge variant={isIncome ? 'default' : 'secondary'}>
                    {isIncome ? tCommon('income') : tCommon('expense')}
                  </Badge>
                </div>

                <div className="flex justify-between">
                  <span className="text-gray-600">{tCommon('reviewed')}</span>
                  <Badge variant={transaction.isReviewed ? 'default' : 'secondary'}>
                    {transaction.isReviewed ? tCommon('yes') : tCommon('no')}
                  </Badge>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      </main>

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={deleteConfirm.show}
        onClose={() => setDeleteConfirm({ show: false, transactionId: 0 })}
        onConfirm={handleDeleteTransaction}
        title={t('deleteTransaction')}
        description={t('deleteConfirmFull')}
        confirmText={deleting ? tCommon('loading') : tCommon('delete')}
        variant="danger"
      />
    </div>
  );
}
