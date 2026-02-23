'use client';

import { useAuth } from '@/contexts/auth-context';
import { useTranslations } from 'next-intl';
import { useRouter, useParams, useSearchParams } from 'next/navigation';
import { getReturnUrl } from '@/lib/navigation-utils';
import { useEffect, useState, useCallback } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { TransactionForm, TransactionStatus } from '@/components/forms/transaction-form';
import { apiClient } from '@/lib/api-client';
import { TransactionBackButton } from '@/components/ui/smart-back-button';
import { 
  PencilIcon,
  CheckIcon,
  ExclamationTriangleIcon,
  ArrowsRightLeftIcon
} from '@heroicons/react/24/outline';
import { toast } from 'sonner';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  userDescription?: string;
  accountId: number;
  categoryId?: number;
  notes?: string;
  location?: string;
  tags?: string[];
  status: string | number;
  // Transfer-related properties
  transferId?: string;
  isTransferSource?: boolean;
  relatedTransactionId?: number;
  relatedAccountName?: string;
  type?: number; // TransactionType from backend
}

// Utility function to map enum values to display names
const getTransactionStatus = (status: string | number): TransactionStatus => {
  if (typeof status === 'string') return status.toLowerCase() as TransactionStatus;
  
  switch (status) {
    case 1: return 'pending';
    case 2: return 'cleared';
    case 3: return 'reconciled';
    case 4: return 'cancelled';
    default: return 'cleared';
  }
};

// Helper function to determine if transaction is a transfer
const isTransfer = (transaction: Transaction): boolean => {
  return !!(transaction.transferId || transaction.type === 3); // TransactionType.TransferComponent = 3
};

// Helper function to get transaction type for form
const getTransactionType = (transaction: Transaction): 'income' | 'expense' => {
  return transaction.amount > 0 ? 'income' : 'expense';
};

export default function EditTransactionPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const searchParams = useSearchParams();
  const transactionId = params?.id as string;
  const returnUrl = getReturnUrl(searchParams);
  
  const t = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const [loading, setLoading] = useState(false);
  const [loadingTransaction, setLoadingTransaction] = useState(true);
  const [success, setSuccess] = useState(false);
  const [transaction, setTransaction] = useState<Transaction | null>(null);
  const [error, setError] = useState<string | null>(null);

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
      
      // Note: Transfer transactions are edited without modifying their transfer relationship
    } catch (err) {
      console.error('Failed to load transaction:', err);
      setError(t('failedToLoadTransaction'));
    } finally {
      setLoadingTransaction(false);
    }
  }, [transactionId]);

  useEffect(() => {
    if (isAuthenticated && transactionId) {
      loadTransaction();
    }
  }, [isAuthenticated, transactionId, loadTransaction]);

  const handleSubmit = async (formData: {
    amount: string;
    type: 'expense' | 'income' | 'transfer';
    transactionDate: string;
    description: string;
    userDescription?: string;
    categoryId?: string;
    notes?: string;
    location?: string;
    tags?: string;
    status?: string;
  }) => {
    try {
      setLoading(true);
      setError(null);

      // Check if the current transaction is a transfer
      const isCurrentTransfer = transaction && isTransfer(transaction);

      // For transfer transactions, preserve the original sign to maintain transfer integrity
      const amount = parseFloat(formData.amount);
      let finalAmount: number;
      
      if (isCurrentTransfer && transaction) {
        // Keep the original sign: negative for source, positive for destination
        finalAmount = transaction.amount < 0 ? -amount : amount;
      } else {
        // For regular transactions
        finalAmount = formData.type === 'expense' ? -amount : amount;
      }

      // Map status to enum values
      const statusMap: Record<string, number> = {
        'pending': 1,
        'cleared': 2, 
        'reconciled': 3,
        'cancelled': 4
      };

      const updateData = {
        id: parseInt(transactionId),
        amount: finalAmount,
        transactionDate: formData.transactionDate,
        description: formData.description,
        userDescription: formData.userDescription || undefined,
        // Don't allow category changes for transfer transactions
        categoryId: isCurrentTransfer ? undefined : (formData.categoryId ? parseInt(formData.categoryId) : undefined),
        notes: formData.notes || undefined,
        location: formData.location || undefined,
        tags: formData.tags ? formData.tags.split(',').map((tag: string) => tag.trim()).filter(Boolean) : undefined,
        status: statusMap[formData.status || 'cleared'] || 2, // Default to Cleared (2)
      };

      await apiClient.updateTransaction(parseInt(transactionId), updateData);
      
      setSuccess(true);
      const transferNote = isCurrentTransfer ? ` ${t('transferUpdateNote')}` : '';
      toast.success(`${tToasts('transactionUpdated')}${transferNote}`, { duration: 4000 });
      
      // Redirect after a short delay - preserve filter context
      setTimeout(() => {
        router.push(returnUrl);
      }, 1000);
    } catch (err) {
      console.error('Failed to update transaction:', err);
      setError(t('updateFailed'));
      toast.error(tToasts('transactionUpdateFailed'), { duration: 4000 });
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    router.push(returnUrl);
  };

  if (isLoading || loadingTransaction) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <PencilIcon className="w-8 h-8 text-white" />
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
      <AppLayout>
        <Card className="max-w-2xl mx-auto bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardContent className="p-8 text-center">
            <ExclamationTriangleIcon className="w-16 h-16 text-danger-500 mx-auto mb-4" />
            <h2 className="text-xl font-semibold text-gray-900 mb-2">{t('notFound')}</h2>
            <p className="text-gray-600 mb-6">{error}</p>
            <TransactionBackButton />
          </CardContent>
        </Card>
      </AppLayout>
    );
  }

  const initialData = transaction ? {
    type: getTransactionType(transaction),
    amount: Math.abs(transaction.amount).toString(),
    transactionDate: transaction.transactionDate.split('T')[0], // Convert to YYYY-MM-DD format
    description: transaction.description,
    userDescription: transaction.userDescription || '',
    accountId: transaction.accountId.toString(),
    // Don't include categoryId for transfer transactions
    categoryId: isTransfer(transaction) ? '' : (transaction.categoryId?.toString() || ''),
    notes: transaction.notes || '',
    location: transaction.location || '',
    tags: transaction.tags?.join(', ') || '',
    status: getTransactionStatus(transaction.status),
  } : undefined;

  return (
    <AppLayout>
      {/* Header */}
      <div className="mb-6 lg:mb-8">
        {/* Navigation Bar */}
        <div className="flex items-center justify-between mb-6">
          <TransactionBackButton size="sm" />
        </div>

        {/* Page Title */}
        <div className="text-center mb-8">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
            {t('editTransaction')}
          </h1>
          <p className="text-gray-600 text-sm sm:text-base">
            {t('editTransactionDesc')}
          </p>
        </div>
      </div>

      {/* Success Message */}
      {success && (
        <Card className="mb-6 bg-success-50 border-success-200 shadow-lg">
          <CardContent className="p-4">
            <div className="flex items-center gap-2">
              <CheckIcon className="w-5 h-5 text-success-600" />
              <span className="text-success-800 font-medium">{t('transactionUpdated')}</span>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Error Message */}
      {error && (
        <Card className="mb-6 bg-danger-50 border-danger-200 shadow-lg">
          <CardContent className="p-4">
            <div className="flex items-center gap-2">
              <ExclamationTriangleIcon className="w-5 h-5 text-danger-600" />
              <span className="text-danger-800 font-medium">{error}</span>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Transfer Warning */}
      {transaction && isTransfer(transaction) && (
        <Card className="mb-6 bg-blue-50 border-blue-200 shadow-lg">
          <CardContent className="p-4">
            <div className="flex items-center gap-2">
              <ArrowsRightLeftIcon className="w-5 h-5 text-blue-600" />
              <div>
                <span className="text-blue-800 font-medium">{t('transferTransaction')}</span>
                <p className="text-blue-700 text-sm mt-1">
                  {t('transferInfo')}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Edit Form */}
      <Card className="max-w-4xl mx-auto bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardHeader>
          <CardTitle className="text-xl font-bold text-gray-900 flex items-center gap-2">
            <PencilIcon className="w-5 h-5" />
            {t('transactionDetails')}
            {transaction && isTransfer(transaction) && (
              <ArrowsRightLeftIcon className="w-5 h-5 text-blue-500" />
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {transaction && (
            <TransactionForm
              initialData={initialData}
              onSubmit={handleSubmit}
              onCancel={handleCancel}
              submitText={loading ? t('updating') : t('updateTransaction')}
            />
          )}
        </CardContent>
      </Card>
    </AppLayout>
  );
}
