'use client';

import { useState, useEffect, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import {
  CheckCircleIcon, 
  ExclamationTriangleIcon,
  ArrowPathIcon,
  EyeIcon,
  AdjustmentsHorizontalIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import { formatCurrency } from '@/lib/utils';

// Date parsing utility function
const parseDateWithFormat = (dateStr: string, format: string): Date | null => {
  if (!dateStr || !format) return null;
  
  const cleanDateStr = dateStr.trim();
  
  try {
    // Handle different separators
    const separators = ['/', '-', '.'];
    let parts: string[] = [];
    
    for (const sep of separators) {
      if (cleanDateStr.includes(sep)) {
        parts = cleanDateStr.split(sep);
        break;
      }
    }
    
    if (parts.length !== 3) {
      // Fallback to JavaScript's built-in parsing, but ensure UTC
      const fallbackDate = new Date(cleanDateStr + 'T12:00:00.000Z');
      return isNaN(fallbackDate.getTime()) ? null : fallbackDate;
    }
    
    // Parse based on format
    let year: number, month: number, day: number;
    
    if (format.startsWith('yyyy')) {
      // yyyy-MM-dd or yyyy/MM/dd
      year = parseInt(parts[0]);
      month = parseInt(parts[1]) - 1; // JavaScript months are 0-based
      day = parseInt(parts[2]);
    } else if (format.startsWith('MM') || format.startsWith('M')) {
      // MM/dd/yyyy or M/d/yyyy (US format)
      month = parseInt(parts[0]) - 1;
      day = parseInt(parts[1]);
      year = parseInt(parts[2]);
    } else if (format.startsWith('dd') || format.startsWith('d')) {
      // dd/MM/yyyy or d/M/yyyy (International format)
      day = parseInt(parts[0]);
      month = parseInt(parts[1]) - 1;
      year = parseInt(parts[2]);
    } else {
      // Fallback to JavaScript's built-in parsing, but ensure UTC
      const fallbackDate = new Date(cleanDateStr + 'T12:00:00.000Z');
      return isNaN(fallbackDate.getTime()) ? null : fallbackDate;
    }
    
    // Handle 2-digit years
    if (year < 100) {
      year += year < 50 ? 2000 : 1900;
    }
    
    const parsedDate = new Date(Date.UTC(year, month, day, 12, 0, 0));
    return isNaN(parsedDate.getTime()) ? null : parsedDate;
    
  } catch (e) {
    console.warn('Date parsing error:', e);
    return null;
  }
};

interface CSVMappingReviewProps {
  analysisResult: CSVAnalysisResult;
  file: File;
  onImportComplete?: (result: ImportResult) => void;
  onBack?: () => void;
  accountId?: number;
  accountName?: string;
}

interface CSVAnalysisResult {
  success: boolean;
  suggestedMappings: Record<string, {
    csvColumnName: string;
    targetField: string;
    confidence: number;
    interpretation: string;
    sampleValues: string[];
  }>;
  sampleRows: Record<string, string>[];
  confidenceScores: Record<string, number>;
  detectedBankFormat: string;
  detectedCurrency?: string;
  dateFormats: string[];
  amountConvention: string;
  availableColumns: string[];
  warnings: string[];
}

interface ImportResult {
  isSuccess: boolean;
  message: string;
  importedTransactionsCount: number;
  skippedTransactionsCount: number;
  duplicateTransactionsCount: number;
  warnings: string[];
  errors: string[];
  createdAccountId?: number;
  updatedMappings?: {
    amountColumn?: string;
    dateColumn?: string;
    descriptionColumn?: string;
    referenceColumn?: string;
    typeColumn?: string;
    dateFormat: string;
    amountConvention: string;
    currency?: string;
    typeValueMappings?: {
      incomeValues: string[];
      expenseValues: string[];
    };
  };
}

interface ColumnMappings {
  dateColumn?: string;
  amountColumn?: string;
  descriptionColumn?: string;
  typeColumn?: string;
  balanceColumn?: string;
  referenceColumn?: string;
  categoryColumn?: string;
  dateFormat: string;
  amountConvention: string;
  typeValueMappings?: {
    incomeValues: string[];
    expenseValues: string[];
  };
}

interface PreviewDataItem {
  original: Record<string, unknown>;
  mapped: {
    date: string;
    displayAmount: string;
    transactionType: string;
    description: string;
    type?: string;
  };
}

export function CSVMappingReview({ 
  analysisResult, 
  file,
  onImportComplete,
  onBack,
  accountId: initialAccountId,
  accountName: initialAccountName
}: CSVMappingReviewProps) {
  const tCommon = useTranslations('common');
  const tImport = useTranslations('import');
  const tToasts = useTranslations('toasts');
  // Helper function to detect likely date format from sample data
  const detectDateFormat = useCallback((sampleDates: string[]): string => {
    // Look for obvious ISO format first
    if (sampleDates.some(date => /^\d{4}-\d{1,2}-\d{1,2}$/.test(date))) {
      return 'yyyy-MM-dd';
    }

    // For ambiguous cases, check if any dates have day > 12 (which would indicate dd/MM format)
    const hasHighDay = sampleDates.some(date => {
      const parts = date.split(/[\/\-]/);
      if (parts.length === 3) {
        const firstPart = parseInt(parts[0]);
        const secondPart = parseInt(parts[1]);
        return firstPart > 12 || (secondPart <= 12 && firstPart > secondPart && firstPart > 12);
      }
      return false;
    });

    if (hasHighDay) {
      return sampleDates[0]?.includes('/') ? 'dd/MM/yyyy' : 'dd-MM-yyyy';
    }

    // Default to AI suggestion or US format
    return analysisResult.dateFormats[0] || 'MM/dd/yyyy';
  }, [analysisResult.dateFormats]);

  const [mappings, setMappings] = useState<ColumnMappings>({
    dateFormat: analysisResult.dateFormats[0] || 'MM/dd/yyyy',
    amountConvention: analysisResult.amountConvention
  });
  const [isImporting, setIsImporting] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<PreviewDataItem[]>([]);
  const [accounts, setAccounts] = useState<Array<{id: number, name: string, type: string}>>([]);
  const [selectedAccountId, setSelectedAccountId] = useState<number | undefined>(initialAccountId);
  const [newAccountName, setNewAccountName] = useState<string>(initialAccountName || '');
  const [uniqueTypeValues, setUniqueTypeValues] = useState<string[]>([]);

  // Initialize mappings from AI analysis
  useEffect(() => {
    // Extract sample dates for format detection
    const sampleDates = analysisResult.sampleRows
      .map(row => {
        const dateField = Object.entries(analysisResult.suggestedMappings)
          .find(([field]) => field === 'date')?.[1]?.csvColumnName;
        return dateField ? row[dateField] : '';
      })
      .filter(date => date && date.trim())
      .slice(0, 10);

    const detectedFormat = detectDateFormat(sampleDates);

    const initialMappings: ColumnMappings = {
      dateFormat: detectedFormat,
      amountConvention: analysisResult.amountConvention
    };

    // Map AI suggestions to form fields
    Object.entries(analysisResult.suggestedMappings).forEach(([field, mapping]) => {
      switch (field) {
        case 'date':
          initialMappings.dateColumn = mapping.csvColumnName;
          break;
        case 'amount':
          initialMappings.amountColumn = mapping.csvColumnName;
          break;
        case 'description':
          initialMappings.descriptionColumn = mapping.csvColumnName;
          break;
        case 'type':
          initialMappings.typeColumn = mapping.csvColumnName;
          break;
        case 'balance':
          initialMappings.balanceColumn = mapping.csvColumnName;
          break;
        case 'reference':
          initialMappings.referenceColumn = mapping.csvColumnName;
          break;
        case 'category':
          initialMappings.categoryColumn = mapping.csvColumnName;
          break;
      }
    });

    setMappings(initialMappings);
  }, [analysisResult, detectDateFormat]);

  // Load accounts
  useEffect(() => {
    const loadAccounts = async () => {
      try {
        const accountsData = await apiClient.getAccounts();
        setAccounts(accountsData as Array<{id: number, name: string, type: string}>);
      } catch (error) {
        console.error('Error loading accounts:', error);
      }
    };

    loadAccounts();
  }, []);

  // Extract unique type values when type column is selected
  useEffect(() => {
    if (mappings.typeColumn) {
      // First try to get the distinct values from the AI analysis if the type column was detected
      const typeMapping = Object.values(analysisResult.suggestedMappings).find(
        mapping => mapping.targetField === 'type' && mapping.csvColumnName === mappings.typeColumn
      );
      
      if (typeMapping && typeMapping.sampleValues && typeMapping.sampleValues.length > 0) {
        // Use the complete list of distinct values provided by the backend, ensuring uniqueness
        const uniqueValues = [...new Set(typeMapping.sampleValues)].sort();
        console.log('Type values from backend:', typeMapping.sampleValues);
        console.log('Unique type values:', uniqueValues);
        setUniqueTypeValues(uniqueValues);
      } else if (analysisResult.sampleRows.length > 0) {
        // Fallback to extracting from sample rows if not provided by backend
        const typeValues = analysisResult.sampleRows
          .map(row => row[mappings.typeColumn!])
          .filter((value, index, arr) => value && arr.indexOf(value) === index)
          .sort();
        setUniqueTypeValues(typeValues);
      } else {
        setUniqueTypeValues([]);
      }
      
      // Initialize type value mappings if not already set
      if (!mappings.typeValueMappings) {
        setMappings(prev => ({
          ...prev,
          typeValueMappings: {
            incomeValues: [],
            expenseValues: []
          }
        }));
      }
    } else {
      setUniqueTypeValues([]);
    }
  }, [mappings.typeColumn, mappings.typeValueMappings, analysisResult.sampleRows]);

  const handleMappingChange = (field: keyof ColumnMappings, value: string) => {
    setMappings(prev => ({
      ...prev,
      [field]: value
    }));
  };

  const handleTypeValueToggle = (value: string, type: 'income' | 'expense') => {
    setMappings(prev => {
      const currentMappings = prev.typeValueMappings || { incomeValues: [], expenseValues: [] };
      const newMappings = { ...currentMappings };
      
      // Remove from both arrays first
      newMappings.incomeValues = newMappings.incomeValues.filter(v => v !== value);
      newMappings.expenseValues = newMappings.expenseValues.filter(v => v !== value);
      
      // Add to the selected type
      if (type === 'income') {
        newMappings.incomeValues.push(value);
      } else {
        newMappings.expenseValues.push(value);
      }
      
      return {
        ...prev,
        typeValueMappings: newMappings
      };
    });
  };

  // Refresh preview when amount convention or type mappings change
  useEffect(() => {
    if (showPreview) {
      handlePreview();
    }
  }, [mappings.amountConvention, mappings.typeValueMappings]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleImport = async () => {
    try {
      setIsImporting(true);

      // Comprehensive validation
      if (!file) {
        toast.error(tToasts('csvMappingReviewNoFileSelected'));
        return;
      }

      if (file.size === 0) {
        toast.error(tToasts('csvMappingReviewFileEmpty'));
        return;
      }

      if (file.size > 10 * 1024 * 1024) { // 10MB limit
        toast.error(tToasts('csvMappingReviewFileTooLarge'));
        return;
      }

      if (!file.name.toLowerCase().endsWith('.csv')) {
        toast.error(tToasts('csvMappingReviewCsvOnly'));
        return;
      }

      // Validate required mappings
      if (!mappings.dateColumn) {
        toast.error(tToasts('csvMappingReviewDateRequired'));
        return;
      }

      if (!mappings.amountColumn) {
        toast.error(tToasts('csvMappingReviewAmountRequired'));
        return;
      }

      if (!mappings.descriptionColumn) {
        toast.error(tToasts('csvMappingReviewDescriptionRequired'));
        return;
      }

      // Validate account selection
      if (!selectedAccountId && !newAccountName.trim()) {
        toast.error(tToasts('csvMappingReviewAccountRequired'));
        return;
      }

      if (newAccountName.trim() && newAccountName.trim().length < 2) {
        toast.error(tToasts('csvMappingReviewAccountNameTooShort'));
        return;
      }

      // Convert file to base64 with error handling
      const fileContent = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const result = e.target?.result as string;
            if (!result) {
              reject(new Error('Failed to read file content'));
              return;
            }
            const base64 = result.split(',')[1]; // Remove data URL prefix
            if (!base64) {
              reject(new Error('Invalid file format'));
              return;
            }
            resolve(base64);
          } catch (error) {
            reject(error);
          }
        };
        reader.onerror = () => reject(new Error('Failed to read file'));
        reader.readAsDataURL(file);
      });

      // Use Import Review System instead of direct import
      const analysisResult = await apiClient.analyzeImportForReview({
        source: 'csv',
        accountId: selectedAccountId || 0, // Will create account if 0 and accountName provided
        csvData: {
          content: fileContent,
          mappings: {
            DateColumn: mappings.dateColumn || '',
            AmountColumn: mappings.amountColumn || '',
            DescriptionColumn: mappings.descriptionColumn || '',
            ReferenceColumn: mappings.referenceColumn || '',
            TypeColumn: mappings.typeColumn || '',
            BalanceColumn: mappings.balanceColumn || '',
            CategoryColumn: mappings.categoryColumn || '',
            DateFormat: mappings.dateFormat,
            AmountConvention: mappings.amountConvention,
            TypeValueMappings: mappings.typeValueMappings ? {
              IncomeValues: mappings.typeValueMappings.incomeValues || [],
              ExpenseValues: mappings.typeValueMappings.expenseValues || []
            } : undefined
          },
          hasHeader: true
        },
        options: {
          dateToleranceDays: 3,
          amountTolerance: 0.01,
          enableTransferDetection: true,
          conflictDetectionLevel: 'moderate'
        }
      });

      // The backend returns ImportAnalysisResult directly without a 'success' wrapper
      // If we got this far without an exception, the analysis was successful
      if (analysisResult && analysisResult.summary) {
        // Show success message and navigate to Import Review screen
        toast.success(`Analysis complete! Found ${analysisResult.summary.totalCandidates} transactions to review.`);
        
        if (analysisResult.warnings && analysisResult.warnings.length > 0) {
          analysisResult.warnings.forEach(warning => toast.info(warning));
        }

        // Call onImportComplete with the analysis result to navigate to Import Review
        if (onImportComplete) {
          onImportComplete({
            isSuccess: true,
            message: 'Analysis complete - ready for review',
            importedTransactionsCount: 0, // No transactions imported yet
            skippedTransactionsCount: 0,
            duplicateTransactionsCount: analysisResult.summary.exactDuplicates || 0,
            warnings: Array.isArray(analysisResult.warnings) ? analysisResult.warnings : [],
            errors: Array.isArray(analysisResult.errors) ? analysisResult.errors : [],
            // Pass back the selected account ID for existing accounts
            createdAccountId: selectedAccountId || analysisResult.accountId,
            // Pass the current mappings with user selections INCLUDING TypeValueMappings
            updatedMappings: {
              amountColumn: mappings.amountColumn,
              dateColumn: mappings.dateColumn,
              descriptionColumn: mappings.descriptionColumn,
              referenceColumn: mappings.referenceColumn,
              typeColumn: mappings.typeColumn,
              dateFormat: mappings.dateFormat,
              amountConvention: mappings.amountConvention,
              currency: 'USD',
              typeValueMappings: mappings.typeValueMappings ? {
                incomeValues: mappings.typeValueMappings.incomeValues || [],
                expenseValues: mappings.typeValueMappings.expenseValues || []
              } : undefined
            },
            // Pass the analysis result for the Import Review screen
            analysisResult: analysisResult
          } as ImportResult & { analysisResult: any });
        }
      } else {
        toast.error(tToasts('csvMappingReviewInvalidResponse'));
        console.error('Invalid analysis result:', analysisResult);
      }
    } catch (error) {
      console.error('Import analysis error:', error);
      
      // Detailed error handling
      if (error instanceof Error) {
        if (error.message.includes('Network Error') || error.message.includes('fetch')) {
          toast.error(tToasts('networkErrorRetry'));
        } else if (error.message.includes('413')) {
          toast.error(tToasts('csvMappingReviewFileTooLargeProcessing'));
        } else if (error.message.includes('400')) {
          toast.error(tToasts('csvMappingReviewInvalidFormat'));
        } else if (error.message.includes('401') || error.message.includes('403')) {
          toast.error(tToasts('authErrorRefresh'));
        } else if (error.message.includes('500')) {
          toast.error(tToasts('serverErrorTryLater'));
        } else {
          toast.error(tToasts('csvMappingReviewUnexpectedError'));
        }
      } else {
        toast.error(tToasts('csvMappingReviewUnexpectedError'));
      }
      
      // Log detailed error information for debugging
      console.error('Detailed error info:', {
        error,
        fileName: file.name,
        fileSize: file.size,
        mappings,
        selectedAccountId,
        newAccountName
      });
    } finally {
      setIsImporting(false);
    }
  };

  const handlePreview = () => {
    // Generate preview data based on mappings
    const preview = analysisResult.sampleRows.slice(0, 5).map(row => {
      const amount = mappings.amountColumn ? row[mappings.amountColumn] : '';
      const amountValue = parseFloat(amount) || 0;
      
      // Determine transaction type based on amount convention
      let transactionType = 'expense'; // Default
      
      if (mappings.amountConvention === 'negative-expense' || mappings.amountConvention === 'negative-debits') {
        transactionType = amountValue >= 0 ? 'income' : 'expense';
      } else if (mappings.amountConvention === 'positive-expense' || mappings.amountConvention === 'positive-debits') {
        transactionType = amountValue >= 0 ? 'expense' : 'income';
      } else if (mappings.amountConvention === 'type-column' && mappings.typeColumn) {
        const typeValue = row[mappings.typeColumn] || '';
        
        // Use custom type mappings if available
        if (mappings.typeValueMappings) {
          if (mappings.typeValueMappings.incomeValues.includes(typeValue)) {
            transactionType = 'income';
          } else if (mappings.typeValueMappings.expenseValues.includes(typeValue)) {
            transactionType = 'expense';
          } else {
            // Fall back to amount sign if type value not mapped
            transactionType = amountValue >= 0 ? 'income' : 'expense';
          }
        } else {
          // Legacy: use hardcoded patterns (for backwards compatibility)
          const typeLower = typeValue.toLowerCase();
          if (typeLower.includes('credit') || typeLower.includes('deposit') || typeLower.includes('income') || 
              typeLower.includes('pay') || typeLower.includes('refund') || typeLower.includes('transfer in')) {
            transactionType = 'income';
          } else if (typeLower.includes('debit') || typeLower.includes('withdrawal') || typeLower.includes('expense') || 
                     typeLower.includes('payment') || typeLower.includes('purchase') || typeLower.includes('transfer out')) {
            transactionType = 'expense';
          } else {
            // Fall back to amount sign
            transactionType = amountValue >= 0 ? 'income' : 'expense';
          }
        }
      }
      
      // Parse and format date for preview
      const rawDate = mappings.dateColumn ? row[mappings.dateColumn] : '';
      let formattedDate = rawDate;
      if (rawDate) {
        const parsedDate = parseDateWithFormat(rawDate, mappings.dateFormat);
        if (parsedDate) {
          const parsedDisplay = parsedDate.toISOString().split('T')[0].split('-').reverse().join('/');
          formattedDate = tImport('aiCsv.mappingReview.datePreviewFormatted', {
            rawDate,
            parsedDate: parsedDisplay
          });
        } else {
          formattedDate = tImport('aiCsv.mappingReview.datePreviewInvalid', { rawDate });
        }
      }
      
      return {
        original: row,
        mapped: {
          date: formattedDate,
          amount: amount,
          description: mappings.descriptionColumn ? row[mappings.descriptionColumn] : '',
          type: mappings.typeColumn ? row[mappings.typeColumn] : '',
          transactionType: transactionType,
          displayAmount: formatCurrency(Math.abs(amountValue))
        }
      };
    });
    
    setPreviewData(preview);
    setShowPreview(true);
  };

  const getConfidenceBadge = (confidence: number) => {
    if (confidence >= 0.8) {
      return (
        <span className="px-2 py-1 text-xs rounded-full bg-green-100 text-green-800">
          {tImport('aiCsv.mappingReview.confidence.high', { percent: Math.round(confidence * 100) })}
        </span>
      );
    } else if (confidence >= 0.6) {
      return (
        <span className="px-2 py-1 text-xs rounded-full bg-yellow-100 text-yellow-800">
          {tImport('aiCsv.mappingReview.confidence.medium', { percent: Math.round(confidence * 100) })}
        </span>
      );
    } else {
      return (
        <span className="px-2 py-1 text-xs rounded-full bg-red-100 text-red-800">
          {tImport('aiCsv.mappingReview.confidence.low', { percent: Math.round(confidence * 100) })}
        </span>
      );
    }
  };

  const fieldMappings = [
    { key: 'dateColumn', label: tCommon('date'), field: 'date', required: true },
    { key: 'amountColumn', label: tCommon('amount'), field: 'amount', required: true },
    { key: 'descriptionColumn', label: tCommon('description'), field: 'description', required: true },
    { key: 'typeColumn', label: tImport('aiCsv.mappingReview.typeCategory'), field: 'type', required: false },
    { key: 'balanceColumn', label: tCommon('balance'), field: 'balance', required: false },
    { key: 'referenceColumn', label: tImport('aiCsv.mappingReview.referenceColumn'), field: 'reference', required: false }
  ];

  return (
    <div className="space-y-6">
      {/* Analysis Summary */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <CheckCircleIcon className="w-5 h-5 text-green-600" />
            {tImport('aiCsv.mappingReview.analysisComplete')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <h4 className="font-medium text-gray-900">{tImport('aiCsv.mappingReview.detectedFormat')}</h4>
              <p className="text-sm text-gray-600">{analysisResult.detectedBankFormat}</p>
            </div>
            <div>
              <h4 className="font-medium text-gray-900">{tImport('aiCsv.mappingReview.amountConventionTitle')}</h4>
              <p className="text-sm text-gray-600">
                {analysisResult.amountConvention === 'negative-debits' || analysisResult.amountConvention === 'negative-expense'
                  ? tImport('aiCsv.mappingReview.amountConventionSummary.negativeExpense')
                  : analysisResult.amountConvention === 'positive-expense'
                  ? tImport('aiCsv.mappingReview.amountConventionSummary.positiveExpense')
                  : analysisResult.amountConvention === 'type-column'
                  ? tImport('aiCsv.mappingReview.amountConventionSummary.typeColumn')
                  : tImport('aiCsv.mappingReview.amountConventionSummary.allPositive')}
              </p>
            </div>
            <div>
              <h4 className="font-medium text-gray-900">{tImport('aiCsv.mappingReview.dateFormat')}</h4>
              <p className="text-sm text-gray-600">{analysisResult.dateFormats[0] || 'MM/dd/yyyy'}</p>
            </div>
          </div>

          {/* Warnings */}
          {analysisResult.warnings.length > 0 && (
            <div className="mt-4 p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
              <div className="flex items-start gap-2">
                <ExclamationTriangleIcon className="w-5 h-5 text-yellow-600 mt-0.5" />
                <div>
                  <h4 className="font-medium text-yellow-900">{tImport('aiCsv.mappingReview.warningsTitle')}</h4>
                  <ul className="text-sm text-yellow-700 mt-1">
                    {analysisResult.warnings.map((warning, index) => (
                      <li key={index}>• {warning}</li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Account Selection */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <svg className="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2H5a2 2 0 00-2-2v0"/>
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 1v6"/>
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 1v6"/>
            </svg>
            {tImport('aiCsv.mappingReview.selectAccount')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsv.mappingReview.importExistingAccount')}
              </label>
              <Select
                value={selectedAccountId?.toString() || ''}
                onChange={(e) => {
                  setSelectedAccountId(e.target.value ? parseInt(e.target.value) : undefined);
                  if (e.target.value) setNewAccountName(''); // Clear new account name if existing selected
                }}
                placeholder={tImport('aiCsv.mappingReview.selectExistingAccount')}
              >
                {accounts.map(account => (
                  <option key={account.id} value={account.id}>
                    {account.name} ({account.type})
                  </option>
                ))}
              </Select>
            </div>
            
            <div className="flex items-center">
              <div className="flex-1 border-t border-gray-300"></div>
              <span className="px-3 text-sm text-gray-500">{tCommon('or')}</span>
              <div className="flex-1 border-t border-gray-300"></div>
            </div>
            
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsv.mappingReview.createNewAccount')}
              </label>
              <Input
                type="text"
                value={newAccountName}
                onChange={(e) => {
                  setNewAccountName(e.target.value);
                  if (e.target.value) setSelectedAccountId(undefined); // Clear selected account if new name entered
                }}
                placeholder={tImport('aiCsv.mappingReview.newAccountPlaceholder')}
              />
              <p className="text-xs text-gray-500 mt-1">
                {tImport('aiCsv.mappingReview.newAccountHelp')}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Column Mappings */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <AdjustmentsHorizontalIcon className="w-5 h-5 text-purple-600" />
            {tImport('aiCsv.mappingReview.columnMappingsTitle')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {fieldMappings.map(({ key, label, field, required }) => (
              <div key={key} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-center">
                <div>
                  <label className="font-medium text-gray-900">
                    {label}
                    {required && <span className="text-red-500 ml-1">*</span>}
                  </label>
                </div>
                <div>
                  <Select
                    value={typeof mappings[key as keyof ColumnMappings] === 'string' ? mappings[key as keyof ColumnMappings] as string : ''}
                    onChange={(e) => handleMappingChange(key as keyof ColumnMappings, e.target.value)}
                    placeholder={tImport('aiCsv.mappingReview.selectColumn')}
                  >
                    {analysisResult.availableColumns.map(col => (
                      <option key={col} value={col}>{col}</option>
                    ))}
                  </Select>
                </div>
                <div>
                  {analysisResult.suggestedMappings[field] && 
                    getConfidenceBadge(analysisResult.suggestedMappings[field].confidence)}
                </div>
                <div>
                  {analysisResult.suggestedMappings[field] && (
                    <p className="text-xs text-gray-500">
                      {analysisResult.suggestedMappings[field].interpretation}
                    </p>
                  )}
                </div>
              </div>
            ))}
          </div>

          {/* Additional Settings */}
          <div className="mt-6 pt-6 border-t border-gray-200">
            <h4 className="font-medium text-gray-900 mb-4">{tImport('aiCsv.mappingReview.importSettings')}</h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tImport('aiCsv.mappingReview.dateFormat')}
                </label>
                <Select
                  value={mappings.dateFormat}
                  onChange={(e) => handleMappingChange('dateFormat', e.target.value)}
                  data-testid="date-format"
                >
                  <option value="MM/dd/yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.mmDdYyyy')}</option>
                  <option value="dd/MM/yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.ddMmYyyy')}</option>
                  <option value="yyyy-MM-dd">{tImport('aiCsv.mappingReview.dateFormatOptions.yyyyMmDd')}</option>
                  <option value="MM-dd-yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.mmDdYyyyDash')}</option>
                  <option value="dd-MM-yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.ddMmYyyyDash')}</option>
                  <option value="M/d/yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.mDyyyy')}</option>
                  <option value="d/M/yyyy">{tImport('aiCsv.mappingReview.dateFormatOptions.dMyyyy')}</option>
                </Select>
                <p className="text-xs text-gray-500 mt-1.5">
                  {tImport('aiCsv.mappingReview.autoDetectedFormat', { format: mappings.dateFormat })}
                </p>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {tImport('aiCsv.mappingReview.amountConvention')}
                </label>
                <Select
                  value={mappings.amountConvention}
                  onChange={(e) => handleMappingChange('amountConvention', e.target.value)}
                  data-testid="amount-convention"
                >
                  <option value="negative-expense">{tImport('aiCsv.mappingReview.amountConventionOptions.negativeExpense')}</option>
                  <option value="positive-expense">{tImport('aiCsv.mappingReview.amountConventionOptions.positiveExpense')}</option>
                  <option value="type-column">{tImport('aiCsv.mappingReview.amountConventionOptions.typeColumn')}</option>
                </Select>
                <p className="text-xs text-gray-500 mt-1.5">
                  {tImport('aiCsv.mappingReview.amountConventionHelp')}
                </p>
              </div>

              {/* Type Value Mapping - only show when type-column is selected */}
              {mappings.amountConvention === 'type-column' && mappings.typeColumn && uniqueTypeValues.length > 0 && (
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    {tImport('aiCsv.mappingReview.typeValueMappingTitle')}
                  </label>
                  <div className="bg-gray-50 p-4 rounded-lg">
                    <p className="text-sm text-gray-600 mb-3">
                      {tImport('aiCsv.mappingReview.typeValueMappingHelp', { column: mappings.typeColumn })}
                    </p>
                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                      {uniqueTypeValues.map((value, index) => {
                        const isIncome = mappings.typeValueMappings?.incomeValues.includes(value);
                        const isExpense = mappings.typeValueMappings?.expenseValues.includes(value);
                        
                        return (
                          <div key={`${value}-${index}`} className="bg-white p-3 rounded border">
                            <div className="font-medium text-sm mb-2 text-gray-900">&quot;{value}&quot;</div>
                            <div className="flex gap-2">
                              <button
                                type="button"
                                onClick={() => handleTypeValueToggle(value, 'income')}
                                className={`px-3 py-1 text-xs rounded-full font-medium ${
                                  isIncome 
                                    ? 'bg-green-100 text-green-800 border border-green-300' 
                                    : 'bg-gray-100 text-gray-600 border border-gray-300 hover:bg-green-50'
                                }`}
                              >
                                {tCommon('income')}
                              </button>
                              <button
                                type="button"
                                onClick={() => handleTypeValueToggle(value, 'expense')}
                                className={`px-3 py-1 text-xs rounded-full font-medium ${
                                  isExpense 
                                    ? 'bg-red-100 text-red-800 border border-red-300' 
                                    : 'bg-gray-100 text-gray-600 border border-gray-300 hover:bg-red-50'
                                }`}
                              >
                                {tCommon('expense')}
                              </button>
                            </div>
                            {!isIncome && !isExpense && (
                              <div className="text-xs text-yellow-600 mt-1">
                                {tImport('aiCsv.mappingReview.typeValueNotMapped')}
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                </div>
              )}

            </div>
          </div>
        </CardContent>
      </Card>

      {/* Preview */}
      {showPreview && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <EyeIcon className="w-5 h-5 text-blue-600" />
              {tImport('aiCsv.mappingReview.previewTitle')}
            </CardTitle>
            <p className="text-sm text-gray-600 mt-1">
              {tImport('aiCsv.mappingReview.previewSubtitle')}
            </p>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {previewData.map((row, index) => (
                <div key={index} className="grid grid-cols-1 lg:grid-cols-2 gap-4 p-4 border rounded-lg">
                  <div>
                    <h4 className="font-medium text-gray-900 mb-2">{tImport('aiCsv.mappingReview.originalRow')}</h4>
                    <div className="text-sm text-gray-600 space-y-1">
                      {Object.entries(row.original).map(([key, value]) => (
                        <div key={key} className="flex">
                          <span className="font-medium w-24">{key}:</span>
                          <span className="flex-1">{String(value)}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                  <div>
                    <h4 className="font-medium text-gray-900 mb-2">{tImport('aiCsv.mappingReview.willImportAs')}</h4>
                    <div className="text-sm space-y-2">
                      <div className="flex items-center">
                        <span className="font-medium w-24">{tImport('aiCsv.mappingReview.previewLabels.datePreview')}</span>
                        <span className="flex-1 text-sm">{row.mapped.date}</span>
                      </div>
                      <div className="flex items-center">
                        <span className="font-medium w-24">{tCommon('amount')}:</span>
                        <span className="flex-1">{row.mapped.displayAmount}</span>
                      </div>
                      <div className="flex items-center">
                        <span className="font-medium w-24">{tCommon('type')}:</span>
                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                          row.mapped.transactionType === 'income' 
                            ? 'bg-green-100 text-green-800' 
                            : 'bg-red-100 text-red-800'
                        }`}>
                          {row.mapped.transactionType === 'income' ? tCommon('income') : tCommon('expense')}
                        </span>
                      </div>
                      <div className="flex items-center">
                        <span className="font-medium w-24">{tCommon('description')}:</span>
                        <span className="flex-1">{row.mapped.description}</span>
                      </div>
                      {row.mapped.type && (
                        <div className="flex items-center">
                          <span className="font-medium w-24">{tImport('aiCsv.mappingReview.previewLabels.csvType')}</span>
                          <span className="flex-1 text-gray-500">{row.mapped.type}</span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
            
            <div className="mt-4 p-3 bg-blue-50 border border-blue-200 rounded-lg">
              <div className="flex items-start gap-2">
                <svg className="w-5 h-5 text-blue-600 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <div>
                  <h4 className="font-medium text-blue-900">{tImport('aiCsv.mappingReview.amountConventionApplied')}</h4>
                  <p className="text-sm text-blue-700 mt-1">
                    {mappings.amountConvention === 'negative-expense' && 
                      tImport('aiCsv.mappingReview.amountConventionAppliedDescriptions.negativeExpense')}
                    {mappings.amountConvention === 'positive-expense' && 
                      tImport('aiCsv.mappingReview.amountConventionAppliedDescriptions.positiveExpense')}
                    {mappings.amountConvention === 'type-column' && 
                      mappings.typeValueMappings && (mappings.typeValueMappings.incomeValues.length + mappings.typeValueMappings.expenseValues.length > 0) &&
                      tImport('aiCsv.mappingReview.amountConventionAppliedDescriptions.typeColumnCustom')}
                    {mappings.amountConvention === 'type-column' && 
                      (!mappings.typeValueMappings || (mappings.typeValueMappings.incomeValues.length + mappings.typeValueMappings.expenseValues.length === 0)) &&
                      tImport('aiCsv.mappingReview.amountConventionAppliedDescriptions.typeColumnDefault')}
                  </p>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Actions */}
      <div className="flex justify-between">
        <div className="flex gap-2">
          {onBack && (
            <Button variant="secondary" onClick={onBack}>
              {tCommon('back')}
            </Button>
          )}
          <Button 
            variant="secondary" 
            onClick={handlePreview}
            className="flex items-center gap-2"
          >
            <EyeIcon className="w-4 h-4" />
            {tImport('aiCsv.mappingReview.previewButton')}
          </Button>
        </div>
        
        <Button 
          onClick={handleImport}
          disabled={
            isImporting || 
            !mappings.dateColumn || 
            !mappings.amountColumn || 
            !mappings.descriptionColumn ||
            (!selectedAccountId && !newAccountName.trim())
          }
          className="flex items-center gap-2"
        >
          {isImporting && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
          {isImporting ? tImport('aiCsv.mappingReview.analyzing') : tImport('aiCsv.mappingReview.reviewAndImport')}
        </Button>
      </div>
    </div>
  );
}
