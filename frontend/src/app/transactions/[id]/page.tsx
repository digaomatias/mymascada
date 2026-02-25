'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useParams } from 'next/navigation';
import { useEffect, useState, useCallback } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { apiClient } from '@/lib/api-client';
import { formatCurrency, formatDate, cn } from '@/lib/utils';
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

  CheckCircleIcon,
  ClockIcon,
} from '@heroicons/react/24/outline';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import Link from 'next/link';

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
  const [reviewingTransaction, setReviewingTransaction] = useState(false);

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

  const handleMarkAsReviewed = async () => {
    if (!transaction) return;

    try {
      setReviewingTransaction(true);
      await apiClient.reviewTransaction(transaction.id);
      setTransaction(prev => prev ? { ...prev, isReviewed: !prev.isReviewed } : null);
      toast.success(tToasts('transactionReviewed'));
    } catch (err) {
      console.error('Failed to review transaction:', err);
      toast.error(t('failedToLoadTransaction'));
    } finally {
      setReviewingTransaction(false);
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
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <EyeIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('loadingTransaction')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (error && !transaction) {
    return (
      <AppLayout>
        <div className="rounded-[26px] border border-violet-100/60 bg-white/90 p-8 shadow-lg shadow-violet-200/20 backdrop-blur-xs max-w-2xl mx-auto text-center">
          <ExclamationTriangleIcon className="w-16 h-16 text-red-500 mx-auto mb-4" />
          <h2 className="font-[var(--font-dash-sans)] text-xl font-semibold text-slate-900 mb-2">{t('transactionNotFound')}</h2>
          <p className="text-slate-600 mb-6">{error}</p>
          <TransactionBackButton />
        </div>
      </AppLayout>
    );
  }

  if (!transaction) {
    return null;
  }

  const isIncome = transaction.amount > 0;

  const hasAdditionalInfo =
    (transaction.description !== transaction.userDescription) ||
    transaction.notes ||
    transaction.location ||
    (Array.isArray(transaction.tags) && transaction.tags.length > 0) ||
    transaction.externalId;

  return (
    <AppLayout>
      {/* Navigation Bar */}
      <header className="flex flex-wrap items-center justify-between gap-4 mb-5">
        <TransactionBackButton />

        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            className="flex items-center gap-2 border-red-300 text-red-600 hover:bg-red-50"
            onClick={() => setDeleteConfirm({ show: true, transactionId: transaction.id })}
          >
            <TrashIcon className="w-4 h-4" />
            <span className="hidden sm:inline">{t('deleteTransaction')}</span>
          </Button>
          <EditTransactionButton
            transactionId={transaction.id.toString()}
            onSuccess={loadTransaction}
            variant="secondary"
            size="sm"
          />
        </div>
      </header>

      <div className="space-y-5">
        {/* Hero Section */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-6 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <div className="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-6">
            {/* Left: Transaction identity + amount */}
            <div className="min-w-0 flex-1">
              {/* Description */}
              <h1
                className="font-[var(--font-dash-sans)] text-2xl sm:text-3xl font-semibold tracking-[-0.03em] text-slate-900 line-clamp-2"
                title={transaction.userDescription || transaction.description}
              >
                {transaction.userDescription || transaction.description}
              </h1>

              {/* Badges */}
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <Badge variant={isIncome ? 'default' : 'secondary'}>
                  {isIncome ? tCommon('income') : tCommon('expense')}
                </Badge>
                <Badge variant={getStatusKey(transaction.status) === 'cleared' ? 'default' : 'secondary'}>
                  {t(`status.${getStatusKey(transaction.status)}`)}
                </Badge>
              </div>

              {/* Amount */}
              <div className="mt-6">
                <p
                  className={cn(
                    'font-[var(--font-dash-mono)] text-4xl sm:text-5xl font-semibold tracking-[-0.02em]',
                    isIncome ? 'text-emerald-600' : 'text-red-600',
                  )}
                >
                  {formatCurrency(transaction.amount)}
                </p>
              </div>
            </div>

            {/* Right: Quick stats */}
            <div className="flex items-start gap-5 lg:gap-6">
              {/* Date */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {tCommon('date')}
                </p>
                <div className="mt-1 flex items-center lg:justify-end gap-1.5">
                  <CalendarIcon className="w-4 h-4 text-slate-400" />
                  <p className="font-[var(--font-dash-mono)] text-lg font-semibold text-slate-900">
                    {formatDate(transaction.transactionDate)}
                  </p>
                </div>
              </div>

              <div className="h-12 w-px bg-slate-200 self-center" />

              {/* Account */}
              <div className="text-left lg:text-right">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                  {tCommon('account')}
                </p>
                {transaction.accountName ? (
                  <Link
                    href={`/accounts/${transaction.accountId}`}
                    className="mt-1 flex items-center lg:justify-end gap-1.5 group"
                  >
                    <WalletIcon className="w-4 h-4 text-slate-400 group-hover:text-violet-500 transition-colors" />
                    <p className="font-[var(--font-dash-sans)] text-lg font-semibold text-slate-900 group-hover:text-violet-600 transition-colors">
                      {transaction.accountName}
                    </p>
                  </Link>
                ) : (
                  <p className="mt-1 text-sm font-medium text-slate-400">—</p>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* Details Card */}
        <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
          <h2 className="font-[var(--font-dash-sans)] text-base font-semibold text-slate-900 mb-4">
            {tCommon('details')}
          </h2>
          <div className="space-y-4">
            {/* Category */}
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {tCommon('category')}
              </span>
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
                      <div className="w-4 h-4 border-2 border-violet-500 border-t-transparent rounded-full animate-spin" />
                      <span className="ml-2 text-sm text-slate-500">{t('saving')}</span>
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

            <div className="h-px bg-slate-100" />

            {/* Source */}
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {tCommon('source')}
              </span>
              <Badge variant="outline">
                {t(`source.${getSourceKey(transaction.source)}`)}
              </Badge>
            </div>

            <div className="h-px bg-slate-100" />

            {/* Reviewed Status + Toggle */}
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {tCommon('reviewed')}
              </span>
              <div className="flex items-center gap-2">
                {transaction.isReviewed ? (
                  <Badge variant="default" className="bg-emerald-100 text-emerald-700 border-0">
                    <CheckCircleIcon className="w-3.5 h-3.5 mr-1" />
                    {t('reviewed')}
                  </Badge>
                ) : (
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={handleMarkAsReviewed}
                    disabled={reviewingTransaction}
                    className="flex items-center gap-1.5 text-xs"
                  >
                    {reviewingTransaction ? (
                      <>
                        <div className="w-3.5 h-3.5 border-2 border-violet-500 border-t-transparent rounded-full animate-spin" />
                        {t('reviewing')}
                      </>
                    ) : (
                      <>
                        <ClockIcon className="w-3.5 h-3.5" />
                        {t('markAsReviewed')}
                      </>
                    )}
                  </Button>
                )}
              </div>
            </div>

            <div className="h-px bg-slate-100" />

            {/* Transaction ID */}
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('id')}
              </span>
              <span className="font-[var(--font-dash-mono)] text-sm text-slate-700">
                #{transaction.id}
              </span>
            </div>
          </div>
        </section>

        {/* Additional Information Card */}
        {hasAdditionalInfo && (
          <section className="rounded-[26px] border border-violet-100/60 bg-white/90 p-5 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
            <h2 className="font-[var(--font-dash-sans)] flex items-center gap-2 text-base font-semibold text-slate-900 mb-4">
              <DocumentTextIcon className="w-5 h-5 text-violet-600" />
              {t('additionalInformation')}
            </h2>
            <div className="space-y-4">
              {transaction.description !== transaction.userDescription && (
                <div>
                  <label className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1 block">
                    {t('originalDescription')}
                  </label>
                  <p className="text-sm text-slate-700">{transaction.description}</p>
                </div>
              )}

              {transaction.notes && (
                <div>
                  <label className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1 block">
                    {tCommon('notes')}
                  </label>
                  <p className="text-sm text-slate-700 whitespace-pre-wrap">{transaction.notes}</p>
                </div>
              )}

              {transaction.location && (
                <div>
                  <label className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1 block">
                    {t('location')}
                  </label>
                  <div className="flex items-center gap-2">
                    <MapPinIcon className="w-4 h-4 text-slate-400" />
                    <span className="text-sm text-slate-700">{transaction.location}</span>
                  </div>
                </div>
              )}

              {Array.isArray(transaction.tags) && transaction.tags.length > 0 && (
                <div>
                  <label className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-2 block">
                    {t('tags')}
                  </label>
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
                  <label className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1 block">
                    {t('externalId')}
                  </label>
                  <code className="text-xs bg-slate-100 px-2 py-1 rounded font-[var(--font-dash-mono)] text-slate-600">
                    {transaction.externalId}
                  </code>
                </div>
              )}
            </div>
          </section>
        )}
      </div>

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
    </AppLayout>
  );
}
