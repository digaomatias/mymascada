'use client';

import { useTranslations } from 'next-intl';
import { BaseModal } from './base-modal';
import { TransactionForm, TransactionFormData } from '@/components/forms/transaction-form';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';

interface AddTransactionModalProps {
  isOpen: boolean;
  onClose: () => void;
  accountId?: string;
  onSuccess?: () => void;
}

export function AddTransactionModal({ 
  isOpen, 
  onClose, 
  accountId,
  onSuccess 
}: AddTransactionModalProps) {
  const t = useTranslations('transactions');
  const handleSubmit = async (formData: TransactionFormData) => {
    // Handle regular transaction (income/expense) - transfer type no longer available
    const amount = parseFloat(formData.amount);
    const finalAmount = formData.type === 'expense' ? -amount : amount;
    
    // Map status to enum values
    const statusMap: Record<string, number> = {
      'pending': 1,
      'cleared': 2, 
      'reconciled': 3,
      'cancelled': 4
    };

    if (!formData.accountId) {
      throw new Error('Account ID is required');
    }

    const transactionData = {
      amount: finalAmount,
      transactionDate: new Date(formData.transactionDate).toISOString(),
      description: formData.description.trim(),
      userDescription: formData.userDescription.trim() || undefined,
      accountId: parseInt(formData.accountId), // Required field
      categoryId: formData.categoryId ? parseInt(formData.categoryId) : undefined,
      notes: formData.notes.trim() || undefined,
      location: formData.location.trim() || undefined,
      status: statusMap[formData.status] || 2, // Default to Cleared (2)
    };

    await apiClient.createTransaction(transactionData);
    
    toast.success(`${formData.type === 'income' ? 'Income' : 'Expense'} added: $${amount.toFixed(2)} - ${formData.description}`, {
      duration: 4000,
    });

    if (onSuccess) {
      onSuccess();
    }
    
    // Don't close modal - form will reset internally for continuous entry
  };

  return (
    <BaseModal
      isOpen={isOpen}
      onClose={onClose}
      title={t('addTransaction')}
      size="lg"
    >
      <TransactionForm
        initialData={accountId ? { accountId } : undefined}
        onSubmit={handleSubmit}
        onCancel={onClose}
        isModal={true}
      />
    </BaseModal>
  );
}