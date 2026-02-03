'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  BuildingOffice2Icon,
  CurrencyDollarIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import {
  AccountType,
  convertFrontendToBackendAccountType,
  convertBackendToFrontendAccountType
} from '@/lib/utils';
import { useTranslations } from 'next-intl';

export interface Account {
  id?: number;
  name: string;
  type: number; // Backend enum value (1-based)
  institution?: string;
  currentBalance: number;
  currency: string;
  notes?: string;
}

interface AccountFormProps {
  /** Initial account data for editing */
  initialData?: Partial<Account>;
  /** Whether this is a quick/minimal form (for modal) or full form */
  variant?: 'minimal' | 'full';
  /** Called when form is submitted successfully */
  onSubmit: (data: Omit<Account, 'id'>) => Promise<void>;
  /** Called when form is cancelled */
  onCancel?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Submit button text */
  submitText?: string;
  /** Show cancel button */
  showCancel?: boolean;
  /** Whether this account has transactions (affects editing restrictions) */
  hasTransactions?: boolean;
}

const accountTypeKeys: { value: number; key: string }[] = [
  { value: AccountType.Checking, key: 'checking' },
  { value: AccountType.Savings, key: 'savings' },
  { value: AccountType.CreditCard, key: 'creditCard' },
  { value: AccountType.Investment, key: 'investment' },
  { value: AccountType.Loan, key: 'loan' },
  { value: AccountType.Cash, key: 'cash' },
];

const currencyKeys = ['USD', 'EUR', 'GBP', 'CAD', 'AUD', 'NZD', 'BRL'];

export default function AccountForm({
  initialData = {},
  variant = 'full',
  onSubmit,
  onCancel,
  loading = false,
  submitText = 'Create Account',
  showCancel = true,
  hasTransactions = false
}: AccountFormProps) {
  const t = useTranslations('accounts.form');
  const tTypes = useTranslations('accounts.types');
  const tCurrencies = useTranslations('accounts.currencies');
  const tCommon = useTranslations('common');

  const [formData, setFormData] = useState({
    name: initialData.name || '',
    type: initialData.type !== undefined ? convertBackendToFrontendAccountType(initialData.type) : AccountType.Checking,
    institution: initialData.institution || '',
    currentBalance: initialData.currentBalance?.toString() || '0',
    currency: initialData.currency || 'USD',
    notes: initialData.notes || '',
  });

  const [errors, setErrors] = useState<{ [key: string]: string }>({});

  const validateForm = () => {
    const newErrors: { [key: string]: string } = {};

    if (!formData.name.trim()) {
      newErrors.name = t('nameRequired');
    }

    // Only validate balance for new accounts (not editing)
    if (!isEditing && isNaN(parseFloat(formData.currentBalance))) {
      newErrors.currentBalance = t('invalidBalance');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) {
      return;
    }

    try {
      const submitData: Omit<Account, 'id'> = {
        name: formData.name.trim(),
        type: convertFrontendToBackendAccountType(formData.type), // Convert to backend enum
        institution: formData.institution.trim() || undefined,
        currency: formData.currency,
        notes: formData.notes.trim() || undefined,
        // Only include balance for new accounts, preserve existing balance when editing
        currentBalance: isEditing ? (initialData.currentBalance || 0) : parseFloat(formData.currentBalance),
      };
      
      await onSubmit(submitData);
    } catch (error) {
      console.error('Failed to submit account form:', error);
      setErrors({
        submit: error instanceof Error ? error.message : t('saveFailed')
      });
    }
  };

  const handleInputChange = (field: string, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    
    // Clear error when user starts typing
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: '' }));
    }
  };

  const isMinimal = variant === 'minimal';
  const isEditing = !!initialData.name; // We're editing if initial data has a name

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {/* General Error */}
      {errors.submit && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
          <ExclamationTriangleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
          <div>
            <h4 className="text-sm font-medium text-red-800">{t('error')}</h4>
            <p className="text-sm text-red-700 mt-1">{errors.submit}</p>
          </div>
        </div>
      )}

      {/* Account Name */}
      <div>
        <label htmlFor="name" className="block text-sm font-medium text-gray-700 mb-2">
          <BuildingOffice2Icon className="w-4 h-4 inline mr-1" />
          {t('name')} *
        </label>
        <Input
          id="name"
          type="text"
          placeholder={t('namePlaceholder')}
          value={formData.name}
          onChange={(e) => handleInputChange('name', e.target.value)}
          className={errors.name ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}
        />
        {errors.name && (
          <p className="mt-1 text-sm text-red-600">{errors.name}</p>
        )}
      </div>

      {/* Account Type */}
      <div>
        <label htmlFor="type" className="block text-sm font-medium text-gray-700 mb-2">
          {t('type')} *
        </label>
        <select
          id="type"
          value={formData.type}
          onChange={(e) => handleInputChange('type', parseInt(e.target.value))}
          className="select"
        >
          {accountTypeKeys.map((option) => (
            <option key={option.value} value={option.value}>
              {tTypes(option.key)}
            </option>
          ))}
        </select>
      </div>

      {/* Current Balance - Only show for new accounts */}
      {!isEditing && (
        <div>
          <label htmlFor="currentBalance" className="block text-sm font-medium text-gray-700 mb-2">
            <CurrencyDollarIcon className="w-4 h-4 inline mr-1" />
            {t('initialBalance')} *
          </label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <span className="text-gray-500 text-sm">$</span>
            </div>
            <Input
              id="currentBalance"
              type="number"
              step="0.01"
              placeholder="0.00"
              value={formData.currentBalance}
              onChange={(e) => handleInputChange('currentBalance', e.target.value)}
              className={`pl-8 ${errors.currentBalance ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
            />
          </div>
          {errors.currentBalance && (
            <p className="mt-1 text-sm text-red-600">{errors.currentBalance}</p>
          )}
          <p className="mt-1 text-xs text-gray-500">
            {t('initialBalanceHelp')}
          </p>
        </div>
      )}
      
      {/* Balance Information for Editing */}
      {isEditing && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <div className="flex items-start gap-3">
            <CurrencyDollarIcon className="w-5 h-5 text-blue-500 flex-shrink-0 mt-0.5" />
            <div>
              <h4 className="text-sm font-medium text-blue-800">{t('accountBalanceTitle')}</h4>
              <p className="text-sm text-blue-700 mt-1">
                {t('accountBalanceDesc')}
              </p>
              <p className="text-xs text-blue-600 mt-2">
                {t('currentBalanceDisplay', { balance: `$${initialData.currentBalance?.toLocaleString() || '0.00'}` })}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Currency */}
      <div>
        <label htmlFor="currency" className="block text-sm font-medium text-gray-700 mb-2">
          {t('currency')} *
        </label>
        <select
          id="currency"
          value={formData.currency}
          onChange={(e) => handleInputChange('currency', e.target.value)}
          className="select"
        >
          {currencyKeys.map((code) => (
            <option key={code} value={code}>
              {tCurrencies(code)}
            </option>
          ))}
        </select>
        {isEditing && hasTransactions && (
          <div className="mt-2 bg-yellow-50 border border-yellow-200 rounded p-3">
            <div className="flex items-start gap-2">
              <ExclamationTriangleIcon className="w-4 h-4 text-yellow-600 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-xs text-yellow-700 font-medium">
                  {t('currencyChangeNotice')}
                </p>
                <p className="text-xs text-yellow-600 mt-1">
                  {t('currencyChangeNoticeDesc')}
                </p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Additional fields for full form */}
      {!isMinimal && (
        <>
          {/* Institution */}
          <div>
            <label htmlFor="institution" className="block text-sm font-medium text-gray-700 mb-2">
              {t('institution')}
            </label>
            <Input
              id="institution"
              type="text"
              placeholder={t('institutionPlaceholder')}
              value={formData.institution}
              onChange={(e) => handleInputChange('institution', e.target.value)}
            />
          </div>

          {/* Notes */}
          <div>
            <label htmlFor="notes" className="block text-sm font-medium text-gray-700 mb-2">
              {t('notes')}
            </label>
            <textarea
              id="notes"
              rows={3}
              placeholder={t('notesPlaceholder')}
              value={formData.notes}
              onChange={(e) => handleInputChange('notes', e.target.value)}
              className="w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 resize-none cursor-text"
            />
          </div>
        </>
      )}

      {/* Action Buttons */}
      <div className={`flex ${isMinimal ? 'flex-col' : 'flex-col sm:flex-row'} gap-3 pt-4 pb-4 md:pb-0`}>
        {showCancel && onCancel && (
          <Button
            type="button"
            variant="secondary"
            className={isMinimal ? 'w-full' : 'flex-1'}
            disabled={loading}
            onClick={onCancel}
          >
            {tCommon('cancel')}
          </Button>
        )}
        
        <Button
          type="submit"
          className={isMinimal ? 'w-full' : 'flex-1 sm:flex-2'}
          disabled={loading}
        >
          {loading ? (
            <div className="flex items-center gap-2">
              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
              {t('saving')}
            </div>
          ) : (
            submitText
          )}
        </Button>
      </div>
    </form>
  );
}