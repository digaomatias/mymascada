'use client';

import { useState, Suspense } from 'react';

// Force dynamic rendering since this page uses useSearchParams
export const dynamic = 'force-dynamic';
import { useRouter, useSearchParams } from 'next/navigation';
import { useAuth } from '@/contexts/auth-context';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';
import { AICSVUpload } from '@/components/forms/ai-csv-upload';
import { CSVMappingReview } from '@/components/forms/csv-mapping-review';
import { ImportReviewScreen } from '@/components/import-review/import-review-screen';
import {
  SparklesIcon,
  ArrowLeftIcon,
  CheckCircleIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import { ImportAnalysisResult, ImportExecutionResult } from '@/types/import-review';
import { apiClient } from '@/lib/api-client';
import { useFeatures } from '@/contexts/features-context';
import { useTranslations } from 'next-intl';

type ImportStep = 'upload' | 'review' | 'conflicts' | 'success';

// Helper function to determine correct transaction type based on amount and convention
function determineCorrectTransactionType(amount: number, amountConvention: string, originalType?: number): number {
  // TransactionType enum: Income = 1, Expense = 2
  switch (amountConvention) {
    case 'type-column':
      // When using type column, the type is already determined by the mapping (D->Expense, etc.)
      // Return the original type as it was mapped during the analysis phase
      return originalType || (amount >= 0 ? 1 : 2);
    case 'positive-expense':
      // Positive amounts are expenses, negative are income (credit card style)
      return amount >= 0 ? 2 : 1;
    case 'negative-expense':
    default:
      // Positive amounts are income, negative are expenses (standard bank style)
      return amount >= 0 ? 1 : 2;
  }
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
  mergedTransactionsCount?: number;
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

function AICSVImportContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { features, isLoading: featuresLoading } = useFeatures();
  const t = useTranslations('import');
  const tCommon = useTranslations('common');

  const [currentStep, setCurrentStep] = useState<ImportStep>('upload');
  const [analysisResult, setAnalysisResult] = useState<CSVAnalysisResult | null>(null);
  const [uploadedFile, setUploadedFile] = useState<File | null>(null);
  const [csvContent, setCsvContent] = useState<string | null>(null);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [importAnalysisResult, setImportAnalysisResult] = useState<ImportAnalysisResult | null>(null);
  
  // Get account info from URL params
  const accountId = searchParams.get('accountId') ? parseInt(searchParams.get('accountId')!) : undefined;
  const accountName = searchParams.get('accountName') || undefined;
  const accountType = searchParams.get('accountType') || 'Generic';

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <SparklesIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('aiCsv.loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    router.push('/auth/login');
    return null;
  }

  // Redirect to standard import if AI categorization is not available
  if (!featuresLoading && !features.aiCategorization) {
    router.push('/import');
    return null;
  }

  const handleAnalysisComplete = async (result: CSVAnalysisResult, file: File) => {
    setAnalysisResult(result);
    setUploadedFile(file);
    
    // Read and store the CSV content for later analysis
    try {
      const text = await file.text();
      setCsvContent(text);
    } catch (error) {
      console.error('Failed to read CSV content:', error);
    }
    
    setCurrentStep('review');
  };

  const handleMappingComplete = async (result: ImportResult) => {
    // Store import result for further processing
    setImportResult(result);
    
    if (!csvContent || !analysisResult) {
      console.error('Missing CSV content or analysis result');
      return;
    }
    
    // Use the account ID from the mapping result, fall back to URL param, or use 0 as last resort
    const selectedAccountId = result.createdAccountId || accountId || 0;
    
    try {
      // CRITICAL FIX: Ensure we use the complete mappings from result.updatedMappings
      // If updatedMappings is not available, we should not proceed as it means 
      // the user's configuration (including TypeValueMappings) is missing
      if (!result.updatedMappings) {
        console.error('Updated mappings not available from CSV mapping review');
        throw new Error('Mapping configuration incomplete - please review CSV mappings again');
      }
      
      const mappingsToUse = result.updatedMappings;

      // Transform frontend typeValueMappings to backend format
      const backendMappings = {
        amountColumn: mappingsToUse.amountColumn,
        dateColumn: mappingsToUse.dateColumn,
        descriptionColumn: mappingsToUse.descriptionColumn,
        referenceColumn: mappingsToUse.referenceColumn,
        typeColumn: mappingsToUse.typeColumn,
        dateFormat: mappingsToUse.dateFormat,
        amountConvention: mappingsToUse.amountConvention,
        // Transform typeValueMappings to backend format (capitalize property names)
        typeValueMappings: mappingsToUse.typeValueMappings ? {
          IncomeValues: mappingsToUse.typeValueMappings.incomeValues,
          ExpenseValues: mappingsToUse.typeValueMappings.expenseValues
        } : undefined
      };

      // Analyze CSV for conflicts directly
      const analysisRequest = {
        source: 'csv' as const,
        accountId: selectedAccountId,
        csvData: {
          content: btoa(csvContent), // Base64 encode
          mappings: backendMappings,
          hasHeader: true
        },
        options: {
          dateToleranceDays: 3,
          amountTolerance: 0.01,
          enableTransferDetection: true,
          conflictDetectionLevel: 'moderate' as const
        }
      };

      const analysisResponse = await apiClient.analyzeImportForReview(analysisRequest);

      // Fix transaction types in candidates based on amount convention
      const typedAnalysisResponse = analysisResponse as any; // Cast to bypass unknown[] type limitation
      
      const correctedResponse = {
        ...typedAnalysisResponse,
        reviewItems: typedAnalysisResponse.reviewItems?.map((item: any) => {
          if (!item?.importCandidate) {
            return item;
          }

          const originalType = item.importCandidate.type;
          const correctedType = determineCorrectTransactionType(item.importCandidate.amount, mappingsToUse.amountConvention, originalType);
          return {
            ...item,
            importCandidate: {
              ...item.importCandidate,
              type: correctedType
            }
          };
        })
      };
      
      setImportAnalysisResult(correctedResponse as unknown as ImportAnalysisResult);
      setCurrentStep('conflicts');
    } catch (error) {
      console.error('Failed to analyze import for conflicts:', error);

      // Show proper error instead of fallback to mock data
      toast.error(t('aiCsv.analysisFailed'));
      
      // Log the error for debugging
      if (error instanceof Error) {
        console.error('Error details:', {
          message: error.message,
          stack: error.stack,
          accountId,
          resultData: result
        });
      }
      
      // Don't proceed to conflicts step if analysis failed
      setCurrentStep('upload');
    }
  };

  const handleConflictReviewComplete = (result: ImportExecutionResult) => {
    setImportResult({
      isSuccess: result.success,
      message: result.message,
      importedTransactionsCount: result.importedTransactionsCount,
      skippedTransactionsCount: result.skippedTransactionsCount,
      duplicateTransactionsCount: result.duplicateTransactionsCount,
      mergedTransactionsCount: result.mergedTransactionsCount,
      warnings: result.warnings,
      errors: result.errors,
      createdAccountId: result.createdAccountId
    });
    setCurrentStep('success');
  };


  const handleBackToUpload = () => {
    setCurrentStep('upload');
    setAnalysisResult(null);
    setUploadedFile(null);
  };

  const handleStartNewImport = () => {
    setCurrentStep('upload');
    setAnalysisResult(null);
    setUploadedFile(null);
    setImportResult(null);
  };

  const handleGoToTransactions = () => {
    if (importResult?.createdAccountId) {
      router.push(`/accounts/${importResult.createdAccountId}`);
    } else if (accountId) {
      router.push(`/accounts/${accountId}`);
    } else {
      router.push('/transactions');
    }
  };

  const getStepIndicator = () => {
    const steps = [
      { key: 'upload', label: t('aiCsv.steps.uploadAnalyze'), completed: ['review', 'conflicts', 'success'].includes(currentStep) },
      { key: 'review', label: t('aiCsv.steps.reviewMap'), completed: ['conflicts', 'success'].includes(currentStep) },
      { key: 'conflicts', label: t('aiCsv.steps.reviewConflicts'), completed: currentStep === 'success' },
      { key: 'success', label: t('aiCsv.steps.complete'), completed: currentStep === 'success' }
    ];

    return (
      <div className="flex items-center justify-center mb-8">
        {steps.map((step, index) => (
          <div key={step.key} className="flex items-center">
            <div className={`w-10 h-10 rounded-full flex items-center justify-center text-sm font-medium ${
              step.completed || step.key === currentStep
                ? 'bg-purple-600 text-white'
                : 'bg-gray-200 text-gray-600'
            }`}>
              {step.completed ? <CheckCircleIcon className="w-6 h-6" /> : index + 1}
            </div>
            <span className={`ml-2 text-sm ${
              step.key === currentStep ? 'text-purple-600 font-medium' : 'text-gray-500'
            }`}>
              {step.label}
            </span>
            {index < steps.length - 1 && (
              <div className={`w-16 h-px mx-4 ${
                step.completed ? 'bg-purple-600' : 'bg-gray-200'
              }`} />
            )}
          </div>
        ))}
      </div>
    );
  };

  const renderCurrentStep = () => {
    switch (currentStep) {
      case 'upload':
        return (
          <AICSVUpload
            onAnalysisComplete={handleAnalysisComplete}
            accountType={accountType}
            currencyHint="USD"
          />
        );
      
      case 'review':
        return analysisResult && uploadedFile ? (
          <CSVMappingReview
            analysisResult={analysisResult}
            file={uploadedFile}
            onImportComplete={handleMappingComplete}
            onBack={handleBackToUpload}
            accountId={accountId}
            accountName={accountName}
          />
        ) : (
          <Card>
            <CardContent className="p-6 text-center">
              <p className="text-gray-600">{t('aiCsv.missingResult')}</p>
              <Button onClick={handleBackToUpload} className="mt-4">
                {t('aiCsv.backToUpload')}
              </Button>
            </CardContent>
          </Card>
        );

      case 'conflicts':
        // Debug logging for importAnalysisResult
        console.log('AICSVImportContent (conflicts): importAnalysisResult', importAnalysisResult);
        console.log('AICSVImportContent (conflicts): importAnalysisResult?.summary', importAnalysisResult?.summary);
        return importAnalysisResult ? (
          <ImportReviewScreen
            analysisResult={importAnalysisResult}
            onImportComplete={handleConflictReviewComplete}
            onCancel={() => setCurrentStep('review')}
            accountName={accountName}
            showBulkActions={true}
          />
        ) : (
          <Card>
            <CardContent className="p-6 text-center">
              <div className="w-16 h-16 bg-orange-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <ExclamationTriangleIcon className="w-8 h-8 text-orange-600" />
              </div>
              <h3 className="text-lg font-semibold mb-2">{t('review.analyzingImport')}</h3>
              <p className="text-gray-600 mb-4">
                {t('review.checkingConflicts')}
              </p>
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-600 mx-auto"></div>
            </CardContent>
          </Card>
        );
      
      case 'success':
        return (
          <Card>
            <CardContent className="p-6">
              <div className="text-center space-y-6">
                <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
                  <CheckCircleIcon className="w-10 h-10 text-green-600" />
                </div>

                <div>
                  <h3 className="text-lg font-semibold mb-2">
                    {((importResult?.importedTransactionsCount ?? 0) + (importResult?.mergedTransactionsCount ?? 0)) > 0
                      ? t('aiCsv.success.importSuccessful')
                      : t('aiCsv.success.reviewCompleted')
                    }
                  </h3>
                  <p className="text-gray-600">
                    {((importResult?.importedTransactionsCount ?? 0) + (importResult?.mergedTransactionsCount ?? 0)) > 0
                      ? t('aiCsv.success.importedMessage')
                      : t('aiCsv.success.reviewMessage')
                    }
                  </p>
                </div>

                {importResult && (
                  <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                      <div className="text-center">
                        <div className="font-semibold text-green-800">
                          {importResult.importedTransactionsCount || 0}
                        </div>
                        <div className="text-green-600">{t('aiCsv.success.imported')}</div>
                      </div>
                      <div className="text-center">
                        <div className="font-semibold text-orange-800">
                          {importResult.skippedTransactionsCount || 0}
                        </div>
                        <div className="text-orange-600">{t('aiCsv.success.skipped')}</div>
                      </div>
                      {(importResult.mergedTransactionsCount ?? 0) > 0 && (
                        <div className="text-center">
                          <div className="font-semibold text-blue-800">
                            {importResult.mergedTransactionsCount}
                          </div>
                          <div className="text-blue-600">{t('aiCsv.success.merged')}</div>
                        </div>
                      )}
                      {(importResult.mergedTransactionsCount ?? 0) === 0 && (
                        <div className="text-center">
                          <div className="font-semibold text-gray-800">
                            {(importResult.importedTransactionsCount || 0) + (importResult.skippedTransactionsCount || 0) + (importResult.mergedTransactionsCount || 0)}
                          </div>
                          <div className="text-gray-600">{t('aiCsv.success.totalProcessed')}</div>
                        </div>
                      )}
                    </div>
                  </div>
                )}

                <div className="flex justify-center gap-4">
                  <Button variant="secondary" onClick={handleStartNewImport}>
                    {t('importAnother')}
                  </Button>
                  <Button onClick={handleGoToTransactions}>
                    {t('review.viewTransactions')}
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        );
      
      default:
        return null;
    }
  };

  return (
    <AppLayout>
      {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex items-center justify-between mb-6">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => router.back()}
              className="flex items-center gap-2"
            >
              <ArrowLeftIcon className="w-4 h-4" />
              {tCommon('back')}
            </Button>
          </div>

          <div className="text-center mb-8">
            <div className="w-20 h-20 bg-gradient-to-br from-purple-500 to-purple-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <SparklesIcon className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('aiCsv.title')}
            </h1>
            <p className="text-gray-600 max-w-2xl mx-auto">
              {t('aiCsv.subtitle')}
            </p>
          </div>
        </div>

        {/* Progress Indicator */}
        {getStepIndicator()}
        
        {/* Current Step Content */}
        <div className="max-w-4xl mx-auto">
          {renderCurrentStep()}
        </div>
    </AppLayout>
  );
}

function AICSVImportFallback() {
  const t = useTranslations('import');
  return (
    <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
      <div className="text-center">
        <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
          <SparklesIcon className="w-8 h-8 text-white" />
        </div>
        <div className="mt-6 text-gray-700 font-medium">{t('aiCsv.loading')}</div>
      </div>
    </div>
  );
}

export default function AICSVImportPage() {
  return (
    <Suspense fallback={<AICSVImportFallback />}>
      <AICSVImportContent />
    </Suspense>
  );
}