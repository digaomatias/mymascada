'use client';

import { useState, useRef, useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  DocumentArrowUpIcon,
  CloudArrowUpIcon,
  CheckCircleIcon,
  InformationCircleIcon,
  XCircleIcon,
  DocumentTextIcon,
  ArrowDownTrayIcon,
  TagIcon,
  SparklesIcon,
  ArrowLeftIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import { toast } from 'sonner';
import { apiClient } from '@/lib/api-client';
import { BackendAccountType } from '@/lib/utils';
import { useFeatures } from '@/contexts/features-context';
import Link from 'next/link';
import { ImportReviewScreen } from '@/components/import-review/import-review-screen';
import { ImportAnalysisResult, ImportExecutionResult } from '@/types/import-review';
import { useTranslations } from 'next-intl';

interface CsvFormat {
  key: string;
  name: string;
  description: string;
  columns: string[];
}

interface Account {
  id: number;
  name: string;
  type: number; // Backend returns numeric enum value
  currentBalance: number;
}

interface ImportResult {
  success: boolean;
  totalRows: number;
  importedCount: number;
  skippedCount: number;
  errorCount: number;
  errors: string[];
}

interface OfxImportResult {
  success: boolean;
  message: string;
  importedTransactionsCount: number;
  skippedTransactionsCount: number;
  duplicateTransactionsCount: number;
  errors?: string[];
  warnings?: string[];
}

type FileFormat = 'csv' | 'ofx';
type ImportStep = 'configure' | 'review' | 'complete';

export default function ImportPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { features } = useFeatures();
  const t = useTranslations('import');
  const tCommon = useTranslations('common');
  const tAccountTypes = useTranslations('accounts.types');

  // Helper to get localized account type label
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
  
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileFormat, setFileFormat] = useState<FileFormat>('ofx');
  const [selectedFormat, setSelectedFormat] = useState<string>('Generic');
  const [selectedAccount, setSelectedAccount] = useState<number | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [formats, setFormats] = useState<Record<string, CsvFormat>>({});
  const [hasHeader, setHasHeader] = useState(true);
  const [skipDuplicates, setSkipDuplicates] = useState(true);
  const [autoCategorize, setAutoCategorize] = useState(true);
  const [createAccount, setCreateAccount] = useState(false);
  const [accountName, setAccountName] = useState('');
  
  // Import flow state
  const [currentStep, setCurrentStep] = useState<ImportStep>('configure');
  const [importAnalysisResult, setImportAnalysisResult] = useState<ImportAnalysisResult | null>(null);
  
  // const [loading, setLoading] = useState(false); // Unused for now
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<ImportResult | OfxImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [, setValidationResult] = useState<{
    success: boolean;
    message: string;
    errors?: string[];
    warnings?: string[];
    accountInfo?: {
      accountId: string;
      accountNumber: string;
      bankId?: string;
      branchId?: string;
      accountType: string;
    };
    transactionCount: number;
    statementPeriod?: {
      startDate?: string;
      endDate?: string;
    };
  } | null>(null);
  
  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadFormats();
      loadAccounts();
    }
  }, [isAuthenticated]);

  const loadFormats = async () => {
    try {
      const data = await apiClient.getCsvFormats() as Record<string, CsvFormat>;
      setFormats(data);
      if (Object.keys(data).length > 0) {
        setSelectedFormat(Object.keys(data)[0]);
      }
    } catch (error) {
      console.error('Failed to load CSV formats:', error);
    }
  };

  const loadAccounts = async () => {
    try {
      const data = await apiClient.getAccounts() as Account[];
      setAccounts(data || []);
      if (data && data.length > 0) {
        setSelectedAccount(data[0].id);
      }
    } catch (error) {
      console.error('Failed to load accounts:', error);
      setAccounts([]);
    }
  };

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      const isValidFile = validateFile(file);
      if (isValidFile) {
        setSelectedFile(file);
        setError(null);
        setResult(null);
        setValidationResult(null);
        // Auto-detect file format
        const extension = file.name.toLowerCase();
        if (extension.endsWith('.ofx') || extension.endsWith('.qfx')) {
          setFileFormat('ofx');
        } else if (extension.endsWith('.csv')) {
          setFileFormat('csv');
        }
      } else {
        setSelectedFile(null);
      }
    }
  };

  const validateFile = (file: File): boolean => {
    const validExtensions = ['.csv', '.ofx', '.qfx'];
    const extension = file.name.toLowerCase();
    const isValidExtension = validExtensions.some(ext => extension.endsWith(ext));

    if (!isValidExtension) {
      setError(t('validation.invalidFileType'));
      return false;
    }

    return true;
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    const file = event.dataTransfer.files[0];
    if (file) {
      const isValidFile = validateFile(file);
      if (isValidFile) {
        setSelectedFile(file);
        setError(null);
        setResult(null);
        setValidationResult(null);
        // Auto-detect file format
        const extension = file.name.toLowerCase();
        if (extension.endsWith('.ofx') || extension.endsWith('.qfx')) {
          setFileFormat('ofx');
        } else if (extension.endsWith('.csv')) {
          setFileFormat('csv');
        }
      }
    }
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
  };

  const downloadTemplate = async () => {
    try {
      const blob = await apiClient.downloadCsvTemplate(selectedFormat);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${selectedFormat}_template.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (error) {
      console.error('Failed to download template:', error);
      setError('Failed to download template');
    }
  };

  const handleAnalyzeForImport = async () => {
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

    setImporting(true);
    setError(null);
    setResult(null);

    try {
      // TODO: Implement actual import analysis when backend is ready
      // For now, create mock analysis result
      const mockAnalysisResult: ImportAnalysisResult = {
        success: true,
        accountId: selectedAccount || 0,
        analysisId: `mock-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        importSource: fileFormat === 'csv' ? 2 : 3, // TransactionSource enum values
        reviewItems: [],
        summary: {
          totalCandidates: 0,
          cleanImports: 0,
          exactDuplicates: 0,
          potentialDuplicates: 0,
          transferConflicts: 0,
          manualConflicts: 0,
          requiresReview: 0
        },
        analysisTimestamp: new Date().toISOString(),
        warnings: ['Import analysis feature not yet implemented'],
        errors: []
      };

      setImportAnalysisResult(mockAnalysisResult);
      setCurrentStep('review');

      toast.success(t('toasts.analysisComplete'), { duration: 3000 });
    } catch (err) {
      console.error('Import analysis error:', err);
      setError(t('validation.invalidFileType'));
    } finally {
      setImporting(false);
    }
  };

  const handleImportComplete = (result: ImportExecutionResult) => {
    setResult({
      success: result.success,
      message: result.message,
      importedTransactionsCount: result.importedTransactionsCount,
      skippedTransactionsCount: result.skippedTransactionsCount,
      duplicateTransactionsCount: result.duplicateTransactionsCount,
      errors: result.errors,
      warnings: result.warnings
    });
    setCurrentStep('complete');

    toast.success(
      t('toasts.importComplete', { count: result.importedTransactionsCount }),
      { duration: 4000 }
    );
  };

  const handleBackToConfigure = () => {
    setCurrentStep('configure');
    setImportAnalysisResult(null);
    setError(null);
  };

  const handleValidateFile = async () => {
    if (!selectedFile) {
      setError(t('validation.selectFile'));
      return;
    }

    if (fileFormat !== 'ofx') {
      setError(t('validation.ofxValidationOnly'));
      return;
    }

    try {
      const validation = await apiClient.validateOfxFile(selectedFile);
      setValidationResult(validation);
      setError(null);

      if (validation.success) {
        toast.success(t('validation.ofxValidationSuccess'), { duration: 3000 });
      } else {
        toast.error(t('validation.ofxValidationFailed'), { duration: 3000 });
      }
    } catch (err) {
      console.error('Validation error:', err);
      setError(t('validation.ofxValidationFailed'));
    }
  };

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

  if (!isAuthenticated) {
    return null;
  }

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
            {features.aiCategorization && (
              <Link href="/import/ai-csv">
                <Button variant="secondary" size="sm" className="flex items-center gap-2">
                  <SparklesIcon className="w-4 h-4" />
                  {t('aiCsv.buttonLabel')}
                </Button>
              </Link>
            )}
          </div>

          <div className="text-center mb-8">
            <div className="w-20 h-20 bg-gradient-to-br from-blue-500 to-blue-600 rounded-3xl shadow-2xl flex items-center justify-center mx-auto mb-4">
              <DocumentArrowUpIcon className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
              {t('ofx.title')}
            </h1>
            <p className="text-gray-600 max-w-2xl mx-auto">
              {t('ofx.subtitle')}
            </p>
          </div>
        </div>

        {/* Current Step Content */}
        <div className="max-w-4xl mx-auto">
          {renderCurrentStep()}
        </div>
    </AppLayout>
  );

  function renderCurrentStep() {
    switch (currentStep) {
      case 'configure':
        return renderConfigureStep();
      case 'review':
        return renderReviewStep();
      case 'complete':
        return renderCompleteStep();
      default:
        return renderConfigureStep();
    }
  }

  function renderConfigureStep() {
    return (
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Main Import Form */}
            <div className="lg:col-span-2 space-y-6">
            {/* File Upload */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-xl font-bold text-gray-900">{t('steps.selectFile')}</CardTitle>
              </CardHeader>
              <CardContent>
                <div
                  className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center hover:border-primary-400 transition-colors"
                  onDrop={handleDrop}
                  onDragOver={handleDragOver}
                >
                  {selectedFile ? (
                    <div className="space-y-4">
                      <CheckCircleIcon className="w-12 h-12 text-success-500 mx-auto" />
                      <div>
                        <p className="text-lg font-medium text-gray-900">{selectedFile.name}</p>
                        <p className="text-sm text-gray-500">
                          {(selectedFile.size / 1024).toFixed(1)} KB
                        </p>
                      </div>
                      <Button
                        variant="secondary"
                        onClick={() => {
                          setSelectedFile(null);
                          if (fileInputRef.current) {
                            fileInputRef.current.value = '';
                          }
                        }}
                      >
                        {t('file.removeFile')}
                      </Button>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <CloudArrowUpIcon className="w-12 h-12 text-gray-400 mx-auto" />
                      <div>
                        <p className="text-lg font-medium text-gray-900">
                          {t('file.dropHere')}
                        </p>
                        <p className="text-sm text-gray-500">
                          {t('file.supports')}
                        </p>
                      </div>
                      <Button
                        variant="secondary"
                        onClick={() => fileInputRef.current?.click()}
                      >
                        <DocumentTextIcon className="w-4 h-4 mr-2" />
                        {t('file.chooseFile')}
                      </Button>
                    </div>
                  )}
                </div>

                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".csv,.ofx,.qfx"
                  onChange={handleFileSelect}
                  className="hidden"
                />

                {selectedFile && (
                  <div className="mt-4 p-3 bg-gray-50 rounded-lg">
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium text-gray-700">{t('file.format')}</span>
                      <span className="text-sm font-bold text-primary-600 uppercase">{fileFormat}</span>
                    </div>

                    {fileFormat === 'ofx' && (
                      <div className="mt-2">
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={handleValidateFile}
                          disabled={importing}
                        >
                          <CheckCircleIcon className="w-4 h-4 mr-1" />
                          {t('file.validateOfx')}
                        </Button>
                      </div>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Format Selection - Only for CSV */}
            {fileFormat === 'csv' && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardHeader>
                  <CardTitle className="text-xl font-bold text-gray-900">{t('steps.csvFormat')}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {t('csv.bankFormat')}
                    </label>
                    <select
                      value={selectedFormat}
                      onChange={(e) => setSelectedFormat(e.target.value)}
                      className="select"
                    >
                      {Object.entries(formats).map(([key, format]) => (
                        <option key={key} value={key}>
                          {format.name}
                        </option>
                      ))}
                    </select>
                  </div>

                  {formats[selectedFormat] && (
                    <div className="bg-gray-50 p-4 rounded-lg">
                      <p className="text-sm font-medium text-gray-700 mb-2">{t('csv.expectedColumns')}</p>
                      <p className="text-sm text-gray-600">
                        {formats[selectedFormat].columns.join(', ')}
                      </p>
                    </div>
                  )}

                  <Button
                    variant="secondary"
                    onClick={downloadTemplate}
                    className="w-full"
                  >
                    <ArrowDownTrayIcon className="w-4 h-4 mr-2" />
                    {t('csv.downloadTemplate')}
                  </Button>
                </CardContent>
              </Card>
            )}

            {/* Account Selection */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-xl font-bold text-gray-900">
                  {fileFormat === 'csv' ? '3.' : '2.'} {t('steps.accountOptions')}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center space-x-4">
                  <div className="flex items-center">
                    <input
                      type="radio"
                      id="existingAccount"
                      name="accountOption"
                      checked={!createAccount}
                      onChange={() => setCreateAccount(false)}
                      className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300"
                    />
                    <label htmlFor="existingAccount" className="ml-2 text-sm text-gray-700">
                      {t('account.useExisting')}
                    </label>
                  </div>

                  {fileFormat === 'ofx' && (
                    <div className="flex items-center">
                      <input
                        type="radio"
                        id="newAccount"
                        name="accountOption"
                        checked={createAccount}
                        onChange={() => setCreateAccount(true)}
                        className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300"
                      />
                      <label htmlFor="newAccount" className="ml-2 text-sm text-gray-700">
                        {t('account.createNew')}
                      </label>
                    </div>
                  )}
                </div>

                {!createAccount && (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {t('account.targetAccount')}
                    </label>
                    <select
                      value={selectedAccount || ''}
                      onChange={(e) => setSelectedAccount(Number(e.target.value))}
                      className="select"
                    >
                      <option value="">{t('account.selectAccount')}</option>
                      {accounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.name} ({getLocalizedAccountType(account.type)})
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                {createAccount && fileFormat === 'ofx' && (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      {t('account.newAccountName')}
                    </label>
                    <input
                      type="text"
                      value={accountName}
                      onChange={(e) => setAccountName(e.target.value)}
                      placeholder={t('account.enterAccountName')}
                      className="input"
                    />
                    <p className="text-xs text-gray-500 mt-1">
                      {t('account.detailsFromOfx')}
                    </p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Import Options - Only for CSV */}
            {fileFormat === 'csv' && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardHeader>
                  <CardTitle className="text-xl font-bold text-gray-900">4. {t('steps.importOptions')}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="flex items-center">
                    <input
                      type="checkbox"
                      id="hasHeader"
                      checked={hasHeader}
                      onChange={(e) => setHasHeader(e.target.checked)}
                      className="checkbox h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
                    />
                    <label htmlFor="hasHeader" className="ml-2 text-sm text-gray-700">
                      {t('csv.hasHeader')}
                    </label>
                  </div>

                  <div className="flex items-center">
                    <input
                      type="checkbox"
                      id="skipDuplicates"
                      checked={skipDuplicates}
                      onChange={(e) => setSkipDuplicates(e.target.checked)}
                      className="checkbox h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
                    />
                    <label htmlFor="skipDuplicates" className="ml-2 text-sm text-gray-700">
                      {t('options.skipDuplicates')}
                    </label>
                  </div>

                  <div className="flex items-center">
                    <input
                      type="checkbox"
                      id="autoCategorize"
                      checked={autoCategorize}
                      onChange={(e) => setAutoCategorize(e.target.checked)}
                      className="checkbox h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
                    />
                    <label htmlFor="autoCategorize" className="ml-2 text-sm text-gray-700">
                      {t('options.autoCategorize')}
                    </label>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Import Button */}
            <div className="pb-4 md:pb-0">
              <Button
                onClick={handleAnalyzeForImport}
                disabled={!selectedFile || (!selectedAccount && !createAccount) || importing}
                className="w-full bg-linear-to-r from-primary-500 to-primary-700 hover:from-primary-600 hover:to-primary-800 text-white py-4 text-lg font-semibold rounded-lg shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-300"
            >
              {importing ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
                  {t('importing')}
                </>
              ) : (
                <>
                  <DocumentArrowUpIcon className="w-5 h-5 mr-2" />
                  {t('buttons.import', { format: fileFormat.toUpperCase() })}
                </>
              )}
            </Button>
            </div>
          </div>

          {/* Sidebar */}
          <div className="space-y-6">
            {/* Results */}
            {result && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
                <CardHeader>
                  <CardTitle className="text-lg font-bold text-gray-900 flex items-center">
                    <CheckCircleIcon className="w-5 h-5 text-success-500 mr-2" />
                    {t('results.title')}
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="text-sm">
                    {/* CSV Results */}
                    {'totalRows' in result && (
                      <>
                        <div className="flex justify-between">
                          <span>{t('results.totalRows')}</span>
                          <span className="font-medium">{result.totalRows}</span>
                        </div>
                        <div className="flex justify-between">
                          <span>{t('results.imported')}</span>
                          <span className="font-medium text-success-600">{result.importedCount}</span>
                        </div>
                        <div className="flex justify-between">
                          <span>{t('results.skipped')}</span>
                          <span className="font-medium text-warning-600">{result.skippedCount}</span>
                        </div>
                        {result.errorCount > 0 && (
                          <div className="flex justify-between">
                            <span>{t('results.errors')}</span>
                            <span className="font-medium text-danger-600">{result.errorCount}</span>
                          </div>
                        )}
                      </>
                    )}

                    {/* OFX Results */}
                    {'importedTransactionsCount' in result && (
                      <>
                        <div className="flex justify-between">
                          <span>{t('results.imported')}</span>
                          <span className="font-medium text-success-600">{result.importedTransactionsCount}</span>
                        </div>
                        <div className="flex justify-between">
                          <span>{t('results.skipped')}</span>
                          <span className="font-medium text-warning-600">{result.skippedTransactionsCount}</span>
                        </div>
                        <div className="flex justify-between">
                          <span>{t('results.duplicates')}</span>
                          <span className="font-medium text-info-600">{result.duplicateTransactionsCount}</span>
                        </div>
                      </>
                    )}
                  </div>

                  {result.errors && result.errors.length > 0 && (
                    <div className="mt-4">
                      <p className="text-sm font-medium text-gray-700 mb-2">{t('results.errors')}</p>
                      <div className="max-h-32 overflow-y-auto text-xs text-danger-600 bg-danger-50 p-2 rounded">
                        {result.errors.map((error, index) => (
                          <div key={index}>{error}</div>
                        ))}
                      </div>
                    </div>
                  )}

                  {'warnings' in result && result.warnings && result.warnings.length > 0 && (
                    <div className="mt-4">
                      <p className="text-sm font-medium text-gray-700 mb-2">{t('results.warnings')}</p>
                      <div className="max-h-32 overflow-y-auto text-xs text-warning-600 bg-warning-50 p-2 rounded">
                        {result.warnings.map((warning, index) => (
                          <div key={index}>{warning}</div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Categorization Prompt */}
                  {(('importedCount' in result && result.importedCount > 0) ||
                    ('importedTransactionsCount' in result && result.importedTransactionsCount > 0)) && (
                    <div className="mt-4 pt-4 border-t border-gray-200">
                      <p className="text-sm text-gray-600 mb-3">
                        {t('results.categorizePrompt')}
                      </p>
                      <Button
                        onClick={() => router.push('/transactions/categorize')}
                        className="w-full"
                      >
                        <TagIcon className="w-4 h-4 mr-2" />
                        {t('results.categorizeTransactions')}
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            )}

            {/* Error Display */}
            {error && (
              <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg border-l-4 border-l-danger-500">
                <CardContent className="p-4">
                  <div className="flex items-center">
                    <XCircleIcon className="w-5 h-5 text-danger-500 mr-2" />
                    <span className="text-sm text-danger-700">{error}</span>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Instructions */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900 flex items-center">
                  <InformationCircleIcon className="w-5 h-5 text-info-500 mr-2" />
                  {t('instructions.title')}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm text-gray-600">
                <div>
                  <p className="font-medium text-gray-700">{t('instructions.step1Title')}</p>
                  <p>{t('instructions.step1Desc')}</p>
                </div>
                <div>
                  <p className="font-medium text-gray-700">{t('instructions.step2Title')}</p>
                  <p>{t('instructions.step2Desc')}</p>
                </div>
                <div>
                  <p className="font-medium text-gray-700">{t('instructions.step3Title')}</p>
                  <p>{t('instructions.step3Desc')}</p>
                </div>
                <div>
                  <p className="font-medium text-gray-700">{t('instructions.step4Title')}</p>
                  <p>{t('instructions.step4Desc')}</p>
                </div>
              </CardContent>
            </Card>

            {/* Supported Formats */}
            <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
              <CardHeader>
                <CardTitle className="text-lg font-bold text-gray-900">{t('supportedFormatsTitle')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm text-gray-600">
                {Object.entries(formats).map(([key, format]) => (
                  <div key={key}>
                    <p className="font-medium text-gray-700">{format.name}</p>
                    <p className="text-xs">{format.description}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
      </div>
    );
  }

  function renderReviewStep() {
    if (!importAnalysisResult) {
      return (
        <div className="text-center py-8">
          <div className="w-16 h-16 bg-orange-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <ExclamationTriangleIcon className="w-8 h-8 text-orange-600" />
          </div>
          <h3 className="text-lg font-semibold mb-2">{t('review.analyzingImport')}</h3>
          <p className="text-gray-600 mb-4">{t('review.checkingConflicts')}</p>
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-orange-600 mx-auto"></div>
        </div>
      );
    }

    return (
      <ImportReviewScreen
        analysisResult={importAnalysisResult}
        onImportComplete={handleImportComplete}
        onCancel={handleBackToConfigure}
        accountName={accounts.find(a => a.id === selectedAccount)?.name}
        showBulkActions={true}
      />
    );
  }

  function renderCompleteStep() {
    return (
      <div className="text-center py-8">
        <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
          <CheckCircleIcon className="w-10 h-10 text-green-600" />
        </div>

        <div>
          <h3 className="text-lg font-semibold mb-2">{t('review.importSuccessful')}</h3>
          <p className="text-gray-600">
            {t('review.transactionsImported')}
          </p>
        </div>

        {result && (
          <div className="bg-green-50 border border-green-200 rounded-lg p-4 mt-6 max-w-md mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div className="text-center">
                <div className="font-semibold text-green-800">
                  {'importedTransactionsCount' in result ? result.importedTransactionsCount : result.importedCount}
                </div>
                <div className="text-green-600">{t('results.imported')}</div>
              </div>
              <div className="text-center">
                <div className="font-semibold text-yellow-800">
                  {'skippedTransactionsCount' in result ? result.skippedTransactionsCount : result.skippedCount}
                </div>
                <div className="text-yellow-600">{t('results.skipped')}</div>
              </div>
              <div className="text-center">
                <div className="font-semibold text-blue-800">
                  {'duplicateTransactionsCount' in result ? result.duplicateTransactionsCount : result.errorCount}
                </div>
                <div className="text-blue-600">{t('results.duplicates')}</div>
              </div>
            </div>
          </div>
        )}

        <div className="flex justify-center gap-4 mt-6">
          <Button
            variant="secondary"
            onClick={() => {
              setCurrentStep('configure');
              setImportAnalysisResult(null);
              setResult(null);
              setSelectedFile(null);
              if (fileInputRef.current) {
                fileInputRef.current.value = '';
              }
            }}
          >
            {t('importAnother')}
          </Button>
          <Button onClick={() => {
            if (result && 'createdAccountId' in result && result.createdAccountId) {
              router.push(`/accounts/${result.createdAccountId}`);
            } else if (selectedAccount) {
              router.push(`/accounts/${selectedAccount}`);
            } else {
              router.push('/transactions');
            }
          }}>
            {t('review.viewTransactions')}
          </Button>
        </div>
      </div>
    );
  }
}