'use client';

import { useState, useRef, useEffect, Suspense } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter, useSearchParams } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  DocumentArrowUpIcon,
  CloudArrowUpIcon,
  CheckCircleIcon,
  XCircleIcon,
  SparklesIcon,
  ArrowLeftIcon,
  ExclamationTriangleIcon,
  AdjustmentsHorizontalIcon,
} from '@heroicons/react/24/outline';
import { toast } from 'sonner';
import { apiClient } from '@/lib/api-client';
import { useFeatures } from '@/contexts/features-context';
import { CSVMappingReview } from '@/components/forms/csv-mapping-review';
import { ImportReviewScreen } from '@/components/import-review/import-review-screen';
import { ImportAnalysisResult, ImportExecutionResult, ImportReviewItem } from '@/types/import-review';
import { useTranslations } from 'next-intl';
import { BackendAccountType } from '@/lib/utils';

// ────────────────────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────────────────────

type FileFormat = 'csv' | 'ofx';
type ImportStep = 'configure' | 'csv-mapping' | 'conflicts' | 'complete';

interface Account {
  id: number;
  name: string;
  type: number;
  currentBalance: number;
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

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const result = e.target?.result as string;
      const base64 = result.split(',')[1];
      if (!base64) {
        reject(new Error('Failed to read file'));
        return;
      }
      resolve(base64);
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

function parseCSVForManualMapping(text: string): CSVAnalysisResult {
  const lines = text.split(/\r?\n/).filter((l) => l.trim());
  const headerLine = lines[0] ?? '';

  // Simple CSV header parsing (handles basic quoted fields)
  const columns = headerLine
    .split(',')
    .map((h) => h.trim().replace(/^"|"$/g, ''))
    .filter(Boolean);

  const sampleRows: Record<string, string>[] = [];
  for (let i = 1; i < Math.min(6, lines.length); i++) {
    const values = lines[i].split(',');
    const row: Record<string, string> = {};
    columns.forEach((col, idx) => {
      row[col] = (values[idx] ?? '').trim().replace(/^"|"$/g, '');
    });
    sampleRows.push(row);
  }

  return {
    success: true,
    suggestedMappings: {},
    sampleRows,
    confidenceScores: {},
    detectedBankFormat: 'Unknown',
    dateFormats: ['dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd'],
    amountConvention: 'negative-expense',
    availableColumns: columns,
    warnings: [],
  };
}

function determineCorrectTransactionType(
  amount: number,
  amountConvention: string,
  originalType?: number
): number {
  switch (amountConvention) {
    case 'type-column':
      return originalType || (amount >= 0 ? 1 : 2);
    case 'positive-expense':
      return amount >= 0 ? 2 : 1;
    case 'negative-expense':
    default:
      return amount >= 0 ? 1 : 2;
  }
}

// ────────────────────────────────────────────────────────────────────────────
// Main component (inner, uses useSearchParams)
// ────────────────────────────────────────────────────────────────────────────

function ImportPageContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { features } = useFeatures();
  const t = useTranslations('import');
  const tCommon = useTranslations('common');
  const tAccountTypes = useTranslations('accounts.types');
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Pre-fill account from URL params (e.g. when navigating from an account page)
  const urlAccountId = searchParams.get('accountId')
    ? parseInt(searchParams.get('accountId')!)
    : undefined;
  const urlAccountName = searchParams.get('accountName') || undefined;

  // ── Core state ────────────────────────────────────────────────────────────
  const [currentStep, setCurrentStep] = useState<ImportStep>('configure');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileFormat, setFileFormat] = useState<FileFormat>('ofx');

  // OFX-specific
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selectedAccount, setSelectedAccount] = useState<number | null>(urlAccountId ?? null);
  const [createAccount, setCreateAccount] = useState(false);
  const [accountName, setAccountName] = useState('');

  // CSV path
  const [csvAnalysisResult, setCsvAnalysisResult] = useState<CSVAnalysisResult | null>(null);
  const [csvContent, setCsvContent] = useState<string | null>(null);
  const [isAnalyzingCSV, setIsAnalyzingCSV] = useState(false);

  // Conflicts / success
  const [importAnalysisResult, setImportAnalysisResult] = useState<ImportAnalysisResult | null>(null);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [isAnalyzingOFX, setIsAnalyzingOFX] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getLocalizedAccountType = (type: number): string => {
    const typeMap: Record<number, string> = {
      [BackendAccountType.Checking]: 'checking',
      [BackendAccountType.Savings]: 'savings',
      [BackendAccountType.CreditCard]: 'creditCard',
      [BackendAccountType.Investment]: 'investment',
      [BackendAccountType.Loan]: 'loan',
      [BackendAccountType.Cash]: 'cash',
      [BackendAccountType.Other]: 'other',
    };
    return tAccountTypes(typeMap[type] || 'other');
  };

  // ── Effects ───────────────────────────────────────────────────────────────

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      apiClient
        .getAccounts()
        .then((data) => {
          const list = (data as Account[]) || [];
          setAccounts(list);
          if (!selectedAccount && list.length > 0) {
            setSelectedAccount(list[0].id);
          }
        })
        .catch(() => setAccounts([]));
    }
  }, [isAuthenticated]); // eslint-disable-line react-hooks/exhaustive-deps

  // ── File handling ─────────────────────────────────────────────────────────

  const detectFormat = (file: File): FileFormat => {
    const name = file.name.toLowerCase();
    if (name.endsWith('.ofx') || name.endsWith('.qfx')) return 'ofx';
    return 'csv';
  };

  const validateFile = (file: File): boolean => {
    const valid = ['.csv', '.ofx', '.qfx'];
    if (!valid.some((ext) => file.name.toLowerCase().endsWith(ext))) {
      setError(t('validation.invalidFileType'));
      return false;
    }
    return true;
  };

  const applyFile = (file: File) => {
    if (!validateFile(file)) return;
    setSelectedFile(file);
    setFileFormat(detectFormat(file));
    setError(null);
    setCsvAnalysisResult(null);
    setCsvContent(null);
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) applyFile(file);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (file) applyFile(file);
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
  };

  const removeFile = () => {
    setSelectedFile(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
    setError(null);
  };

  // ── OFX path ──────────────────────────────────────────────────────────────

  const handleAnalyzeOFX = async () => {
    if (!selectedFile) {
      setError(t('validation.selectFile'));
      return;
    }
    if (!selectedAccount && !createAccount) {
      setError(t('validation.selectAccountOrCreate'));
      return;
    }
    if (createAccount && !accountName.trim()) {
      setError(t('validation.provideAccountName'));
      return;
    }

    setIsAnalyzingOFX(true);
    setError(null);

    try {
      const content = await fileToBase64(selectedFile);
      const result = await apiClient.analyzeImportForReview({
        source: 'ofx',
        accountId: selectedAccount || 0,
        ofxData: {
          content,
          createAccount,
          accountName: createAccount ? accountName : undefined,
        },
        options: {
          dateToleranceDays: 3,
          amountTolerance: 0.01,
          enableTransferDetection: true,
          conflictDetectionLevel: 'moderate',
        },
      });
      setImportAnalysisResult(result as unknown as ImportAnalysisResult);
      setCurrentStep('conflicts');
      toast.success(t('toasts.analysisComplete'), { duration: 3000 });
    } catch (err) {
      console.error('OFX analysis error:', err);
      setError(err instanceof Error ? err.message : t('validation.invalidFileType'));
    } finally {
      setIsAnalyzingOFX(false);
    }
  };

  const handleValidateOFX = async () => {
    if (!selectedFile) return;
    try {
      const validation = await apiClient.validateOfxFile(selectedFile);
      if (validation.success) {
        toast.success(t('validation.ofxValidationSuccess'), { duration: 3000 });
      } else {
        toast.error(t('validation.ofxValidationFailed'), { duration: 3000 });
      }
    } catch {
      toast.error(t('validation.ofxValidationFailed'), { duration: 3000 });
    }
  };

  // ── CSV + AI path ─────────────────────────────────────────────────────────

  const handleAnalyzeCSVWithAI = async () => {
    if (!selectedFile) {
      setError(t('validation.selectFile'));
      return;
    }
    setIsAnalyzingCSV(true);
    setError(null);
    try {
      const [result, text] = await Promise.all([
        apiClient.analyzeCsvWithAI(selectedFile, { accountType: 'Generic', currencyHint: 'USD' }),
        selectedFile.text(),
      ]);
      setCsvAnalysisResult(result as CSVAnalysisResult);
      setCsvContent(text);
      setCurrentStep('csv-mapping');
    } catch (err) {
      console.error('AI CSV analysis error:', err);
      setError(err instanceof Error ? err.message : t('aiCsv.analysisFailed'));
    } finally {
      setIsAnalyzingCSV(false);
    }
  };

  // ── CSV manual path ───────────────────────────────────────────────────────

  const handleMapCSVManually = async () => {
    if (!selectedFile) {
      setError(t('validation.selectFile'));
      return;
    }
    setIsAnalyzingCSV(true);
    setError(null);
    try {
      const text = await selectedFile.text();
      setCsvContent(text);
      setCsvAnalysisResult(parseCSVForManualMapping(text));
      setCurrentStep('csv-mapping');
    } catch (err) {
      console.error('CSV parse error:', err);
      setError(err instanceof Error ? err.message : t('validation.invalidFileType'));
    } finally {
      setIsAnalyzingCSV(false);
    }
  };

  // ── CSV mapping complete → conflict analysis ───────────────────────────────

  const handleMappingComplete = async (result: ImportResult) => {
    if (!csvContent || !csvAnalysisResult) {
      console.error('Missing CSV content or analysis result');
      return;
    }

    if (!result.updatedMappings) {
      console.error('Updated mappings not available');
      toast.error(t('aiCsv.analysisFailed'));
      setCurrentStep('configure');
      return;
    }

    const mappingsToUse = result.updatedMappings;
    const selectedAccountId = result.createdAccountId || urlAccountId || 0;

    const backendMappings = {
      amountColumn: mappingsToUse.amountColumn,
      dateColumn: mappingsToUse.dateColumn,
      descriptionColumn: mappingsToUse.descriptionColumn,
      referenceColumn: mappingsToUse.referenceColumn,
      typeColumn: mappingsToUse.typeColumn,
      dateFormat: mappingsToUse.dateFormat,
      amountConvention: mappingsToUse.amountConvention,
      typeValueMappings: mappingsToUse.typeValueMappings
        ? {
            IncomeValues: mappingsToUse.typeValueMappings.incomeValues,
            ExpenseValues: mappingsToUse.typeValueMappings.expenseValues,
          }
        : undefined,
    };

    try {
      const analysisResponse = await apiClient.analyzeImportForReview({
        source: 'csv',
        accountId: selectedAccountId,
        csvData: {
          content: btoa(csvContent),
          mappings: backendMappings,
          hasHeader: true,
        },
        options: {
          dateToleranceDays: 3,
          amountTolerance: 0.01,
          enableTransferDetection: true,
          conflictDetectionLevel: 'moderate',
        },
      });

      // Fix transaction types based on amount convention
      const typedResponse = analysisResponse as unknown as ImportAnalysisResult;
      const correctedResponse: ImportAnalysisResult = {
        ...typedResponse,
        reviewItems: typedResponse.reviewItems?.map((item: ImportReviewItem) => {
          if (!item?.importCandidate) return item;
          const correctedType = determineCorrectTransactionType(
            item.importCandidate.amount,
            mappingsToUse.amountConvention,
            item.importCandidate.type
          );
          return {
            ...item,
            importCandidate: { ...item.importCandidate, type: correctedType },
          };
        }),
      };

      setImportAnalysisResult(correctedResponse as unknown as ImportAnalysisResult);
      setCurrentStep('conflicts');
    } catch (err) {
      console.error('CSV conflict analysis error:', err);
      toast.error(t('aiCsv.analysisFailed'));
      setCurrentStep('configure');
    }
  };

  // ── Conflict review complete ───────────────────────────────────────────────

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
      createdAccountId: result.createdAccountId,
    });
    setCurrentStep('complete');
    toast.success(
      t('toasts.importComplete', { count: result.importedTransactionsCount }),
      { duration: 4000 }
    );
  };

  const handleStartNew = () => {
    setCurrentStep('configure');
    setSelectedFile(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
    setCsvAnalysisResult(null);
    setCsvContent(null);
    setImportAnalysisResult(null);
    setImportResult(null);
    setError(null);
  };

  const handleGoToTransactions = () => {
    if (importResult?.createdAccountId) {
      router.push(`/accounts/${importResult.createdAccountId}`);
    } else if (urlAccountId) {
      router.push(`/accounts/${urlAccountId}`);
    } else if (selectedAccount) {
      router.push(`/accounts/${selectedAccount}`);
    } else {
      router.push('/transactions');
    }
  };

  // ── Loading / auth guard ───────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <DocumentArrowUpIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) return null;

  // ── Step indicator ────────────────────────────────────────────────────────

  const steps: { key: ImportStep; label: string }[] = [
    { key: 'configure', label: t('aiCsv.steps.uploadAnalyze') },
    ...(fileFormat === 'csv'
      ? [{ key: 'csv-mapping' as ImportStep, label: t('aiCsv.steps.reviewMap') }]
      : []),
    { key: 'conflicts', label: t('aiCsv.steps.reviewConflicts') },
    { key: 'complete', label: t('aiCsv.steps.complete') },
  ];

  const stepIndex = steps.findIndex((s) => s.key === currentStep);

  const StepIndicator = () => (
    <div className="flex items-center justify-center mb-8 flex-wrap gap-y-2">
      {steps.map((step, index) => {
        const isActive = step.key === currentStep;
        const isCompleted = index < stepIndex;
        return (
          <div key={step.key} className="flex items-center">
            <div
              className={`w-9 h-9 rounded-full flex items-center justify-center text-sm font-medium transition-colors ${
                isCompleted || isActive ? 'bg-violet-600 text-white' : 'bg-gray-200 text-gray-500'
              }`}
            >
              {isCompleted ? <CheckCircleIcon className="w-5 h-5" /> : index + 1}
            </div>
            <span
              className={`ml-2 text-sm whitespace-nowrap ${
                isActive ? 'text-violet-600 font-medium' : 'text-gray-400'
              }`}
            >
              {step.label}
            </span>
            {index < steps.length - 1 && (
              <div
                className={`w-12 h-px mx-3 ${isCompleted ? 'bg-violet-600' : 'bg-gray-200'}`}
              />
            )}
          </div>
        );
      })}
    </div>
  );

  // ── Render steps ──────────────────────────────────────────────────────────

  const renderConfigure = () => (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* File upload zone */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <div className="p-6">
          <div
            className="border-2 border-dashed border-gray-300 rounded-xl p-10 text-center hover:border-violet-400 transition-colors cursor-pointer"
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onClick={() => !selectedFile && fileInputRef.current?.click()}
          >
            {selectedFile ? (
              <div className="space-y-3">
                <CheckCircleIcon className="w-12 h-12 text-green-500 mx-auto" />
                <div>
                  <p className="text-lg font-semibold text-gray-900">{selectedFile.name}</p>
                  <p className="text-sm text-gray-500">
                    {(selectedFile.size / 1024).toFixed(1)} KB &mdash;{' '}
                    <span className="font-medium text-violet-600 uppercase">{fileFormat}</span>
                  </p>
                </div>
                <Button variant="secondary" size="sm" onClick={(e) => { e.stopPropagation(); removeFile(); }}>
                  {t('file.removeFile')}
                </Button>
              </div>
            ) : (
              <div className="space-y-4">
                <CloudArrowUpIcon className="w-12 h-12 text-gray-400 mx-auto" />
                <div>
                  <p className="text-lg font-semibold text-gray-900">{t('unified.dropHere')}</p>
                  <p className="text-sm text-gray-500 mt-1">{t('unified.supports')}</p>
                </div>
                <Button variant="secondary" onClick={(e) => { e.stopPropagation(); fileInputRef.current?.click(); }}>
                  <DocumentArrowUpIcon className="w-4 h-4 mr-2" />
                  {t('file.chooseFile')}
                </Button>
              </div>
            )}
          </div>

          <input
            ref={fileInputRef}
            type="file"
            accept=".csv,.ofx,.qfx"
            onChange={handleFileInputChange}
            className="hidden"
          />
        </div>
      </Card>

      {error && (
        <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <XCircleIcon className="w-5 h-5 shrink-0" />
          {error}
        </div>
      )}

      {/* OFX: account selection + import button */}
      {selectedFile && fileFormat === 'ofx' && (
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <div className="p-6 space-y-5">
            <p className="text-sm text-slate-600">{t('unified.ofxReady')}</p>

            {/* Account selection */}
            <div className="space-y-3">
              <div className="flex items-center gap-4">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    checked={!createAccount}
                    onChange={() => setCreateAccount(false)}
                    className="h-4 w-4 text-violet-600"
                  />
                  <span className="text-sm text-gray-700">{t('account.useExisting')}</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    checked={createAccount}
                    onChange={() => setCreateAccount(true)}
                    className="h-4 w-4 text-violet-600"
                  />
                  <span className="text-sm text-gray-700">{t('account.createNew')}</span>
                </label>
              </div>

              {!createAccount && (
                <select
                  value={selectedAccount || ''}
                  onChange={(e) => setSelectedAccount(Number(e.target.value))}
                  className="select w-full"
                >
                  <option value="">{t('account.selectAccount')}</option>
                  {accounts.map((acc) => (
                    <option key={acc.id} value={acc.id}>
                      {acc.name} ({getLocalizedAccountType(acc.type)})
                    </option>
                  ))}
                </select>
              )}

              {createAccount && (
                <div className="space-y-1">
                  <input
                    type="text"
                    value={accountName}
                    onChange={(e) => setAccountName(e.target.value)}
                    placeholder={t('account.enterAccountName')}
                    className="input w-full"
                  />
                  <p className="text-xs text-gray-500">{t('account.detailsFromOfx')}</p>
                </div>
              )}
            </div>

            <div className="flex gap-3">
              <Button
                variant="secondary"
                size="sm"
                onClick={handleValidateOFX}
                disabled={isAnalyzingOFX}
              >
                <CheckCircleIcon className="w-4 h-4 mr-1" />
                {t('file.validateOfx')}
              </Button>
              <Button
                onClick={handleAnalyzeOFX}
                disabled={isAnalyzingOFX || (!selectedAccount && !createAccount)}
                className="flex-1 bg-linear-to-r from-violet-600 to-violet-700 hover:from-violet-700 hover:to-violet-800 text-white font-semibold"
              >
                {isAnalyzingOFX ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2" />
                    {t('importing')}
                  </>
                ) : (
                  <>
                    <DocumentArrowUpIcon className="w-4 h-4 mr-2" />
                    {t('buttons.analyzeImport')}
                  </>
                )}
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* CSV: AI or manual */}
      {selectedFile && fileFormat === 'csv' && (
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <div className="p-6 space-y-4">
            {features.aiCategorization ? (
              <>
                <div className="flex items-start gap-3 rounded-xl bg-violet-50 p-4">
                  <SparklesIcon className="w-5 h-5 text-violet-600 mt-0.5 shrink-0" />
                  <p className="text-sm text-violet-800">{t('unified.csvWithAI')}</p>
                </div>
                <Button
                  onClick={handleAnalyzeCSVWithAI}
                  disabled={isAnalyzingCSV}
                  className="w-full bg-linear-to-r from-violet-600 to-fuchsia-600 hover:from-violet-700 hover:to-fuchsia-700 text-white font-semibold py-3"
                >
                  {isAnalyzingCSV ? (
                    <>
                      <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2" />
                      {t('unified.analyzing')}
                    </>
                  ) : (
                    <>
                      <SparklesIcon className="w-4 h-4 mr-2" />
                      {t('buttons.analyzeWithAI')}
                    </>
                  )}
                </Button>
                <p className="text-center text-xs text-gray-400">{tCommon('or')}</p>
                <Button
                  variant="secondary"
                  onClick={handleMapCSVManually}
                  disabled={isAnalyzingCSV}
                  className="w-full"
                >
                  <AdjustmentsHorizontalIcon className="w-4 h-4 mr-2" />
                  {t('buttons.mapColumns')}
                </Button>
              </>
            ) : (
              <>
                <div className="flex items-start gap-3 rounded-xl bg-slate-50 p-4">
                  <AdjustmentsHorizontalIcon className="w-5 h-5 text-slate-500 mt-0.5 shrink-0" />
                  <p className="text-sm text-slate-700">{t('unified.csvManual')}</p>
                </div>
                <Button
                  onClick={handleMapCSVManually}
                  disabled={isAnalyzingCSV}
                  className="w-full bg-linear-to-r from-violet-600 to-violet-700 hover:from-violet-700 hover:to-violet-800 text-white font-semibold py-3"
                >
                  {isAnalyzingCSV ? (
                    <>
                      <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2" />
                      {t('unified.analyzing')}
                    </>
                  ) : (
                    <>
                      <AdjustmentsHorizontalIcon className="w-4 h-4 mr-2" />
                      {t('buttons.continueToMapping')}
                    </>
                  )}
                </Button>
              </>
            )}
          </div>
        </Card>
      )}
    </div>
  );

  const renderCsvMapping = () => {
    if (!csvAnalysisResult || !selectedFile) {
      return (
        <Card>
          <CardContent className="p-6 text-center">
            <p className="text-gray-600">{t('aiCsv.missingResult')}</p>
            <Button onClick={() => setCurrentStep('configure')} className="mt-4">
              {t('aiCsv.backToUpload')}
            </Button>
          </CardContent>
        </Card>
      );
    }
    return (
      <CSVMappingReview
        analysisResult={csvAnalysisResult}
        file={selectedFile}
        onImportComplete={handleMappingComplete}
        onBack={() => setCurrentStep('configure')}
        accountId={urlAccountId}
        accountName={urlAccountName}
      />
    );
  };

  const renderConflicts = () => {
    if (!importAnalysisResult) {
      return (
        <Card>
          <CardContent className="p-6 text-center">
            <div className="w-16 h-16 bg-orange-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <ExclamationTriangleIcon className="w-8 h-8 text-orange-600" />
            </div>
            <h3 className="text-lg font-semibold mb-2">{t('review.analyzingImport')}</h3>
            <p className="text-gray-600 mb-4">{t('review.checkingConflicts')}</p>
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-600 mx-auto" />
          </CardContent>
        </Card>
      );
    }
    return (
      <ImportReviewScreen
        analysisResult={importAnalysisResult}
        onImportComplete={handleConflictReviewComplete}
        onCancel={() =>
          setCurrentStep(fileFormat === 'csv' ? 'csv-mapping' : 'configure')
        }
        accountName={
          accounts.find((a) => a.id === selectedAccount)?.name || urlAccountName
        }
        showBulkActions={true}
      />
    );
  };

  const renderComplete = () => (
    <Card>
      <CardContent className="p-6">
        <div className="text-center space-y-6">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
            <CheckCircleIcon className="w-10 h-10 text-green-600" />
          </div>

          <div>
            <h3 className="text-lg font-semibold mb-2">
              {((importResult?.importedTransactionsCount ?? 0) +
                (importResult?.mergedTransactionsCount ?? 0)) > 0
                ? t('aiCsv.success.importSuccessful')
                : t('aiCsv.success.reviewCompleted')}
            </h3>
            <p className="text-gray-600">
              {((importResult?.importedTransactionsCount ?? 0) +
                (importResult?.mergedTransactionsCount ?? 0)) > 0
                ? t('aiCsv.success.importedMessage')
                : t('aiCsv.success.reviewMessage')}
            </p>
          </div>

          {importResult && (
            <div className="bg-green-50 border border-green-200 rounded-xl p-4 max-w-sm mx-auto">
              <div className="grid grid-cols-3 gap-4 text-sm">
                <div className="text-center">
                  <div className="font-bold text-green-800 text-xl">
                    {importResult.importedTransactionsCount || 0}
                  </div>
                  <div className="text-green-600 text-xs">{t('aiCsv.success.imported')}</div>
                </div>
                <div className="text-center">
                  <div className="font-bold text-orange-800 text-xl">
                    {importResult.skippedTransactionsCount || 0}
                  </div>
                  <div className="text-orange-600 text-xs">{t('aiCsv.success.skipped')}</div>
                </div>
                <div className="text-center">
                  <div className="font-bold text-blue-800 text-xl">
                    {importResult.mergedTransactionsCount || importResult.duplicateTransactionsCount || 0}
                  </div>
                  <div className="text-blue-600 text-xs">{t('aiCsv.success.merged')}</div>
                </div>
              </div>
            </div>
          )}

          <div className="flex justify-center gap-4">
            <Button variant="secondary" onClick={handleStartNew}>
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

  return (
    <AppLayout>
      {/* Header */}
      <div className="mb-6 lg:mb-8">
        <div className="flex items-center mb-6">
          <Button
            variant="secondary"
            size="sm"
            onClick={currentStep === 'configure' ? () => router.back() : () => setCurrentStep('configure')}
            className="flex items-center gap-2"
          >
            <ArrowLeftIcon className="w-4 h-4" />
            {tCommon('back')}
          </Button>
        </div>

        <div className="text-center mb-8">
          <div className="w-20 h-20 bg-gradient-to-br from-violet-500 to-fuchsia-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4">
            <DocumentArrowUpIcon className="w-10 h-10 text-white" />
          </div>
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
            {t('unified.title')}
          </h1>
          <p className="text-gray-600 max-w-2xl mx-auto">{t('unified.subtitle')}</p>
        </div>
      </div>

      {/* Progress */}
      <StepIndicator />

      {/* Content */}
      <div className="max-w-4xl mx-auto">
        {currentStep === 'configure' && renderConfigure()}
        {currentStep === 'csv-mapping' && renderCsvMapping()}
        {currentStep === 'conflicts' && renderConflicts()}
        {currentStep === 'complete' && renderComplete()}
      </div>
    </AppLayout>
  );
}

function ImportPageFallback() {
  const t = useTranslations('import');
  return (
    <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
      <div className="text-center">
        <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
          <DocumentArrowUpIcon className="w-8 h-8 text-white" />
        </div>
        <div className="mt-6 text-gray-700 font-medium">{t('loading')}</div>
      </div>
    </div>
  );
}

export default function ImportPage() {
  return (
    <Suspense fallback={<ImportPageFallback />}>
      <ImportPageContent />
    </Suspense>
  );
}

export const dynamic = 'force-dynamic';
