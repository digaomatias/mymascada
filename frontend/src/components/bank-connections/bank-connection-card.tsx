'use client';

import { useState } from 'react';
import { BankConnection } from '@/types/bank-connections';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { SyncStatusIndicator } from './sync-status-indicator';
import {
  BuildingLibraryIcon,
  ArrowPathIcon,
  TrashIcon,
  LinkIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface BankConnectionCardProps {
  connection: BankConnection;
  onSync: (connectionId: number) => Promise<void>;
  onDisconnect: (connectionId: number) => Promise<void>;
  onRefresh: () => void;
}

export function BankConnectionCard({
  connection,
  onSync,
  onDisconnect,
  onRefresh
}: BankConnectionCardProps) {
  const t = useTranslations('bankConnections');
  const tCommon = useTranslations('common');
  const tToasts = useTranslations('toasts');
  const [isSyncing, setIsSyncing] = useState(false);
  const [showDisconnectConfirm, setShowDisconnectConfirm] = useState(false);

  const handleSync = async () => {
    setIsSyncing(true);
    try {
      await onSync(connection.id);
      toast.success(tToasts('bankSyncSuccess'));
      onRefresh();
    } catch (error) {
      console.error('Sync failed:', error);
      toast.error(tToasts('bankSyncFailed'));
    } finally {
      setIsSyncing(false);
    }
  };

  const handleDisconnect = async () => {
    try {
      await onDisconnect(connection.id);
      toast.success(tToasts('bankConnectionRemoved'));
      onRefresh();
    } catch (error) {
      console.error('Disconnect failed:', error);
      toast.error(tToasts('bankDisconnectFailed'));
    } finally {
      setShowDisconnectConfirm(false);
    }
  };

  return (
    <>
      <Card className="bg-white border border-gray-200 hover:shadow-md transition-shadow">
        <CardContent className="p-4">
          <div className="flex items-start gap-4">
            {/* Provider Icon */}
            <div className="w-12 h-12 bg-gradient-to-br from-blue-400 to-blue-600 rounded-xl flex items-center justify-center shrink-0">
              <BuildingLibraryIcon className="w-6 h-6 text-white" />
            </div>

            {/* Connection Details */}
            <div className="flex-1 min-w-0">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <h3 className="text-lg font-semibold text-gray-900 truncate">
                    {connection.accountName}
                  </h3>
                  <p className="text-sm text-gray-500 mt-0.5">
                    {t('viaProvider', { name: connection.providerName })}
                  </p>
                  {connection.externalAccountName && (
                    <p className="text-sm text-gray-600 mt-1 flex items-center gap-1">
                      <LinkIcon className="w-3.5 h-3.5" />
                      {connection.externalAccountName}
                    </p>
                  )}
                </div>

                {/* Sync Status */}
                <SyncStatusIndicator
                  isActive={connection.isActive}
                  lastSyncAt={connection.lastSyncAt}
                  lastSyncError={connection.lastSyncError}
                  isSyncing={isSyncing}
                />
              </div>

              {/* Error Message */}
              {connection.lastSyncError && (
                <div className="mt-3 p-2 bg-red-50 rounded-lg border border-red-200">
                  <div className="flex items-start gap-2">
                    <ExclamationTriangleIcon className="w-4 h-4 text-red-500 mt-0.5 shrink-0" />
                    <p className="text-sm text-red-700">{connection.lastSyncError}</p>
                  </div>
                </div>
              )}

              {/* Actions */}
              <div className="mt-4 flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleSync}
                  disabled={isSyncing || !connection.isActive}
                  className="flex items-center gap-1.5"
                >
                  <ArrowPathIcon className={`w-4 h-4 ${isSyncing ? 'animate-spin' : ''}`} />
                  {isSyncing ? t('syncing') : t('syncNow')}
                </Button>

                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowDisconnectConfirm(true)}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                >
                  <TrashIcon className="w-4 h-4" />
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Disconnect Confirmation */}
      <ConfirmationDialog
        isOpen={showDisconnectConfirm}
        title={t('disconnectBankAccount')}
        description={t('disconnectConfirm', { account: connection.accountName, provider: connection.providerName })}
        confirmText={t('disconnect')}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={handleDisconnect}
        onClose={() => setShowDisconnectConfirm(false)}
      />
    </>
  );
}
