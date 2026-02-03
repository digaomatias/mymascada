'use client';

import { BankConnection } from '@/types/bank-connections';
import { BankConnectionCard } from './bank-connection-card';
import { BuildingLibraryIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface BankConnectionListProps {
  connections: BankConnection[];
  onSync: (connectionId: number) => Promise<void>;
  onDisconnect: (connectionId: number) => Promise<void>;
  onRefresh: () => void;
  isLoading?: boolean;
}

export function BankConnectionList({
  connections,
  onSync,
  onDisconnect,
  onRefresh,
  isLoading = false
}: BankConnectionListProps) {
  const t = useTranslations('bankConnections');
  if (isLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="p-4 bg-gray-100 rounded-lg">
              <div className="flex items-start gap-4">
                <div className="w-12 h-12 bg-gray-300 rounded-xl" />
                <div className="flex-1">
                  <div className="h-5 bg-gray-300 rounded w-1/3 mb-2" />
                  <div className="h-4 bg-gray-300 rounded w-1/4 mb-3" />
                  <div className="h-8 bg-gray-300 rounded w-24" />
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (connections.length === 0) {
    return (
      <div className="text-center py-12">
        <div className="w-16 h-16 bg-gradient-to-br from-gray-300 to-gray-400 rounded-2xl flex items-center justify-center mx-auto mb-4">
          <BuildingLibraryIcon className="w-8 h-8 text-white" />
        </div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">{t('noBankConnections')}</h3>
        <p className="text-gray-600 max-w-sm mx-auto">
          {t('connectDescription')}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {connections.map((connection) => (
        <BankConnectionCard
          key={connection.id}
          connection={connection}
          onSync={onSync}
          onDisconnect={onDisconnect}
          onRefresh={onRefresh}
        />
      ))}
    </div>
  );
}
