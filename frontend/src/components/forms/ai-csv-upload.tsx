'use client';

import { useState, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { 
  DocumentArrowUpIcon, 
  SparklesIcon,
  ArrowPathIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface AICSVUploadProps {
  onAnalysisComplete?: (result: CSVAnalysisResult, file: File) => void;
  accountType?: string;
  currencyHint?: string;
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
  errorMessage?: string;
}

export function AICSVUpload({ 
  onAnalysisComplete, 
  accountType = 'Generic',
  currencyHint = 'USD'
}: AICSVUploadProps) {
  const t = useTranslations('import.aiCsv');
  const tCommon = useTranslations('common');
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileUpload = async (file: File) => {
    if (!file) return;

    // Validate file type
    if (!file.name.toLowerCase().endsWith('.csv')) {
      toast.error(t('errors.csvOnly'));
      return;
    }

    // Validate file size (10MB limit)
    if (file.size > 10 * 1024 * 1024) {
      toast.error(t('errors.fileTooLarge'));
      return;
    }

    try {
      setIsAnalyzing(true);
      
      const result = await apiClient.analyzeCsvWithAI(file, {
        accountType,
        currencyHint,
        sampleSize: 10
      });

      if (!result.success) {
        toast.error(result.errorMessage || t('errors.analysisFailed'));
        return;
      }

      // Show success message
      toast.success(t('analysisSuccess', { format: result.detectedBankFormat }));
      
      // Show warnings if any
      if (result.warnings.length > 0) {
        result.warnings.forEach(warning => {
          toast.info(warning);
        });
      }

      // Call the callback with results
      if (onAnalysisComplete) {
        onAnalysisComplete(result, file);
      }

    } catch (error) {
      console.error('CSV analysis error:', error);
      toast.error(t('errors.analysisFailedRetry'));
    } finally {
      setIsAnalyzing(false);
    }
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

  return (
    <Card>
      <input
        ref={fileInputRef}
        type="file"
        accept=".csv"
        onChange={handleFileInputChange}
        className="hidden"
      />
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <SparklesIcon className="w-5 h-5 text-purple-600" />
          {t('title')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {/* Info Section */}
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex items-start gap-3">
              <SparklesIcon className="w-5 h-5 text-blue-600 mt-0.5" />
              <div>
                <h4 className="font-medium text-blue-900">{t('howItWorksTitle')}</h4>
                <p className="text-sm text-blue-700 mt-1">
                  {t('howItWorksDescription')}
                </p>
              </div>
            </div>
          </div>

          {/* Upload Area */}
          <div
            className={`border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors ${
              dragActive 
                ? 'border-purple-400 bg-purple-50' 
                : 'border-gray-300 hover:border-purple-400 hover:bg-gray-50'
            }`}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={handleFileSelect}
          >
            {isAnalyzing ? (
              <div className="flex flex-col items-center">
                <ArrowPathIcon className="w-12 h-12 text-purple-600 animate-spin mb-4" />
                <h4 className="text-lg font-medium text-gray-900 mb-2">
                  {t('analyzingTitle')}
                </h4>
                <p className="text-gray-600">
                  {t('analyzingDescription')}
                </p>
              </div>
            ) : (
              <div className="flex flex-col items-center">
                <DocumentArrowUpIcon className="w-12 h-12 text-gray-400 mb-4" />
                <h4 className="text-lg font-medium text-gray-900 mb-2">
                  {t('uploadTitle')}
                </h4>
                <p className="text-gray-600 mb-4">
                  {t('uploadDescription')}
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
            <p>{t('requirements.format')}</p>
            <p>{t('requirements.maxSize')}</p>
            <p>{t('requirements.compatibility')}</p>
          </div>

          {/* AI Features */}
          <div className="bg-gradient-to-r from-purple-50 to-blue-50 rounded-lg p-4">
            <h4 className="font-medium text-gray-900 mb-2">{t('featuresTitle')}</h4>
            <ul className="text-sm text-gray-700 space-y-1">
              <li>{t('features.detectFormat')}</li>
              <li>{t('features.identifyColumns')}</li>
              <li>{t('features.detectDebitsCredits')}</li>
              <li>{t('features.suggestFormats')}</li>
              <li>{t('features.confidenceScores')}</li>
            </ul>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
