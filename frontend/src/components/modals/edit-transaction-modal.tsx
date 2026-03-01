'use client';

import { useState, useEffect, useCallback } from 'react';
import { BaseModal } from './base-modal';
import { TransactionForm, TransactionFormData, TransactionStatus } from '@/components/forms/transaction-form';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { formatCurrency } from '@/lib/utils';

interface EditTransactionModalProps {
  isOpen: boolean;
  onClose: () => void;
  transactionId: string;
  onSuccess?: () => void;
}

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
  source: string | number;
  status: string | number;
  externalId?: string;
}

export function EditTransactionModal({
  isOpen,
  onClose,
  transactionId,
  onSuccess
}: EditTransactionModalProps) {
  const t = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const [transaction, setTransaction] = useState<Transaction | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const loadTransaction = useCallback(async () => {
    try {
      setIsLoading(true);
      const response = await apiClient.getTransaction(parseInt(transactionId));
      setTransaction(response as Transaction);
    } catch (error) {
      console.error('Error loading transaction:', error);
      toast.error(t('failedToLoadTransaction'));
      onClose();
    } finally {
      setIsLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [transactionId, onClose]);

  // Load transaction data when modal opens
  useEffect(() => {
    if (isOpen && transactionId) {
      loadTransaction();
    }
  }, [isOpen, transactionId, loadTransaction]);

  const handleSubmit = async (formData: TransactionFormData) => {
    try {
      // Handle regular transaction update
      const amount = parseFloat(formData.amount);
      const finalAmount = formData.type === 'expense' ? -amount : amount;
      
      // Map status to enum values
      const statusMap: Record<string, number> = {
        'pending': 1,
        'cleared': 2, 
        'reconciled': 3,
        'cancelled': 4
      };
      
      const transactionData = {
        id: parseInt(transactionId),
        amount: finalAmount,
        transactionDate: new Date(formData.transactionDate).toISOString(),
        description: formData.description.trim(),
        userDescription: formData.userDescription.trim() || undefined,
        categoryId: formData.categoryId ? parseInt(formData.categoryId) : undefined,
        notes: formData.notes.trim() || undefined,
        location: formData.location.trim() || undefined,
        status: statusMap[formData.status] || 2, // Default to Cleared (2)
      };

      await apiClient.updateTransaction(parseInt(transactionId), transactionData);
      
      toast.success(tToasts('transactionUpdatedWithAmount', {
        amount: formatCurrency(Math.abs(finalAmount)),
        description: formData.description
      }), {
        duration: 4000,
      });

      if (onSuccess) {
        onSuccess();
      }
    } catch (error) {
      console.error('Error updating transaction:', error);
      toast.error(tToasts('transactionUpdateFailed'));
    }
  };

  // Convert transaction to form data
  const getInitialFormData = (): Partial<TransactionFormData> | undefined => {
    if (!transaction) return undefined;

    const isIncome = transaction.amount > 0;
    
    return {
      amount: Math.abs(transaction.amount).toString(),
      transactionDate: transaction.transactionDate, // Keep full ISO format for DateTimePicker
      description: transaction.description,
      userDescription: transaction.userDescription || '',
      accountId: transaction.accountId.toString(),
      categoryId: transaction.categoryId?.toString() || '',
      notes: transaction.notes || '',
      location: transaction.location || '',
      type: isIncome ? 'income' : 'expense',
      status: (typeof transaction.status === 'string' ? transaction.status : 'cleared') as TransactionStatus,
    };
  };

  return (
    <BaseModal
      isOpen={isOpen}
      onClose={onClose}
      title={t('editTransaction')}
      size="lg"
    >
      {isLoading ? (
        <div className="flex items-center justify-center py-8">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
        </div>
      ) : transaction ? (
        <TransactionForm
          initialData={getInitialFormData()}
          onSubmit={handleSubmit}
          onCancel={onClose}
          submitText={t('updateTransaction')}
          isModal={false} // No continuous entry for editing
          transactionId={parseInt(transactionId)}
        />
      ) : null}
    </BaseModal>
  );
}
