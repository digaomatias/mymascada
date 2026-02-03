'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { ArrowPathIcon } from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';
import type { BankSyncResult } from '@/types/bank-connections';

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
    setIsSyncing(true);
    try {
      const results: BankSyncResult[] = await apiClient.syncAllConnections();

      const successful = results.filter(r => r.isSuccess);
      const totalImported = results.reduce((sum, r) => sum + r.transactionsImported, 0);

      if (results.length === 0) {
        toast.info(t('syncSuccessNoNew'));
      } else if (successful.length === results.length) {
        // All syncs succeeded
        if (totalImported > 0) {
          toast.success(t('syncSuccess', { imported: totalImported }));
        } else {
          toast.success(t('syncSuccessNoNew'));
        }
      } else if (successful.length > 0) {
        // Partial success
        toast.warning(t('syncPartial', {
          successful: successful.length,
          total: results.length
        }));
      } else {
        // All failed
        toast.error(t('syncFailed'));
      }

      if (onSyncComplete) {
        onSyncComplete();
      }
    } catch (error) {
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
