'use client';

import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ArrowPathIcon } from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface AkahuSyncButtonProps {
  onSyncComplete?: () => void;
  className?: string;
}

export function AkahuSyncButton({
  onSyncComplete,
  className = ''
}: AkahuSyncButtonProps) {
  const t = useTranslations('dashboard.akahuSync');
  const [hasAkahuConnection, setHasAkahuConnection] = useState<boolean | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const syncAbortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    return () => {
      syncAbortRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    const checkConnection = async () => {
      try {
        const response = await apiClient.hasAkahuCredentials();
        setHasAkahuConnection(response.hasCredentials);
      } catch (error) {
        console.error('Failed to check Akahu connection:', error);
        setHasAkahuConnection(false);
      }
    };

    checkConnection();
  }, []);

  const handleSync = async () => {
    syncAbortRef.current?.abort();
    syncAbortRef.current = new AbortController();

    setIsSyncing(true);
    toast.info(t('syncStarting'));
    try {
      const accepted = await apiClient.syncAllConnections();
      const status = await apiClient.waitForSyncJob(accepted.jobId, { signal: syncAbortRef.current.signal });
      const successful = status.completedConnections - status.failedConnections;

      if (status.status === 'succeeded') {
        if (status.transactionsImported > 0) {
          toast.success(t('syncSuccess', { imported: status.transactionsImported }));
        } else {
          toast.success(t('syncSuccessNoNew'));
        }
      } else if (status.status === 'completed_with_errors' && successful > 0) {
        toast.warning(t('syncPartial', {
          successful,
          total: status.totalConnections
        }));
      } else {
        toast.error(t('syncFailed'));
      }

      if (onSyncComplete) {
        onSyncComplete();
      }
    } catch (error) {
      if ((error as Error).name === 'AbortError') return;
      console.error('Failed to sync bank data:', error);
      toast.error(t('syncFailed'));
    } finally {
      setIsSyncing(false);
    }
  };

  // Only render if user has Akahu credentials
  if (hasAkahuConnection !== true) {
    return null;
  }

  return (
    <div className={`relative group ${className}`}>
      <Button
        variant="secondary"
        size="icon"
        onClick={handleSync}
        disabled={isSyncing}
        aria-label={t('refreshButtonLabel')}
      >
        <ArrowPathIcon
          className={`w-5 h-5 ${isSyncing ? 'animate-spin' : ''}`}
        />
      </Button>

      {/* Tooltip (desktop hover) */}
      <div
        className="pointer-events-none absolute right-0 top-full z-50 mt-2 hidden whitespace-nowrap rounded-md bg-black px-2 py-1 text-xs text-white opacity-0 transition group-hover:block group-hover:opacity-100"
        role="tooltip"
      >
        {isSyncing ? t('syncing') : t('refresh')}
      </div>
    </div>
  );
}
