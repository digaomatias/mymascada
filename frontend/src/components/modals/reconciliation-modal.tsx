'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { createPortal } from 'react-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  XMarkIcon,
  DocumentArrowUpIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon,
  ArrowPathIcon,
  ScaleIcon,
  CloudArrowDownIcon,
  LinkIcon
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { AkahuReconciliationForm } from '@/components/reconciliation/akahu-reconciliation-form';
import { AkahuBalanceComparison } from '@/components/reconciliation/akahu-balance-comparison';

interface ReconciliationModalProps {
  isOpen: boolean;
  onClose: () => void;
  accountId: number;
  accountName: string;
  onSuccess?: () => void;
}

type ReconciliationStep = 'initiate' | 'import' | 'matching' | 'review' | 'complete';
type ImportSource = 'file' | 'akahu';

interface ReconciliationData {
  id?: number;
  accountId: number;
  statementEndDate: string;
  statementEndBalance: number;
  calculatedBalance?: number;
  balanceDifference?: number;
  status?: string;
  notes?: string;
}

interface AkahuAvailability {
  isAvailable: boolean;
  externalAccountId?: string;
  unavailableReason?: string;
}

interface AkahuBalanceData {
  akahuBalance: number;
  myMascadaBalance: number;
  difference: number;
  isBalanced: boolean;
  isCurrentBalance?: boolean;
  pendingTransactionsTotal?: number;
  pendingTransactionsCount?: number;
}

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
  reference?: string;
}

export function ReconciliationModal({ 
  isOpen, 
  onClose, 
  accountId, 
  accountName, 
  onSuccess 
}: ReconciliationModalProps) {
  const t = useTranslations('reconciliation');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [step, setStep] = useState<ReconciliationStep>('initiate');
  const [loading, setLoading] = useState(false);
  const [reconciliation, setReconciliation] = useState<ReconciliationData>({
    accountId,
    statementEndDate: new Date().toISOString(),
    statementEndBalance: 0,
    notes: ''
  });
  const [bankTransactions, setBankTransactions] = useState<BankTransaction[]>([]);
  const [matchingResults, setMatchingResults] = useState<{
    exactMatches?: number;
    fuzzyMatches?: number;
    unmatchedBank?: number;
    unmatchedApp?: number;
    totalBankTransactions?: number;
    totalAppTransactions?: number;
    overallMatchPercentage?: number;
  } | null>(null);

  // Akahu-specific state
  const [akahuAvailability, setAkahuAvailability] = useState<AkahuAvailability | null>(null);
  const [checkingAkahu, setCheckingAkahu] = useState(false);
  const [importSource, setImportSource] = useState<ImportSource>('file');
  const [akahuBalanceData, setAkahuBalanceData] = useState<AkahuBalanceData | null>(null);

  useEffect(() => {
    if (isOpen) {
      // Reset to initial state when modal opens
      setStep('initiate');
      setReconciliation({
        accountId,
        statementEndDate: new Date().toISOString(),
        statementEndBalance: 0,
        notes: ''
      });
      setBankTransactions([]);
      setMatchingResults(null);
      setAkahuAvailability(null);
      setImportSource('file');
      setAkahuBalanceData(null);

      // Check Akahu availability
      checkAkahuAvailability();
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, accountId]);

  const checkAkahuAvailability = async () => {
    try {
      setCheckingAkahu(true);
      const availability = await apiClient.checkAkahuReconciliationAvailability(accountId);
      setAkahuAvailability(availability);
      // If Akahu is available, default to Akahu import
      if (availability.isAvailable) {
        setImportSource('akahu');
      }
    } catch (error) {
      console.error('Failed to check Akahu availability:', error);
      setAkahuAvailability({ isAvailable: false, unavailableReason: t('akahuAvailabilityFailed') });
    } finally {
      setCheckingAkahu(false);
    }
  };

  const handleClose = () => {
    onClose();
  };

  // Handle successful Akahu reconciliation (fetch + match in one step)
  const handleAkahuSuccess = (result: {
    reconciliationId: number;
    matchingResult: {
      totalBankTransactions: number;
      totalAppTransactions: number;
      exactMatches: number;
      fuzzyMatches: number;
      unmatchedBank: number;
      unmatchedApp: number;
      overallMatchPercentage: number;
    };
    balanceComparison?: {
      akahuBalance: number;
      myMascadaBalance: number;
      difference: number;
      isBalanced: boolean;
      isCurrentBalance: boolean;
      pendingTransactionsTotal?: number;
      pendingTransactionsCount?: number;
    };
  }) => {
    // Update reconciliation with the ID
    setReconciliation(prev => ({
      ...prev,
      id: result.reconciliationId
    }));

    // Set matching results
    setMatchingResults({
      exactMatches: result.matchingResult.exactMatches,
      fuzzyMatches: result.matchingResult.fuzzyMatches,
      unmatchedBank: result.matchingResult.unmatchedBank,
      unmatchedApp: result.matchingResult.unmatchedApp,
      totalBankTransactions: result.matchingResult.totalBankTransactions,
      totalAppTransactions: result.matchingResult.totalAppTransactions,
      overallMatchPercentage: result.matchingResult.overallMatchPercentage
    });

    // Set balance comparison data if available
    if (result.balanceComparison) {
      setAkahuBalanceData({
        akahuBalance: result.balanceComparison.akahuBalance,
        myMascadaBalance: result.balanceComparison.myMascadaBalance,
        difference: result.balanceComparison.difference,
        isBalanced: result.balanceComparison.isBalanced,
        isCurrentBalance: result.balanceComparison.isCurrentBalance,
        pendingTransactionsTotal: result.balanceComparison.pendingTransactionsTotal,
        pendingTransactionsCount: result.balanceComparison.pendingTransactionsCount
      });
    }

    // Skip to review step (matching already done server-side)
    setStep('review');
  };

  const handleStartReconciliation = async () => {
    try {
      setLoading(true);
      const result = await apiClient.createReconciliation({
        accountId: reconciliation.accountId,
        statementEndDate: reconciliation.statementEndDate,
        statementEndBalance: reconciliation.statementEndBalance,
        notes: reconciliation.notes
      });
      
      const createdReconciliation = result as ReconciliationData;
      setReconciliation(prev => ({ ...prev, ...createdReconciliation }));
      setStep('import');
      toast.success(tToasts('reconciliationStarted'));
    } catch (error) {
      console.error('Failed to start reconciliation:', error);
      toast.error(tToasts('reconciliationStartFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleImportTransactions = () => {
    // For now, just move to matching step
    // In a real implementation, this would handle file upload
    if (bankTransactions.length === 0) {
      toast.error(t('bankTransactionsRequired'));
      return;
    }
    setStep('matching');
  };

  const handleMatchTransactions = async () => {
    if (!reconciliation.id) return;
    
    try {
      setLoading(true);
      const result = await apiClient.matchTransactions(reconciliation.id, {
        bankTransactions,
        toleranceAmount: 0.01,
        useDescriptionMatching: true,
        useDateRangeMatching: true,
        dateRangeToleranceDays: 2
      });
      
      setMatchingResults(result as typeof matchingResults);
      setStep('review');
      toast.success(tToasts('transactionsMatched'));
    } catch (error) {
      console.error('Failed to match transactions:', error);
      toast.error(tToasts('transactionsMatchFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCompleteReconciliation = async () => {
    if (!reconciliation.id) return;
    
    try {
      setLoading(true);
      await apiClient.updateReconciliation(reconciliation.id, {
        status: 1 // ReconciliationStatus.Completed = 1
      });
      
      toast.success(tToasts('reconciliationCompleted'));
      setStep('complete');
      if (onSuccess) onSuccess();
    } catch (error) {
      console.error('Failed to complete reconciliation:', error);
      toast.error(tToasts('reconciliationCompleteFailed'));
    } finally {
      setLoading(false);
    }
  };

  const addSampleTransaction = () => {
    const sampleTransaction: BankTransaction = {
      bankTransactionId: `BANK_${Date.now()}`,
      amount: -50.00,
      transactionDate: new Date().toISOString().split('T')[0],
      description: t('sampleTransactionDescription'),
      bankCategory: t('sampleBankCategory'),
      reference: t('sampleReference')
    };
    setBankTransactions(prev => [...prev, sampleTransaction]);
  };

  if (!isOpen) return null;

  const renderStepContent = () => {
    switch (step) {
      case 'initiate':
        return (
          <div className="space-y-6 pb-64">
            <div>
              <h3 className="text-lg font-semibold mb-4">{t('startBankReconciliation')}</h3>
              <p className="text-gray-600 mb-6">
                {t('startBankReconciliationDesc')}
              </p>
            </div>
            
            <div className="space-y-4 relative">
              <div className="relative z-50">
                <DateTimePicker
                  label={t('statementEndDate')}
                  value={reconciliation.statementEndDate}
                  onChange={(value) => setReconciliation(prev => ({
                    ...prev,
                    statementEndDate: value
                  }))}
                  placeholder={t('selectStatementDate')}
                  showTime={false}
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t('statementEndingBalance')}
                </label>
                <input
                  type="number"
                  step="0.01"
                  value={reconciliation.statementEndBalance}
                  onChange={(e) => setReconciliation(prev => ({ 
                    ...prev, 
                    statementEndBalance: parseFloat(e.target.value) || 0 
                  }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
                  placeholder={t('form.amountPlaceholder')}
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t('notesOptional')}
                </label>
                <textarea
                  value={reconciliation.notes}
                  onChange={(e) => setReconciliation(prev => ({
                    ...prev,
                    notes: e.target.value
                  }))}
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
                  placeholder={t('notesPlaceholder')}
                />
              </div>
            </div>
            
            <div className="flex justify-end gap-3">
              <Button variant="secondary" onClick={handleClose}>
                {tCommon('cancel')}
              </Button>
              <Button
                onClick={handleStartReconciliation}
                disabled={loading || !reconciliation.statementEndDate}
                className="flex items-center gap-2"
              >
                {loading && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
                {t('startReconciliation')}
              </Button>
            </div>
          </div>
        );

      case 'import':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold mb-4">{t('importBankTransactions')}</h3>
              <p className="text-gray-600 mb-6">
                {t('importBankTransactionsDesc')}
              </p>
            </div>

            {/* Source Toggle - Show only if Akahu is available */}
            {akahuAvailability?.isAvailable && (
              <div className="flex rounded-lg border border-gray-200 p-1 bg-gray-50">
                <button
                  onClick={() => setImportSource('akahu')}
                  className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                    importSource === 'akahu'
                      ? 'bg-white text-primary-700 shadow-sm'
                      : 'text-gray-600 hover:text-gray-900'
                  }`}
                >
                  <CloudArrowDownIcon className="w-4 h-4" />
                  {t('fetchFromAkahu')}
                </button>
                <button
                  onClick={() => setImportSource('file')}
                  className={`flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                    importSource === 'file'
                      ? 'bg-white text-primary-700 shadow-sm'
                      : 'text-gray-600 hover:text-gray-900'
                  }`}
                >
                  <DocumentArrowUpIcon className="w-4 h-4" />
                  {t('uploadFile')}
                </button>
              </div>
            )}

            {/* Show connection status if checking */}
            {checkingAkahu && (
              <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
                <div className="flex items-center gap-3">
                  <ArrowPathIcon className="w-5 h-5 text-gray-500 animate-spin" />
                  <span className="text-gray-600">{t('checkingConnection')}</span>
                </div>
              </div>
            )}

            {/* Show why Akahu is unavailable (if checked and not available) */}
            {!checkingAkahu && akahuAvailability && !akahuAvailability.isAvailable && (
              <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <LinkIcon className="w-5 h-5 text-amber-600 mt-0.5" />
                  <div>
                      <h4 className="font-medium text-amber-900">{t('connectionNotAvailable')}</h4>
                      <p className="text-sm text-amber-700 mt-1">
                      {akahuAvailability.unavailableReason || t('connectAkahuToFetch')}
                      </p>
                    </div>
                  </div>
                </div>
            )}

            {/* Akahu Import Form */}
            {importSource === 'akahu' && akahuAvailability?.isAvailable && (
              <AkahuReconciliationForm
                accountId={accountId}
                onSuccess={handleAkahuSuccess}
                onError={(error) => toast.error(error)}
              />
            )}

            {/* File Upload (existing functionality) */}
            {importSource === 'file' && (
              <>
                <div className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
                  <DocumentArrowUpIcon className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                  <h4 className="text-lg font-medium text-gray-900 mb-2">{t('uploadBankStatement')}</h4>
                  <p className="text-gray-600 mb-4">
                    {t('dragAndDropFile')}
                  </p>
                  <Button variant="secondary" className="mb-4">
                    {t('chooseFile')}
                  </Button>
                  <p className="text-xs text-gray-500">
                    {t('supportedFormats')}
                  </p>
                </div>

                {/* Demo: Manual transaction entry */}
                <div className="bg-gray-50 rounded-lg p-4">
                  <h4 className="font-medium mb-2">{t('addSampleTransaction')}</h4>
                  <Button onClick={addSampleTransaction} variant="secondary" size="sm">
                    {t('addSampleTransactionBtn')}
                  </Button>
                </div>

                {/* Show added transactions */}
                {bankTransactions.length > 0 && (
                  <div>
                    <h4 className="font-medium mb-3">{t('bankTransactionsCount', { count: bankTransactions.length })}</h4>
                    <div className="space-y-2 max-h-40 overflow-y-auto">
                      {bankTransactions.map((transaction, index) => (
                        <div key={index} className="flex justify-between items-center p-3 bg-white rounded border">
                          <div>
                            <div className="font-medium">{transaction.description}</div>
                            <div className="text-sm text-gray-500">{transaction.transactionDate}</div>
                          </div>
                          <div className={`font-semibold ${transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                            {formatCurrency(transaction.amount)}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex justify-end gap-3">
                  <Button variant="secondary" onClick={() => setStep('initiate')}>
                    {tCommon('back')}
                  </Button>
                  <Button
                    onClick={handleImportTransactions}
                    disabled={bankTransactions.length === 0}
                    className="flex items-center gap-2"
                  >
                    <ArrowPathIcon className="w-4 h-4" />
                    {t('continueToMatching')}
                  </Button>
                </div>
              </>
            )}

            {/* Back button for Akahu mode */}
            {importSource === 'akahu' && (
              <div className="flex justify-start">
                <Button variant="secondary" onClick={() => setStep('initiate')}>
                  {tCommon('back')}
                </Button>
              </div>
            )}
          </div>
        );

      case 'matching':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold mb-4">{t('matchTransactions')}</h3>
              <p className="text-gray-600 mb-6">
                {t('matchTransactionsDesc')}
              </p>
            </div>
            
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <div className="flex items-center gap-3">
                <ArrowPathIcon className="w-5 h-5 text-blue-600" />
                <div>
                  <h4 className="font-medium text-blue-900">{t('readyToMatch')}</h4>
                  <p className="text-sm text-blue-700">
                    {t('transactionsToCompare', { count: bankTransactions.length })}
                  </p>
                </div>
              </div>
            </div>
            
            <div className="flex justify-end gap-3">
              <Button variant="secondary" onClick={() => setStep('import')}>
                {tCommon('back')}
              </Button>
              <Button
                onClick={handleMatchTransactions}
                disabled={loading}
                className="flex items-center gap-2"
              >
                {loading && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
                {t('matchTransactions')}
              </Button>
            </div>
          </div>
        );

      case 'review':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold mb-4">{t('reviewResults')}</h3>
              <p className="text-gray-600 mb-6">
                {t('reviewResultsDesc')}
              </p>
            </div>

            {/* Balance Comparison (Akahu only) */}
            {akahuBalanceData && (
              <AkahuBalanceComparison
                akahuBalance={akahuBalanceData.akahuBalance}
                myMascadaBalance={akahuBalanceData.myMascadaBalance}
                difference={akahuBalanceData.difference}
                isBalanced={akahuBalanceData.isBalanced}
                isCurrentBalance={akahuBalanceData.isCurrentBalance}
                pendingTransactionsTotal={akahuBalanceData.pendingTransactionsTotal}
                pendingTransactionsCount={akahuBalanceData.pendingTransactionsCount}
              />
            )}

            {matchingResults && (
              <div className="space-y-4">
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center gap-2">
                        <CheckCircleIcon className="w-5 h-5 text-green-600" />
                        <div>
                          <div className="font-semibold">{matchingResults.exactMatches || 0}</div>
                          <div className="text-sm text-gray-600">{t('exactMatches')}</div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center gap-2">
                        <ExclamationTriangleIcon className="w-5 h-5 text-yellow-600" />
                        <div>
                          <div className="font-semibold">{matchingResults.fuzzyMatches || 0}</div>
                          <div className="text-sm text-gray-600">{t('fuzzyMatches')}</div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center gap-2">
                        <CloudArrowDownIcon className="w-5 h-5 text-blue-600" />
                        <div>
                          <div className="font-semibold">{matchingResults.unmatchedBank || 0}</div>
                          <div className="text-sm text-gray-600">{t('unmatchedBank')}</div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center gap-2">
                        <XMarkIcon className="w-5 h-5 text-red-600" />
                        <div>
                          <div className="font-semibold">{matchingResults.unmatchedApp || 0}</div>
                          <div className="text-sm text-gray-600">{t('unmatchedApp')}</div>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </div>

                <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                  <div className="flex items-center gap-3">
                    <CheckCircleIcon className="w-5 h-5 text-green-600" />
                    <div>
                      <h4 className="font-medium text-green-900">{t('matchingComplete')}</h4>
                      <p className="text-sm text-green-700">
                        {((matchingResults.exactMatches || 0) + (matchingResults.fuzzyMatches || 0))} out of {matchingResults.totalBankTransactions || 0} bank transactions matched
                        {matchingResults.overallMatchPercentage !== undefined && (
                          <span className="ml-1">({matchingResults.overallMatchPercentage.toFixed(1)}% match rate)</span>
                        )}
                      </p>
                    </div>
                  </div>
                </div>

                {/* Link to detailed review */}
                {reconciliation.id && (matchingResults.unmatchedBank || 0) + (matchingResults.unmatchedApp || 0) > 0 && (
                  <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                    <div className="flex items-start gap-3">
                      <ExclamationTriangleIcon className="w-5 h-5 text-blue-600 mt-0.5" />
                      <div>
                        <h4 className="font-medium text-blue-900">{t('unmatchedTransactionsFound')}</h4>
                        <p className="text-sm text-blue-700 mt-1">
                          {t('reviewDetailedResults')}
                        </p>
                        <Button
                          variant="secondary"
                          size="sm"
                          className="mt-3"
                          onClick={() => {
                            // Navigate to detailed reconciliation view
                            window.location.href = `/reconciliation/${reconciliation.id}`;
                          }}
                        >
                          {t('viewDetailedResults')}
                        </Button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="flex justify-end gap-3">
              <Button variant="secondary" onClick={() => setStep(importSource === 'akahu' ? 'import' : 'matching')}>
                {tCommon('back')}
              </Button>
              <Button
                onClick={handleCompleteReconciliation}
                disabled={loading}
                className="flex items-center gap-2"
              >
                {loading && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
                {t('completeReconciliation')}
              </Button>
            </div>
          </div>
        );

      case 'complete':
        return (
          <div className="space-y-6 text-center">
            <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
              <CheckCircleIcon className="w-10 h-10 text-green-600" />
            </div>
            
            <div>
              <h3 className="text-lg font-semibold mb-2">{t('reconciliationComplete')}</h3>
              <p className="text-gray-600">
                {t('reconciliationCompleteDesc')}
              </p>
            </div>
            
            <div className="flex justify-center">
              <Button onClick={handleClose}>
                {tCommon('close')}
              </Button>
            </div>
          </div>
        );

      default:
        return null;
    }
  };

  const getStepIndicator = () => {
    const steps = [
      { key: 'initiate', label: t('steps.start'), completed: ['import', 'matching', 'review', 'complete'].includes(step) },
      { key: 'import', label: t('steps.import'), completed: ['matching', 'review', 'complete'].includes(step) },
      { key: 'matching', label: t('steps.match'), completed: ['review', 'complete'].includes(step) },
      { key: 'review', label: t('steps.review'), completed: ['complete'].includes(step) },
      { key: 'complete', label: t('steps.complete'), completed: step === 'complete' }
    ];

    return (
      <div className="flex items-center justify-between mb-8">
        {steps.map((stepItem, index) => (
          <div key={stepItem.key} className="flex items-center">
            <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
              stepItem.completed || stepItem.key === step
                ? 'bg-primary-600 text-white'
                : 'bg-gray-200 text-gray-600'
            }`}>
              {stepItem.completed ? <CheckCircleIcon className="w-5 h-5" /> : index + 1}
            </div>
            <span className={`ml-2 text-sm ${
              stepItem.key === step ? 'text-primary-600 font-medium' : 'text-gray-500'
            }`}>
              {stepItem.label}
            </span>
            {index < steps.length - 1 && (
              <div className={`w-12 h-px mx-4 ${
                stepItem.completed ? 'bg-primary-600' : 'bg-gray-200'
              }`} />
            )}
          </div>
        ))}
      </div>
    );
  };

  if (!isOpen) return null;

  return createPortal(
    <div 
      className="fixed inset-0 bg-black/5 flex items-center justify-center z-[9999] p-4" 
      onMouseDown={(e) => {
        // Only close if clicking directly on the backdrop, not dragging from modal
        if (e.target === e.currentTarget) {
          handleClose();
        }
      }}
    >
      <div className="bg-white rounded-xl shadow-xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between p-6 border-b flex-shrink-0">
          <div className="flex items-center gap-3">
            <ScaleIcon className="w-6 h-6 text-primary-600" />
            <h2 className="text-xl font-semibold">{t('reconcileAccount', { name: accountName })}</h2>
          </div>
          <Button
            variant="secondary"
            size="sm"
            onClick={handleClose}
            className="p-2"
          >
            <XMarkIcon className="w-4 h-4" />
          </Button>
        </div>
        
        <div className="p-6 flex-1 overflow-visible">
          {getStepIndicator()}
          <div className="overflow-visible">
            {renderStepContent()}
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
}
