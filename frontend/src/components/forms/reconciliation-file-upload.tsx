'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { 
  SparklesIcon,
  DocumentTextIcon
} from '@heroicons/react/24/outline';
import { AICSVUploadForReconciliation } from './ai-csv-upload-reconciliation';
import { OFXUploadForReconciliation } from './ofx-upload-reconciliation';
import { useFeatures } from '@/contexts/features-context';
import { useTranslations } from 'next-intl';

export interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
  reference?: string;
}

interface ReconciliationFileUploadProps {
  onTransactionsExtracted: (transactions: BankTransaction[]) => void;
}

type UploadMode = 'csv' | 'ofx';

export function ReconciliationFileUpload({
  onTransactionsExtracted
}: ReconciliationFileUploadProps) {
  const t = useTranslations('reconciliation');
  const { features } = useFeatures();
  const [mode, setMode] = useState<UploadMode>(features.aiCategorization ? 'csv' : 'ofx');

  return (
    <div className="space-y-6">
      {/* Mode Toggle */}
      <Card>
        <CardHeader>
          <CardTitle>{t('importMethod.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex gap-4">
            {features.aiCategorization && (
              <Button
                variant={mode === 'csv' ? 'primary' : 'secondary'}
                onClick={() => setMode('csv')}
                className="flex-1 h-20 flex-col"
              >
                <SparklesIcon className="w-6 h-6 mb-2" />
                <div>
                  <div className="font-medium">{t('importMethod.aiCsv.title')}</div>
                  <div className="text-xs opacity-80">{t('importMethod.aiCsv.subtitle')}</div>
                </div>
              </Button>
            )}

            <Button
              variant={mode === 'ofx' ? 'primary' : 'secondary'}
              onClick={() => setMode('ofx')}
              className="flex-1 h-20 flex-col"
            >
              <DocumentTextIcon className="w-6 h-6 mb-2" />
              <div>
                <div className="font-medium">{t('importMethod.ofx.title')}</div>
                <div className="text-xs opacity-80">{t('importMethod.ofx.subtitle')}</div>
              </div>
            </Button>
          </div>
          
          <div className="mt-4 p-3 bg-gray-50 rounded-lg">
            <div className="text-sm">
              {mode === 'csv' && features.aiCategorization ? (
                <>
                  <strong>{t('importMethod.aiCsv.inlineTitle')}</strong> {t('importMethod.aiCsv.description')}
                </>
              ) : (
                <>
                  <strong>{t('importMethod.ofx.inlineTitle')}</strong> {t('importMethod.ofx.description')}
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Upload Component */}
      {mode === 'csv' ? (
        <AICSVUploadForReconciliation
          onTransactionsExtracted={onTransactionsExtracted}
        />
      ) : (
        <OFXUploadForReconciliation
          onTransactionsExtracted={onTransactionsExtracted}
        />
      )}
    </div>
  );
}
