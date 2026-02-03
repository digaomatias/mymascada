'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  CurrencyDollarIcon,
  ExclamationTriangleIcon,
  CheckIcon,
  CalculatorIcon,
  ArrowRightIcon
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface BalanceAdjustmentProps {
  currentBalance: number;
  currency: string;
  accountId: string;
  onAdjustmentComplete?: () => void;
}

export function BalanceAdjustment({ 
  currentBalance, 
  accountId,
  onAdjustmentComplete 
}: BalanceAdjustmentProps) {
  const t = useTranslations('accounts.balanceAdjustment');
  const tCommon = useTranslations('common');
  const [verifiedBalance, setVerifiedBalance] = useState('');
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState<{ [key: string]: string }>({});

  // Calculate the adjustment amount
  const adjustmentAmount = verifiedBalance ? 
    parseFloat(verifiedBalance) - currentBalance : 0;
  
  const hasAdjustment = Math.abs(adjustmentAmount) > 0.01;
  const isLargeAdjustment = Math.abs(adjustmentAmount) > Math.abs(currentBalance) * 0.1;

  const validateInput = (): boolean => {
    const newErrors: { [key: string]: string } = {};

    if (!verifiedBalance.trim()) {
      newErrors.verifiedBalance = t('enterVerifiedBalance');
      setErrors(newErrors);
      return false;
    }

    const balance = parseFloat(verifiedBalance);
    if (isNaN(balance)) {
      newErrors.verifiedBalance = t('enterValidNumber');
      setErrors(newErrors);
      return false;
    }

    setErrors({});
    return true;
  };

  const handlePreviewAdjustment = () => {
    if (validateInput()) {
      setShowConfirmation(true);
    }
  };

  const handleApplyAdjustment = async () => {
    try {
      setLoading(true);
      
      const adjustmentData = {
        accountId: parseInt(accountId),
        amount: adjustmentAmount,
        description: `Balance adjustment - Reconciled to ${formatCurrency(parseFloat(verifiedBalance))}`,
        notes: `Adjustment to match verified balance. Previous balance: ${formatCurrency(currentBalance)}, Verified balance: ${formatCurrency(parseFloat(verifiedBalance))}`
      };

      await apiClient.createAdjustmentTransaction(adjustmentData);
      
      // Show success message
      toast.success(
        t('adjustmentSuccess', { amount: `${adjustmentAmount >= 0 ? '+' : ''}${formatCurrency(adjustmentAmount)}` }),
        { duration: 4000 }
      );
      
      // Reset form
      setVerifiedBalance('');
      setShowConfirmation(false);
      
      if (onAdjustmentComplete) {
        onAdjustmentComplete();
      }
      
    } catch (error) {
      console.error('Failed to create adjustment transaction:', error);
      setErrors({ submit: t('adjustmentFailed') });
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    setShowConfirmation(false);
    setErrors({});
  };

  if (showConfirmation) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-6">
        <div className="flex items-start gap-3 mb-4">
          <div className="w-8 h-8 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded-lg flex items-center justify-center">
            <ExclamationTriangleIcon className="w-5 h-5 text-white" />
          </div>
          <div>
            <h4 className="text-lg font-semibold text-gray-900">{t('confirmTitle')}</h4>
            <p className="text-sm text-gray-600 mt-1">
              {t('confirmDesc')}
            </p>
          </div>
        </div>

        {/* Adjustment Summary */}
        <div className="bg-gray-50 rounded-lg p-4 mb-4">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="text-center">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{t('currentBalance')}</p>
              <p className="text-lg font-bold text-gray-900">{formatCurrency(currentBalance)}</p>
            </div>
            
            <div className="flex items-center justify-center">
              <ArrowRightIcon className="w-5 h-5 text-gray-400" />
            </div>
            
            <div className="text-center">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{t('verifiedBalance')}</p>
              <p className="text-lg font-bold text-gray-900">{formatCurrency(parseFloat(verifiedBalance))}</p>
            </div>
          </div>
          
          <div className="mt-4 pt-4 border-t border-gray-200 text-center">
            <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{t('adjustmentAmount')}</p>
            <p className={`text-xl font-bold ${adjustmentAmount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {adjustmentAmount >= 0 ? '+' : ''}{formatCurrency(adjustmentAmount)}
            </p>
          </div>
        </div>

        {/* Large Adjustment Warning */}
        {isLargeAdjustment && (
          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 mb-4">
            <div className="flex items-start gap-3">
              <ExclamationTriangleIcon className="w-5 h-5 text-yellow-500 flex-shrink-0 mt-0.5" />
              <div>
                <h5 className="text-sm font-medium text-yellow-800">{t('largeAdjustmentTitle')}</h5>
                <p className="text-sm text-yellow-700 mt-1">
                  {t('largeAdjustmentDesc')}
                </p>
              </div>
            </div>
          </div>
        )}

        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-6">
          <p className="text-sm text-blue-700">
            {t('auditTrailInfo')}
          </p>
        </div>

        {/* Error Display */}
        {errors.submit && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4 flex items-start gap-3">
            <ExclamationTriangleIcon className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
            <div>
              <h4 className="text-sm font-medium text-red-800">{t('error')}</h4>
              <p className="text-sm text-red-700 mt-1">{errors.submit}</p>
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex flex-col sm:flex-row gap-3">
          <Button
            type="button"
            variant="secondary"
            className="flex-1"
            onClick={handleCancel}
            disabled={loading}
          >
            {tCommon('cancel')}
          </Button>

          <Button
            type="button"
            className="flex-1 bg-primary-500 hover:bg-primary-600"
            onClick={handleApplyAdjustment}
            disabled={loading}
          >
            {loading ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                {t('creatingAdjustment')}
              </div>
            ) : (
              <>
                <CheckIcon className="w-4 h-4 mr-2" />
                {t('applyAdjustment')}
              </>
            )}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6">
      <div className="flex items-start gap-3 mb-4">
        <div className="w-8 h-8 bg-gradient-to-br from-primary-400 to-primary-600 rounded-lg flex items-center justify-center">
          <CalculatorIcon className="w-5 h-5 text-white" />
        </div>
        <div>
          <h4 className="text-lg font-semibold text-gray-900">{t('title')}</h4>
          <p className="text-sm text-gray-600 mt-1">
            {t('description')}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
        <div className="bg-gray-50 rounded-lg p-4">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">{t('currentCalculatedBalance')}</p>
          <p className="text-xl font-bold text-gray-900">{formatCurrency(currentBalance)}</p>
          <p className="text-xs text-gray-500 mt-1">{t('basedOnTransactions')}</p>
        </div>
        
        <div>
          <label htmlFor="verifiedBalance" className="block text-sm font-medium text-gray-700 mb-2">
            <CurrencyDollarIcon className="w-4 h-4 inline mr-1" />
            {t('verifiedBalanceLabel')} *
          </label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <span className="text-gray-500 text-sm">$</span>
            </div>
            <Input
              id="verifiedBalance"
              type="number"
              step="0.01"
              placeholder="0.00"
              value={verifiedBalance}
              onChange={(e) => {
                setVerifiedBalance(e.target.value);
                if (errors.verifiedBalance) {
                  setErrors(prev => ({ ...prev, verifiedBalance: '' }));
                }
              }}
              className={`pl-8 ${errors.verifiedBalance ? 'border-red-300 focus:border-red-500 focus:ring-red-500' : ''}`}
            />
          </div>
          {errors.verifiedBalance && (
            <p className="mt-1 text-sm text-red-600">{errors.verifiedBalance}</p>
          )}
        </div>
      </div>

      {/* Adjustment Preview */}
      {verifiedBalance && !isNaN(parseFloat(verifiedBalance)) && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-blue-800">
                {hasAdjustment ? t('adjustmentRequired') : t('balancesMatch')}
              </p>
              {hasAdjustment ? (
                <p className="text-sm text-blue-700">
                  {adjustmentAmount > 0
                    ? t('addToAccount', { amount: formatCurrency(Math.abs(adjustmentAmount)) })
                    : t('subtractFromAccount', { amount: formatCurrency(Math.abs(adjustmentAmount)) })}
                </p>
              ) : (
                <p className="text-sm text-blue-700">
                  {t('balancesMatchDesc')}
                </p>
              )}
            </div>
            
            <div className="text-right">
              <p className="text-sm text-blue-600">{t('adjustment')}</p>
              <p className={`text-lg font-bold ${adjustmentAmount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {adjustmentAmount >= 0 ? '+' : ''}{formatCurrency(adjustmentAmount)}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Action Button */}
      <Button
        type="button"
        className="w-full"
        onClick={handlePreviewAdjustment}
        disabled={!hasAdjustment || loading}
      >
        {hasAdjustment ? (
          <>
            <CalculatorIcon className="w-4 h-4 mr-2" />
            {t('previewAdjustment')}
          </>
        ) : (
          t('noAdjustmentNeeded')
        )}
      </Button>
    </div>
  );
}