'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { DateTimePicker } from '@/components/ui/date-time-picker';
import { ArrowPathIcon, CloudArrowDownIcon, ExclamationCircleIcon } from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { format, subDays } from 'date-fns';

interface AkahuReconciliationFormProps {
  accountId: number;
  onSuccess: (result: {
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
    };
  }) => void;
  onError?: (error: string) => void;
}

export function AkahuReconciliationForm({
  accountId,
  onSuccess,
  onError
}: AkahuReconciliationFormProps) {
  const t = useTranslations('reconciliation');
  // Default: last 30 days
  const defaultEndDate = new Date();
  const defaultStartDate = subDays(defaultEndDate, 30);

  const [startDate, setStartDate] = useState<string>(defaultStartDate.toISOString());
  const [endDate, setEndDate] = useState<string>(defaultEndDate.toISOString());
  const [statementEndBalance, setStatementEndBalance] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!startDate || !endDate) {
      setError(t('selectBothDates'));
      return;
    }

    const startDateObj = new Date(startDate);
    const endDateObj = new Date(endDate);

    if (startDateObj > endDateObj) {
      setError(t('startDateBeforeEndDate'));
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await apiClient.createAkahuReconciliation({
        accountId,
        startDate: format(startDateObj, 'yyyy-MM-dd'),
        endDate: format(endDateObj, 'yyyy-MM-dd'),
        statementEndBalance: statementEndBalance ? parseFloat(statementEndBalance) : undefined,
        notes: notes || undefined
      });

      toast.success(`Fetched ${result.matchingResult.totalBankTransactions} transactions from Akahu`);
      onSuccess(result);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch from Akahu';
      setError(errorMessage);
      onError?.(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h4 className="text-sm font-medium text-gray-700 mb-2">{t('reconcileWithAkahu')}</h4>
        <p className="text-sm text-gray-500 mb-4">
          {t('akahuDescription')}
        </p>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="flex items-start gap-3">
          <CloudArrowDownIcon className="w-5 h-5 text-blue-600 mt-0.5" />
          <div>
            <h4 className="font-medium text-blue-900">{t('howItWorks')}</h4>
            <p className="text-sm text-blue-700 mt-1">
              {t('akahuHowItWorksDesc')}
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <DateTimePicker
            label={t('startDate')}
            value={startDate}
            onChange={setStartDate}
            placeholder={t('selectStartDate')}
            showTime={false}
          />
        </div>
        <div>
          <DateTimePicker
            label={t('endDate')}
            value={endDate}
            onChange={setEndDate}
            placeholder={t('selectEndDate')}
            showTime={false}
          />
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-2">
          {t('statementBalanceOptional')}
        </label>
        <input
          type="number"
          step="0.01"
          value={statementEndBalance}
          onChange={(e) => setStatementEndBalance(e.target.value)}
          className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          placeholder={t('enterStatementBalance')}
        />
        <p className="text-xs text-gray-500 mt-1">
          {t('akahuBalanceNote')}
        </p>
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-2">
          {t('notesOptional')}
        </label>
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={2}
          className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          placeholder={t('notesPlaceholder')}
        />
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <div className="flex items-start gap-3">
            <ExclamationCircleIcon className="w-5 h-5 text-red-600 mt-0.5" />
            <div>
              <h4 className="font-medium text-red-900">{t('error')}</h4>
              <p className="text-sm text-red-700 mt-1">{error}</p>
            </div>
          </div>
        </div>
      )}

      <Button
        onClick={handleSubmit}
        disabled={loading || !startDate || !endDate}
        className="w-full flex items-center justify-center gap-2"
      >
        {loading ? (
          <>
            <ArrowPathIcon className="w-4 h-4 animate-spin" />
            {t('fetchingFromAkahu')}
          </>
        ) : (
          <>
            <CloudArrowDownIcon className="w-4 h-4" />
            {t('fetchFromBank')}
          </>
        )}
      </Button>
    </div>
  );
}
