'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { BankConnectionList } from '@/components/bank-connections/bank-connection-list';
import { LinkAccountDialog } from '@/components/bank-connections/link-account-dialog';
import { AkahuSetupDialog } from '@/components/bank-connections/akahu-setup-dialog';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import Link from 'next/link';
import {
  BuildingLibraryIcon,
  PlusIcon,
  ArrowLeftIcon,
  InformationCircleIcon
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';
import type { BankConnection, BankProviderInfo, AkahuAccount } from '@/types/bank-connections';

export default function BankConnectionsPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('settings.bankConnections');
  const tNav = useTranslations('nav');

  const [connections, setConnections] = useState<BankConnection[]>([]);
  const [providers, setProviders] = useState<BankProviderInfo[]>([]);
  const [loadingConnections, setLoadingConnections] = useState(true);
  const [akahuAccounts, setAkahuAccounts] = useState<AkahuAccount[]>([]);
  const [showLinkDialog, setShowLinkDialog] = useState(false);
  const [showSetupDialog, setShowSetupDialog] = useState(false);
  const [credentialsError, setCredentialsError] = useState<string | undefined>(undefined);
  const [isInitiatingConnection, setIsInitiatingConnection] = useState(false);

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  useEffect(() => {
    if (isAuthenticated) {
      loadConnections();
      loadProviders();
    }
  }, [isAuthenticated]);

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
        localStorage.removeItem('akahu_access_token');
        localStorage.removeItem('akahu_available_accounts');
      }
    }
  }, []);

  const loadConnections = async () => {
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
  };

  const loadProviders = async () => {
    try {
      const data = await apiClient.getAvailableProviders();
      setProviders(data);
    } catch (error) {
      console.error('Failed to load providers:', error);
    }
  };

  const handleInitiateConnection = async () => {
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

  const handleSync = async (connectionId: number) => {
    const result = await apiClient.syncBankConnection(connectionId);
    if (result.isSuccess) {
      toast.success(t('toasts.syncSuccess', { count: result.transactionsImported }));
    } else {
      throw new Error(result.errorMessage || 'Sync failed');
    }
  };

  const handleDisconnect = async (connectionId: number) => {
    await apiClient.disconnectBankConnection(connectionId);
  };

  const handleSyncAll = async () => {
    try {
      const results = await apiClient.syncAllConnections();
      const successful = results.filter(r => r.isSuccess).length;
      const totalImported = results.reduce((sum, r) => sum + r.transactionsImported, 0);

      if (successful === results.length) {
        toast.success(t('toasts.syncAllSuccess', { successful, total: totalImported }));
      } else {
        toast.warning(t('toasts.syncPartial', { successful, total: results.length }));
      }
      loadConnections();
    } catch (error) {
      console.error('Failed to sync all:', error);
      toast.error(t('toasts.syncAllFailed'));
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-violet-600 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BuildingLibraryIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-slate-700 font-medium">{t('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  const hasAkahuProvider = providers.some(p => p.providerId === 'akahu');

  return (
    <AppLayout>
      {/* Header */}
        <div className="mb-6 lg:mb-8">
          <div className="flex items-center gap-2 mb-2">
            <Link
              href="/settings"
              className="p-1 hover:bg-white/50 rounded-lg transition-colors"
            >
              <ArrowLeftIcon className="w-5 h-5 text-slate-600" />
            </Link>
            <span className="text-sm text-slate-500">{tNav('settings')}</span>
          </div>

          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="font-[var(--font-dash-sans)] text-3xl font-semibold tracking-[-0.03em] text-slate-900 sm:text-[2.1rem]">
                {t('title')}
              </h1>
              <p className="text-[15px] text-slate-500 mt-1.5">
                {t('subtitle')}
              </p>
            </div>

            <div className="flex items-center gap-2">
              {connections.length > 0 && (
                <Button
                  variant="outline"
                  onClick={handleSyncAll}
                  className="flex items-center gap-2"
                >
                  {t('syncAll')}
                </Button>
              )}

              {hasAkahuProvider && (
                <Button
                  onClick={handleInitiateConnection}
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
        <Card className="rounded-[26px] border border-violet-100/70 bg-white/92 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BuildingLibraryIcon className="w-6 h-6 text-violet-600" />
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
            />

            {connections.length === 0 && !loadingConnections && hasAkahuProvider && (
              <div className="mt-4 text-center">
                <Button
                  onClick={handleInitiateConnection}
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
