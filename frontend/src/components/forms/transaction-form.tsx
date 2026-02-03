'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { apiClient } from '@/lib/api-client';
import { CategoryPicker } from '@/components/forms/category-picker';
import DescriptionAutocomplete from '@/components/forms/description-autocomplete';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { AiSuggestion } from '@/contexts/ai-suggestions-context';
import { 
  TagIcon,
  BuildingOffice2Icon,
  ExclamationTriangleIcon,
  PlusIcon,
  MinusIcon
} from '@heroicons/react/24/outline';
import AccountCreationModal from '@/components/modals/account-creation-modal';

interface Account {
  id: number;
  name: string;
  currentBalance: number;
  currency: string;
}

interface Category {
  id: number;
  name: string;
  color?: string;
  type: number;
  isSystemCategory?: boolean;
  fullPath?: string;
  parentCategoryId?: number | null;
  parentId: number | null;
  icon?: string;
  isSystem?: boolean;
  children?: Category[];
}

export type TransactionStatus = 'pending' | 'cleared' | 'reconciled' | 'cancelled';

export interface TransactionFormData {
  type: 'income' | 'expense';
  amount: string;
  transactionDate: string;
  description: string;
  userDescription: string;
  status: TransactionStatus;
  accountId: string;
  categoryId: string;
  notes: string;
  location: string;
}

interface TransactionFormProps {
  initialData?: Partial<TransactionFormData>;
  onSubmit: (data: TransactionFormData) => Promise<void>;
  onCancel?: () => void;
  submitText?: string;
  isModal?: boolean;
  transactionId?: number; // For edit mode to fetch AI suggestions
}

export function TransactionForm({ 
  initialData,
  onSubmit, 
  onCancel,
  submitText,
  isModal = false,
  transactionId
}: TransactionFormProps) {
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');
  const [loading, setLoading] = useState(false);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [errors, setErrors] = useState<{ [key: string]: string }>({});
  const [showAccountModal, setShowAccountModal] = useState(false);
  const [aiSuggestions, setAiSuggestions] = useState<AiSuggestion[]>([]);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const submitLabel = submitText ?? t('createTransaction');

  // Form state
  const [formData, setFormData] = useState<TransactionFormData>({
    type: 'expense',
    amount: '',
    transactionDate: new Date().toISOString(),
    description: '',
    userDescription: '',
    status: 'cleared',
    accountId: '',
    categoryId: '',
    notes: '',
    location: '',
    ...initialData
  });

  // Load accounts and categories when component mounts
  useEffect(() => {
    loadAccounts();
    loadCategories();
  }, []);

  // Auto-select first account if none selected
  useEffect(() => {
    if (accounts.length > 0 && !formData.accountId) {
      setFormData(prev => ({
        ...prev,
        accountId: accounts[0].id.toString()
      }));
    }
  }, [accounts, formData.accountId]);

  const loadAccounts = async () => {
    try {
      const accountsData = await apiClient.getAccounts() as Account[];
      setAccounts(accountsData || []);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    }
  };

  const loadCategories = async () => {
    try {
      const categoriesData = await apiClient.getCategories() as Category[];
      const categoriesWithPath = (categoriesData || []).map(cat => ({
        ...cat,
        fullPath: cat.fullPath || cat.name
      }));
      setCategories(categoriesWithPath);
    } catch (error) {
      console.error('Failed to load categories:', error);
      setCategories([]);
    }
  };

  const validateForm = () => {
    const newErrors: { [key: string]: string } = {};

    if (!formData.amount || isNaN(parseFloat(formData.amount))) {
      newErrors.amount = t('validation.amountInvalid');
    } else if (parseFloat(formData.amount) === 0) {
      newErrors.amount = t('validation.amountZero');
    }

    if (!formData.transactionDate) {
      newErrors.transactionDate = t('validation.dateRequired');
    }

    if (!formData.description.trim()) {
      newErrors.description = t('validation.descriptionRequired');
    }

    if (accounts.length > 0 && !formData.accountId) {
      newErrors.accountId = t('validation.accountRequired');
    }


    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  // Note: Category creation is handled by the CategoryAutocomplete component

  const handleKeyDown = (e: React.KeyboardEvent) => {
    // Prevent Enter from submitting form when focused on input fields
    if (e.key === 'Enter' && e.target !== e.currentTarget) {
      e.preventDefault();
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (accounts.length === 0) {
      setErrors({ 
        submit: t('createAccountFirst')
      });
      return;
    }
    
    if (!validateForm()) {
      return;
    }

    try {
      setLoading(true);
      setErrors({});
      await onSubmit(formData);
      
      // Reset form for continuous entry (keep account and date)
      if (isModal) {
        setFormData(prev => ({
          type: prev.type,
          amount: '',
          transactionDate: prev.transactionDate,
          description: '',
          userDescription: '',
          status: prev.status,
          accountId: prev.accountId,
          categoryId: '',
          notes: '',
          location: '',
        }));
        
        // Focus back on amount field
        setTimeout(() => {
          const amountField = document.getElementById('amount') as HTMLInputElement;
          if (amountField) {
            amountField.focus();
            amountField.select();
          }
        }, 100);
      }
    } catch (error: unknown) {
      console.error('Failed to create transaction:', error);
      setErrors({ 
        submit: error instanceof Error ? error.message : t('createFailed') 
      });
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: keyof TransactionFormData, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }
  };

  const handleAccountCreated = (account: Account & { id: number }) => {
    setAccounts(prev => [...prev, account]);
    setFormData(prev => ({ ...prev, accountId: account.id.toString() }));
    setErrors(prev => ({ ...prev, accountId: '', submit: '' }));
  };

  // Fetch AI suggestions for existing transaction (edit mode only)
  const fetchAiSuggestions = async () => {
    // Only fetch suggestions if we have a transaction ID (edit mode)
    if (!transactionId) {
      setAiSuggestions([]);
      return;
    }

    try {
      setLoadingSuggestions(true);
      
      // Get saved categorization candidates for this transaction
      const response = await apiClient.getTransactionSuggestions(transactionId);
      
      if (response.suggestions && response.suggestions.length > 0) {
        const suggestions: AiSuggestion[] = response.suggestions.map(suggestion => ({
          categoryId: suggestion.categoryId,
          categoryName: suggestion.categoryName,
          confidence: suggestion.confidence,
          reasoning: suggestion.reasoning,
          matchingRules: suggestion.matchingRules || [],
          method: suggestion.method || 'Manual'
        }));
        
        setAiSuggestions(suggestions);
      } else {
        setAiSuggestions([]);
      }
    } catch (error) {
      console.error('Failed to fetch AI suggestions:', error);
      setAiSuggestions([]);
    } finally {
      setLoadingSuggestions(false);
    }
  };

  // Load AI suggestions when component mounts (for edit mode)
  useEffect(() => {
    if (transactionId) {
      fetchAiSuggestions();
    }
  }, [transactionId]);

  return (
    <>
      <form onSubmit={handleSubmit} onKeyDown={handleKeyDown} className="space-y-6">
        {/* General Error */}
        {errors.submit && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
            <ExclamationTriangleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
            <div>
              <h4 className="text-sm font-medium text-red-800">{tCommon('error')}</h4>
              <p className="text-sm text-red-700 mt-1">{errors.submit}</p>
            </div>
          </div>
        )}

        {/* Transaction Type Toggle */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-3">
            {t('transactionType')}
          </label>
          <div className="grid grid-cols-2 gap-2 p-1 bg-gray-100 rounded-lg">
            {[
              { type: 'expense' as const, label: t('expense'), icon: MinusIcon, color: 'text-red-600 bg-red-50 border-red-200' },
              { type: 'income' as const, label: t('income'), icon: PlusIcon, color: 'text-green-600 bg-green-50 border-green-200' }
            ].map(({ type, label, icon: Icon, color }) => (
              <button
                key={type}
                type="button"
                onClick={() => handleInputChange('type', type)}
                className={`flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium rounded-md border transition-all ${
                  formData.type === type
                    ? `${color} border-2 shadow-sm`
                    : 'text-gray-600 bg-white border-gray-200 hover:bg-gray-50'
                }`}
              >
                <Icon className="w-4 h-4" />
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* Transaction Status */}
        <div>
          <label htmlFor="status" className="block text-sm font-medium text-gray-700 mb-2">
            {tCommon('status')}
          </label>
          <select
            id="status"
            value={formData.status}
            onChange={(e) => handleInputChange('status', e.target.value)}
            className={`select ${errors.status ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
          >
            <option value="pending">{t('status.pending')}</option>
            <option value="cleared">{t('status.cleared')}</option>
            <option value="reconciled">{t('status.reconciled')}</option>
            <option value="cancelled">{t('status.cancelled')}</option>
          </select>
          {errors.status && (
            <p className="mt-1 text-sm text-red-600">{errors.status}</p>
          )}
          <p className="mt-1 text-xs text-gray-500">
            {t('statusHelp')}
          </p>
        </div>

        {/* Amount Field */}
        <div>
          <label htmlFor="amount" className="block text-sm font-medium text-gray-700 mb-2">
            {t('amountLabel')}
          </label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <span className="text-gray-500 text-lg font-medium">{tCommon('currencySymbol')}</span>
            </div>
            <Input
              id="amount"
              type="number"
              step="0.01"
              placeholder={t('form.amountPlaceholder')}
              value={formData.amount}
              onChange={(e) => handleInputChange('amount', e.target.value)}
              className={`pl-8 text-lg font-medium ${errors.amount ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
              autoFocus={isModal}
            />
          </div>
          {errors.amount && (
            <p className="mt-1 text-sm text-red-600">{errors.amount}</p>
          )}
          <p className="mt-1 text-xs text-gray-500">
            {t('amountHelp')}
          </p>
        </div>

        {/* Date Field */}
        <div>
          <DateTimePicker
            label={t('dateLabel')}
            value={formData.transactionDate}
            onChange={(value) => handleInputChange('transactionDate', value)}
            placeholder={t('selectDate')}
            showTime={false}
            error={errors.transactionDate}
          />
        </div>

        {/* Description Field */}
        <div>
          <label htmlFor="description" className="block text-sm font-medium text-gray-700 mb-2">
            {t('descriptionLabel')}
          </label>
          <DescriptionAutocomplete
            value={formData.description}
            onChange={(value) => handleInputChange('description', value)}
            placeholder={t('descriptionExample')}
            disabled={loading}
            error={errors.description}
          />
        </div>

        {/* User Description Field */}
        <div>
          <label htmlFor="userDescription" className="block text-sm font-medium text-gray-700 mb-2">
            {t('personalNote')}
          </label>
          <Input
            id="userDescription"
            type="text"
            placeholder={t('notesExample')}
            value={formData.userDescription}
            onChange={(e) => handleInputChange('userDescription', e.target.value)}
          />
          <p className="mt-1 text-xs text-gray-500">
            {t('personalNoteHelp')}
          </p>
        </div>

        {/* Account Selection */}
        <div>
          <label htmlFor="accountId" className="block text-sm font-medium text-gray-700 mb-2">
            <BuildingOffice2Icon className="w-4 h-4 inline mr-1" />
            {tCommon('account')} {accounts.length > 0 ? '*' : ''}
          </label>
          {accounts.length > 0 ? (
            <select
              id="accountId"
              value={formData.accountId}
              onChange={(e) => handleInputChange('accountId', e.target.value)}
              className={`select ${errors.accountId ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
            >
              <option value="">{t('selectAccount')}</option>
              {accounts.map((account) => (
                <option key={account.id} value={account.id}>
                  {account.name} ({account.currency})
                </option>
              ))}
            </select>
          ) : (
            <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
              <div className="flex items-start gap-3">
                <ExclamationTriangleIcon className="w-5 h-5 text-yellow-500 flex-shrink-0 mt-0.5" />
                <div>
                  <h4 className="text-sm font-medium text-yellow-800">{t('noAccountsAvailable')}</h4>
                  <p className="text-sm text-yellow-700 mt-1">
                    {t('createAccountFirst')}
                  </p>
                  <div className="mt-3">
                    <Button
                      type="button"
                      variant="secondary"
                      size="sm"
                      onClick={() => setShowAccountModal(true)}
                      className="text-yellow-800 border-yellow-300 hover:bg-yellow-100"
                    >
                      {t('createAccount')}
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          )}
          {errors.accountId && (
            <p className="mt-1 text-sm text-red-600">{errors.accountId}</p>
          )}
        </div>


        {/* Category Selection */}
        <div>
          <label htmlFor="categoryId" className="block text-sm font-medium text-gray-700 mb-2">
            <TagIcon className="w-4 h-4 inline mr-1" />
            {tCommon('category')}
          </label>
          
          <CategoryPicker
            categories={categories}
            value={formData.categoryId}
            onChange={(categoryId) => handleInputChange('categoryId', categoryId.toString())}
            placeholder={t('selectCategory')}
            disabled={loading}
            error={!!errors.categoryId}
            aiSuggestions={aiSuggestions}
            isLoadingAiSuggestions={loadingSuggestions}
          />
          {errors.categoryId && (
            <p className="mt-1 text-sm text-red-600">{errors.categoryId}</p>
          )}
        </div>

        {/* Location Field */}
        <div>
          <label htmlFor="location" className="block text-sm font-medium text-gray-700 mb-2">
            {tCommon('location')}
          </label>
          <Input
            id="location"
            type="text"
            placeholder={t('merchantExample')}
            value={formData.location}
            onChange={(e) => handleInputChange('location', e.target.value)}
          />
        </div>

        {/* Notes Field */}
        <div>
          <label htmlFor="notes" className="block text-sm font-medium text-gray-700 mb-2">
            {tCommon('notes')}
          </label>
          <textarea
            id="notes"
            rows={3}
            placeholder={t('notesPlaceholder')}
            value={formData.notes}
            onChange={(e) => handleInputChange('notes', e.target.value)}
            className="w-full px-4 py-3 text-sm border border-gray-300 rounded-md bg-white placeholder-gray-400 focus:border-primary focus:ring-2 focus:ring-primary/20 resize-none"
          />
        </div>

        {/* Submit Buttons */}
        <div className="flex flex-col sm:flex-row gap-3 pt-4 pb-4 md:pb-0">
          {onCancel && (
            <Button
              type="button"
              variant="secondary"
              className="flex-1"
              onClick={onCancel}
              disabled={loading}
            >
              {tCommon('cancel')}
            </Button>
          )}

          <Button
            type="submit"
            variant="primary"
            className="flex-1 sm:flex-2"
            disabled={loading}
          >
            {loading ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                {t('creating')}
              </div>
            ) : (
              submitLabel
            )}
          </Button>
        </div>
      </form>

      {/* Account Creation Modal */}
      <AccountCreationModal
        isOpen={showAccountModal}
        onClose={() => setShowAccountModal(false)}
        onAccountCreated={handleAccountCreated}
      />
    </>
  );
}
