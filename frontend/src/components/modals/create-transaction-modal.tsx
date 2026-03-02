'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { CurrencyInput } from '@/components/ui/currency-input';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { CategoryPicker } from '@/components/forms/category-picker';
import { Select } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { 
  XMarkIcon,
  CheckIcon,
  ArrowPathIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
  reference?: string;
}


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

interface CreateTransactionModalProps {
  isOpen: boolean;
  onClose: () => void;
  bankTransaction: BankTransaction;
  accountId: number;
  onTransactionCreated: (transactionId: number) => void;
}

export function CreateTransactionModal({
  isOpen,
  onClose,
  bankTransaction,
  accountId,
  onTransactionCreated
}: CreateTransactionModalProps) {
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [isLoading, setIsLoading] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(false);

  const buildNotes = (transaction: BankTransaction) => {
    if (transaction.reference) {
      return t('createdFromBankTransactionWithRef', { id: transaction.bankTransactionId, reference: transaction.reference });
    }
    return t('createdFromBankTransaction', { id: transaction.bankTransactionId });
  };
  
  const [formData, setFormData] = useState({
    amount: bankTransaction.amount,
    transactionDate: bankTransaction.transactionDate,
    description: bankTransaction.description,
    userDescription: '',
    notes: buildNotes(bankTransaction),
    categoryId: null as number | null,
    status: 2 // Cleared
  });

  useEffect(() => {
    if (isOpen) {
      loadCategories();
      // Reset form with bank transaction data
      setFormData({
        amount: bankTransaction.amount,
        transactionDate: bankTransaction.transactionDate,
        description: bankTransaction.description,
        userDescription: '',
        notes: buildNotes(bankTransaction),
        categoryId: null,
        status: 2
      });
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, bankTransaction]);

  const loadCategories = async () => {
    try {
      setLoadingCategories(true);
      const response = await apiClient.getCategories() as Category[];
      setCategories(response || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      toast.error(tToasts('categoriesLoadFailed'));
    } finally {
      setLoadingCategories(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.description.trim()) {
      toast.error(t('validation.descriptionRequired'));
      return;
    }

    try {
      setIsLoading(true);
      
      const response = await apiClient.request('/api/transactions', {
        method: 'POST',
        body: JSON.stringify({
          amount: formData.amount,
          transactionDate: formData.transactionDate,
          description: formData.description,
          userDescription: formData.userDescription || null,
          notes: formData.notes || null,
          accountId: accountId,
          categoryId: formData.categoryId,
          status: formData.status
        })
      }) as { id: number };

      toast.success(tToasts('transactionCreated'));
      onTransactionCreated(response.id);
      onClose();
    } catch (error: unknown) {
      console.error('Failed to create transaction:', error);
      const errorMessage = error instanceof Error ? error.message : t('createFailed');
      toast.error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-30 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-lg font-semibold">{t('createFromBankImport')}</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
            disabled={isLoading}
          >
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {/* Bank Transaction Reference */}
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
            <div className="text-sm text-blue-800">
              <div className="font-medium">{t('bankTransaction')}</div>
              <div className="text-xs mt-1">
                ID: {bankTransaction.bankTransactionId}
                {bankTransaction.reference && (
                  <span className="ml-2">{t('ref')} {bankTransaction.reference}</span>
                )}
              </div>
            </div>
          </div>

          {/* Amount */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {tCommon('amount')}
            </label>
            <CurrencyInput
              value={formData.amount}
              onChange={(value) => setFormData(prev => ({ ...prev, amount: value }))}
              placeholder={t('form.amountPlaceholder')}
              className="w-full"
              disabled={isLoading}
            />
          </div>

          {/* Date */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {tCommon('date')}
            </label>
            <DateTimePicker
              value={formData.transactionDate}
              onChange={(value) => setFormData(prev => ({ ...prev, transactionDate: value }))}
              className="w-full"
              disabled={isLoading}
              showTime={false}
            />
          </div>

          {/* Description */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {tCommon('description')}
            </label>
            <Input
              type="text"
              value={formData.description}
              onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
              placeholder={t('descriptionPlaceholder')}
              disabled={isLoading}
              required
            />
          </div>

          {/* User Description */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {t('userDescriptionLabel')}
            </label>
            <Input
              type="text"
              value={formData.userDescription}
              onChange={(e) => setFormData(prev => ({ ...prev, userDescription: e.target.value }))}
              placeholder={t('customDescriptionPlaceholder')}
              disabled={isLoading}
            />
          </div>

          {/* Category */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {t('categoryOptional')}
            </label>
            <CategoryPicker
              value={formData.categoryId || ''}
              onChange={(categoryId) => setFormData(prev => ({ ...prev, categoryId: categoryId ? Number(categoryId) : null }))}
              categories={categories}
              placeholder={t('selectCategory')}
              disabled={isLoading || loadingCategories}
              disableQuickPicks={false}
            />
          </div>

          {/* Status */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {tCommon('status')}
            </label>
            <Select
              value={formData.status.toString()}
              onChange={(e) => setFormData(prev => ({ ...prev, status: parseInt(e.target.value) }))}
              disabled={isLoading}
            >
              <option value={1}>{t('status.pending')}</option>
              <option value={2}>{t('status.cleared')}</option>
              <option value={3}>{t('status.reconciled')}</option>
            </Select>
          </div>

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {tCommon('notes')}
            </label>
            <textarea
              value={formData.notes}
              onChange={(e) => setFormData(prev => ({ ...prev, notes: e.target.value }))}
              className="input"
              rows={3}
              placeholder={t('additionalNotes')}
              disabled={isLoading}
            />
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-4">
            <Button
              type="button"
              variant="secondary"
              onClick={onClose}
              disabled={isLoading}
              className="flex-1"
            >
              {tCommon('cancel')}
            </Button>
            <Button
              type="submit"
              disabled={isLoading}
              className="flex-1 flex items-center justify-center gap-2"
            >
              {isLoading ? (
                <>
                  <ArrowPathIcon className="w-4 h-4 animate-spin" />
                  {t('creating')}
                </>
              ) : (
                <>
                  <CheckIcon className="w-4 h-4" />
                  {t('createTransaction')}
                </>
              )}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
