'use client';

import { useState, useEffect } from 'react';
import { AkahuAccount } from '@/types/bank-connections';
import { Button } from '@/components/ui/button';
import {
  BuildingLibraryIcon,
  CheckCircleIcon,
  XMarkIcon,
  ArrowRightIcon,
  CurrencyDollarIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { formatCurrency } from '@/lib/utils';
import { useTranslations } from 'next-intl';

interface Account {
  id: number;
  name: string;
  type: number;
  institution?: string;
  currentBalance: number;
  currency: string;
  isActive: boolean;
}

interface LinkAccountDialogProps {
  isOpen: boolean;
  onClose: () => void;
  akahuAccounts: AkahuAccount[];
  onComplete: (accountId: number, akahuAccountId: string) => Promise<void>;
}

export function LinkAccountDialog({
  isOpen,
  onClose,
  akahuAccounts,
  onComplete
}: LinkAccountDialogProps) {
  const t = useTranslations('bankConnections');
  const [step, setStep] = useState<'select-akahu' | 'select-local'>('select-akahu');
  const [selectedAkahu, setSelectedAkahu] = useState<AkahuAccount | null>(null);
  const [localAccounts, setLocalAccounts] = useState<Account[]>([]);
  const [loadingAccounts, setLoadingAccounts] = useState(false);
  const [isLinking, setIsLinking] = useState(false);

  useEffect(() => {
    if (isOpen && step === 'select-local') {
      loadLocalAccounts();
    }
  }, [isOpen, step]);

  useEffect(() => {
    if (!isOpen) {
      // Reset state when dialog closes
      setStep('select-akahu');
      setSelectedAkahu(null);
    }
  }, [isOpen]);

  const loadLocalAccounts = async () => {
    setLoadingAccounts(true);
    try {
      const accounts = await apiClient.getAccountsWithBalances() as Account[];
      // Filter to active accounts without existing bank connections
      setLocalAccounts(accounts.filter((a: Account) => a.isActive));
    } catch (error) {
      console.error('Failed to load accounts:', error);
    } finally {
      setLoadingAccounts(false);
    }
  };

  const handleSelectAkahu = (account: AkahuAccount) => {
    setSelectedAkahu(account);
    setStep('select-local');
  };

  const handleLinkAccount = async (localAccountId: number) => {
    if (!selectedAkahu) return;

    setIsLinking(true);
    try {
      await onComplete(localAccountId, selectedAkahu.id);
      onClose();
    } catch (error) {
      console.error('Failed to link account:', error);
    } finally {
      setIsLinking(false);
    }
  };

  if (!isOpen) return null;

  const availableAkahuAccounts = akahuAccounts.filter(a => !a.isAlreadyLinked);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-xl max-w-lg w-full mx-4 max-h-[80vh] overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">
              {step === 'select-akahu' ? 'Select Bank Account' : 'Link to MyMascada Account'}
            </h2>
            <p className="text-sm text-gray-500 mt-0.5">
              {step === 'select-akahu'
                ? 'Choose which bank account to connect'
                : `Link "${selectedAkahu?.name}" to a local account`
              }
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <XMarkIcon className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <div className="px-6 py-4 overflow-y-auto max-h-[60vh]">
          {step === 'select-akahu' ? (
            <div className="space-y-3">
              {availableAkahuAccounts.length === 0 ? (
                <div className="text-center py-8">
                  <CheckCircleIcon className="w-12 h-12 text-green-500 mx-auto mb-3" />
                  <h3 className="font-medium text-gray-900">{t('allAccountsLinked')}</h3>
                  <p className="text-sm text-gray-500 mt-1">
                    All your Akahu accounts are already connected.
                  </p>
                </div>
              ) : (
                availableAkahuAccounts.map((account) => (
                  <button
                    key={account.id}
                    onClick={() => handleSelectAkahu(account)}
                    className="w-full p-4 border border-gray-200 rounded-lg hover:border-blue-500 hover:bg-blue-50 transition-colors text-left"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-gradient-to-br from-blue-400 to-blue-600 rounded-lg flex items-center justify-center shrink-0">
                        <BuildingLibraryIcon className="w-5 h-5 text-white" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <h4 className="font-medium text-gray-900 truncate">{account.name}</h4>
                        <p className="text-sm text-gray-500">{account.bankName}</p>
                        <p className="text-xs text-gray-400 mt-0.5">{account.formattedAccount}</p>
                      </div>
                      {account.currentBalance !== undefined && (
                        <div className="text-right">
                          <p className="font-medium text-gray-900">
                            {formatCurrency(account.currentBalance, account.currency)}
                          </p>
                          <p className="text-xs text-gray-500">{account.type}</p>
                        </div>
                      )}
                      <ArrowRightIcon className="w-5 h-5 text-gray-400" />
                    </div>
                  </button>
                ))
              )}
            </div>
          ) : (
            <div className="space-y-3">
              {loadingAccounts ? (
                <div className="space-y-3">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <div key={i} className="animate-pulse p-4 bg-gray-100 rounded-lg">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-gray-300 rounded-lg" />
                        <div className="flex-1">
                          <div className="h-4 bg-gray-300 rounded w-1/3 mb-2" />
                          <div className="h-3 bg-gray-300 rounded w-1/4" />
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : localAccounts.length === 0 ? (
                <div className="text-center py-8">
                  <CurrencyDollarIcon className="w-12 h-12 text-gray-400 mx-auto mb-3" />
                  <h3 className="font-medium text-gray-900">{t('noAccountsAvailable')}</h3>
                  <p className="text-sm text-gray-500 mt-1">
                    Create an account in MyMascada first to link it.
                  </p>
                </div>
              ) : (
                localAccounts.map((account) => (
                  <button
                    key={account.id}
                    onClick={() => handleLinkAccount(account.id)}
                    disabled={isLinking}
                    className="w-full p-4 border border-gray-200 rounded-lg hover:border-green-500 hover:bg-green-50 transition-colors text-left disabled:opacity-50"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-gradient-to-br from-primary-400 to-primary-600 rounded-lg flex items-center justify-center shrink-0">
                        <CurrencyDollarIcon className="w-5 h-5 text-white" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <h4 className="font-medium text-gray-900 truncate">{account.name}</h4>
                        {account.institution && (
                          <p className="text-sm text-gray-500">{account.institution}</p>
                        )}
                      </div>
                      <div className="text-right">
                        <p className="font-medium text-gray-900">
                          {formatCurrency(account.currentBalance, account.currency)}
                        </p>
                      </div>
                      <CheckCircleIcon className="w-5 h-5 text-green-500" />
                    </div>
                  </button>
                ))
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-between">
          {step === 'select-local' && (
            <Button
              variant="ghost"
              onClick={() => setStep('select-akahu')}
              disabled={isLinking}
            >
              Back
            </Button>
          )}
          <div className="flex-1" />
          <Button variant="outline" onClick={onClose} disabled={isLinking}>
            Cancel
          </Button>
        </div>
      </div>
    </div>
  );
}
