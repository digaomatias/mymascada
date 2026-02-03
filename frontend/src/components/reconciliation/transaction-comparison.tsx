'use client';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { 
  ChevronDownIcon,
  ChevronUpIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon,
  XMarkIcon,
  ArrowsRightLeftIcon
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { useTranslations } from 'next-intl';

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

interface TransactionComparisonProps {
  bankTransaction: BankTransaction;
  systemTransaction: SystemTransaction;
  matchConfidence: number;
  matchMethod?: number;
  className?: string;
  expanded?: boolean;
  onToggleExpanded?: () => void;
}

export function TransactionComparison({ 
  bankTransaction, 
  systemTransaction, 
  matchConfidence,
  className = '',
  expanded = false,
  onToggleExpanded
}: TransactionComparisonProps) {
  const t = useTranslations('reconciliation');
  const tTransactions = useTranslations('transactions');
  
  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.9) return 'text-green-600 bg-green-50 border-green-200';
    if (confidence >= 0.75) return 'text-yellow-600 bg-yellow-50 border-yellow-200';
    if (confidence >= 0.6) return 'text-orange-600 bg-orange-50 border-orange-200';
    return 'text-red-600 bg-red-50 border-red-200';
  };

  const getConfidenceLabel = (confidence: number) => {
    if (confidence >= 0.95) return t('transactionComparison.confidence.excellent');
    if (confidence >= 0.85) return t('transactionComparison.confidence.good');
    if (confidence >= 0.75) return t('transactionComparison.confidence.fair');
    if (confidence >= 0.6) return t('transactionComparison.confidence.possible');
    return t('transactionComparison.confidence.weak');
  };

  const getConfidenceIcon = (confidence: number) => {
    if (confidence >= 0.8) return <CheckCircleIcon className="w-4 h-4" />;
    if (confidence >= 0.6) return <ExclamationTriangleIcon className="w-4 h-4" />;
    return <XMarkIcon className="w-4 h-4" />;
  };

  const getStatusLabel = (status: number): string => {
    switch (status) {
      case 1: return tTransactions('status.pending');
      case 2: return tTransactions('status.cleared');
      case 3: return tTransactions('status.reconciled');
      case 4: return tTransactions('status.cancelled');
      default: return tTransactions('status.unknown');
    }
  };

  // Calculate differences
  const amountDiff = Math.abs(bankTransaction.amount - systemTransaction.amount);
  const dateDiff = Math.abs(new Date(bankTransaction.transactionDate).getTime() - new Date(systemTransaction.transactionDate).getTime());
  const dateDiffDays = Math.ceil(dateDiff / (1000 * 60 * 60 * 24));
  
  // Description similarity analysis
  const bankDesc = bankTransaction.description.toLowerCase().trim();
  const systemDesc = systemTransaction.description.toLowerCase().trim();
  const descriptionSimilar = bankDesc.includes(systemDesc) || systemDesc.includes(bankDesc) || 
    levenshteinDistance(bankDesc, systemDesc) / Math.max(bankDesc.length, systemDesc.length) < 0.3;

  const highlightDifferences = (text1: string, text2: string, isBank: boolean) => {
    const words1 = text1.toLowerCase().split(/\s+/);
    const words2 = text2.toLowerCase().split(/\s+/);
    const originalWords = (isBank ? text1 : text2).split(/\s+/);
    
    return originalWords.map((word, index) => {
      const lowerWord = word.toLowerCase();
      const hasMatch = (isBank ? words2 : words1).some(w => 
        w === lowerWord || w.includes(lowerWord) || lowerWord.includes(w)
      );
      
      return (
        <span 
          key={index}
          className={hasMatch ? 'text-green-700 bg-green-100 px-1 rounded' : 'text-red-700 bg-red-100 px-1 rounded'}
        >
          {word}
        </span>
      );
    });
  };

  return (
    <Card className={`${className} border-l-4 ${matchConfidence >= 0.8 ? 'border-l-yellow-400' : 'border-l-orange-400'}`}>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <ArrowsRightLeftIcon className="w-5 h-5 text-gray-500" />
            <CardTitle className="text-lg">{t('transactionComparison.title')}</CardTitle>
            <div className={`flex items-center gap-1 px-2 py-1 rounded-full border text-xs font-medium ${getConfidenceColor(matchConfidence)}`}>
              {getConfidenceIcon(matchConfidence)}
              {(matchConfidence * 100).toFixed(0)}% - {getConfidenceLabel(matchConfidence)}
            </div>
          </div>
          {onToggleExpanded && (
            <Button variant="ghost" size="sm" onClick={onToggleExpanded}>
              {expanded ? (
                <ChevronUpIcon className="w-4 h-4" />
              ) : (
                <ChevronDownIcon className="w-4 h-4" />
              )}
            </Button>
          )}
        </div>
      </CardHeader>
      
      <CardContent>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Bank Transaction */}
          <div className="space-y-4">
            <div className="flex items-center gap-2 mb-3">
              <div className="w-3 h-3 bg-blue-500 rounded-full"></div>
              <h4 className="font-medium text-gray-900">{t('transactionComparison.bankStatement')}</h4>
            </div>
            
            <div className="space-y-3 p-4 bg-blue-50 rounded-lg border border-blue-200">
              <div>
                <span className="text-sm text-gray-600">{t('transactionComparison.fields.description')}</span>
                <div className="font-medium mt-1">
                  {expanded ? 
                    <div className="space-x-1">{highlightDifferences(bankTransaction.description, systemTransaction.description, true)}</div>
                    : bankTransaction.description
                  }
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.amount')}</span>
                  <div className={`font-medium ${
                    amountDiff === 0 ? 'text-green-600' : 'text-red-600'
                  }`}>
                    {formatCurrency(bankTransaction.amount)}
                    {amountDiff > 0 && expanded && (
                      <span className="text-xs text-red-500 block">
                        {t('transactionComparison.diff', { amount: formatCurrency(amountDiff) })}
                      </span>
                    )}
                  </div>
                </div>
                
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.date')}</span>
                  <div className={`font-medium ${
                    dateDiffDays === 0 ? 'text-green-600' : 'text-orange-600'
                  }`}>
                    {new Date(bankTransaction.transactionDate).toLocaleDateString()}
                    {dateDiffDays > 0 && expanded && (
                      <span className="text-xs text-orange-500 block">
                        {t('transactionComparison.dateDiff', { count: dateDiffDays })}
                      </span>
                    )}
                  </div>
                </div>
              </div>
              
              {bankTransaction.bankCategory && (
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.bankCategory')}</span>
                  <div className="font-medium">{bankTransaction.bankCategory}</div>
                </div>
              )}
            </div>
          </div>

          {/* System Transaction */}
          <div className="space-y-4">
            <div className="flex items-center gap-2 mb-3">
              <div className="w-3 h-3 bg-purple-500 rounded-full"></div>
              <h4 className="font-medium text-gray-900">{t('transactionComparison.system')}</h4>
            </div>
            
            <div className="space-y-3 p-4 bg-purple-50 rounded-lg border border-purple-200">
              <div>
                <span className="text-sm text-gray-600">{t('transactionComparison.fields.description')}</span>
                <div className="font-medium mt-1">
                  {expanded ? 
                    <div className="space-x-1">{highlightDifferences(systemTransaction.description, bankTransaction.description, false)}</div>
                    : systemTransaction.description
                  }
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.amount')}</span>
                  <div className={`font-medium ${
                    amountDiff === 0 ? 'text-green-600' : 'text-red-600'
                  }`}>
                    {formatCurrency(systemTransaction.amount)}
                  </div>
                </div>
                
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.date')}</span>
                  <div className={`font-medium ${
                    dateDiffDays === 0 ? 'text-green-600' : 'text-orange-600'
                  }`}>
                    {new Date(systemTransaction.transactionDate).toLocaleDateString()}
                  </div>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                {systemTransaction.categoryName && (
                  <div>
                    <span className="text-sm text-gray-600">{t('transactionComparison.fields.category')}</span>
                    <div className="font-medium">{systemTransaction.categoryName}</div>
                  </div>
                )}
                
                <div>
                  <span className="text-sm text-gray-600">{t('transactionComparison.fields.status')}</span>
                  <div className="font-medium">
                    {getStatusLabel(systemTransaction.status)}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Detailed Analysis (when expanded) */}
        {expanded && (
          <div className="mt-6 p-4 bg-gray-50 rounded-lg border">
            <h5 className="font-medium text-gray-900 mb-3">{t('transactionComparison.matchAnalysis')}</h5>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div className="flex items-center gap-2">
                <div className={`w-2 h-2 rounded-full ${amountDiff === 0 ? 'bg-green-500' : 'bg-red-500'}`}></div>
                <span className="text-gray-600">{t('transactionComparison.analysis.amountMatch')}</span>
                <span className={amountDiff === 0 ? 'text-green-600 font-medium' : 'text-red-600 font-medium'}>
                  {amountDiff === 0
                    ? t('transactionComparison.analysis.exact')
                    : t('transactionComparison.analysis.offBy', { amount: formatCurrency(amountDiff) })}
                </span>
              </div>
              
              <div className="flex items-center gap-2">
                <div className={`w-2 h-2 rounded-full ${dateDiffDays === 0 ? 'bg-green-500' : dateDiffDays <= 2 ? 'bg-yellow-500' : 'bg-red-500'}`}></div>
                <span className="text-gray-600">{t('transactionComparison.analysis.dateMatch')}</span>
                <span className={`font-medium ${
                  dateDiffDays === 0 ? 'text-green-600' : dateDiffDays <= 2 ? 'text-yellow-600' : 'text-red-600'
                }`}>
                  {dateDiffDays === 0
                    ? t('transactionComparison.analysis.sameDay')
                    : t('transactionComparison.analysis.daysApart', { count: dateDiffDays })}
                </span>
              </div>
              
              <div className="flex items-center gap-2">
                <div className={`w-2 h-2 rounded-full ${descriptionSimilar ? 'bg-green-500' : 'bg-red-500'}`}></div>
                <span className="text-gray-600">{t('transactionComparison.analysis.description')}</span>
                <span className={`font-medium ${descriptionSimilar ? 'text-green-600' : 'text-red-600'}`}>
                  {descriptionSimilar
                    ? t('transactionComparison.analysis.similar')
                    : t('transactionComparison.analysis.different')}
                </span>
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// Helper function to calculate Levenshtein distance
function levenshteinDistance(str1: string, str2: string): number {
  const matrix = Array(str2.length + 1).fill(null).map(() => Array(str1.length + 1).fill(null));

  for (let i = 0; i <= str1.length; i++) {
    matrix[0][i] = i;
  }

  for (let j = 0; j <= str2.length; j++) {
    matrix[j][0] = j;
  }

  for (let j = 1; j <= str2.length; j++) {
    for (let i = 1; i <= str1.length; i++) {
      const substitutionCost = str1[i - 1] === str2[j - 1] ? 0 : 1;
      matrix[j][i] = Math.min(
        matrix[j][i - 1] + 1, // deletion
        matrix[j - 1][i] + 1, // insertion
        matrix[j - 1][i - 1] + substitutionCost // substitution
      );
    }
  }

  return matrix[str2.length][str1.length];
}
