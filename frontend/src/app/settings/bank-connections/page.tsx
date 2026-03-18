'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BankConnectionList } from '@/components/bank-connections/bank-connection-list';
import { LinkAccountDialog } from '@/components/bank-connections/link-account-dialog';
import { AkahuSetupDialog } from '@/components/bank-connections/akahu-setup-dialog';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import {
  BuildingLibraryIcon,
  PlusIcon,
  InformationCircleIcon,
  ArrowPathIcon
} from '@heroicons/react/24/outline';
import { BackButton } from '@/components/ui/back-button';
import { useTranslations } from 'next-intl';
import type {
  AkahuAccount,
  BankConnection,
  BankProviderInfo,
  BankSyncJobAccepted,
  BankSyncJobStatus
} from '@/types/bank-connections';

export default function BankConnectionsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('settings.bankConnections');

  const [connections, setConnections] = useState<BankConnection[]>([]);
  const [providers, setProviders] = useState<BankProviderInfo[]>([]);
  const [loadingConnections, setLoadingConnections] = useState(true);
  const [akahuAccounts, setAkahuAccounts] = useState<AkahuAccount[]>([]);
  const [showLinkDialog, setShowLinkDialog] = useState(false);
  const [showSetupDialog, setShowSetupDialog] = useState(false);
  const [credentialsError, setCredentialsError] = useState<string | undefined>(undefined);
  const [isInitiatingConnection, setIsInitiatingConnection] = useState(false);
  const [activeSyncJobs, setActiveSyncJobs] = useState<Record<string, BankSyncJobStatus>>({});
  const pollTimersRef = useRef<Record<string, number>>({});
  const pollSyncJobRef = useRef<((jobId: string) => Promise<void>) | undefined>(undefined);
  const pollAbortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  // Check for OAuth callback data on mount (Production App mode only)
  useEffect(() => {
    if (typeof window !== 'undefined') {
      const storedAccounts = localStorage.getItem('akahu_available_accounts');

      if (storedAccounts) {
        try {
          const accounts = JSON.parse(storedAccounts) as AkahuAccount[];
          setAkahuAccounts(accounts);
          setShowLinkDialog(true);
        } catch (error) {
          console.error('Failed to parse stored accounts:', error);
        }
        // Clear stored data
        localStorage.removeItem('akahu_oauth_state');
        localStorage.removeItem('akahu_available_accounts');
      }
    }
  }, []);

  const loadConnections = useCallback(async () => {
    setLoadingConnections(true);
    try {
      const data = await apiClient.getBankConnections();
      setConnections(data);
    } catch (error) {
      console.error('Failed to load connections:', error);
      toast.error(t('toasts.loadFailed'));
    } finally {
      setLoadingConnections(false);
    }
  }, [t]);

  const loadProviders = useCallback(async () => {
    try {
      const data = await apiClient.getAvailableProviders();
      setProviders(data);
    } catch (error) {
      console.error('Failed to load providers:', error);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      void loadConnections();
      void loadProviders();
    }
  }, [isAuthenticated, loadConnections, loadProviders]);

  const handleInitiateConnection = async (providerId: string) => {
    if (providerId !== 'akahu') {
      toast.info(`${providerId} is not available yet.`);
      return;
    }

    setIsInitiatingConnection(true);
    try {
      const result = await apiClient.initiateAkahuConnection();

      if (result.requiresCredentials) {
        // User needs to set up credentials first
        setCredentialsError(result.credentialsError);
        setShowSetupDialog(true);
        setIsInitiatingConnection(false);
      } else if (result.isPersonalAppMode) {
        // Personal App mode - accounts are returned directly
        setAkahuAccounts(result.availableAccounts || []);
        setShowLinkDialog(true);
        setIsInitiatingConnection(false);
      } else {
        // Production App mode - redirect to OAuth
        if (typeof window !== 'undefined' && result.state) {
          localStorage.setItem('akahu_oauth_state', result.state);
        }

        // Redirect to Akahu OAuth page
        if (result.authorizationUrl) {
          window.location.href = result.authorizationUrl;
        } else {
          throw new Error('No authorization URL returned');
        }
      }
    } catch (error) {
      console.error('Failed to initiate connection:', error);
      toast.error(t('toasts.connectionFailed'));
      setIsInitiatingConnection(false);
    }
  };

  const handleCredentialsSaved = (accounts: AkahuAccount[]) => {
    setShowSetupDialog(false);
    setCredentialsError(undefined);
    setAkahuAccounts(accounts);
    setShowLinkDialog(true);
  };

  const handleCompleteConnection = async (accountId: number, akahuAccountId: string) => {
    try {
      // Simplified: no longer needs code/state - uses stored credentials
      await apiClient.completeAkahuConnection({
        accountId,
        akahuAccountId
      });

      toast.success(t('toasts.connected'));
      setShowLinkDialog(false);
      loadConnections();
    } catch (error) {
      console.error('Failed to complete connection:', error);
      toast.error(t('toasts.linkFailed'));
    }
  };

  const clearPollTimer = useCallback((jobId: string) => {
    if (typeof window === 'undefined') {
      return;
    }

    const timerId = pollTimersRef.current[jobId];
    if (timerId) {
      window.clearTimeout(timerId);
      delete pollTimersRef.current[jobId];
    }
  }, []);

  const removeActiveJob = useCallback((jobId: string) => {
    clearPollTimer(jobId);
    setActiveSyncJobs((current) => {
      const next = { ...current };
      delete next[jobId];
      return next;
    });
  }, [clearPollTimer]);

  const schedulePoll = useCallback((jobId: string, delay = 1500) => {
    if (typeof window === 'undefined') {
      return;
    }

    clearPollTimer(jobId);
    pollTimersRef.current[jobId] = window.setTimeout(() => {
      void pollSyncJobRef.current?.(jobId);
    }, delay);
  }, [clearPollTimer]);

  const handleTerminalJob = useCallback(async (status: BankSyncJobStatus) => {
    const successfulConnections = status.completedConnections - status.failedConnections;

    if (status.scope === 'all') {
      if (status.status === 'succeeded') {
        toast.success(t('toasts.syncAllSuccess', {
          successful: successfulConnections,
          total: status.transactionsImported
        }));
      } else if (status.status === 'completed_with_errors' && successfulConnections > 0) {
        toast.warning(t('toasts.syncPartial', {
          successful: successfulConnections,
          total: status.totalConnections
        }));
      } else {
        toast.error(status.errorMessage || t('toasts.syncAllFailed'));
      }
    } else if (status.status === 'succeeded') {
      toast.success(t('toasts.syncSuccess', { count: status.transactionsImported }));
    } else {
      toast.error(status.errorMessage || t('toasts.syncAllFailed'));
    }

    await loadConnections();
    removeActiveJob(status.jobId);
  }, [loadConnections, removeActiveJob, t]);

  const pollSyncJob = useCallback(async (jobId: string) => {
    try {
      pollAbortRef.current = new AbortController();
      const status = await apiClient.getSyncJobStatus(jobId, { signal: pollAbortRef.current.signal });

      if (pollAbortRef.current?.signal.aborted) return;

      setActiveSyncJobs((current) => ({
        ...current,
        [jobId]: status,
      }));

      if (status.status === 'queued' || status.status === 'processing') {
        schedulePoll(jobId);
        return;
      }

      await handleTerminalJob(status);
    } catch (error) {
      if ((error as Error).name === 'AbortError') return;
      console.error('Failed to poll sync job:', error);
      toast.error(t('toasts.syncAllFailed'));
      removeActiveJob(jobId);
    }
  }, [handleTerminalJob, removeActiveJob, schedulePoll, t]);

  // Keep ref in sync so schedulePoll always calls the latest pollSyncJob
  pollSyncJobRef.current = pollSyncJob;

  const trackAcceptedJob = useCallback((accepted: BankSyncJobAccepted) => {
    const queuedStatus: BankSyncJobStatus = {
      jobId: accepted.jobId,
      scope: accepted.scope,
      status: 'queued',
      startedAt: accepted.startedAt,
      connectionIds: accepted.connectionIds,
      totalConnections: accepted.totalConnections,
      completedConnections: 0,
      failedConnections: 0,
      transactionsImported: 0,
      transactionsSkipped: 0,
    };

    setActiveSyncJobs((current) => ({
      ...current,
      [accepted.jobId]: queuedStatus,
    }));

    schedulePoll(accepted.jobId, 250);
  }, [schedulePoll]);

  const handleSync = async (connectionId: number) => {
    // Prevent duplicate sync if already in progress for this connection
    if (syncingConnectionIds.has(connectionId)) return;
    const accepted = await apiClient.syncBankConnection(connectionId);
    trackAcceptedJob(accepted);
    toast.info(t('toasts.syncStarting'));
  };

  const handleDisconnect = async (connectionId: number) => {
    await apiClient.disconnectBankConnection(connectionId);
  };

  const handleSyncAll = async () => {
    if (isSyncing) return;
    toast.info(t('toasts.syncStarting'));

    try {
      const accepted = await apiClient.syncAllConnections();
      trackAcceptedJob(accepted);
    } catch (error) {
      console.error('Failed to start sync-all job:', error);
      toast.error(t('toasts.syncAllFailed'));
    }
  };

  useEffect(() => {
    return () => {
      if (typeof window === 'undefined') {
        return;
      }

      pollAbortRef.current?.abort();
      Object.values(pollTimersRef.current).forEach((timerId) => window.clearTimeout(timerId));
      pollTimersRef.current = {};
    };
  }, []);

  const activeJobList = useMemo(
    () => Object.values(activeSyncJobs),
    [activeSyncJobs]
  );

  const isSyncing = activeJobList.some(
    (job) => job.status === 'queued' || job.status === 'processing'
  );

  const syncingConnectionIds = useMemo(() => {
    const ids = new Set<number>();
    activeJobList
      .filter((job) => job.status === 'queued' || job.status === 'processing')
      .forEach((job) => {
        job.connectionIds.forEach((connectionId) => ids.add(connectionId));
      });
    return ids;
  }, [activeJobList]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-600 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingLibraryIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-ink-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  const primaryProvider = providers.find(p => p.providerId === 'akahu');
  const canConnectProvider = !!primaryProvider;
  return (
    <AppLayout>
      {/* Header */}
        <div className="mb-6 lg:mb-8">
          <BackButton variant="link" href="/settings" label={t('backToSettings')} />

          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-ink-900 sm:text-[2.1rem]">
                {t('title')}
              </h1>
              <p className="text-[15px] text-ink-500 mt-1.5">
                {t('subtitle')}
              </p>
            </div>

            <div className="flex items-center gap-2">
              {connections.length > 0 && (
                <Button
                  variant="outline"
                  onClick={handleSyncAll}
                  disabled={isSyncing}
                  className="flex items-center gap-2"
                >
                  <ArrowPathIcon className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />
                  {isSyncing ? t('syncing') : t('syncAll')}
                </Button>
              )}

              {canConnectProvider && (
                <Button
                  onClick={() => handleInitiateConnection(primaryProvider.providerId)}
                  disabled={isInitiatingConnection}
                  className="flex items-center gap-2"
                >
                  <PlusIcon className="w-4 h-4" />
                  {isInitiatingConnection ? t('connecting') : t('connectBank')}
                </Button>
              )}
            </div>
          </div>
        </div>

        {/* Info Banner */}
        <div className="rounded-2xl border border-blue-200/60 bg-blue-50/80 backdrop-blur-xs p-4 mb-6">
          <div className="flex items-start gap-3">
            <InformationCircleIcon className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
            <div className="text-sm text-blue-800">
              <p className="font-medium">{t('aboutTitle')}</p>
              <p className="mt-1 text-blue-700">
                {t('aboutDescription')}
              </p>
            </div>
          </div>
        </div>

        {/* Connections List */}
        <Card className="rounded-[26px] border border-ink-200 bg-white/92 shadow-[0_20px_46px_-30px_rgba(47,129,112,0.20)] backdrop-blur-xs">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BuildingLibraryIcon className="w-6 h-6 text-primary-600" />
              {t('connectedAccounts')}
            </CardTitle>
          </CardHeader>

          <CardContent>
            <BankConnectionList
              connections={connections}
              onSync={handleSync}
              onDisconnect={handleDisconnect}
              onRefresh={loadConnections}
              isLoading={loadingConnections}
              syncingConnectionIds={syncingConnectionIds}
            />

            {connections.length === 0 && !loadingConnections && canConnectProvider && (
              <div className="mt-4 text-center">
                <Button
                  onClick={() => handleInitiateConnection(primaryProvider.providerId)}
                  disabled={isInitiatingConnection}
                  className="flex items-center gap-2 mx-auto"
                >
                  <PlusIcon className="w-4 h-4" />
                  {t('connectFirst')}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>

      {/* Akahu Setup Dialog - for entering credentials */}
      <AkahuSetupDialog
        isOpen={showSetupDialog}
        onClose={() => {
          setShowSetupDialog(false);
          setCredentialsError(undefined);
        }}
        onSuccess={handleCredentialsSaved}
        credentialsError={credentialsError}
      />

      {/* Link Account Dialog - for selecting which account to link */}
      <LinkAccountDialog
        isOpen={showLinkDialog}
        onClose={() => {
          setShowLinkDialog(false);
        }}
        akahuAccounts={akahuAccounts}
        onComplete={handleCompleteConnection}
      />
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
