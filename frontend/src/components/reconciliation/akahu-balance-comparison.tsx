'use client';

import { InformationCircleIcon, CheckCircleIcon, ExclamationTriangleIcon } from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { useTranslations } from 'next-intl';

interface AkahuBalanceComparisonProps {
  akahuBalance: number;
  myMascadaBalance: number;
  difference: number;
  isBalanced: boolean;
  isCurrentBalance?: boolean;
}

export function AkahuBalanceComparison({
  akahuBalance,
  myMascadaBalance,
  difference,
  isBalanced,
  isCurrentBalance = true
}: AkahuBalanceComparisonProps) {
  const t = useTranslations('reconciliation');
  return (
    <div className={`rounded-lg border p-4 ${
      isBalanced
        ? 'bg-green-50 border-green-200'
        : 'bg-amber-50 border-amber-200'
    }`}>
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-2">
          {isBalanced ? (
            <CheckCircleIcon className="w-5 h-5 text-green-600" />
          ) : (
            <ExclamationTriangleIcon className="w-5 h-5 text-amber-600" />
          )}
          <h4 className={`font-medium ${
            isBalanced ? 'text-green-900' : 'text-amber-900'
          }`}>
            {t('akahuBalanceComparison.title')}
          </h4>
        </div>
        <div className="group relative">
          <InformationCircleIcon className="w-4 h-4 text-gray-400 cursor-help" />
          <div className="absolute right-0 top-6 z-10 hidden group-hover:block w-64 p-2 bg-gray-900 text-white text-xs rounded-lg shadow-lg">
            {isCurrentBalance
              ? t('akahuBalanceComparison.tooltip.currentBalance')
              : t('akahuBalanceComparison.tooltip.statementBalance')
            }
          </div>
        </div>
      </div>

      <div className="mt-4 space-y-3">
        <div className="flex justify-between items-center">
          <span className="text-sm text-gray-600">{t('akahuBalanceComparison.bankBalance')}</span>
          <span className="font-medium text-gray-900">
            {formatCurrency(akahuBalance)}
          </span>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-sm text-gray-600">{t('akahuBalanceComparison.systemBalance')}</span>
          <span className="font-medium text-gray-900">
            {formatCurrency(myMascadaBalance)}
          </span>
        </div>

        <div className="border-t border-gray-200 pt-3">
          <div className="flex justify-between items-center">
            <span className="text-sm font-medium text-gray-700">{t('akahuBalanceComparison.difference')}</span>
            <span className={`font-semibold ${
              isBalanced
                ? 'text-green-600'
                : difference > 0
                  ? 'text-red-600'
                  : 'text-amber-600'
            }`}>
              {difference === 0
                ? t('akahuBalanceComparison.balanced')
                : `${difference > 0 ? '+' : ''}${formatCurrency(difference)}`
              }
            </span>
          </div>
        </div>
      </div>

      {!isBalanced && (
        <p className={`mt-3 text-xs ${
          isBalanced ? 'text-green-700' : 'text-amber-700'
        }`}>
          {Math.abs(difference) < 1
            ? t('akahuBalanceComparison.minorDifference')
            : t('akahuBalanceComparison.reviewUnmatched')
          }
        </p>
      )}

      {isCurrentBalance && (
        <p className="mt-2 text-xs text-gray-500 italic">
          {t('akahuBalanceComparison.currentBalanceNote')}
        </p>
      )}
    </div>
  );
}
