'use client';

import { useState, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { 
  DocumentTextIcon, 
  ArrowPathIcon,
  CheckCircleIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { BankTransaction } from './reconciliation-file-upload';
import { formatCurrency } from '@/lib/utils';

interface OFXUploadForReconciliationProps {
  onTransactionsExtracted: (transactions: BankTransaction[]) => void;
}

interface OFXValidationResult {
  success: boolean;
  message: string;
  transactions?: Array<{
    transactionId: string;
    amount: number;
    transactionDate: string;
    description: string;
    memo: string;
    transactionType: string;
    checkNumber?: string;
    referenceNumber?: string;
  }>;
  accountInfo?: {
    accountId: string;
    bankName: string;
    accountType: string;
  };
  totalTransactions?: number;
  dateRange?: {
    startDate: string;
    endDate: string;
  };
}

export function OFXUploadForReconciliation({ 
  onTransactionsExtracted 
}: OFXUploadForReconciliationProps) {
  const tCommon = useTranslations('common');
  const tReconciliation = useTranslations('reconciliation');
  const tToasts = useTranslations('toasts');
  const [isProcessing, setIsProcessing] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const [validationResult, setValidationResult] = useState<OFXValidationResult | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileUpload = async (file: File) => {
    if (!file) return;

    // Validate file type
    const fileExtension = file.name.split('.').pop()?.toLowerCase();
    if (!['ofx', 'qfx'].includes(fileExtension || '')) {
      toast.error(tToasts('ofxUploadInvalidType'));
      return;
    }

    // Validate file size (10MB limit)
    if (file.size > 10 * 1024 * 1024) {
      toast.error(tToasts('ofxUploadFileTooLarge'));
      return;
    }

    try {
      setIsProcessing(true);
      
      // Use the enhanced OFX validation endpoint with transaction details
      const result = await apiClient.validateOfxFile(file, true) as OFXValidationResult;
      
      if (!result.success) {
        toast.error(result.message || tToasts('ofxUploadProcessFailed'));
        return;
      }

      setValidationResult(result);
      
      // Show success message with details
      const transactionCount = result.transactions?.length || 0;
      toast.success(tToasts('ofxUploadProcessed', { count: transactionCount }));
      
      if (result.accountInfo) {
        toast.info(tToasts('ofxUploadAccountInfo', {
          bank: result.accountInfo.bankName,
          accountId: result.accountInfo.accountId
        }));
      }

    } catch (error) {
      console.error('OFX processing error:', error);
      toast.error(tToasts('ofxUploadProcessFailedRetry'));
    } finally {
      setIsProcessing(false);
    }
  };

  const handleExtractTransactions = () => {
    if (!validationResult || !validationResult.transactions) {
      toast.error(tToasts('ofxUploadNoTransactions'));
      return;
    }

    // Convert OFX transactions to BankTransaction format
    const bankTransactions: BankTransaction[] = validationResult.transactions.map((transaction, index) => {
      // Ensure proper ISO date format
      let isoDate = new Date().toISOString();
      if (transaction.transactionDate) {
        try {
          const parsedDate = new Date(transaction.transactionDate);
          if (!isNaN(parsedDate.getTime())) {
            isoDate = parsedDate.toISOString();
          }
        } catch (e) {
          console.warn(`Failed to parse OFX date: ${transaction.transactionDate}`, e);
        }
      }
      
      return {
        bankTransactionId: transaction.transactionId || `OFX_${Date.now()}_${index}`,
        amount: transaction.amount || 0,
        transactionDate: isoDate,
        description: transaction.description || transaction.memo || tReconciliation('ofxUpload.defaultDescription'),
        bankCategory: transaction.transactionType || tReconciliation('ofxUpload.defaultCategory'),
        reference: transaction.checkNumber || transaction.referenceNumber || ''
      };
    });

    toast.success(tToasts('ofxUploadExtracted', { count: bankTransactions.length }));
    onTransactionsExtracted(bankTransactions);
  };

  const handleFileSelect = () => {
    fileInputRef.current?.click();
  };

  const handleFileInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      handleFileUpload(file);
    }
    // Reset so the same file can be re-selected
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);

    const files = e.dataTransfer.files;
    if (files.length > 0) {
      handleFileUpload(files[0]);
    }
  };

  const handleStartOver = () => {
    setValidationResult(null);
  };

  if (validationResult) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <DocumentTextIcon className="w-5 h-5 text-green-600" />
            {tReconciliation('ofxUpload.processedTitle')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-6">
            {/* Success Summary */}
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <div className="flex items-start gap-3">
                <CheckCircleIcon className="w-5 h-5 text-green-600 mt-0.5" />
                <div>
                  <h4 className="font-medium text-green-900">{tReconciliation('ofxUpload.successTitle')}</h4>
                  <p className="text-sm text-green-700 mt-1">
                    {tReconciliation('ofxUpload.successSubtitle')}
                  </p>
                </div>
              </div>
            </div>

            {/* File Details */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="bg-white border rounded-lg p-4">
                <div className="flex items-center gap-2 mb-2">
                  <DocumentTextIcon className="w-4 h-4 text-gray-600" />
                  <span className="font-medium text-gray-900">{tReconciliation('ofxUpload.transactions')}</span>
                </div>
                <div className="text-2xl font-bold text-gray-900">
                  {validationResult.transactions?.length || 0}
                </div>
                <div className="text-sm text-gray-600">{tReconciliation('ofxUpload.foundInFile')}</div>
              </div>

              {validationResult.dateRange && (
                <div className="bg-white border rounded-lg p-4">
                  <div className="flex items-center gap-2 mb-2">
                    <DocumentTextIcon className="w-4 h-4 text-gray-600" />
                    <span className="font-medium text-gray-900">{tReconciliation('ofxUpload.dateRange')}</span>
                  </div>
                  <div className="text-sm text-gray-900">
                    {validationResult.dateRange.startDate.split('T')[0].split('-').reverse().join('/')} - {' '}
                    {validationResult.dateRange.endDate.split('T')[0].split('-').reverse().join('/')}
                  </div>
                </div>
              )}
            </div>

            {/* Account Info */}
            {validationResult.accountInfo && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <h4 className="font-medium text-blue-900 mb-2">{tReconciliation('ofxUpload.accountInfoTitle')}</h4>
                <div className="space-y-1 text-sm text-blue-700">
                  <div><strong>{tReconciliation('ofxUpload.accountInfo.bank')}</strong> {validationResult.accountInfo.bankName}</div>
                  <div><strong>{tReconciliation('ofxUpload.accountInfo.accountId')}</strong> {validationResult.accountInfo.accountId}</div>
                  <div><strong>{tReconciliation('ofxUpload.accountInfo.accountType')}</strong> {validationResult.accountInfo.accountType}</div>
                </div>
              </div>
            )}

            {/* Sample Transactions Preview */}
            {validationResult.transactions && validationResult.transactions.length > 0 && (
              <div>
                <h4 className="font-medium text-gray-900 mb-3">{tReconciliation('ofxUpload.previewTitle')}</h4>
                <div className="border rounded-lg overflow-hidden">
                  <table className="min-w-full">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-4 py-2 text-left text-sm font-medium text-gray-700">{tCommon('date')}</th>
                        <th className="px-4 py-2 text-left text-sm font-medium text-gray-700">{tCommon('amount')}</th>
                        <th className="px-4 py-2 text-left text-sm font-medium text-gray-700">{tCommon('description')}</th>
                        <th className="px-4 py-2 text-left text-sm font-medium text-gray-700">{tCommon('type')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {validationResult.transactions.slice(0, 5).map((transaction, index) => (
                        <tr key={index} className="border-t">
                          <td className="px-4 py-2 text-sm">
                            {transaction.transactionDate.split('T')[0].split('-').reverse().join('/')}
                          </td>
                          <td className="px-4 py-2 text-sm font-medium">
                            <span className={transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'}>
                              {formatCurrency(Math.abs(transaction.amount))}
                            </span>
                          </td>
                          <td className="px-4 py-2 text-sm">
                            {transaction.description || transaction.memo}
                          </td>
                          <td className="px-4 py-2 text-sm">
                            <span className="px-2 py-1 text-xs rounded-full bg-gray-100 text-gray-800">
                              {transaction.transactionType}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {validationResult.transactions.length > 5 && (
                    <div className="px-4 py-2 bg-gray-50 text-sm text-gray-600">
                      {tReconciliation('ofxUpload.moreTransactions', { count: validationResult.transactions.length - 5 })}
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex flex-col sm:flex-row gap-3 justify-end">
              <Button
                variant="secondary"
                onClick={handleStartOver}
              >
                {tReconciliation('ofxUpload.uploadDifferent')}
              </Button>
              
              <Button
                onClick={handleExtractTransactions}
                className="flex items-center gap-2"
              >
                <CheckCircleIcon className="w-4 h-4" />
                {tReconciliation('ofxUpload.extractButton')}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <input
        ref={fileInputRef}
        type="file"
        accept=".ofx,.qfx"
        onChange={handleFileInputChange}
        className="hidden"
      />
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <DocumentTextIcon className="w-5 h-5 text-blue-600" />
          {tReconciliation('ofxUpload.uploadTitle')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {/* Info Section */}
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex items-start gap-3">
              <DocumentTextIcon className="w-5 h-5 text-blue-600 mt-0.5" />
              <div>
                <h4 className="font-medium text-blue-900">{tReconciliation('ofxUpload.infoTitle')}</h4>
                <p className="text-sm text-blue-700 mt-1">
                  {tReconciliation('ofxUpload.infoSubtitle')}
                </p>
              </div>
            </div>
          </div>

          {/* Upload Area */}
          <div
            className={`border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors ${
              dragActive 
                ? 'border-blue-400 bg-blue-50' 
                : 'border-gray-300 hover:border-blue-400 hover:bg-gray-50'
            }`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={handleFileSelect}
          >
            {isProcessing ? (
              <div className="flex flex-col items-center">
                <ArrowPathIcon className="w-12 h-12 text-blue-600 animate-spin mb-4" />
                <h4 className="text-lg font-medium text-gray-900 mb-2">
                  {tReconciliation('ofxUpload.processingTitle')}
                </h4>
                <p className="text-gray-600">
                  {tReconciliation('ofxUpload.processingSubtitle')}
                </p>
              </div>
            ) : (
              <div className="flex flex-col items-center">
                <DocumentTextIcon className="w-12 h-12 text-gray-400 mb-4" />
                <h4 className="text-lg font-medium text-gray-900 mb-2">
                  {tReconciliation('ofxUpload.uploadPrompt')}
                </h4>
                <p className="text-gray-600 mb-4">
                  {tReconciliation('ofxUpload.uploadHelp')}
                </p>
                <Button 
                  variant="secondary" 
                  onClick={(e) => {
                    e.stopPropagation();
                    handleFileSelect();
                  }}
                >
                  {tCommon('chooseFile')}
                </Button>
              </div>
            )}
          </div>

          {/* File Requirements */}
          <div className="text-xs text-gray-500 space-y-1">
            <p>{tReconciliation('ofxUpload.requirements.format')}</p>
            <p>{tReconciliation('ofxUpload.requirements.maxSize')}</p>
            <p>{tReconciliation('ofxUpload.requirements.compatibility')}</p>
            <p>{tReconciliation('ofxUpload.requirements.reconciliationOnly')}</p>
          </div>

          {/* OFX Features */}
          <div className="bg-gradient-to-r from-blue-50 to-green-50 rounded-lg p-4">
            <h4 className="font-medium text-gray-900 mb-2">{tReconciliation('ofxUpload.featuresTitle')}</h4>
            <ul className="text-sm text-gray-700 space-y-1">
              <li>{tReconciliation('ofxUpload.features.standardized')}</li>
              <li>{tReconciliation('ofxUpload.features.noMapping')}</li>
              <li>{tReconciliation('ofxUpload.features.detailsIncluded')}</li>
              <li>{tReconciliation('ofxUpload.features.accountInfo')}</li>
              <li>{tReconciliation('ofxUpload.features.mostAccurate')}</li>
            </ul>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
