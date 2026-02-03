'use client';

import { CheckCircleIcon, ExclamationCircleIcon, ArrowPathIcon, ClockIcon } from '@heroicons/react/24/outline';
import { formatDistanceToNow } from 'date-fns';
import { useTranslations } from 'next-intl';

interface SyncStatusIndicatorProps {
  isActive: boolean;
  lastSyncAt?: string;
  lastSyncError?: string;
  isSyncing?: boolean;
}

export function SyncStatusIndicator({
  isActive,
  lastSyncAt,
  lastSyncError,
  isSyncing = false
}: SyncStatusIndicatorProps) {
  const t = useTranslations('bankConnections');
  if (!isActive) {
    return (
      <div className="flex items-center gap-1.5 text-gray-500">
        <div className="w-2 h-2 rounded-full bg-gray-400" />
        <span className="text-sm">{t('inactive')}</span>
      </div>
    );
  }

  if (isSyncing) {
    return (
      <div className="flex items-center gap-1.5 text-blue-600">
        <ArrowPathIcon className="w-4 h-4 animate-spin" />
        <span className="text-sm">{t('syncing')}</span>
      </div>
    );
  }

  if (lastSyncError) {
    return (
      <div className="flex items-center gap-1.5 text-red-600">
        <ExclamationCircleIcon className="w-4 h-4" />
        <span className="text-sm">{t('syncError')}</span>
      </div>
    );
  }

  if (lastSyncAt) {
    const syncDate = new Date(lastSyncAt);
    const formattedTime = formatDistanceToNow(syncDate, { addSuffix: true });

    return (
      <div className="flex items-center gap-1.5 text-green-600">
        <CheckCircleIcon className="w-4 h-4" />
        <span className="text-sm">{t('syncedTime', { time: formattedTime })}</span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-1.5 text-amber-600">
      <ClockIcon className="w-4 h-4" />
      <span className="text-sm">{t('neverSynced')}</span>
    </div>
  );
}
