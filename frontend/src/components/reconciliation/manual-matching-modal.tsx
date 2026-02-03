'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  CheckCircleIcon,
  ArrowsRightLeftIcon,
  MagnifyingGlassIcon
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { BaseModal } from '@/components/modals/base-modal';

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
}

interface SystemTransaction {
  id: number;
  amount: number;
  description: string;
  transactionDate: string;
  categoryName?: string;
  status: number;
}

interface PotentialMatch {
  systemTransaction: SystemTransaction;
  confidence: number;
  matchReasons: {
    amountMatch: boolean;
    dateMatch: boolean;
    descriptionSimilar: boolean;
    amountDifference: number;
    dateDifferenceInDays: number;
  };
}

interface ManualMatchingModalProps {
  isOpen: boolean;
  onClose: () => void;
  bankTransaction: BankTransaction | null;
  potentialMatches: PotentialMatch[];
  onMatch: (bankTransactionId: string, systemTransactionId: number) => Promise<void>;
  loading?: boolean;
}

export function ManualMatchingModal({
  isOpen,
  onClose,
  bankTransaction,
  potentialMatches,
  onMatch,
  loading = false
}: ManualMatchingModalProps) {
  const t = useTranslations('reconciliation');
  const tCommon = useTranslations('common');
  const [selectedMatch, setSelectedMatch] = useState<PotentialMatch | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [isMatching, setIsMatching] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const handleMatch = async () => {
    if (!bankTransaction || !selectedMatch) return;

    setIsMatching(true);
    try {
      await onMatch(bankTransaction.bankTransactionId, selectedMatch.systemTransaction.id);
      
      // Show success animation
      setShowSuccess(true);
      
      // Close modal after success animation
      setTimeout(() => {
        onClose();
        setSelectedMatch(null);
        setSearchTerm('');
        setShowSuccess(false);
      }, 1500);
    } catch (error) {
      console.error('Failed to match transactions:', error);
      setIsMatching(false);
    }
  };

  const handleClose = () => {
    setSelectedMatch(null);
    setSearchTerm('');
    onClose();
  };

  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.8) return 'text-green-600 bg-green-50 border-green-200';
    if (confidence >= 0.6) return 'text-yellow-600 bg-yellow-50 border-yellow-200';
    return 'text-orange-600 bg-orange-50 border-orange-200';
  };

  const getConfidenceLabel = (confidence: number) => {
    if (confidence >= 0.9) return t('excellentMatch');
    if (confidence >= 0.8) return t('goodMatch');
    if (confidence >= 0.6) return t('fairMatch');
    return t('possibleMatch');
  };

  const filteredMatches = potentialMatches.filter(match =>
    match.systemTransaction.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
    match.systemTransaction.categoryName?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const sortedMatches = [...filteredMatches].sort((a, b) => b.confidence - a.confidence);

  if (!bankTransaction) return null;

  return (
    <BaseModal 
      isOpen={isOpen} 
      onClose={handleClose}
      title={t('matchTransaction')}
    >
      <div className="relative">
        {/* Success Overlay */}
        {showSuccess && (
          <div className="absolute inset-0 bg-white/95 backdrop-blur-sm z-50 flex items-center justify-center">
            <div className="text-center">
              <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4 animate-bounce">
                <CheckCircleIcon className="w-10 h-10 text-green-600" />
              </div>
              <h3 className="text-lg font-semibold text-gray-900 mb-2">{t('matchSuccessful')}</h3>
              <p className="text-gray-600">{t('matchSuccessfulDesc')}</p>
            </div>
          </div>
        )}
        
        <div className="space-y-6">
        {/* Bank Transaction Header */}
        <div className="border-b border-gray-200 pb-4">
          <h3 className="text-lg font-medium text-gray-900 mb-3">{t('bankStatementTransaction')}</h3>
          <Card className="bg-blue-50 border-blue-200">
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium text-gray-900">{bankTransaction.description}</p>
                  <p className="text-sm text-gray-600">
                    {new Date(bankTransaction.transactionDate).toLocaleDateString()}
                    {bankTransaction.bankCategory && ` • ${bankTransaction.bankCategory}`}
                  </p>
                </div>
                <div className="text-right">
                  <p className={`text-lg font-semibold ${
                    bankTransaction.amount >= 0 ? 'text-green-600' : 'text-red-600'
                  }`}>
                    {formatCurrency(bankTransaction.amount)}
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Search */}
        <div className="relative">
          <MagnifyingGlassIcon className="w-4 h-4 absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder={t('searchSystemTransactions')}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-10 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        {/* Potential Matches */}
        <div className="space-y-3">
          <h3 className="text-lg font-medium text-gray-900">
            {t('systemTransactions', { count: sortedMatches.length })}
          </h3>
          
          {loading ? (
            <div className="space-y-3">
              {[1, 2, 3].map((i) => (
                <div key={i} className="animate-pulse bg-gray-100 h-20 rounded-lg"></div>
              ))}
            </div>
          ) : sortedMatches.length === 0 ? (
            <div className="text-center py-8 text-gray-500">
              <ArrowsRightLeftIcon className="w-12 h-12 mx-auto mb-4 text-gray-300" />
              <p>{t('noMatchingTransactions')}</p>
              {searchTerm && (
                <p className="text-sm mt-2">
                  {t('tryAdjustingSearch')}{' '}
                  <button
                    onClick={() => setSearchTerm('')}
                    className="text-primary-600 hover:text-primary-700 underline"
                  >
                    {t('clearFilter')}
                  </button>
                </p>
              )}
            </div>
          ) : (
            <div className="max-h-96 overflow-y-auto space-y-3">
              {sortedMatches.map((match) => (
                <Card 
                  key={match.systemTransaction.id}
                  className={`cursor-pointer transition-all duration-200 ${
                    selectedMatch?.systemTransaction.id === match.systemTransaction.id
                      ? 'ring-2 ring-primary-500 bg-primary-50 scale-[1.02] shadow-lg'
                      : 'hover:shadow-md hover:scale-[1.01]'
                  }`}
                  onClick={() => setSelectedMatch(match)}
                >
                  <CardContent className="p-4">
                    <div className="flex items-center justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-2">
                          <div className={`flex items-center gap-1 px-2 py-1 rounded-full border text-xs font-medium ${getConfidenceColor(match.confidence)}`}>
                            {Math.round(match.confidence * 100)}% - {getConfidenceLabel(match.confidence)}
                          </div>
                          {selectedMatch?.systemTransaction.id === match.systemTransaction.id && (
                            <CheckCircleIcon className="w-5 h-5 text-primary-600" />
                          )}
                        </div>
                        
                        <p className="font-medium text-gray-900 mb-1">
                          {match.systemTransaction.description}
                        </p>
                        
                        <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm text-gray-600">
                          <div>
                            <span className="font-medium">{tCommon('amount')}:</span>
                            <span className={`ml-1 ${
                              match.matchReasons.amountMatch ? 'text-green-600' : 'text-orange-600'
                            }`}>
                              {formatCurrency(match.systemTransaction.amount)}
                              {!match.matchReasons.amountMatch && (
                                <span className="text-xs block">
                                  {t('diff')}: {formatCurrency(match.matchReasons.amountDifference)}
                                </span>
                              )}
                            </span>
                          </div>

                          <div>
                            <span className="font-medium">{tCommon('date')}:</span>
                            <span className={`ml-1 ${
                              match.matchReasons.dateMatch ? 'text-green-600' : 'text-orange-600'
                            }`}>
                              {new Date(match.systemTransaction.transactionDate).toLocaleDateString()}
                              {!match.matchReasons.dateMatch && match.matchReasons.dateDifferenceInDays > 0 && (
                                <span className="text-xs block">
                                  {match.matchReasons.dateDifferenceInDays} day{match.matchReasons.dateDifferenceInDays > 1 ? 's' : ''} diff
                                </span>
                              )}
                            </span>
                          </div>
                          
                          {match.systemTransaction.categoryName && (
                            <div>
                              <span className="font-medium">{tCommon('category')}:</span>
                              <span className="ml-1">{match.systemTransaction.categoryName}</span>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
          <Button variant="secondary" onClick={handleClose}>
            {tCommon('cancel')}
          </Button>
          <Button
            onClick={handleMatch}
            disabled={!selectedMatch || isMatching}
            loading={isMatching}
            className="flex items-center gap-2"
          >
            <ArrowsRightLeftIcon className="w-4 h-4" />
            {isMatching ? t('matching') : t('confirmMatch')}
          </Button>
        </div>
        </div>
      </div>
    </BaseModal>
  );
}