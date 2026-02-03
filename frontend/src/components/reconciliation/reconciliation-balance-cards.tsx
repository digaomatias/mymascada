'use client';

import { Card, CardContent } from '@/components/ui/card';
import { 
  BanknotesIcon, 
  ComputerDesktopIcon, 
  ScaleIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon 
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { cn } from '@/lib/utils';
import { useTranslations } from 'next-intl';

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
}

interface SystemTransaction {
  id: number;
  amount: number;
  description: string;
  transactionDate: string;
  categoryName?: string;
  status: number;
}

interface ReconciliationBalanceCardsProps {
  statementEndBalance: number;
  unmatchedBankTransactions: Array<{ bankTransaction?: BankTransaction }>;
  unmatchedSystemTransactions: Array<{ systemTransaction?: SystemTransaction }>;
  currency?: string;
}

export function ReconciliationBalanceCards({
  statementEndBalance,
  unmatchedBankTransactions,
  unmatchedSystemTransactions
}: ReconciliationBalanceCardsProps) {
  const t = useTranslations('reconciliation');
  
  // Calculate unmatched bank transactions total
  const unmatchedBankTotal = unmatchedBankTransactions.reduce((sum, item) => {
    return sum + (item.bankTransaction?.amount || 0);
  }, 0);

  // Calculate unmatched system transactions total
  const unmatchedSystemTotal = unmatchedSystemTransactions.reduce((sum, item) => {
    return sum + (item.systemTransaction?.amount || 0);
  }, 0);

  // Calculate expected balance after accounting for unmatched bank transactions
  const expectedBalance = statementEndBalance - unmatchedBankTotal;

  // Calculate system balance discrepancy
  const systemBalance = statementEndBalance - unmatchedSystemTotal;
  const discrepancy = Math.abs(expectedBalance - systemBalance);

  // Determine reconciliation status
  const isReconciled = discrepancy < 0.01; // Within 1 cent tolerance
  const hasSignificantDiscrepancy = discrepancy > 1.00; // More than $1 difference

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
      {/* Statement Balance Card */}
      <Card className="bg-blue-50 border-blue-200">
        <CardContent className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-blue-600 font-medium">{t('balanceCards.statementBalance')}</p>
              <p className="text-2xl font-bold text-blue-800">
                {formatCurrency(statementEndBalance)}
              </p>
            </div>
            <BanknotesIcon className="h-8 w-8 text-blue-600" />
          </div>
        </CardContent>
      </Card>

      {/* Unmatched Bank Difference Card */}
      <Card className={cn(
        "border-2",
        unmatchedBankTotal !== 0 
          ? "bg-red-50 border-red-200" 
          : "bg-green-50 border-green-200"
      )}>
        <CardContent className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className={cn(
                "text-sm font-medium",
                unmatchedBankTotal !== 0 ? "text-red-600" : "text-green-600"
              )}>
                {t('balanceCards.unmatchedBank')}
              </p>
              <p className={cn(
                "text-2xl font-bold",
                unmatchedBankTotal !== 0 ? "text-red-800" : "text-green-800"
              )}>
                {formatCurrency(unmatchedBankTotal)}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                {t('balanceCards.expectedBalance', { amount: formatCurrency(expectedBalance) })}
              </p>
            </div>
            <div className={cn(
              "h-8 w-8",
              unmatchedBankTotal !== 0 ? "text-red-600" : "text-green-600"
            )}>
              {unmatchedBankTotal !== 0 ? (
                <ExclamationTriangleIcon />
              ) : (
                <CheckCircleIcon />
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Unmatched System Difference Card */}
      <Card className={cn(
        "border-2",
        unmatchedSystemTotal !== 0 
          ? "bg-yellow-50 border-yellow-200" 
          : "bg-green-50 border-green-200"
      )}>
        <CardContent className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className={cn(
                "text-sm font-medium",
                unmatchedSystemTotal !== 0 ? "text-yellow-600" : "text-green-600"
              )}>
                {t('balanceCards.unmatchedSystem')}
              </p>
              <p className={cn(
                "text-2xl font-bold",
                unmatchedSystemTotal !== 0 ? "text-yellow-800" : "text-green-800"
              )}>
                {formatCurrency(unmatchedSystemTotal)}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                {t('balanceCards.systemBalance', { amount: formatCurrency(systemBalance) })}
              </p>
            </div>
            <ComputerDesktopIcon className={cn(
              "h-8 w-8",
              unmatchedSystemTotal !== 0 ? "text-yellow-600" : "text-green-600"
            )} />
          </div>
        </CardContent>
      </Card>

      {/* Reconciliation Status Card */}
      <Card className={cn(
        "border-2",
        isReconciled 
          ? "bg-green-50 border-green-200"
          : hasSignificantDiscrepancy 
            ? "bg-red-50 border-red-200"
            : "bg-yellow-50 border-yellow-200"
      )}>
        <CardContent className="p-4">
          <div className="flex items-center justify-between">
            <div>
              <p className={cn(
                "text-sm font-medium",
                isReconciled 
                  ? "text-green-600"
                  : hasSignificantDiscrepancy 
                    ? "text-red-600"
                    : "text-yellow-600"
              )}>
                {t('balanceCards.discrepancy')}
              </p>
              <p className={cn(
                "text-2xl font-bold",
                isReconciled 
                  ? "text-green-800"
                  : hasSignificantDiscrepancy 
                    ? "text-red-800"
                    : "text-yellow-800"
              )}>
                {formatCurrency(discrepancy)}
              </p>
              <p className="text-xs text-gray-500 mt-1">
                {isReconciled ? t('balanceCards.reconciled') : t('balanceCards.needsReview')}
              </p>
            </div>
            <ScaleIcon className={cn(
              "h-8 w-8",
              isReconciled 
                ? "text-green-600"
                : hasSignificantDiscrepancy 
                  ? "text-red-600"
                  : "text-yellow-600"
            )} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
