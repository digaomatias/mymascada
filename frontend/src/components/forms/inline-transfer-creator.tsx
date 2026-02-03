'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatCurrency, formatDate } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import {
  MagnifyingGlassIcon,
  ArrowsRightLeftIcon,
  XMarkIcon,
  PlusIcon,
  CheckIcon,
  EyeIcon
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface Transaction {
  id: number;
  amount: number;
  transactionDate: string;
  description: string;
  accountName: string;
  accountId: number;
  transferId?: string;
}

interface Account {
  id: number;
  name: string;
}

interface InlineTransferCreatorProps {
  sourceTransaction: Transaction;
  onCancel: () => void;
  onSuccess: () => void;
}

export function InlineTransferCreator({ sourceTransaction, onCancel, onSuccess }: InlineTransferCreatorProps) {
  const t = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const [searchTerm, setSearchTerm] = useState('');
  const [showAllTransactions, setShowAllTransactions] = useState(false);
  const [suggestedTransactions, setSuggestedTransactions] = useState<Transaction[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selectedDestinationTransaction, setSelectedDestinationTransaction] = useState<Transaction | null>(null);
  const [selectedDestinationAccount, setSelectedDestinationAccount] = useState<Account | null>(null);
  const [mode, setMode] = useState<'link' | 'create'>('link');
  const [loading, setLoading] = useState(false);
  const [searching, setSearching] = useState(false);

  const loadAccounts = useCallback(async () => {
    try {
      const accountsData = await apiClient.getAccounts() as Account[];
      // Filter out the source account
      const filteredAccounts = accountsData.filter(account => account.id !== sourceTransaction.accountId);
      setAccounts(filteredAccounts);
    } catch (error) {
      console.error('Failed to load accounts:', error);
      toast.error(tToasts('accountsLoadFailed'));
    }
  }, [sourceTransaction.accountId]);

  const searchSuggestedTransactions = useCallback(async () => {
    setSearching(true);
    try {
      let params;
      let minAmount, maxAmount;
      
      if (!showAllTransactions && searchTerm.length === 0) {
        // Default mode: Show exact amount and date matches
        const sourceDate = new Date(sourceTransaction.transactionDate);
        const startDate = new Date(sourceDate);
        startDate.setDate(sourceDate.getDate() - 1); // 1 day before
        const endDate = new Date(sourceDate);
        endDate.setDate(sourceDate.getDate() + 1); // 1 day after
        
        params = {
          startDate: startDate.toISOString().split('T')[0],
          endDate: endDate.toISOString().split('T')[0],
        };
        minAmount = Math.abs(sourceTransaction.amount) * 0.95; // 5% tolerance for exact matches
        maxAmount = Math.abs(sourceTransaction.amount) * 1.05;
      } else {
        // Expanded mode: Broader search with search term
        params = {
          searchTerm: searchTerm || undefined,
          startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0], // Last 30 days
        };
        minAmount = Math.abs(sourceTransaction.amount) * 0.7; // 30% tolerance for broader search
        maxAmount = Math.abs(sourceTransaction.amount) * 1.3;
      }

      const response = await apiClient.getTransactions(params) as {
        transactions: Transaction[];
      };

      // Filter out the source transaction, transfers, and apply amount filtering
      const candidates = response.transactions.filter(t => {
        const absAmount = Math.abs(t.amount);
        return t.id !== sourceTransaction.id && 
               t.accountId !== sourceTransaction.accountId &&
               !t.transferId &&
               absAmount >= minAmount &&
               absAmount <= maxAmount;
      });

      setSuggestedTransactions(candidates);
    } catch (error) {
      console.error('Failed to search transactions:', error);
    } finally {
      setSearching(false);
    }
  }, [showAllTransactions, searchTerm, sourceTransaction.transactionDate, sourceTransaction.amount, sourceTransaction.id, sourceTransaction.accountId]);

  useEffect(() => {
    loadAccounts();
    searchSuggestedTransactions();
  }, [loadAccounts, searchSuggestedTransactions]);

  useEffect(() => {
    if (searchTerm.length >= 2) {
      setShowAllTransactions(true);
      searchSuggestedTransactions();
    } else if (searchTerm.length === 0 && showAllTransactions) {
      setShowAllTransactions(false);
      searchSuggestedTransactions();
    } else if (searchTerm.length === 0) {
      searchSuggestedTransactions();
    }
  }, [searchTerm, showAllTransactions, searchSuggestedTransactions]);

  const handleLinkExistingTransaction = async () => {
    if (!selectedDestinationTransaction) {
      toast.error(t('validation.destinationTransactionRequired'));
      return;
    }

    setLoading(true);
    try {
      await apiClient.linkTransactionsAsTransfer({
        sourceTransactionId: sourceTransaction.id,
        destinationTransactionId: selectedDestinationTransaction.id,
        description: t('transferDescription', { from: sourceTransaction.accountName, to: selectedDestinationTransaction.accountName })
      });

      toast.success(tToasts('transferCreated'));
      onSuccess();
    } catch (error) {
      console.error('Failed to link transactions:', error);
      toast.error(tToasts('transferCreateFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCreateNewTransaction = async () => {
    if (!selectedDestinationAccount) {
      toast.error(t('validation.destinationAccountRequired'));
      return;
    }

    setLoading(true);
    try {
      await apiClient.createMissingTransfer({
        existingTransactionId: sourceTransaction.id,
        missingAccountId: selectedDestinationAccount.id,
        description: t('transferFromLabel', { name: sourceTransaction.accountName }),
        transactionDate: sourceTransaction.transactionDate
      });

      toast.success(tToasts('transferCreated'));
      onSuccess();
    } catch (error) {
      console.error('Failed to create transfer:', error);
      toast.error(tToasts('transferCreateFailed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white border border-slate-200 rounded-lg shadow-sm mt-2 p-6 space-y-6 animate-in slide-in-from-top-2 duration-200">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <ArrowsRightLeftIcon className="w-5 h-5 text-slate-600" />
          <h3 className="font-medium text-slate-900">{t('createTransfer')}</h3>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={onCancel}
          className="text-slate-500 hover:text-slate-700 -mr-2"
        >
          <XMarkIcon className="w-4 h-4" />
        </Button>
      </div>

      {/* Source Transaction */}
      <div className="bg-slate-50 rounded-lg p-4 border-l-4 border-red-500">
        <div className="flex items-center gap-3 mb-2">
          <div className="w-2 h-2 rounded-full bg-red-500"></div>
          <span className="text-sm font-medium text-slate-700">{t('transferFrom', { name: sourceTransaction.accountName })}</span>
        </div>
        <div className="text-sm text-slate-600">
          <div className="font-medium text-slate-900 mb-1">{sourceTransaction.description}</div>
          <div className="flex items-center gap-4">
            <span className="font-medium">{formatCurrency(Math.abs(sourceTransaction.amount))}</span>
            <span>{formatDate(sourceTransaction.transactionDate)}</span>
          </div>
        </div>
      </div>

      {/* Mode Selection */}
      <div className="flex border border-slate-200 rounded-lg p-1 bg-slate-50">
        <button
          onClick={() => setMode('link')}
          className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-all duration-150 ${
            mode === 'link'
              ? 'bg-white text-slate-900 shadow-sm border border-slate-200'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <ArrowsRightLeftIcon className="w-4 h-4" />
          {t('linkExisting')}
        </button>
        <button
          onClick={() => setMode('create')}
          className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-all duration-150 ${
            mode === 'create'
              ? 'bg-white text-slate-900 shadow-sm border border-slate-200'
              : 'text-slate-600 hover:text-slate-900'
          }`}
        >
          <PlusIcon className="w-4 h-4" />
          {t('createNew')}
        </button>
      </div>

      {/* Link Existing Transaction Mode */}
      {mode === 'link' && (
        <div className="space-y-3">
          {/* Search Box */}
          <div className="space-y-3">
            <div className="relative">
              <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
              <Input
                placeholder={showAllTransactions ? t('searchMatchingTransaction') : t('typeToSearchTransactions')}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10"
              />
            </div>
            
            {!showAllTransactions && searchTerm.length === 0 && (
              <div className="flex items-center justify-between text-sm bg-slate-50 rounded-lg p-3">
                <span className="text-slate-600">
                  {t('matchingExactLabel', { date: formatDate(sourceTransaction.transactionDate) })}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowAllTransactions(true)}
                  className="text-blue-600 hover:text-blue-700 hover:bg-blue-50"
                >
                  {t('showAll')}
                </Button>
              </div>
            )}
            
            {showAllTransactions && searchTerm.length === 0 && (
              <div className="flex items-center justify-between text-sm bg-slate-50 rounded-lg p-3">
                <span className="text-slate-600">
                  {t('matchingAllLabel')}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowAllTransactions(false)}
                  className="text-blue-600 hover:text-blue-700 hover:bg-blue-50"
                >
                  {t('showExactMatches')}
                </Button>
              </div>
            )}
          </div>

          {/* Suggested Transactions */}
          <div className="space-y-2 max-h-60 overflow-y-auto">
            {searching && (
              <div className="text-center text-sm text-slate-500 py-6">
                <div className="inline-flex items-center gap-2">
                  <div className="w-4 h-4 border-2 border-slate-300 border-t-blue-600 rounded-full animate-spin"></div>
                  {t('searchingMatches')}
                </div>
              </div>
            )}
            {!searching && suggestedTransactions.length === 0 && (
              <div className="text-center text-sm text-slate-500 py-6 bg-slate-50 rounded-lg">
                {t('noMatchingTransactions')}
              </div>
            )}
            {suggestedTransactions.map((transaction) => (
              <div
                key={transaction.id}
                className={`p-4 border rounded-lg cursor-pointer transition-all duration-150 ${
                  selectedDestinationTransaction?.id === transaction.id
                    ? 'border-blue-500 bg-blue-50 shadow-sm border-l-4'
                    : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50'
                }`}
                onClick={() => setSelectedDestinationTransaction(transaction)}
              >
                <div className="flex items-center justify-between">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 mb-2">
                      <div className="w-2 h-2 rounded-full bg-green-500"></div>
                      <span className="text-sm font-medium text-slate-900">{transaction.accountName}</span>
                    </div>
                    <div className="text-sm text-slate-700 font-medium mb-1 truncate">{transaction.description}</div>
                    <div className="flex items-center gap-4 text-xs text-slate-500">
                      <span className="font-medium">{formatCurrency(transaction.amount)}</span>
                      <span>{formatDate(transaction.transactionDate)}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 ml-4">
                    {selectedDestinationTransaction?.id === transaction.id && (
                      <CheckIcon className="w-5 h-5 text-blue-600" />
                    )}
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={(e) => {
                        e.stopPropagation();
                        window.open(`/transactions/${transaction.id}`, '_blank');
                      }}
                      className="text-slate-500 hover:text-slate-700"
                    >
                      <EyeIcon className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Link Button */}
          <Button
            onClick={handleLinkExistingTransaction}
            disabled={!selectedDestinationTransaction || loading}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white shadow-sm"
          >
            {loading ? t('creatingTransfer') : t('linkAsTransfer')}
          </Button>
        </div>
      )}

      {/* Create New Transaction Mode */}
      {mode === 'create' && (
        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="destinationAccount" className="text-sm font-medium text-slate-700">
              {t('destinationAccount')}
            </Label>
            <select
              id="destinationAccount"
              className="w-full p-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white text-slate-900"
              value={selectedDestinationAccount?.id || ''}
              onChange={(e) => {
                const accountId = parseInt(e.target.value);
                const account = accounts.find(a => a.id === accountId);
                setSelectedDestinationAccount(account || null);
              }}
            >
              <option value="">{t('selectDestinationAccount')}</option>
              {accounts.map(account => (
                <option key={account.id} value={account.id}>
                  {account.name}
                </option>
              ))}
            </select>
          </div>

          {selectedDestinationAccount && (
            <div className="bg-green-50 rounded-lg p-4 border-l-4 border-green-500">
              <div className="flex items-center gap-3 mb-2">
                <div className="w-2 h-2 rounded-full bg-green-500"></div>
                <span className="text-sm font-medium text-slate-700">{t('transferTo', { name: selectedDestinationAccount.name })}</span>
              </div>
              <div className="text-sm text-slate-600">
                <div className="font-medium text-slate-900 mb-1">{t('newTransferTransactionHint')}</div>
                <div className="flex items-center gap-4">
                  <span className="font-medium">{formatCurrency(Math.abs(sourceTransaction.amount))}</span>
                  <span>{formatDate(sourceTransaction.transactionDate)}</span>
                </div>
              </div>
            </div>
          )}

          <Button
            onClick={handleCreateNewTransaction}
            disabled={!selectedDestinationAccount || loading}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white shadow-sm"
          >
            {loading ? t('creatingTransfer') : t('createTransfer')}
          </Button>
        </div>
      )}
    </div>
  );
}
