'use client';

import { useState, useEffect, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/auth-context';
import Navigation from '@/components/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  CheckCircleIcon,
  ArrowPathIcon,
  ScaleIcon,
  ArrowLeftIcon,
  CloudArrowDownIcon,
  DocumentArrowUpIcon,
  LinkIcon
} from '@heroicons/react/24/outline';
import { ReconciliationFileUpload } from '@/components/forms/reconciliation-file-upload';
import { ReconciliationDetailsView } from '@/components/reconciliation/reconciliation-details-view';
import { AkahuReconciliationForm } from '@/components/reconciliation/akahu-reconciliation-form';
import { AkahuBalanceComparison } from '@/components/reconciliation/akahu-balance-comparison';
import { formatCurrency } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { CurrencyInput } from '@/components/ui/currency-input';
import { useTranslations } from 'next-intl';
import Link from 'next/link';

type ReconciliationStep = 'initiate' | 'import' | 'matching' | 'review' | 'complete';
type ImportSource = 'file' | 'akahu';

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

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
  reference?: string;
}

interface Account {
  id: number;
  name: string;
  type: number;
  institution?: string;
  currentBalance: number;
  currency: string;
}

export default function ReconcileAccountPage() {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const router = useRouter();
  const params = useParams();
  const accountId = parseInt(params.id as string);
  const t = useTranslations('reconciliation');
  const tCommon = useTranslations('common');

  const [account, setAccount] = useState<Account | null>(null);
  const [step, setStep] = useState<ReconciliationStep>('initiate');
  const [loading, setLoading] = useState(false);
  const [reconciliation, setReconciliation] = useState<ReconciliationData>({
    accountId,
    statementEndDate: new Date().toISOString(),
    statementEndBalance: 0,
    notes: ''
  });
  const [bankTransactions, setBankTransactions] = useState<BankTransaction[]>([]);
  // Upload loading is now handled by individual upload components

  // Akahu-specific state
  const [akahuAvailability, setAkahuAvailability] = useState<AkahuAvailability | null>(null);
  const [checkingAkahu, setCheckingAkahu] = useState(false);
  const [importSource, setImportSource] = useState<ImportSource>('file');
  const [akahuBalanceData, setAkahuBalanceData] = useState<AkahuBalanceData | null>(null);


  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, authLoading, router]);

  const loadAccount = useCallback(async () => {
    try {
      const accountData = await apiClient.getAccount(accountId) as Account;
      setAccount(accountData);
    } catch (error) {
      console.error('Failed to load account:', error);
      toast.error(t('failedToLoadAccount'));
      router.push('/accounts');
    }
  }, [accountId, router, t]);

  useEffect(() => {
    if (isAuthenticated && accountId) {
      loadAccount();
      checkAkahuAvailability();
    }
  }, [isAuthenticated, accountId, loadAccount]);

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
      setAkahuAvailability({ isAvailable: false, unavailableReason: 'Failed to check availability' });
    } finally {
      setCheckingAkahu(false);
    }
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
      toast.success(t('started'));
    } catch (error) {
      console.error('Failed to start reconciliation:', error);
      toast.error(t('startFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleImportTransactions = () => {
    // For now, just move to matching step
    // In a real implementation, this would handle file upload
    if (bankTransactions.length === 0) {
      toast.error(t('addTransactionsFirst'));
      return;
    }
    setStep('matching');
  };

  const handleMatchTransactions = async () => {
    if (!reconciliation.id) return;
    
    try {
      setLoading(true);
      
      // Calculate date range from bank transactions
      // Handle both full ISO dates and date-only formats
      const transactionDates = bankTransactions
        .map(t => {
          if (!t.transactionDate) return null;
          
          // If date is in YYYY-MM-DD format, append time component
          const dateStr = t.transactionDate.includes('T') 
            ? t.transactionDate 
            : `${t.transactionDate}T00:00:00.000Z`;
          
          const date = new Date(dateStr);
          
          // Validate the date
          if (isNaN(date.getTime())) {
            console.warn('Invalid date found in bank transaction:', t.transactionDate);
            return null;
          }
          
          return date;
        })
        .filter(date => date !== null) as Date[];
      
      const startDate = transactionDates.length > 0 
        ? new Date(Math.min(...transactionDates.map(d => d.getTime()))).toISOString()
        : new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(); // 30 days ago
      const endDate = transactionDates.length > 0
        ? new Date(Math.max(...transactionDates.map(d => d.getTime()))).toISOString()
        : new Date().toISOString();
      
      const matchRequest = {
        bankTransactions,
        startDate,
        endDate,
        toleranceAmount: 0.01,
        useDescriptionMatching: true,
        useDateRangeMatching: true,
        dateRangeToleranceDays: 2
      };
      
      console.log('Sending match request:', matchRequest);
      
      await apiClient.matchTransactions(reconciliation.id, matchRequest);

      // Matching completed successfully
      setStep('review');
      toast.success(t('matchingCompleted'));
    } catch (error: unknown) {
      console.error('Failed to match transactions:', error);
      const errorMessage = error instanceof Error && 'response' in error
        ? (error as Error & { response?: { data?: { message?: string } } }).response?.data?.message
        || (error as Error).message
        : t('matchFailed');
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleCompleteReconciliation = async () => {
    if (!reconciliation.id) return;

    try {
      setLoading(true);
      // Call the finalize endpoint which marks transactions as reconciled
      // and updates account's LastReconciledDate and LastReconciledBalance
      await apiClient.finalizeReconciliation(reconciliation.id, {
        forceFinalize: true // Force because we might have unmatched items
      });

      toast.success(t('completed'));
      setStep('complete');
    } catch (error) {
      console.error('Failed to complete reconciliation:', error);
      toast.error(t('completeFailed'));
    } finally {
      setLoading(false);
    }
  };

  // File upload is now handled by the ReconciliationFileUpload component

  const addSampleTransaction = () => {
    const sampleTransaction: BankTransaction = {
      bankTransactionId: `BANK_${Date.now()}`,
      amount: -50.00,
      transactionDate: new Date().toISOString(),
      description: 'Sample Transaction',
      bankCategory: 'RETAIL',
      reference: 'REF123'
    };
    setBankTransactions(prev => [...prev, sampleTransaction]);
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

  const renderStepContent = () => {
    switch (step) {
      case 'initiate':
        return (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="space-y-6">
                <div>
                  <h3 className="text-lg font-semibold mb-4">{t('startReconciliation')}</h3>
                  <p className="text-gray-600 mb-6">
                    {t('startReconciliationDesc')}
                  </p>
                </div>

                <div className="space-y-4">
                  <div>
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
                    <CurrencyInput
                      label={t('statementEndingBalance')}
                      value={reconciliation.statementEndBalance}
                      onChange={(value) => setReconciliation(prev => ({
                        ...prev,
                        statementEndBalance: value
                      }))}
                      currency="NZD"
                      placeholder={t('enterEndingBalance')}
                      allowNegative={true}
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
                  <Link href={`/accounts/${accountId}`}>
                    <Button variant="secondary">
                      {tCommon('cancel')}
                    </Button>
                  </Link>
                  <Button
                    onClick={handleStartReconciliation}
                    disabled={loading || !reconciliation.statementEndDate}
                    className="flex items-center gap-2"
                  >
                    {loading && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
                    {t('start')}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        );

      case 'import':
        return (
          <div className="space-y-6">
            {/* Header Card */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardContent className="p-6">
                <div>
                  <h3 className="text-lg font-semibold mb-4">{t('importTransactions')}</h3>
                  <p className="text-gray-600 mb-4">
                    {akahuAvailability?.isAvailable
                      ? t('importDescAkahu')
                      : t('importDescFile')
                    }
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
                  <div className="mt-4 bg-gray-50 border border-gray-200 rounded-lg p-4">
                    <div className="flex items-center gap-3">
                      <ArrowPathIcon className="w-5 h-5 text-gray-500 animate-spin" />
                      <span className="text-gray-600">{t('checkingConnection')}</span>
                    </div>
                  </div>
                )}

                {/* Show why Akahu is unavailable (if checked and not available) */}
                {!checkingAkahu && akahuAvailability && !akahuAvailability.isAvailable && (
                  <div className="mt-4 bg-amber-50 border border-amber-200 rounded-lg p-4">
                    <div className="flex items-start gap-3">
                      <LinkIcon className="w-5 h-5 text-amber-600 mt-0.5" />
                      <div>
                        <h4 className="font-medium text-amber-900">{t('connectionNotAvailable')}</h4>
                        <p className="text-sm text-amber-700 mt-1">
                          {akahuAvailability.unavailableReason || t('connectToAkahu')}
                        </p>
                        <Link href="/settings/bank-connections">
                          <Button variant="secondary" size="sm" className="mt-2">
                            {t('connectBankAccount')}
                          </Button>
                        </Link>
                      </div>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Akahu Import Form */}
            {importSource === 'akahu' && akahuAvailability?.isAvailable && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardContent className="p-6">
                  <AkahuReconciliationForm
                    accountId={accountId}
                    onSuccess={handleAkahuSuccess}
                    onError={(error) => toast.error(error)}
                  />
                </CardContent>
              </Card>
            )}

            {/* File Upload (existing functionality) */}
            {importSource === 'file' && (
              <>
                {/* Unified File Upload Component */}
                <ReconciliationFileUpload
                  onTransactionsExtracted={(transactions) => {
                    setBankTransactions(transactions);
                    toast.success(t('transactionsLoaded', { count: transactions.length }));
                  }}
                />

                {/* Show imported transactions */}
                {bankTransactions.length > 0 && (
                  <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                    <CardContent className="p-6">
                      <div>
                        <h4 className="font-medium mb-3">{t('bankTransactionsCount', { count: bankTransactions.length })}</h4>
                        <div className="space-y-2 max-h-60 overflow-y-auto">
                          {bankTransactions.map((transaction, index) => (
                            <div key={index} className="flex justify-between items-center p-3 bg-white rounded border">
                              <div>
                                <div className="font-medium">{transaction.description}</div>
                                <div className="text-sm text-gray-500">
                                  {transaction.transactionDate.split('T')[0].split('-').reverse().join('/')}
                                  {transaction.reference && ` • Ref: ${transaction.reference}`}
                                </div>
                              </div>
                              <div className={`font-semibold ${transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                                {formatCurrency(transaction.amount)}
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                )}

                {/* Demo: Manual transaction entry for testing */}
                <Card className="bg-gray-50/80 backdrop-blur-xs border-0 shadow-sm">
                  <CardContent className="p-4">
                    <div className="flex items-center justify-between">
                      <div>
                        <h4 className="font-medium text-gray-900">{t('demoAddSample')}</h4>
                        <p className="text-sm text-gray-600">{t('demoDesc')}</p>
                      </div>
                      <Button onClick={addSampleTransaction} variant="secondary" size="sm">
                        {t('addSampleTransaction')}
                      </Button>
                    </div>
                  </CardContent>
                </Card>

                {/* Navigation for file upload */}
                <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                  <CardContent className="p-6">
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
                  </CardContent>
                </Card>
              </>
            )}

            {/* Back button for Akahu mode */}
            {importSource === 'akahu' && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardContent className="p-6">
                  <div className="flex justify-start">
                    <Button variant="secondary" onClick={() => setStep('initiate')}>
                      {tCommon('back')}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}
          </div>
        );

      case 'matching':
        return (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
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
            </CardContent>
          </Card>
        );

      case 'review':
        return reconciliation.id ? (
          <div className="space-y-6">
            {/* Balance Comparison (Akahu only) */}
            {akahuBalanceData && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardContent className="p-6">
                  <AkahuBalanceComparison
                    akahuBalance={akahuBalanceData.akahuBalance}
                    myMascadaBalance={akahuBalanceData.myMascadaBalance}
                    difference={akahuBalanceData.difference}
                    isBalanced={akahuBalanceData.isBalanced}
                    isCurrentBalance={akahuBalanceData.isCurrentBalance}
                    pendingTransactionsTotal={akahuBalanceData.pendingTransactionsTotal}
                    pendingTransactionsCount={akahuBalanceData.pendingTransactionsCount}
                  />
                </CardContent>
              </Card>
            )}

            <ReconciliationDetailsView
              reconciliationId={reconciliation.id}
              onCompleteReconciliation={handleCompleteReconciliation}
              onBack={() => setStep(importSource === 'akahu' ? 'import' : 'matching')}
              loading={loading}
              statementEndBalance={reconciliation.statementEndBalance}
              accountId={accountId}
            />
          </div>
        ) : (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
              <div className="text-center text-red-600">
                {t('reconciliationIdNotFound')}
              </div>
            </CardContent>
          </Card>
        );

      case 'complete':
        return (
          <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <CardContent className="p-6">
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
                  <Link href={`/accounts/${accountId}`}>
                    <Button>
                      {t('backToAccount')}
                    </Button>
                  </Link>
                </div>
              </div>
            </CardContent>
          </Card>
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

  if (authLoading || !account) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <ScaleIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <Navigation />

      <main className="container-responsive py-4 sm:py-6 lg:py-8">
        {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex items-center justify-between mb-6">
            <Link href={`/accounts/${accountId}`}>
              <Button variant="secondary" size="sm" className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                {t('backToAccount')}
              </Button>
            </Link>
          </div>

          <div className="text-center mb-8">
            <div className="w-20 h-20 bg-gradient-to-br from-primary-500 to-primary-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <ScaleIcon className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('reconcileAccount', { name: account.name })}
            </h1>
            <p className="text-gray-600">
              {t('balanceWithStatement')}
            </p>
          </div>
        </div>

        {/* Progress Indicator */}
        {getStepIndicator()}
        
        {/* Step Content */}
        {renderStepContent()}
      </main>
    </div>
  );
}
