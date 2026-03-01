'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { 
  CheckCircleIcon, 
  ExclamationTriangleIcon,
  ArrowPathIcon,
  EyeIcon,
  AdjustmentsHorizontalIcon,
  ArrowLeftIcon
} from '@heroicons/react/24/outline';
import { toast } from 'sonner';
import { BankTransaction } from './reconciliation-file-upload';

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

interface CSVMappingReviewForReconciliationProps {
  analysisResult: CSVAnalysisResult;
  csvFile: File;
  onTransactionsExtracted: (transactions: BankTransaction[]) => void;
  onBack?: () => void;
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
  currency: string;
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

export function CSVMappingReviewForReconciliation({ 
  analysisResult, 
  csvFile,
  onTransactionsExtracted,
  onBack
}: CSVMappingReviewForReconciliationProps) {
  const tCommon = useTranslations('common');
  const tImport = useTranslations('import');
  const tToasts = useTranslations('toasts');
  // Map AI convention to dropdown value
  const mapAmountConvention = (aiConvention: string): string => {
    switch (aiConvention) {
      case 'negative-debits':
        return 'negative-expense';
      case 'positive-debits':
        return 'positive-expense';
      case 'unsigned_with_type':
        return 'type-column';
      default:
        return aiConvention;
    }
  };
  
  const [mappings, setMappings] = useState<ColumnMappings>({
    dateFormat: analysisResult.dateFormats[0] || 'MM/dd/yyyy',
    amountConvention: mapAmountConvention(analysisResult.amountConvention),
    currency: analysisResult.detectedCurrency || 'USD'
  });
  const [isExtracting, setIsExtracting] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<PreviewDataItem[]>([]);
  const [uniqueTypeValues, setUniqueTypeValues] = useState<string[]>([]);
  const [fullCsvRows, setFullCsvRows] = useState<Record<string, string>[]>([]);
  const [isLoadingFullData, setIsLoadingFullData] = useState(false);

  // Parse full CSV file when component mounts
  useEffect(() => {
    const parseFullCsvFile = async () => {
      try {
        setIsLoadingFullData(true);
        const text = await csvFile.text();
        const lines = text.split('\n').filter(line => line.trim());
        
        if (lines.length === 0) {
          toast.error(tToasts('csvMappingReviewCsvEmpty'));
          return;
        }
        
        // Simple CSV parser that handles quoted fields
        const parseCSVLine = (line: string): string[] => {
          const result: string[] = [];
          let current = '';
          let inQuotes = false;
          
          for (let i = 0; i < line.length; i++) {
            const char = line[i];
            const nextChar = line[i + 1];
            
            if (char === '"') {
              if (inQuotes && nextChar === '"') {
                // Escaped quote
                current += '"';
                i++; // Skip next quote
              } else {
                // Toggle quote state
                inQuotes = !inQuotes;
              }
            } else if (char === ',' && !inQuotes) {
              // End of field
              result.push(current.trim());
              current = '';
            } else {
              current += char;
            }
          }
          
          // Don't forget the last field
          result.push(current.trim());
          return result;
        };
        
        // Parse header
        const headers = parseCSVLine(lines[0]);
        
        // Parse all data rows
        const allRows: Record<string, string>[] = [];
        for (let i = 1; i < lines.length; i++) {
          const values = parseCSVLine(lines[i]);
          if (values.length !== headers.length) {
            console.warn(`Row ${i} has ${values.length} values but expected ${headers.length}`);
            continue;
          }
          const row: Record<string, string> = {};
          headers.forEach((header, index) => {
            row[header] = values[index] || '';
          });
          allRows.push(row);
        }
        
        setFullCsvRows(allRows);
        toast.info(tToasts('csvMappingReviewLoaded', { count: allRows.length }));
      } catch (error) {
        console.error('Failed to parse full CSV file:', error);
        toast.error(tToasts('csvMappingReviewParseFailed'));
      } finally {
        setIsLoadingFullData(false);
      }
    };
    
    parseFullCsvFile();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [csvFile]);

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
      amountConvention: mapAmountConvention(analysisResult.amountConvention),
      currency: analysisResult.detectedCurrency || 'USD'
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

  // Extract unique type values when type column is selected
  useEffect(() => {
    if (mappings.typeColumn && analysisResult.sampleRows.length > 0) {
      const typeValues = analysisResult.sampleRows
        .map(row => row[mappings.typeColumn!])
        .filter((value, index, arr) => value && arr.indexOf(value) === index)
        .sort();
      setUniqueTypeValues(typeValues);
      
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

  const handlePreview = async () => {
    try {
      setIsExtracting(true);
      
      // Validate required mappings locally
      if (!mappings.dateColumn || !mappings.amountColumn || !mappings.descriptionColumn) {
        toast.error(tToasts('csvMappingReviewMissingColumns'));
        return;
      }
      
      // Generate preview from the analysis result data
      const previewRows = analysisResult.sampleRows.slice(0, 10).map((row) => {
        // Parse amount based on the convention
        let amount = 0;
        const amountStr = mappings.amountColumn ? row[mappings.amountColumn] : '0';
        const cleanAmount = amountStr.replace(/[,$]/g, '').trim();
        amount = parseFloat(cleanAmount) || 0;
        
        // Determine transaction type based on amount convention
        let transactionType = 'expense';
        const displayAmount = Math.abs(amount); // Always show positive amount for display
        
        if (mappings.amountConvention === 'negative-expense' || mappings.amountConvention === 'negative-debits') {
          // Standard convention: negative = expense, positive = income
          transactionType = amount >= 0 ? 'income' : 'expense';
        } else if (mappings.amountConvention === 'positive-expense' || mappings.amountConvention === 'positive-debits') {
          // Credit card convention: positive = expense, negative = income (reversed)
          transactionType = amount >= 0 ? 'expense' : 'income';
        } else if ((mappings.amountConvention === 'type-column' || mappings.amountConvention === 'unsigned_with_type') && mappings.typeColumn) {
          const typeValue = row[mappings.typeColumn];
          if (mappings.typeValueMappings?.incomeValues.includes(typeValue)) {
            transactionType = 'income';
          } else if (mappings.typeValueMappings?.expenseValues.includes(typeValue)) {
            transactionType = 'expense';
          }
        } else {
          // For unsigned amounts without type column, default to expense
          transactionType = 'expense';
        }
        
        // Parse and format date for preview
        const rawDate = mappings.dateColumn ? row[mappings.dateColumn] : '';
        let formattedDate = rawDate;
        if (rawDate) {
          const parsedDate = parseDateWithFormat(rawDate, mappings.dateFormat);
          if (parsedDate) {
            const parsedDisplay = parsedDate.toISOString().split('T')[0].split('-').reverse().join('/');
            formattedDate = tImport('aiCsvReconciliation.mappingReview.datePreviewFormatted', {
              rawDate,
              parsedDate: parsedDisplay
            });
          } else {
            formattedDate = tImport('aiCsvReconciliation.mappingReview.datePreviewInvalid', { rawDate });
          }
        }
        
        return {
          original: row,
          mapped: {
            date: formattedDate,
            displayAmount: formatAmount(displayAmount, mappings.currency),
            transactionType,
            description: mappings.descriptionColumn ? row[mappings.descriptionColumn] : '',
            type: mappings.typeColumn ? row[mappings.typeColumn] : ''
          }
        };
      });
      
      setPreviewData(previewRows);
      setShowPreview(true);
      toast.success(tToasts('csvMappingReviewPreviewGenerated', { count: previewRows.length }));
      
    } catch (error) {
      console.error('Preview error:', error);
      toast.error(tToasts('csvMappingReviewPreviewFailed'));
    } finally {
      setIsExtracting(false);
    }
  };
  
  // Helper function to format amount
  const formatAmount = (amount: number, currency: string = 'USD'): string => {
    const symbol = currency === 'USD' ? '$' : currency;
    return `${symbol}${Math.abs(amount).toFixed(2)}`;
  };

  const handleExtractTransactions = async () => {
    try {
      setIsExtracting(true);
      
      // Validate required mappings
      if (!mappings.dateColumn || !mappings.amountColumn || !mappings.descriptionColumn) {
        toast.error(tToasts('csvMappingReviewMissingColumns'));
        return;
      }
      
      // Use full CSV rows if available, otherwise fall back to sample rows
      const rowsToProcess = fullCsvRows.length > 0 ? fullCsvRows : analysisResult.sampleRows;
      
      // Extract transactions from all rows
      const extractedTransactions: BankTransaction[] = rowsToProcess.map((row, index) => {
        // Extract values based on mappings
        const dateValue = row[mappings.dateColumn!];
        const amountStr = row[mappings.amountColumn!];
        const descriptionValue = row[mappings.descriptionColumn!];
        const referenceValue = mappings.referenceColumn ? row[mappings.referenceColumn] : '';
        
        // Parse amount
        const cleanAmount = amountStr.replace(/[,$]/g, '').trim();
        let amount = parseFloat(cleanAmount) || 0;
        
        // Handle amount convention
        if (mappings.amountConvention === 'negative-expense' || mappings.amountConvention === 'negative-debits') {
          // Standard convention: negative = expense, positive = income
          // Keep the sign as is
        } else if (mappings.amountConvention === 'positive-expense' || mappings.amountConvention === 'positive-debits') {
          // Credit card convention: positive = expense, negative = income (reversed)
          // Keep the sign as is, we'll handle the type determination later
        } else if ((mappings.amountConvention === 'type-column' || mappings.amountConvention === 'unsigned_with_type') && mappings.typeColumn) {
          const typeValue = row[mappings.typeColumn];
          const isIncome = mappings.typeValueMappings?.incomeValues.includes(typeValue);
          const isExpense = mappings.typeValueMappings?.expenseValues.includes(typeValue);
          
          if (isExpense) {
            amount = -Math.abs(amount);
          } else if (isIncome) {
            amount = Math.abs(amount);
          } else {
            // Default to expense if not mapped
            amount = -Math.abs(amount);
          }
        } else if (mappings.amountConvention === 'unsigned_debit_credit') {
          // For debit/credit convention, negative amounts are usually expenses
          // This would need additional logic based on specific bank formats
          amount = -Math.abs(amount);
        }
        
        // Parse date value using the specified format and convert to ISO string
        let isoDate = new Date().toISOString();
        if (dateValue) {
          try {
            const parsedDate = parseDateWithFormat(dateValue, mappings.dateFormat);
            if (parsedDate && !isNaN(parsedDate.getTime())) {
              isoDate = parsedDate.toISOString();
            } else {
              console.warn(`Could not parse date value: ${dateValue} with format: ${mappings.dateFormat}, using current date`);
            }
          } catch (e) {
            console.warn(`Failed to parse date: ${dateValue}`, e);
          }
        }
        
        return {
          bankTransactionId: `CSV_${Date.now()}_${index}`,
          amount,
          transactionDate: isoDate,
          description: descriptionValue || tImport('aiCsvReconciliation.mappingReview.defaultDescription', { index: index + 1 }),
          bankCategory: mappings.categoryColumn ? row[mappings.categoryColumn] : tImport('aiCsvReconciliation.mappingReview.csvImportCategory'),
          reference: referenceValue
        };
      });
      
      toast.success(tToasts('csvMappingReviewExtracted', { count: extractedTransactions.length }));
      onTransactionsExtracted(extractedTransactions);
      
    } catch (error) {
      console.error('Extraction error:', error);
      toast.error(tToasts('csvMappingReviewExtractFailed'));
    } finally {
      setIsExtracting(false);
    }
  };

  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.8) return 'text-green-600';
    if (confidence >= 0.6) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getConfidenceIcon = (confidence: number) => {
    if (confidence >= 0.8) return <CheckCircleIcon className="w-4 h-4" />;
    return <ExclamationTriangleIcon className="w-4 h-4" />;
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <AdjustmentsHorizontalIcon className="w-6 h-6 text-purple-600" />
              <div>
                <CardTitle>{tImport('aiCsvReconciliation.mappingReview.title')}</CardTitle>
                <p className="text-sm text-gray-600 mt-1">
                  {tImport('aiCsvReconciliation.mappingReview.subtitle')}
                </p>
              </div>
            </div>
            {onBack && (
              <Button variant="secondary" size="sm" onClick={onBack} className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                {tCommon('back')}
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex items-start gap-3">
              <CheckCircleIcon className="w-5 h-5 text-blue-600 mt-0.5" />
              <div>
                <h4 className="font-medium text-blue-900">
                  {tImport('aiCsvReconciliation.mappingReview.detectedFormat', { format: analysisResult.detectedBankFormat })}
                </h4>
                <p className="text-sm text-blue-700 mt-1">
                  {tImport('aiCsvReconciliation.mappingReview.detectedFormatHelp')}
                </p>
                <p className="text-sm text-blue-700 mt-2 font-medium">
                  {isLoadingFullData ? (
                    <span className="flex items-center gap-2">
                      <ArrowPathIcon className="w-4 h-4 animate-spin" />
                      {tImport('aiCsvReconciliation.mappingReview.loadingFullData')}
                    </span>
                  ) : (
                    <span className="flex items-center gap-2">
                      <CheckCircleIcon className="w-4 h-4" />
                      {fullCsvRows.length > 0 
                        ? tImport('aiCsvReconciliation.mappingReview.readyToExtract', { count: fullCsvRows.length })
                        : tImport('aiCsvReconciliation.mappingReview.usingSample', { count: analysisResult.sampleRows.length })
                      }
                    </span>
                  )}
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Column Mappings */}
      <Card>
        <CardHeader>
          <CardTitle>{tImport('aiCsvReconciliation.mappingReview.columnMappingsTitle')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Date Column */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsvReconciliation.mappingReview.dateColumn')}
                {analysisResult.suggestedMappings.date && (
                  <span className={`ml-2 inline-flex items-center gap-1 text-xs ${getConfidenceColor(analysisResult.suggestedMappings.date.confidence)}`}>
                    {getConfidenceIcon(analysisResult.suggestedMappings.date.confidence)}
                    {tImport('aiCsvReconciliation.mappingReview.confidentPercent', { percent: Math.round(analysisResult.suggestedMappings.date.confidence * 100) })}
                  </span>
                )}
              </label>
              <Select
                value={mappings.dateColumn || ''}
                onChange={(e) => handleMappingChange('dateColumn', e.target.value)}
                placeholder={tImport('aiCsvReconciliation.mappingReview.selectDateColumn')}
              >
                {analysisResult.availableColumns.map(column => (
                  <option key={column} value={column}>{column}</option>
                ))}
              </Select>
            </div>

            {/* Amount Column */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsvReconciliation.mappingReview.amountColumn')}
                {analysisResult.suggestedMappings.amount && (
                  <span className={`ml-2 inline-flex items-center gap-1 text-xs ${getConfidenceColor(analysisResult.suggestedMappings.amount.confidence)}`}>
                    {getConfidenceIcon(analysisResult.suggestedMappings.amount.confidence)}
                    {tImport('aiCsvReconciliation.mappingReview.confidentPercent', { percent: Math.round(analysisResult.suggestedMappings.amount.confidence * 100) })}
                  </span>
                )}
              </label>
              <Select
                value={mappings.amountColumn || ''}
                onChange={(e) => handleMappingChange('amountColumn', e.target.value)}
                data-testid="amount-column"
                placeholder={tImport('aiCsvReconciliation.mappingReview.selectAmountColumn')}
              >
                {analysisResult.availableColumns.map(column => (
                  <option key={column} value={column}>{column}</option>
                ))}
              </Select>
            </div>

            {/* Description Column */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsvReconciliation.mappingReview.descriptionColumn')}
                {analysisResult.suggestedMappings.description && (
                  <span className={`ml-2 inline-flex items-center gap-1 text-xs ${getConfidenceColor(analysisResult.suggestedMappings.description.confidence)}`}>
                    {getConfidenceIcon(analysisResult.suggestedMappings.description.confidence)}
                    {tImport('aiCsvReconciliation.mappingReview.confidentPercent', { percent: Math.round(analysisResult.suggestedMappings.description.confidence * 100) })}
                  </span>
                )}
              </label>
              <Select
                value={mappings.descriptionColumn || ''}
                onChange={(e) => handleMappingChange('descriptionColumn', e.target.value)}
                placeholder={tImport('aiCsvReconciliation.mappingReview.selectDescriptionColumn')}
              >
                {analysisResult.availableColumns.map(column => (
                  <option key={column} value={column}>{column}</option>
                ))}
              </Select>
            </div>

            {/* Reference Column */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {tImport('aiCsvReconciliation.mappingReview.referenceColumn')}
              </label>
              <Select
                value={mappings.referenceColumn || ''}
                onChange={(e) => handleMappingChange('referenceColumn', e.target.value)}
                placeholder={tImport('aiCsvReconciliation.mappingReview.selectReferenceColumn')}
              >
                {analysisResult.availableColumns.map(column => (
                  <option key={column} value={column}>{column}</option>
                ))}
              </Select>
            </div>
          </div>

          {/* Date Format */}
          <div className="mt-6">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              {tImport('aiCsvReconciliation.mappingReview.dateFormat')}
              <span className="ml-2 text-xs text-gray-500">
                {tImport('aiCsvReconciliation.mappingReview.autoDetectedFormat', { format: mappings.dateFormat })}
              </span>
            </label>
            <div className="text-xs text-blue-600 mb-2">
              {tImport('aiCsvReconciliation.mappingReview.dateFormatHelp', { format: mappings.dateFormat })}
            </div>
            <Select
              value={mappings.dateFormat}
              onChange={(e) => handleMappingChange('dateFormat', e.target.value)}
              className="max-w-md"
              data-testid="date-format"
            >
              <option value="MM/dd/yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.mmDdYyyy')}</option>
              <option value="dd/MM/yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.ddMmYyyy')}</option>
              <option value="yyyy-MM-dd">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.yyyyMmDd')}</option>
              <option value="MM-dd-yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.mmDdYyyyDash')}</option>
              <option value="dd-MM-yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.ddMmYyyyDash')}</option>
              <option value="M/d/yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.mDyyyy')}</option>
              <option value="d/M/yyyy">{tImport('aiCsvReconciliation.mappingReview.dateFormatOptions.dMyyyy')}</option>
            </Select>
          </div>

          {/* Amount Convention */}
          <div className="mt-6">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              {tImport('aiCsvReconciliation.mappingReview.amountConvention')}
            </label>
            <Select
              value={mappings.amountConvention}
              onChange={(e) => handleMappingChange('amountConvention', e.target.value)}
              className="max-w-md"
              data-testid="amount-convention"
            >
              <option value="negative-expense">{tImport('aiCsvReconciliation.mappingReview.amountConventionOptions.negativeExpense')}</option>
              <option value="positive-expense">{tImport('aiCsvReconciliation.mappingReview.amountConventionOptions.positiveExpense')}</option>
              <option value="type-column">{tImport('aiCsvReconciliation.mappingReview.amountConventionOptions.typeColumn')}</option>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Type Value Mappings (if using type column) */}
      {mappings.typeColumn && uniqueTypeValues.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>{tImport('aiCsvReconciliation.mappingReview.typeValueMappingTitle')}</CardTitle>
            <p className="text-sm text-gray-600">
              {tImport('aiCsvReconciliation.mappingReview.typeValueMappingSubtitle')}
            </p>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {uniqueTypeValues.map(value => {
                const isIncome = mappings.typeValueMappings?.incomeValues.includes(value);
                const isExpense = mappings.typeValueMappings?.expenseValues.includes(value);
                
                return (
                  <div key={value} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                    <span className="font-medium">{value}</span>
                    <div className="flex gap-2">
                      <Button
                        variant={isIncome ? "primary" : "secondary"}
                        size="sm"
                        onClick={() => handleTypeValueToggle(value, 'income')}
                      >
                        {tCommon('income')}
                      </Button>
                      <Button
                        variant={isExpense ? "primary" : "secondary"}
                        size="sm"
                        onClick={() => handleTypeValueToggle(value, 'expense')}
                      >
                        {tCommon('expense')}
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Actions */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex flex-col sm:flex-row gap-3 justify-end">
            <Button
              variant="secondary"
              onClick={handlePreview}
              disabled={isExtracting || !mappings.dateColumn || !mappings.amountColumn || !mappings.descriptionColumn}
              className="flex items-center gap-2"
            >
              <EyeIcon className="w-4 h-4" />
              {tImport('aiCsvReconciliation.mappingReview.previewButton')}
            </Button>
            
            <Button
              onClick={handleExtractTransactions}
              disabled={isExtracting || !mappings.dateColumn || !mappings.amountColumn || !mappings.descriptionColumn}
              className="flex items-center gap-2"
            >
              {isExtracting && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
              {tImport('aiCsvReconciliation.mappingReview.extractButton')}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Preview Data */}
      {showPreview && previewData.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>{tImport('aiCsvReconciliation.mappingReview.previewTitle')}</CardTitle>
            <p className="text-sm text-gray-600">
              {tImport('aiCsvReconciliation.mappingReview.previewSubtitle')}
            </p>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="min-w-full border border-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-2 text-left text-sm font-medium text-gray-700 border-b w-1/3">
                      {tImport('aiCsvReconciliation.mappingReview.tableHeaders.datePreview')}
                    </th>
                    <th className="px-4 py-2 text-left text-sm font-medium text-gray-700 border-b">{tCommon('amount')}</th>
                    <th className="px-4 py-2 text-left text-sm font-medium text-gray-700 border-b">{tCommon('description')}</th>
                    <th className="px-4 py-2 text-left text-sm font-medium text-gray-700 border-b">{tCommon('type')}</th>
                  </tr>
                </thead>
                <tbody>
                  {previewData.slice(0, 10).map((row, index) => (
                    <tr key={index} className="border-b">
                      <td className="px-4 py-2 text-sm">{row.mapped.date}</td>
                      <td className="px-4 py-2 text-sm font-medium">{row.mapped.displayAmount}</td>
                      <td className="px-4 py-2 text-sm">{row.mapped.description}</td>
                      <td className="px-4 py-2 text-sm">
                        <span className={`px-2 py-1 text-xs rounded-full ${
                          row.mapped.transactionType === 'income' 
                            ? 'bg-green-100 text-green-800' 
                            : 'bg-red-100 text-red-800'
                        }`}>
                          {row.mapped.transactionType === 'income' ? tCommon('income') : tCommon('expense')}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {previewData.length > 10 && (
              <p className="text-sm text-gray-500 mt-2">
                {tImport('aiCsvReconciliation.mappingReview.moreRows', { count: previewData.length - 10 })}
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
