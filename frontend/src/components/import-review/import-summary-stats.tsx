'use client';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useTranslations } from 'next-intl';
import { 
  CheckCircleIcon,
  ExclamationTriangleIcon,
  ClockIcon,
  DocumentDuplicateIcon
} from '@heroicons/react/24/outline';

interface ImportSummaryStatsProps {
  summary: {
    totalCandidates: number;
    cleanImports: number;
    exactDuplicates: number;
    potentialDuplicates: number;
    transferConflicts: number;
    manualConflicts: number;
    requiresReview: number;
  };
  progress: {
    total: number;
    reviewed: number;
    pending: number;
    toImport: number;
    toSkip: number;
    progressPercent: number;
  };
}

export function ImportSummaryStats({ summary, progress }: ImportSummaryStatsProps) {
  const tImport = useTranslations('import');
  // Debug logging as suggested by AIs
  console.log('ImportSummaryStats: summary', summary);
  console.log('ImportSummaryStats: progress', progress);

  // Defensive programming - provide default values if summary is undefined
  const safeSummary = summary || {
    totalCandidates: 0,
    cleanImports: 0,
    exactDuplicates: 0,
    potentialDuplicates: 0,
    transferConflicts: 0,
    manualConflicts: 0,
    requiresReview: 0
  };

  const stats = [
    {
      label: tImport('review.summary.readyToImport'),
      value: safeSummary.cleanImports,
      icon: <CheckCircleIcon className="w-5 h-5" />,
      color: 'text-green-600',
      bgColor: 'bg-green-100'
    },
    {
      label: tImport('review.summary.exactDuplicates'),
      value: safeSummary.exactDuplicates,
      icon: <DocumentDuplicateIcon className="w-5 h-5" />,
      color: 'text-red-600',
      bgColor: 'bg-red-100'
    },
    {
      label: tImport('review.summary.potentialDuplicates'),
      value: safeSummary.potentialDuplicates,
      icon: <ExclamationTriangleIcon className="w-5 h-5" />,
      color: 'text-orange-600',
      bgColor: 'bg-orange-100'
    },
    {
      label: tImport('review.summary.otherConflicts'),
      value: safeSummary.transferConflicts + safeSummary.manualConflicts,
      icon: <ExclamationTriangleIcon className="w-5 h-5" />,
      color: 'text-purple-600',
      bgColor: 'bg-purple-100'
    }
  ];

  return (
    <div className="space-y-4">
      {/* Overview Stats */}
      <Card className="bg-white border-0 shadow-sm">
        <CardHeader>
          <CardTitle className="text-lg font-semibold">{tImport('review.summary.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {stats.map((stat, index) => (
              <div key={index} className="text-center">
                <div className={`w-12 h-12 rounded-lg flex items-center justify-center mx-auto mb-2 ${stat.bgColor}`}>
                  <div className={stat.color}>
                    {stat.icon}
                  </div>
                </div>
                <div className="text-2xl font-bold text-gray-900">{stat.value}</div>
                <div className="text-sm text-gray-600">{stat.label}</div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Progress Card */}
      <Card className="bg-white border-0 shadow-sm">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg font-semibold">
            <ClockIcon className="w-5 h-5" />
            {tImport('review.progress.title')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {/* Progress Bar */}
          <div className="mb-4">
            <div className="flex justify-between text-sm text-gray-600 mb-1">
              <span>{tImport('review.progress.progressLabel')}</span>
              <span>{progress.progressPercent}%</span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div 
                className="bg-blue-600 h-2 rounded-full transition-all duration-300"
                style={{ width: `${progress.progressPercent}%` }}
              />
            </div>
          </div>

          {/* Progress Details */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
            <div>
              <div className="text-lg font-bold text-gray-900">{progress.total}</div>
              <div className="text-xs text-gray-500">{tImport('review.progress.total')}</div>
            </div>
            <div>
              <div className="text-lg font-bold text-blue-600">{progress.reviewed}</div>
              <div className="text-xs text-gray-500">{tImport('review.progress.reviewed')}</div>
            </div>
            <div>
              <div className="text-lg font-bold text-green-600">{progress.toImport}</div>
              <div className="text-xs text-gray-500">{tImport('review.progress.toImport')}</div>
            </div>
            <div>
              <div className="text-lg font-bold text-red-600">{progress.toSkip}</div>
              <div className="text-xs text-gray-500">{tImport('review.progress.toSkip')}</div>
            </div>
          </div>

          {/* Remaining Items Alert */}
          {progress.pending > 0 && (
            <div className="mt-4 p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
              <div className="flex items-center gap-2 text-yellow-800">
                <ExclamationTriangleIcon className="w-4 h-4" />
                <span className="text-sm font-medium">
                  {tImport('review.progress.pendingReview', { count: progress.pending })}
                </span>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
